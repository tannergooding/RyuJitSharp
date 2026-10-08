// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

#if TARGET_WASM
internal sealed partial class FgWasm
{
    private readonly Compiler _comp;
    private uint _sccNum;
    private FlowGraphDfsTree? _dfsTree;
    private BitVecTraits? _traits;

    internal FgWasm(Compiler comp)
    {
        _comp = comp;
    }

    internal Compiler Comp() => _comp;

    internal uint GetNextSccNum()
    {
        var number = _sccNum;
        _sccNum = unchecked(_sccNum + 1);

        return number;
    }

    internal FlowGraphDfsTree GetDfsTree() =>
        _dfsTree ?? throw new InvalidOperationException("Wasm SCC analysis requires a DFS tree.");

    internal void SetDfsAndTraits(FlowGraphDfsTree dfsTree)
    {
        assert(_dfsTree is null);
        _dfsTree = dfsTree;
        _traits = dfsTree.PostOrderTraits();
    }

    internal BitVecTraits GetTraits() =>
        _traits ?? throw new InvalidOperationException("Wasm SCC analysis requires postorder traits.");

    internal void WasmFindSccs(ArrayStack<Scc> sccs)
    {
        var dfsTree = GetDfsTree();
        assert(dfsTree.IsForWasm);
        var allBlocks = BitVecOps.MakeFull(GetTraits());
        WasmFindSccsCore(allBlocks, sccs, dfsTree.GetPostOrder(), dfsTree.PostOrderCount);
        uint numIrreducible = 0;

        if (sccs.Height() > 0)
        {
            JITDUMP("\n*** Sccs\n");

            foreach (var scc in sccs.BottomUpOrder())
            {
#if DEBUG
                if (_comp.verbose)
                {
                    scc.DumpAll();
                }
#endif
                numIrreducible = unchecked(numIrreducible + scc.NumIrr());
            }
        }
        else
        {
            JITDUMP("\n*** No Sccs\n");
        }

        if (numIrreducible > 0)
        {
            JITDUMP($"\n*** {numIrreducible} total Irreducible!\n");
        }
    }

    // Kosaraju's reverse-graph walk, in reverse postorder of the supplied subgraph.
    internal void WasmFindSccsCore(BitVec subset, ArrayStack<Scc> sccs, BasicBlock[] postorder, int postorderCount)
    {
        var map = new Dictionary<BasicBlock, Scc?>(ReferenceEqualityComparer.Instance);
        var traits = GetTraits();

        for (var i = 0; i < postorderCount; i++)
        {
            var rpoNum = postorderCount - i - 1;
            var block = postorder[rpoNum];

            if (!BitVecOps.IsMember(traits, subset, block.bbPostorderNum))
            {
                continue;
            }

            AssignBlockToScc(block, block, subset, sccs, map);
        }

        foreach (var scc in sccs.BottomUpOrder())
        {
            scc.FinalizeScc();
        }
    }

    internal void AssignBlockToScc(
        BasicBlock block, BasicBlock root, BitVec subset, ArrayStack<Scc> sccs,
        Dictionary<BasicBlock, Scc?> map)
    {
        var traits = GetTraits();

        if (!BitVecOps.IsMember(traits, subset, block.bbPostorderNum))
        {
            return;
        }

        // A null value marks a visited singleton; key presence, not its value, is the visited test.
        if (map.ContainsKey(block))
        {
            return;
        }

        JITDUMP($"Scc-reverse graph: visiting {FMT_BB(block.bbNum)} with root {FMT_BB(root.bbNum)}\n");

        var found = map.TryGetValue(root, out var scc);
        if (found)
        {
            assert(block != root);

            if (scc is null)
            {
                JITDUMP($"Root has been visited; forming SCC with root {FMT_BB(root.bbNum)}\n");
                scc = new Scc(this, root);
                map[root] = scc;
                sccs.Push(scc);
            }

            JITDUMP($"Adding {FMT_BB(block.bbNum)} to SCC with root {FMT_BB(root.bbNum)}\n");
            scc.Add(block);
        }

        map.Add(block, scc);

        if (_comp.bbIsHandlerBeg(block))
        {
            return;
        }

        if (block.isBBCallFinallyPairTail)
        {
            var callFinally = block.Prev
                ?? throw new InvalidOperationException("A call-finally pair tail requires its call-finally block.");
            AssignBlockToScc(callFinally, root, subset, sccs, map);

            return;
        }

        foreach (var pred in block.PredBlocks)
        {
            if (pred.Kind is BBJ_EHCATCHRET or BBJ_EHFILTERRET or BBJ_EHFAULTRET)
            {
                continue;
            }

            JITDUMP(
                $"Scc-reverse graph: walking back from {FMT_BB(block.bbNum)} to {FMT_BB(pred.bbNum)}, " +
                $"with root {FMT_BB(root.bbNum)}\n");
            AssignBlockToScc(pred, root, subset, sccs, map);
        }
    }

    internal bool WasmTransformSccs(ArrayStack<Scc> sccs)
    {
        var modified = false;

        foreach (var scc in sccs.BottomUpOrder())
        {
            modified |= scc.TransformViaSwitchDispatch();
        }

        return modified;
    }

    // B508: retain the native subgraph DFS until mutation-sensitive successor ordering is established.
    internal int WasmRunSubgraphDfs(
        Action<BasicBlock, int> visitPreorder, Action<BasicBlock, int> visitPostorder,
        Action<BasicBlock, BasicBlock> visitEdge, BitVec subgraph, bool useProfile = false)
    {
        NYI_WASM("WasmRunSubgraphDfs requires B508 successor key-order parity");
        throw new FatalJitException(CORJIT_SKIPPED);
    }

#if DEBUG
    // DumpDot and FindNested require the same ordered successors as WasmSuccessorEnumerator.
    internal IEnumerable<BasicBlock> SccDiagnosticSuccessors(BasicBlock block)
    {
        NYI_WASM("Wasm SCC successor diagnostics require B508 successor key-order parity");
        throw new FatalJitException(CORJIT_SKIPPED);
    }
#endif
}
#endif
