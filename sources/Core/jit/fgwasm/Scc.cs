// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

#if TARGET_WASM
// Includes nested SCCs wholly within this component, excluding its entry nodes.
internal sealed class Scc
{
    private readonly FgWasm _fgWasm;
    private readonly Compiler _compiler;
    private readonly FlowGraphDfsTree _dfsTree;
    private readonly BitVecTraits _traits;
    private readonly BitVec _blocks;
    private readonly BitVec _entries;
    private readonly List<Scc> _nested = [];
    private BasicBlock? _tryEntry;
    private uint _numIrr;
    private ushort _enclosingTryIndex;
    private ushort _enclosingHndIndex;
    private weight_t _entryWeight;
    private readonly uint _num;

    internal Scc(FgWasm fgWasm, BasicBlock block)
    {
        _fgWasm = fgWasm;
        _compiler = fgWasm.Comp();
        _dfsTree = fgWasm.GetDfsTree();
        _traits = fgWasm.GetTraits();
        _blocks = BitVecOps.MakeEmpty(_traits);
        _entries = BitVecOps.MakeEmpty(_traits);
        _num = fgWasm.GetNextSccNum();
        Add(block);
    }

    internal void Add(BasicBlock block)
    {
        BitVecOps.AddElemD(_traits, _blocks, block.bbPostorderNum);
    }

    // Named to avoid the CLR object finalizer contract.
    internal void FinalizeScc()
    {
        ComputeEntries();
        FindNested();
    }

    internal bool IsWasmTryCatch(BasicBlock block)
    {
        assert(_compiler.bbIsTryBeg(block));
        var lastNode = block.LastLIRNode;

        return (lastNode is not null) && lastNode.OperIs(GT_WASM_JEXCEPT);
    }

    internal void ComputeEntries()
    {
        JITDUMP($"Scc {_num} has {BitVecOps.Count(_traits, _blocks)} blocks\n");
        var iterator = new BitVecOps.Iter(_traits, _blocks);
        uint poNum = 0;
        var isFirstEntry = true;

        while (iterator.NextElem(ref poNum))
        {
            var block = _dfsTree.GetPostOrder((int)poNum);

            if (block.isBBCallFinallyPairTail)
            {
                continue;
            }

            foreach (var pred in block.PredBlocks)
            {
                if (pred.Kind is BBJ_EHCATCHRET or BBJ_EHFILTERRET or BBJ_EHFAULTRET)
                {
                    continue;
                }

                if (BitVecOps.IsMember(_traits, _blocks, pred.bbPostorderNum))
                {
                    continue;
                }

                if (BitVecOps.TryAddElemD(_traits, _entries, block.bbPostorderNum))
                {
                    JITDUMP($"{FMT_BB(block.bbNum)} is scc {_num} entry via {FMT_BB(pred.bbNum)}\n");
                    _entryWeight += block.bbWeight;

                    if (isFirstEntry)
                    {
                        _enclosingTryIndex = block.bbTryIndex;
                        _enclosingHndIndex = block.bbHndIndex;
                        isFirstEntry = false;
                    }
                    else
                    {
                        assert(_enclosingHndIndex == block.bbHndIndex);
                        _enclosingTryIndex = _compiler.bbFindInnermostCommonTryRegion(_enclosingTryIndex, block);
                    }

                    // The pinned transform supports at most one SCC entry that is also a try entry.
                    if (_compiler.bbIsTryBeg(block))
                    {
                        if (_tryEntry is null)
                        {
                            _tryEntry = block;
                        }
                        else
                        {
                            JITDUMP($"Multiple try entries in SCC {_num} entry set\n");
                            IMPL_LIMITATION("Wasm SCC with multiple try entry headers");
                        }
                    }
                }
            }
        }

#if DEBUG
        if (_compiler.verbose)
        {
            Dump();
        }
#endif
    }

    internal uint NumEntries() => (uint)BitVecOps.Count(_traits, _entries);

    internal uint NumBlocks() => (uint)BitVecOps.Count(_traits, _blocks);

    internal BitVec InternalBlocks() => BitVecOps.Diff(_traits, _blocks, _entries);

    internal bool IsIrr() => NumEntries() > 1;

    internal uint NumIrr()
    {
        _numIrr = IsIrr() ? 1u : 0u;

        foreach (var nested in _nested)
        {
            _numIrr = unchecked(_numIrr + nested.NumIrr());
        }

        return _numIrr;
    }

