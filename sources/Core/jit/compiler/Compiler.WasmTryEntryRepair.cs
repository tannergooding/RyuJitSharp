// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_WASM
    private readonly struct WasmTrySideEntry
    {
        public BasicBlock Pred { get; }

        public BasicBlock StartHeader { get; }

        public BasicBlock Target { get; }

        public WasmTrySideEntry(BasicBlock pred, BasicBlock startHeader, BasicBlock target)
        {
            Pred = pred;
            StartHeader = startHeader;
            Target = target;
        }
    }

    private sealed class WasmTryEntryDispatch
    {
        public BasicBlock Header { get; }

        public BasicBlock? Dispatcher { get; set; }

        public BasicBlock? NormalTarget { get; set; }

        public WasmTryEntryDispatch(BasicBlock header)
        {
            Header = header;
        }
    }

    public PhaseStatus fgWasmRepairTryEntries()
    {
        assert(fgNodeThreading is NodeThreading.LIR);

        if (compHndBBtabCount == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var sideEntries = new ArrayStack<WasmTrySideEntry>();

        foreach (var block in Blocks)
        {
            if (!block.hasTryIndex)
            {
                continue;
            }

            var tryHeader = ehGetDsc(block.TryIndex).ebdTryBeg;
            if (!BasicBlock.sameHndRegion(block, tryHeader))
            {
                continue;
            }

            foreach (var edge in block.PredEdges)
            {
                var pred = edge.SourceBlock;
                if (pred.Kind is BBJ_EHCATCHRET)
                {
                    continue;
                }

                var outermost = EHblkDsc.NO_ENCLOSING_INDEX;
                for (var index = block.TryIndex; index != EHblkDsc.NO_ENCLOSING_INDEX;
                     index = ehGetDsc(index).ebdEnclosingTryIndex)
                {
                    if (bbInTryRegions(index, pred))
                    {
                        break;
                    }

                    outermost = index;
                }

                if (outermost == EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    continue;
                }

                var startHeader = ehGetDsc(outermost).ebdTryBeg;
                if (startHeader == block)
                {
                    continue;
                }

                JITDUMP(
                    $"Side entry {FMT_BB(pred.bbNum)} -> {FMT_BB(block.bbNum)}, entering try region headed by " +
                    $"{FMT_BB(startHeader.bbNum)}\n");
                sideEntries.Push(new WasmTrySideEntry(pred, startHeader, block));
            }
        }

        if (sideEntries.Empty())
        {
            JITDUMP("No try region side entries\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var targets = new ArrayStack<BasicBlock>();
        var targetPaths = new List<List<BasicBlock>>();
        var giveIndex = new Dictionary<BasicBlock, int>();

        foreach (var sideEntry in sideEntries.BottomUpOrder())
        {
            var path = new List<BasicBlock>();
            for (var tryIndex = sideEntry.Target.TryIndex; tryIndex != EHblkDsc.NO_ENCLOSING_INDEX;
                 tryIndex = ehGetDsc(tryIndex).ebdEnclosingTryIndex)
            {
                var header = ehGetDsc(tryIndex).ebdTryBeg;
                path.Add(header);

                if (header == sideEntry.StartHeader)
                {
                    break;
                }
            }

            assert(path.Count > 0);
            assert(path[^1] == sideEntry.StartHeader);

            if (giveIndex.TryGetValue(sideEntry.Target, out var index))
            {
#if DEBUG
                var existing = targetPaths[index];
                var common = Math.Min(existing.Count, path.Count);
                for (var p = 0; p < common; p++)
                {
                    assert(existing[p] == path[p]);
                }
#endif

                if (path.Count > targetPaths[index].Count)
                {
                    targetPaths[index] = path;
                }
            }
            else
            {
                index = targets.Height();
                giveIndex.Add(sideEntry.Target, index);
                targets.Push(sideEntry.Target);
                targetPaths.Add(path);
                JITDUMP($"Side entry target {index} is {FMT_BB(sideEntry.Target.bbNum)}\n");
            }
        }

        var numTargets = targets.Height();
        var dispatches = new ArrayStack<WasmTryEntryDispatch>();
        var giveDispatch = new Dictionary<BasicBlock, int>();

        for (var v = 0; v < numTargets; v++)
        {
            foreach (var header in targetPaths[v])
            {
                if (!giveDispatch.ContainsKey(header))
                {
                    giveDispatch.Add(header, dispatches.Height());
                    dispatches.Push(new WasmTryEntryDispatch(header));
                }
            }
        }

        var controlVarNum = lvaGrabTemp(shortLifetime: false, "Wasm try entry dispatch");
        ref var controlVarDescriptor = ref lvaGetDesc(controlVarNum);
        controlVarDescriptor.Type = TYP_INT;

        var firstBlock = fgFirstBB
            ?? throw new InvalidOperationException("Wasm try-entry repair requires an entry block.");
        assert(!firstBlock.hasTryIndex && !firstBlock.hasHndIndex);

        var sentinel = gtNewIconNode(TYP_INT, numTargets);
        var sentinelStore = gtNewStoreLclVarNode(controlVarNum, sentinel);
        firstBlock.InsertAtBeginning(LIR.SeqTree(this, sentinelStore));

        foreach (ref var dispatch in dispatches.BottomUpOrder())
        {
            var header = dispatch.Header;
            var lastNode = header.LastLIRNode;
            BasicBlock normalTarget;

            if ((lastNode is not null) && lastNode.OperIs(GT_WASM_JEXCEPT))
            {
                normalTarget = header.FalseTarget;
            }
            else
            {
                normalTarget = fgSplitBlockAtBeginning(header);
            }

            dispatch.NormalTarget = normalTarget;
            var dispatcher = fgNewBBafter(BBJ_SWITCH, header, extendRegion: false);
            dispatch.Dispatcher = dispatcher;
            dispatcher.copyEHRegion(normalTarget);
            dispatcher.inheritWeight(header);

            fgReplaceJumpTarget(header, normalTarget, dispatcher);

            JITDUMP(
                $"Try region headed by {FMT_BB(header.bbNum)}: dispatcher {FMT_BB(dispatcher.bbNum)}, " +
                $"normal entry {FMT_BB(normalTarget.bbNum)}\n");
        }

        foreach (var dispatch in dispatches.BottomUpOrder())
        {
            if ((dispatch.Dispatcher is not BasicBlock dispatcher) ||
                (dispatch.NormalTarget is not BasicBlock normalTarget))
            {
                throw new InvalidOperationException("A Wasm try-entry dispatcher was not initialized.");
            }

            var header = dispatch.Header;
            var caseCount = numTargets + 1;
            var cases = new FlowEdge[caseCount];
            var successors = new List<FlowEdge>(caseCount);
            var resetPads = new BlockToBlockMap();
            var caseEdges = new Dictionary<BasicBlock, FlowEdge>();

            for (var v = 0; v <= numTargets; v++)
            {
                var caseTarget = normalTarget;

                if (v < numTargets)
                {
                    var path = targetPaths[v];
                    var pos = path.Count;

                    for (var p = 0; p < path.Count; p++)
                    {
                        if (path[p] == header)
                        {
                            pos = p;
                            break;
                        }
                    }

                    if (pos == 0)
                    {
                        var dest = (targets.Bottom(v) == header) ? normalTarget : targets.Bottom(v);
                        if (!resetPads.TryGetValue(dest, out var resetPad))
                        {
                            var pad = fgNewBBafter(BBJ_ALWAYS, dispatcher, extendRegion: false);
                            pad.copyEHRegion(dest);
                            pad.inheritWeightPercentage(dispatcher, 0);

                            var padEdge = fgAddRefPred(dest, pad);
                            padEdge.Likelihood = 1.0;
                            pad.TargetEdge = padEdge;

                            var padSentinel = gtNewIconNode(TYP_INT, numTargets);
                            var padStore = gtNewStoreLclVarNode(controlVarNum, padSentinel);
                            pad.InsertAtEnd(LIR.SeqTree(this, padStore));

                            resetPads.Add(dest, pad);
                            caseTarget = pad;

                            JITDUMP($"Reset pad {FMT_BB(pad.bbNum)} for {FMT_BB(dest.bbNum)}\n");
                        }
                        else
                        {
                            caseTarget = resetPad;
                        }
                    }
                    else if (pos < path.Count)
                    {
                        caseTarget = path[pos - 1];
                        assert(giveDispatch.ContainsKey(caseTarget));
                    }
                }

                if (caseEdges.TryGetValue(caseTarget, out var caseEdge))
                {
                    caseEdge.incrementDupCount();
                    caseTarget.bbRefs++;
                }
                else
                {
                    caseEdge = fgAddRefPred(caseTarget, dispatcher);
                    caseEdge.Likelihood = 0.0;
                    caseEdges.Add(caseTarget, caseEdge);
                    successors.Add(caseEdge);
                }

                cases[v] = caseEdge;
            }

            cases[numTargets].Likelihood = 1.0;

            var switchDescriptor = new BBswtDesc([.. successors], new int[caseCount], hasDefault: true);
            cases.AsSpan().CopyTo(switchDescriptor.Cases);
            dispatcher.SwitchTargets = switchDescriptor;

            var controlVar = gtNewLclvNode(TYP_INT, controlVarNum);
            var switchNode = gtNewUnaryNode(GT_SWITCH, TYP_VOID, controlVar);

            assert(dispatcher.isEmpty());
            dispatcher.InsertAtEnd(LIR.SeqTree(this, switchNode));

            JITDUMP(
                $"Dispatcher {FMT_BB(dispatcher.bbNum)} for region headed by {FMT_BB(header.bbNum)}: " +
                $"{caseCount} cases, {successors.Count} unique successors\n");
        }

        fgHasSwitch = true;

        foreach (var sideEntry in sideEntries.BottomUpOrder())
        {
            if (!giveIndex.TryGetValue(sideEntry.Target, out var index))
            {
                throw new InvalidOperationException("A Wasm side-entry target has no assigned index.");
            }

            var pred = sideEntry.Pred;
            BasicBlock transferBlock;
            if (pred.HasTarget && (pred.Target == sideEntry.Target) && !pred.isBBCallFinallyPairTail)
            {
                transferBlock = pred;
            }
            else
            {
                transferBlock = fgSplitEdge(pred, sideEntry.Target);
            }

            var targetIndex = gtNewIconNode(TYP_INT, index);
            var targetStore = gtNewStoreLclVarNode(controlVarNum, targetIndex);
            var range = LIR.SeqTree(this, targetStore);

            if (transferBlock.isEmpty())
            {
                transferBlock.InsertAtEnd(range);
            }
            else
            {
                LIR.InsertBeforeTerminator(transferBlock, range);
            }

            fgReplaceJumpTarget(transferBlock, sideEntry.Target, targetPaths[index][^1]);

            JITDUMP(
                $"Side entry to {FMT_BB(sideEntry.Target.bbNum)} (index {index}) now enters via " +
                $"{FMT_BB(targetPaths[index][^1].bbNum)} from {FMT_BB(transferBlock.bbNum)}\n");
        }

        if (fgPgoConsistent)
        {
            JITDUMP("Profile is now inconsistent: try region entry flow was rerouted\n");
            fgPgoConsistent = false;
        }

        fgInvalidateDfsTree();
        return PhaseStatus.MODIFIED_EVERYTHING;
    }
#endif
}
