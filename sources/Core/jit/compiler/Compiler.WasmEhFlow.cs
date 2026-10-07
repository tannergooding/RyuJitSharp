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
    // Catch funclets resume through a dispatch switch in their nearest enclosing try.
    public unsafe PhaseStatus fgWasmEhFlow()
    {
        assert(fgNodeThreading is NodeThreading.LIR);

        var hasCatch = false;
        foreach (ref var clause in new EHClauses(this))
        {
            if (clause.HasCatchHandler)
            {
                hasCatch = true;
                break;
            }
        }

        if (!hasCatch)
        {
            JITDUMP("Method does not have any catch handlers\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var catchRetBlocksByTryRegion = new ArrayStack<BasicBlock>?[compHndBBtabCount];

        ArrayStack<BasicBlock> GetCatchRetBlocksForTryRegion(ushort tryIndex)
        {
            var catchRetBlocks = catchRetBlocksByTryRegion[tryIndex];
            if (catchRetBlocks is null)
            {
                catchRetBlocks = new ArrayStack<BasicBlock>();
                catchRetBlocksByTryRegion[tryIndex] = catchRetBlocks;
            }

            return catchRetBlocks;
        }

        var foundCatchRetBlocks = false;
        foreach (var block in Blocks)
        {
            if (block.Kind is not BBJ_EHCATCHRET)
            {
                continue;
            }

            var continuationBlock = block.Target;
            assert(block.hasHndIndex);
            var catchingTryIndex = block.HndIndex;
            var dispatchingTryBlock = ehGetDsc(catchingTryIndex).ebdTryBeg;
            var innermostDispatchingTryIndex = dispatchingTryBlock.TryIndex;

            JITDUMP(
                $"Catchret block {FMT_BB(block.bbNum)} has continuation {FMT_BB(continuationBlock.bbNum)}; " +
                $"associated try EH#{catchingTryIndex:D2}; dispatching try EH#{innermostDispatchingTryIndex:D2}\n");

            ref var catchingTry = ref ehGetDsc(catchingTryIndex);
            ref var dispatchingTry = ref ehGetDsc(innermostDispatchingTryIndex);
            assert(EHblkDsc.ebdIsSameTry(catchingTry, dispatchingTry));

            if (bbInTryRegions(innermostDispatchingTryIndex, continuationBlock))
            {
                JITDUMP(
                    $"Continuation {FMT_BB(continuationBlock.bbNum)} is within dispatching try " +
                    $"EH#{innermostDispatchingTryIndex:D2}, marking as catch resumption\n");
                fgWasmHasCatchResumptions = true;
            }

            GetCatchRetBlocksForTryRegion(innermostDispatchingTryIndex).Push(block);
            foundCatchRetBlocks = true;
        }

        if (!foundCatchRetBlocks)
        {
            JITDUMP("No CATCHRETS in this method, so no EH processing needed.");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var catchRetIndex = 1;
        foreach (var catchRetBlocks in catchRetBlocksByTryRegion)
        {
            if (catchRetBlocks is null)
            {
                continue;
            }

            foreach (ref var catchRetBlock in catchRetBlocks.TopDownOrder())
            {
                if (bbIsTryBeg(catchRetBlock))
                {
                    JITDUMP($"Preemptively splitting catchret block {FMT_BB(catchRetBlock.bbNum)} which is also a try entry\n");
                    catchRetBlock = fgSplitBlockAtBeginning(catchRetBlock);
                }

                JITDUMP($"Assigning catchret block {FMT_BB(catchRetBlock.bbNum)} index number {catchRetIndex}\n");
                catchRetBlock.bbPreorderNum = catchRetIndex;
                catchRetIndex++;
            }
        }

        var resumeIPLocalNum = lvaGrabTemp(shortLifetime: true, "Wasm Resume IP");
        ref var resumeIPLocal = ref lvaGetDesc(resumeIPLocalNum);
        resumeIPLocal.Type = TYP_INT;
        lvaSetVarAddrExposed(resumeIPLocalNum, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
        lvaWasmResumeIP = resumeIPLocalNum;

        for (var regionIndex = 0; regionIndex < catchRetBlocksByTryRegion.Length; regionIndex++)
        {
            var catchRetBlocks = catchRetBlocksByTryRegion[regionIndex];
            if (catchRetBlocks is not null)
            {
                fgWasmEhTransformTry(catchRetBlocks, checked((ushort)regionIndex), resumeIPLocalNum);
            }
        }

        foreach (var catchRetBlocks in catchRetBlocksByTryRegion)
        {
            if (catchRetBlocks is null)
            {
                continue;
            }

            foreach (var catchRetBlock in catchRetBlocks.TopDownOrder())
            {
                JITDUMP(
                    $"Setting control variable V{resumeIPLocalNum:D2} to {catchRetBlock.bbPreorderNum} " +
                    $"in {FMT_BB(catchRetBlock.bbNum)}\n");
                var valueNode = gtNewIconNode(TYP_INT, catchRetBlock.bbPreorderNum);
                var storeNode = gtNewStoreLclVarNode(resumeIPLocalNum, valueNode);
                LIR.InsertBeforeTerminator(catchRetBlock, LIR.SeqTree(this, storeNode));
            }
        }

        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private unsafe void fgWasmEhTransformTry(
        ArrayStack<BasicBlock> catchRetBlocks,
        ushort regionIndex,
        int resumeIPLocalNum)
    {
        assert(catchRetBlocks.Height() > 0);

        ref var descriptor = ref ehGetDsc(regionIndex);
        var regionEntryBlock = descriptor.ebdTryBeg;
        var regionLastBlock = descriptor.ebdTryLast;

        var enclosingTryIndex = ehTrueEnclosingTryIndex(regionIndex);
        var enclosingHndIndex = ehGetEnclosingHndIndex(regionIndex);
        var biasedEnclosingTryIndex = enclosingTryIndex is EHblkDsc.NO_ENCLOSING_INDEX
            ? (ushort)0
            : checked((ushort)(enclosingTryIndex + 1));
        var biasedEnclosingHndIndex = enclosingHndIndex is EHblkDsc.NO_ENCLOSING_INDEX
            ? (ushort)0
            : checked((ushort)(enclosingHndIndex + 1));

        var switchBlock = fgNewBBinRegion(
            BBJ_SWITCH, biasedEnclosingTryIndex, biasedEnclosingHndIndex, regionLastBlock);
        var rethrowBlock = fgNewBBinRegion(
            BBJ_THROW, biasedEnclosingTryIndex, biasedEnclosingHndIndex, switchBlock);
        switchBlock.inheritWeightPercentage(regionEntryBlock, 0);
        rethrowBlock.inheritWeightPercentage(regionEntryBlock, 0);

        _ = fgSplitBlockAtBeginning(regionEntryBlock);
        assert(regionEntryBlock.IsEmpty);
        assert(regionEntryBlock.Kind is BBJ_ALWAYS);

        var normalEntryEdge = regionEntryBlock.TargetEdge;
        var resumptionEdge = fgAddRefPred(switchBlock, regionEntryBlock);
        resumptionEdge.Likelihood = 0;
        regionEntryBlock.SetCond(resumptionEdge, normalEntryEdge);

        var jumpNode = new GenTree(GT_WASM_JEXCEPT, TYP_VOID);
        regionEntryBlock.InsertAtEnd(LIR.SeqTree(this, jumpNode));

        var caseCount = catchRetBlocks.Height() + 1;
        var caseBias = catchRetBlocks.Top().bbPreorderNum;
        var cases = new FlowEdge[caseCount];
        var caseNumber = 0;
        JITDUMP($"Dispatch switch block is {FMT_BB(switchBlock.bbNum)}; {caseCount} cases\n");

        var resumePads = new Dictionary<BasicBlock, BasicBlock>();
        var continuationEdges = new Dictionary<BasicBlock, FlowEdge>();
        var verifyGCModeTransitions =
            IsReadyToRun && opts.jitFlags->IsSet(JitFlags.JIT_FLAG_VERIFY_GC_MODE_TRANSITIONS);

        foreach (var catchRetBlock in catchRetBlocks.TopDownOrder())
        {
            assert(catchRetBlock.Kind is BBJ_EHCATCHRET);
            var continuation = catchRetBlock.Target;
            var caseIndex = catchRetBlock.bbPreorderNum;
            assert(caseIndex >= caseBias);
            var biasedCaseIndex = caseIndex - caseBias;
            assert(biasedCaseIndex < caseCount);
            JITDUMP($"  case {biasedCaseIndex}: {FMT_BB(continuation.bbNum)}\n");

            FlowEdge caseEdge;
            if (resumePads.TryGetValue(continuation, out var resumePad))
            {
                if (!continuationEdges.TryGetValue(continuation, out var existingEdge))
                {
                    throw new InvalidOperationException("A Wasm resume pad has no switch edge.");
                }

                caseEdge = existingEdge;
                caseEdge.incrementDupCount();
                resumePad.bbRefs++;
            }
            else
            {
                resumePad = fgNewBBafter(BBJ_ALWAYS, switchBlock, extendRegion: false);
                resumePad.copyEHRegion(switchBlock);
                resumePad.inheritWeightPercentage(switchBlock, 0);
                if (bbInTryRegions(regionIndex, continuation))
                {
                    resumePad.SetFlags(BBF_CATCH_RESUMPTION);
                }

                var padEdge = fgAddRefPred(continuation, resumePad);
                padEdge.Likelihood = 1.0;
                resumePad.SetKindAndTargetEdge(BBJ_ALWAYS, padEdge);

                // Clear the index only after this dispatcher accepts the resumption; an inner mismatch preserves it.
                var zero = gtNewIconNode(TYP_INT, 0);
                var store = gtNewStoreLclVarNode(resumeIPLocalNum, zero);
                resumePad.InsertAtEnd(LIR.SeqTree(this, store));

                if (verifyGCModeTransitions)
                {
                    GenTree resumeAfterCatch = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_JIT_RESUME_AFTER_CATCH);
                    resumeAfterCatch = fgMorphCall(resumeAfterCatch.AsCall());
                    _ = gtSetEvalOrder(resumeAfterCatch);
                    resumePad.InsertAtEnd(LIR.SeqTree(this, resumeAfterCatch));
                }

                resumePads.Add(continuation, resumePad);
                caseEdge = fgAddRefPred(resumePad, switchBlock);
                continuationEdges.Add(continuation, caseEdge);
                caseEdge.Likelihood = 0;

                JITDUMP($"Resume pad {FMT_BB(resumePad.bbNum)} for {FMT_BB(continuation.bbNum)}\n");
            }

            assert(cases[biasedCaseIndex] is null);
            cases[biasedCaseIndex] = caseEdge;
            caseNumber++;
        }

        var rethrowCaseEdge = fgAddRefPred(rethrowBlock, switchBlock);
        rethrowCaseEdge.Likelihood = 1.0;
        JITDUMP($"  case {caseNumber}: {FMT_BB(rethrowBlock.bbNum)} [default]\n");
        cases[caseNumber++] = rethrowCaseEdge;
        assert(caseNumber == caseCount);

        // Keep the native first-seen successor order while using a set only for membership.
        var successors = new List<FlowEdge>(caseCount);
        var uniqueSuccessors = new HashSet<BasicBlock>();
        foreach (var catchRetBlock in catchRetBlocks.TopDownOrder())
        {
            var continuation = catchRetBlock.Target;
            if (!resumePads.TryGetValue(continuation, out var resumePad) ||
                !continuationEdges.TryGetValue(continuation, out var edge))
            {
                throw new InvalidOperationException("A Wasm continuation is missing its resume pad or switch edge.");
            }

            if (uniqueSuccessors.Add(resumePad))
            {
                successors.Add(edge);
            }
        }

        if (uniqueSuccessors.Add(rethrowBlock))
        {
            successors.Add(rethrowCaseEdge);
        }

        var switchTargets = new BBswtDesc([.. successors], new int[caseCount], hasDefault: true);
        cases.AsSpan().CopyTo(switchTargets.Cases);
        switchBlock.SwitchTargets = switchTargets;
        switchBlock.SetFlags(BBF_CATCH_RESUMPTION);

        var biasValue = gtNewIconNode(TYP_INT, caseBias);
        var controlVar = gtNewLclvNode(TYP_INT, resumeIPLocalNum);
        var adjustedControlVar = gtNewBinaryNode(GT_SUB, TYP_INT, controlVar, biasValue);
        var switchNode = gtNewUnaryNode(GT_SWITCH, TYP_VOID, adjustedControlVar);
        switchBlock.InsertAtEnd(LIR.SeqTree(this, switchNode));

        var rethrowNode = new GenTree(GT_WASM_THROW_REF, TYP_VOID);
        rethrowBlock.InsertAtEnd(LIR.SeqTree(this, rethrowNode));
    }
#endif
}
