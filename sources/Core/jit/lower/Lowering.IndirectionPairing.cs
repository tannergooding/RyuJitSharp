// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    // Native smoke_tests found only 30 additional non-interfering candidates when
    // increasing this from 16 to 32, out of 112 additional distance-check passes.
    private const int LDP_STP_REORDERING_MAX_DISTANCE = 16;

    private readonly List<SavedIndir> _blockIndirs = [];

    private struct SavedIndir(GenTreeIndir indir, GenTreeLclVar addrBase, long offset)
    {
        public GenTreeIndir? Indir = indir;
        public GenTreeLclVar AddrBase = addrBase;
        public long Offset = offset;
    }

    private bool OptimizeForLdpStp(GenTreeIndir ind)
    {
        if ((ind.Type is not (TYP_INT or TYP_LONG or TYP_FLOAT or TYP_DOUBLE or TYP_SIMD8 or TYP_SIMD16)) ||
            ind.IsVolatile)
        {
            return false;
        }

        var addr = ind.Addr;
        CompilerInstance.gtPeelOffsets(ref addr, out var offset);
        if (addr.Oper is not GT_LCL_VAR)
        {
            return false;
        }

        // An indirection takes at least two nodes, so only half the distance
        // budget can be previous candidates.
        var maxCount = int.Min(_blockIndirs.Count, LDP_STP_REORDERING_MAX_DISTANCE / 2);
        for (var i = 0; i < maxCount; i++)
        {
            var savedIndex = _blockIndirs.Count - 1 - i;
            var previous = _blockIndirs[savedIndex];
            if (previous.AddrBase.LclNum != addr.AsLclVar().LclNum)
            {
                continue;
            }

            var prevIndir = previous.Indir;
            if ((prevIndir is null) || (prevIndir.Type != ind.Type) || (prevIndir.Next is null) ||
                (prevIndir.Oper.IsStore != ind.Oper.IsStore))
            {
                continue;
            }

#if DEBUG
            JITDUMP($"[{ind.TreeId:D6}] and [{prevIndir.TreeId:D6}] are indirs off the same base " +
                $"with offsets +{unchecked((uint)offset):D3} and +{unchecked((uint)previous.Offset):D3}\n");
#endif
            var distance = unchecked(offset - previous.Offset);
            if ((distance == ind.Type.Size) || (distance == -ind.Type.Size))
            {
                JITDUMP("  ..and they are amenable to ldp/stp optimization\n");
                if (TryMakeIndirsAdjacent(prevIndir, ind))
                {
                    // Reusing a matched candidate can turn offsets 4,0,8,12 into 4,8,0,12.
                    previous.Indir = null;
                    _blockIndirs[savedIndex] = previous;
                    return true;
                }
                break;
            }

            JITDUMP("  ..but at non-adjacent offset\n");
        }

        _blockIndirs.Add(new SavedIndir(ind, addr.AsLclVar(), offset));
        return false;
    }

    private bool TryMakeIndirsAdjacent(GenTreeIndir prevIndir, GenTreeIndir indir)
    {
        GenTree? current = prevIndir;
        for (var i = 0; i < LDP_STP_REORDERING_MAX_DISTANCE; i++)
        {
            assert(current is not null);
            assert((current._lirFlags & LIR.Flags.Mark) == 0);
            current = current.Next;
            if (current == indir)
            {
                break;
            }

            assert(current is not null);
            if (current.Oper is GT_CALL)
            {
#if DEBUG
                JITDUMP($"  ..but they are separated by node [{current.TreeId:D6}] that kills registers\n");
#endif
                return false;
            }
        }

        if (current != indir)
        {
            JITDUMP("  ..but they are too far separated\n");
            return false;
        }

        JITDUMP("  ..and they are close. Trying to move the following range (where * are nodes part of the data flow):\n\n");
#if DEBUG
        var startDumpNode = BlockRange().GetTreeRange(prevIndir, out _).FirstNode;
        var endDumpNode = indir.Next;
        void DumpWithMarks()
        {
            if (!CompilerInstance.verbose)
            {
                return;
            }

            for (var node = startDumpNode; node != endDumpNode; node = node.Next)
            {
                assert(node is not null);
                var prefix = node == prevIndir ? "1. " :
                    node == indir ? "2. " : (node._lirFlags & LIR.Flags.Mark) != 0 ? "*  " : "   ";
                CompilerInstance.gtDispLIRNode(node, prefix);
            }
        }
#endif

        try
        {
            MarkTree(indir);
#if DEBUG
            DumpWithMarks();
#endif
            JITDUMP("\n");

            if ((prevIndir._lirFlags & LIR.Flags.Mark) != 0)
            {
                JITDUMP("Previous indir is part of the data flow of current indir\n");
                return false;
            }

            _scratchSideEffects.Clear();
            var sawData = false;
            for (var node = prevIndir.Next; node != indir; node = node.Next)
            {
                assert(node is not null);
                if ((node._lirFlags & LIR.Flags.Mark) != 0)
                {
                    if (_scratchSideEffects.InterferesWith(CompilerInstance, node, true))
                    {
#if DEBUG
                        JITDUMP($"Giving up due to interference with [{node.TreeId:D6}]\n");
#endif
                        return false;
                    }

                    if (indir.Oper.IsStore)
                    {
                        sawData |= node == indir.Data;
                    }
                }
                else
                {
                    _scratchSideEffects.AddNode(CompilerInstance, node);
                }
            }

            if (_scratchSideEffects.InterferesWith(CompilerInstance, indir, true))
            {
                if (!indir.Oper.IsLoad)
                {
                    JITDUMP("Have conservative interference with last store. Giving up.\n");
                    return false;
                }

                // The earlier access establishes non-faulting behavior. Nonvolatile
                // accesses can move despite ordering hints, and distinct offset
                // ranges can establish non-aliasing for a stable local or REF base.
                JITDUMP("Have conservative interference with last indir. Trying a smarter interference check...\n");
                var indirAddr = indir.Addr;
                CompilerInstance.gtPeelOffsets(ref indirAddr, out var offset);
                var checkLocal = indirAddr.Oper.IsLocal;
                if (checkLocal)
                {
                    var localNumber = indirAddr.AsLclVarCommon().LclNum;
                    checkLocal = !CompilerInstance.lvaGetDesc(localNumber).IsAddressExposed &&
                        !_scratchSideEffects.WritesLocal(localNumber);
                }

                bool Interferes(GenTree node)
                {
                    if (((node.Flags & GTF_ORDER_SIDEEFF) != 0) && node.SupportsOrderingSideEffect() &&
                        ((node.Oper is not (GT_IND or GT_BLK or GT_STOREIND or GT_STORE_BLK)) || node.AsIndir().IsVolatile))
                    {
                        return true;
                    }

                    var nodeInfo = new AliasSet.NodeInfo(CompilerInstance, node);
                    if (nodeInfo.WritesAddressableLocation)
                    {
                        if (node.Oper is not (GT_STOREIND or GT_STORE_BLK))
                        {
                            return true;
                        }

                        var store = node.AsIndir();
                        var storeAddr = store.Addr;
                        CompilerInstance.gtPeelOffsets(ref storeAddr, out var storeOffset);
                        var distinct = (unchecked(storeOffset + store.Size) <= offset) ||
                            (unchecked(offset + indir.Size) <= storeOffset);

                        if (checkLocal && GenTree.Compare(indirAddr, storeAddr) && distinct)
                        {
#if DEBUG
                            JITDUMP($"Cannot interfere with [{node.TreeId:D6}] since they are off the same local " +
                                $"V{indirAddr.AsLclVarCommon().LclNum:D2} and indir range " +
                                $"[{unchecked((uint)offset):D3}..{unchecked((uint)offset + (uint)indir.Size):D3}) " +
                                $"does not interfere with store range " +
                                $"[{unchecked((uint)storeOffset):D3}..{unchecked((uint)storeOffset + (uint)store.Size):D3})\n");
#endif
                        }
                        else if ((indirAddr.Type is TYP_REF) && (storeAddr.Type is TYP_REF) && distinct)
                        {
#if DEBUG
                            JITDUMP($"Cannot interfere with [{node.TreeId:D6}] since they are both off TYP_REF bases " +
                                $"and indir range [{unchecked((uint)offset):D3}..{unchecked((uint)offset + (uint)indir.Size):D3}) " +
                                $"does not interfere with store range " +
                                $"[{unchecked((uint)storeOffset):D3}..{unchecked((uint)storeOffset + (uint)store.Size):D3})\n");
#endif
                        }
                        else
                        {
                            return true;
                        }
                    }

                    return false;
                }

                for (var node = indir.Prev; node != prevIndir; node = node.Prev)
                {
                    assert(node is not null);
                    if ((node._lirFlags & LIR.Flags.Mark) != 0)
                    {
                        continue;
                    }

                    if (Interferes(node))
                    {
#if DEBUG
                        JITDUMP($"Indir [{indir.TreeId:D6}] interferes with [{node.TreeId:D6}]\n");
#endif
                        return false;
                    }
                }
            }

            JITDUMP("Interference checks passed: can move unrelated nodes past second indir.\n");
            if (sawData)
            {
                _scratchSideEffects.Clear();
                _scratchSideEffects.AddNode(CompilerInstance, prevIndir);
                for (var node = prevIndir.Next; ; node = node.Next)
                {
                    assert(node is not null);
                    if (((node._lirFlags & LIR.Flags.Mark) != 0) &&
                        _scratchSideEffects.InterferesWith(CompilerInstance, node, true))
                    {
#if DEBUG
                        JITDUMP($"Cannot move prev indir [{prevIndir.TreeId:D6}] up past [{node.TreeId:D6}] " +
                            $"to get it past the data computation\n");
#endif
                        return false;
                    }

                    if (node == indir.Data)
                    {
                        break;
                    }
                }
            }

            // Some hardware loses store-to-load forwarding when LDRs become LDP.
            if (prevIndir.Oper.IsLoad && indir.Oper.IsLoad && IsStoreToLoadForwardingCandidateInLoop(prevIndir, indir))
            {
                JITDUMP("Avoiding making indirs adjacent; this may be the target of a store-to-load forwarding candidate\n");
                return false;
            }

#if DEBUG
            JITDUMP($"Moving nodes that are not part of data flow of [{indir.TreeId:D6}]\n\n");
#endif
            GenTree previous = prevIndir;
            for (var node = prevIndir.Next; ;)
            {
                assert(node is not null);
                var next = node.Next;
                if ((node._lirFlags & LIR.Flags.Mark) != 0)
                {
                    BlockRange().Remove(node);
                    BlockRange().InsertAfter(previous, node);
                    previous = node;
                }

                if (node == indir)
                {
                    break;
                }
                node = next;
            }

            if (sawData)
            {
                // Keep equal constants non-overlapping so LSRA can reuse their register.
                if ((indir.Data.Oper is GT_CNS_INT or GT_CNS_DBL) && GenTree.Compare(indir.Data, prevIndir.Data))
                {
                    JITDUMP("Not moving previous indir since we are expecting constant reuse for the data\n");
                }
                else
                {
                    BlockRange().Remove(prevIndir);
                    BlockRange().InsertAfter(indir.Data, prevIndir);
                }
            }

            JITDUMP("Result:\n\n");
#if DEBUG
            DumpWithMarks();
#endif
            JITDUMP("\n");
            return true;
        }
        finally
        {
            UnmarkTree(indir);
        }
    }

    private bool IsStoreToLoadForwardingCandidateInLoop(GenTreeIndir prevIndir, GenTreeIndir indir)
    {
        var compiler = CompilerInstance;
        compiler._dfsTree ??= compiler.fgComputeDfs();
        if (!compiler._dfsTree.HasCycle)
        {
            return false;
        }

        if (compiler._loops is null)
        {
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
        }

        assert(compiler._blockToLoop is not null);
        var loop = compiler._blockToLoop.GetLoop(BlockRange());
        if (loop is null)
        {
            return false;
        }

        var addr1 = prevIndir.Addr;
        compiler.gtPeelOffsets(ref addr1, out var offset1);
        var local1 = addr1.Oper is GT_LCL_VAR ? addr1.AsLclVarCommon().LclNum : BAD_VAR_NUM;
        var addr2 = indir.Addr;
        compiler.gtPeelOffsets(ref addr2, out var offset2);
        var local2 = addr1.Oper is GT_LCL_VAR ? addr2.AsLclVarCommon().LclNum : BAD_VAR_NUM;
        var budget = 100;

        bool CheckNodes(GenTree lastNode, GenTree firstNode, out bool hasStore, out bool hasDef)
        {
            hasStore = false;
            hasDef = false;
            for (var node = lastNode; ; node = node.Prev)
            {
                assert(node is not null);
                if (node.Oper is GT_STORE_LCL_VAR)
                {
                    var localNumber = node.AsLclVarCommon().LclNum;
                    if ((localNumber == local1) || (localNumber == local2))
                    {
                        hasDef = true;
                        return true;
                    }
                }
                else if (node.Oper is GT_STOREIND)
                {
                    var storeAddr = node.AsIndir().Addr;
                    compiler.gtPeelOffsets(ref storeAddr, out var storeOffset);
                    if ((storeAddr.Oper is GT_LCL_VAR) && ((storeOffset == offset1) || (storeOffset == offset2)))
                    {
                        var storeLocal = storeAddr.AsLclVarCommon().LclNum;
                        if ((storeLocal == local1) || (storeLocal == local2))
                        {
#if DEBUG
                            JITDUMP($"Store at [{node.TreeId:D6}] may allow store-to-load forwarding of indir " +
                                $"[{(storeLocal == local1 ? prevIndir : indir).TreeId:D6}]\n");
#endif
                            hasStore = true;
                            return true;
                        }
                    }
                }

                if (node == firstNode)
                {
                    break;
                }

                if (--budget == 0)
                {
                    return false;
                }
            }

            return true;
        }

        var firstNode = BlockRange().FirstNode;
        assert(firstNode is not null);
        if (!CheckNodes(prevIndir, firstNode, out var hasStore, out var hasDef))
        {
            return false;
        }

        if (hasStore)
        {
            return true;
        }
        if (hasDef)
        {
            return false;
        }

        var traits = compiler._dfsTree.PostOrderTraits();
        var visited = BitVecOps.MakeEmpty(traits);
        var stack = new Stack<BasicBlock>();
        void PushPreds(BasicBlock block)
        {
            foreach (var pred in block.PredBlocks)
            {
                if (loop.ContainsBlock(pred) && BitVecOps.TryAddElemD(traits, visited, pred.bbPostorderNum))
                {
                    stack.Push(pred);
                }
            }
        }

        PushPreds(BlockRange());
        while (stack.TryPop(out var block))
        {
            firstNode = block == _block ? prevIndir : block.FirstNode;
            if (firstNode is not null)
            {
                assert(block.LastNode is not null);
                if (!CheckNodes(block.LastNode, firstNode, out hasStore, out hasDef))
                {
                    return false;
                }
            }

            if (hasStore)
            {
                return true;
            }
            if (hasDef)
            {
                continue;
            }

            PushPreds(block);
        }

        return false;
    }

    private static void MarkTree(GenTree node)
    {
        node._lirFlags |= LIR.Flags.Mark;
        foreach (var operand in node.Operands)
        {
            MarkTree(operand);
        }
    }

    private static void UnmarkTree(GenTree node)
    {
        node._lirFlags &= ~LIR.Flags.Mark;
        foreach (var operand in node.Operands)
        {
            UnmarkTree(operand);
        }
    }
}
#endif