    internal weight_t TotalEntryWeight() => _entryWeight;

#if DEBUG
    internal void Dump(int indent = 0)
    {
        var iterator = new BitVecOps.Iter(_traits, _blocks);
        uint poNum = 0;
        var first = true;

        while (iterator.NextElem(ref poNum))
        {
            if (first)
            {
                // Native %*c emits one space even when the requested width is zero.
                jitprintf(new string(' ', Math.Max(1, indent)));
                jitprintf(NumEntries() > 1 ? $"[irrd ({NumBlocks()})] " : $"[loop ({NumBlocks()})] ");
            }
            else
            {
                jitprintf(", ");
            }

            first = false;
            var block = _dfsTree.GetPostOrder((int)poNum);
            var isEntry = BitVecOps.IsMember(_traits, _entries, (int)poNum);
            jitprintf($"{FMT_BB(block.bbNum)}{(isEntry ? "e" : "")}");
        }

        jitprintf("\n");
    }

    internal void DumpDot()
    {
        jitprintf($"digraph SCC_{_num} {{\n");
        var iterator = new BitVecOps.Iter(_traits, _blocks);
        uint poNum = 0;

        while (iterator.NextElem(ref poNum))
        {
            var block = _dfsTree.GetPostOrder((int)poNum);
            var isEntry = BitVecOps.IsMember(_traits, _entries, (int)poNum);
            jitprintf($"{FMT_BB(block.bbNum)}{(isEntry ? " [style=filled]" : "")};");

            if (isEntry)
            {
                foreach (var pred in block.PredBlocks)
                {
                    if (pred.Kind is BBJ_EHCATCHRET or BBJ_EHFILTERRET or BBJ_EHFAULTRET)
                    {
                        continue;
                    }

                    if (BitVecOps.IsMember(_traits, _blocks, pred.bbPostorderNum))
                    {
                        continue;
                    }

                    jitprintf($"{FMT_BB(pred.bbNum)} -> {FMT_BB(block.bbNum)};\n");
                }
            }

            foreach (var succ in _fgWasm.SccDiagnosticSuccessors(block))
            {
                jitprintf($"{FMT_BB(block.bbNum)} -> {FMT_BB(succ.bbNum)};\n");
            }
        }

        jitprintf("}\n");
    }

    internal void DumpAll(int indent = 0)
    {
        Dump(indent);

        foreach (var child in _nested)
        {
            child.DumpAll(indent + 3);
        }
    }
#endif

    internal void FindNested()
    {
        var entryCount = NumEntries();
        assert(entryCount > 0);
        var nestedBlocks = InternalBlocks();
        var nestedCount = (int)BitVecOps.Count(_traits, nestedBlocks);

        if (nestedCount == 0)
        {
            return;
        }

        JITDUMP($"Scc {_num}  has {nestedCount} non-entry blocks. Scc Graph:\n");
#if DEBUG
        if (_compiler.verbose)
        {
            DumpDot();
        }
#endif
        JITDUMP($"\nLooking for nested SCCs in SCC {_num}\n");
        var postOrder = new BasicBlock[nestedCount];

#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"digraph scc_{_num}_nested_subgraph{nestedCount} {{\n");
            var iterator = new BitVecOps.Iter(_traits, nestedBlocks);
            uint poNum = 0;

            while (iterator.NextElem(ref poNum))
            {
                var block = _dfsTree.GetPostOrder((int)poNum);
                JITDUMP($"{FMT_BB(block.bbNum)};\n");

                foreach (var succ in _fgWasm.SccDiagnosticSuccessors(block))
                {
                    JITDUMP($"{FMT_BB(block.bbNum)} -> {FMT_BB(succ.bbNum)};\n");
                }
            }

            JITDUMP("}\n");
        }
#endif

        var numBlocks = _fgWasm.WasmRunSubgraphDfs(
            static (block, preorderNum) => { },
            (block, postorderNum) => postOrder[postorderNum] = block,
            static (block, succ) => { },
            nestedBlocks, useProfile: true);

        if (numBlocks != nestedCount)
        {
            JITDUMP($"Eh? numBlocks {numBlocks} nestedCount {nestedCount}\n");
        }

        assert(numBlocks == nestedCount);

        var nestedSccs = new ArrayStack<Scc>();
        _fgWasm.WasmFindSccsCore(nestedBlocks, nestedSccs, postOrder, nestedCount);
        var nNested = nestedSccs.Height();

        if (nNested == 0)
        {
            return;
        }

        for (var i = 0; i < nNested; i++)
        {
            _nested.Add(nestedSccs.Bottom(i));
        }

        JITDUMP($"\n <-- nested in Scc {_num}... \n");
    }

    internal ushort EnclosingTryIndex() => _enclosingTryIndex;

    internal ushort EnclosingHndIndex() => _enclosingHndIndex;

    internal BasicBlock? TryHeader() => _tryEntry;

    // Assign each entry a dense index, route its incoming edges through one switch,
    // then transform nested components. Entry order is ascending original postorder.
    internal bool TransformViaSwitchDispatch()
    {
        var modified = false;
        var numHeaders = (int)NumEntries();

        if (numHeaders > 1)
        {
            JITDUMP("Transforming Scc via switch dispatch: ");
#if DEBUG
            if (_compiler.verbose)
            {
                Dump();
            }
#endif
            modified = true;

            var controlVarNum = _compiler.lvaGrabTemp(shortLifetime: false, "Scc control var");
            ref var controlVarDsc = ref _compiler.lvaGetDesc(controlVarNum);
            controlVarDsc.Type = TYP_INT;
            BasicBlock? dispatcher = null;
            var tryHeader = TryHeader();
            var succs = new FlowEdge[numHeaders];
            var cases = new FlowEdge[numHeaders];
            var predBlocks = new List<BasicBlock>();
            var predOffsets = new List<int>();
            var headerNumber = 0;
            var iterator = new BitVecOps.Iter(_traits, _entries);
            uint poHeaderNumber = 0;
            var netLikelihood = 0.0;

            // Earlier rewrites can create new predecessors of a later header.
            // Only transform edges whose source appeared in the original snapshot.
            while (iterator.NextElem(ref poHeaderNumber))
            {
                var header = _dfsTree.GetPostOrder((int)poHeaderNumber);
                predOffsets.Add(predBlocks.Count);

                foreach (var pred in header.PredBlocks)
                {
                    predBlocks.Add(pred);
                }
            }

            predOffsets.Add(predBlocks.Count);
            iterator = new BitVecOps.Iter(_traits, _entries);

            while (iterator.NextElem(ref poHeaderNumber))
            {
                var header = _dfsTree.GetPostOrder((int)poHeaderNumber);

                if (dispatcher is null)
                {
                    // A try-entry dispatcher must be inside that try, avoiding a
                    // middle-entry case edge rejected by FlowGraphTryRegions.Build.
                    var dispatchTryIndex = EnclosingTryIndex();
                    var dispatchHndIndex = EnclosingHndIndex();
                    BasicBlock? nearBlk = null;

                    if (tryHeader is not null)
                    {
                        dispatchTryIndex = tryHeader.bbTryIndex;
                        dispatchHndIndex = tryHeader.bbHndIndex;
                        nearBlk = tryHeader;
                    }

                    if ((dispatchTryIndex > 0) || (dispatchHndIndex > 0))
                    {
                        var inTry = ((dispatchTryIndex != 0) && (dispatchHndIndex == 0)) ||
                            (dispatchTryIndex < dispatchHndIndex);

                        if (inTry)
                        {
                            JITDUMP($"Dispatch header needs to go in try of EH#{dispatchTryIndex - 1:D2} ...\n");
                        }
                        else
                        {
                            JITDUMP($"Dispatch header needs to go in handler of EH#{dispatchHndIndex - 1:D2} ...\n");
                        }
                    }
                    else
                    {
                        JITDUMP("Dispatch header needs to go in method region\n");
                    }

                    dispatcher = _compiler.fgNewBBinRegion(BBJ_SWITCH, dispatchTryIndex, dispatchHndIndex, nearBlk);
                    dispatcher.setBBProfileWeight(TotalEntryWeight());
                }

                JITDUMP($"\nFixing flow for preds of header {FMT_BB(header.bbNum)}\n");
                var inboundTarget = dispatcher;

                if (tryHeader is not null)
                {
                    // Only enclosing try regions can be reached safely from this dispatcher.
                    if (header.hasTryIndex && !_compiler.bbInTryRegions(header.TryIndex, tryHeader))
                    {
                        NYI_WASM("SCC entry header in a try region that does not enclose the try header");
                    }

                    if (header != tryHeader)
                    {
                        JITDUMP(
                            $"Will route flow to {FMT_BB(header.bbNum)} via try header {FMT_BB(tryHeader.bbNum)}\n");
                    }

                    inboundTarget = tryHeader;
                }

                var headerWeight = header.bbWeight;

                for (var predIndex = predOffsets[headerNumber]; predIndex < predOffsets[headerNumber + 1]; predIndex++)
                {
                    var pred = predBlocks[predIndex];

                    if (_compiler.fgGetPredForBlock(header, pred) is null)
                    {
                        continue;
                    }

                    // Catch resumption is modelled by the post-try dispatch, not this edge.
                    if (pred.Kind is BBJ_EHCATCHRET)
                    {
                        continue;
                    }

                    BasicBlock transferBlock;

                    if (pred.HasTarget && (pred.Target == header) && !pred.isBBCallFinallyPairTail)
                    {
                        transferBlock = pred;
                    }
                    else
                    {
                        assert(pred.Kind is not (BBJ_EHCATCHRET or BBJ_EHFAULTRET or BBJ_EHFILTERRET or BBJ_EHFINALLYRET));
                        transferBlock = _compiler.fgSplitEdge(pred, header);
                    }

                    var targetIndex = _compiler.gtNewIconNode(TYP_INT, headerNumber);
                    var storeControlVar = _compiler.gtNewStoreLclVarNode(controlVarNum, targetIndex);
                    var range = LIR.SeqTree(_compiler, storeControlVar);

                    if (transferBlock.isEmpty())
                    {
                        transferBlock.InsertAtEnd(range);
                    }
                    else
                    {
                        LIR.InsertBeforeTerminator(transferBlock, range);
                    }

                    if (inboundTarget != header)
                    {
                        _compiler.fgReplaceJumpTarget(transferBlock, header, inboundTarget);
                    }
                }

                var outboundTarget = header;

                if (header == tryHeader)
                {
                    // Keep the exception test in the try header; dispatch normal entry
                    // to its false target, or split off the body of a non-Wasm try header.
                    if (IsWasmTryCatch(header))
                    {
                        outboundTarget = header.FalseTarget;
                    }
                    else
                    {
                        outboundTarget = _compiler.fgSplitBlockAtBeginning(header);
                    }

                    _compiler.fgReplaceJumpTarget(header, outboundTarget, dispatcher);
                }

                var dispatchToOutboundTargetEdge = _compiler.fgAddRefPred(outboundTarget, dispatcher);

                if ((headerNumber + 1) == numHeaders)
                {
                    dispatchToOutboundTargetEdge.Likelihood = Math.Max(0.0, 1.0 - netLikelihood);
                }
                else if (TotalEntryWeight() > 0)
                {
                    dispatchToOutboundTargetEdge.Likelihood = headerWeight / TotalEntryWeight();
                }
                else
                {
                    dispatchToOutboundTargetEdge.Likelihood = 1.0 / numHeaders;
                }

                netLikelihood += dispatchToOutboundTargetEdge.Likelihood;
                succs[headerNumber] = dispatchToOutboundTargetEdge;
                cases[headerNumber] = dispatchToOutboundTargetEdge;
                headerNumber++;
            }

            tryHeader?.setBBProfileWeight(TotalEntryWeight());

            var dispatchBlock = dispatcher
                ?? throw new InvalidOperationException("An irreducible SCC requires a dispatch block.");
            JITDUMP($"\nDispatch header is {FMT_BB(dispatchBlock.bbNum)}; {numHeaders} cases\n");
            var swtDesc = new BBswtDesc(succs, new int[numHeaders], hasDefault: true);
            cases.AsSpan().CopyTo(swtDesc.Cases);
            dispatchBlock.SwitchTargets = swtDesc;

            var controlVar = _compiler.gtNewLclvNode(TYP_INT, controlVarNum);
            var switchNode = _compiler.gtNewUnaryNode(GT_SWITCH, TYP_VOID, controlVar);
            assert(dispatchBlock.isEmpty());
            dispatchBlock.InsertAtEnd(LIR.SeqTree(_compiler, switchNode));
        }

        foreach (var nested in _nested)
        {
            modified |= nested.TransformViaSwitchDispatch();
        }

        return modified;
    }
}
#endif
