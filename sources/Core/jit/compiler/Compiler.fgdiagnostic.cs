// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public void fgDebugCheckNodeLinks(BasicBlock block, Statement stmt)
    {
        assert(fgNodeThreading is not NodeThreading.None);
        noway_assert(stmt.TreeListBegin is not null);
        assert(stmt.TreeListBegin.Prev is null);

        for (var tree = stmt.TreeListBegin; tree is not null; tree = tree.Next)
        {
            if (tree.Prev is not null)
            {
                noway_assert(tree.Prev.Next == tree);
            }
            else
            {
                noway_assert(tree == stmt.TreeListBegin);
            }

            if (tree.Next is not null)
            {
                noway_assert(tree.Next.Prev == tree);
            }
            else
            {
                noway_assert(tree == stmt.RootNode);
            }

            GenTree? expectedPrevTree = null;

            if (tree.Oper.IsLeaf)
            {
                if (tree.Oper is GT_CATCH_ARG)
                {
                    noway_assert((tree.Flags & GTF_ORDER_SIDEEFF) is not 0);
                    noway_assert(stmt == block.GetFirstNonPhiDef());
                    noway_assert(stmt.TreeListBegin.Oper is GT_CATCH_ARG);
                    noway_assert((stmt.RootNode.Flags & GTF_ORDER_SIDEEFF) is not 0);
                }
                else if (tree.Oper is GT_ASYNC_CONTINUATION)
                {
                    assert((tree.Flags & GTF_ORDER_SIDEEFF) is not 0);
                }
            }

            if (tree.Oper.IsUnary && (tree.AsOp().Op1 is not null))
            {
                expectedPrevTree = tree.AsOp().Op1;
            }
            else if (tree.Oper.IsBinary && (tree.AsOp().Op1 is not null))
            {
                switch (tree.Oper)
                {
                    case GT_QMARK:
                    {
                        expectedPrevTree = tree.AsOp().Op2.AsColon().ThenNode;
                        break;
                    }

                    case GT_COLON:
                    {
                        expectedPrevTree = tree.AsColon().ElseNode;
                        break;
                    }

                    default:
                    {
                        expectedPrevTree = (tree.AsOp().Op2 is null) || tree.IsReverseOp ? tree.AsOp().Op1 : tree.AsOp().Op2;
                        break;
                    }
                }
            }

            noway_assert((expectedPrevTree is null) || (tree.Prev == expectedPrevTree));
        }
    }

    private static int bbTraverseLabel = 1;

    // Check that bbNum, bbRefs, and bbPreds are consistent with the block list.
    public unsafe void fgDebugCheckBBlist(bool checkBBNum = false, bool checkBBRefs = true)
    {
        if (verbose)
        {
            JITDUMP("*************** In fgDebugCheckBBlist\n");
        }

        if (compIsForInlining && compInlineResult.IsFailure)
        {
            JITDUMP("... failed inline attempt, no checking needed\n");
            return;
        }

        if ((fgBBcount > 10000) && (expensiveDebugCheckLevel < 1))
        {
            return;
        }

        fgDebugCheckBlockLinks();

        var reachedFirstFunclet = false;

        if (fgFuncletsCreated && (fgFirstFuncletBB is not null))
        {
            assert(bbIsFuncletBeg(fgFirstFuncletBB));
        }

        var curTraversalStamp = Interlocked.Increment(ref bbTraverseLabel);

        foreach (var block in Blocks)
        {
            block.bbTraversalStamp = curTraversalStamp;
        }

        var allNodesLinked = (fgNodeThreading is NodeThreading.AllTrees) || (fgNodeThreading is NodeThreading.LIR);
        var numBlocks = 0;
        var maxBBNum = 0;

        foreach (var block in Blocks)
        {
            numBlocks++;

            if (checkBBNum)
            {
                assert(block.IsLast || (block.bbNum + 1 == block.Next.bbNum));
            }

            maxBBNum = Math.Max(maxBBNum, block.bbNum);

            foreach (var succBlock in block.Succs)
            {
                assert(succBlock.bbTraversalStamp == curTraversalStamp);
            }

            if (compPostImportationCleanupDone || block.HasFlag(BBF_IMPORTED))
            {
                if (block.Kind is BBJ_COND)
                {
                    var lastNode = block.IsLIR ? block.LastNode : block.LastStmt?.RootNode;
                    assert(lastNode is not null);
                    assert((!allNodesLinked || (lastNode.Next is null)) && lastNode.Oper.IsConditionalJump);
                }
                else if (block.Kind is BBJ_SWITCH)
                {
                    var lastNode = block.IsLIR ? block.LastNode : block.LastStmt?.RootNode;
                    assert(lastNode is not null);
                    assert((!allNodesLinked || (lastNode.Next is null)) && (lastNode.Oper is GT_SWITCH or GT_SWITCH_TABLE));
                }
            }

            if (block.CatchType is BBCT_FILTER)
            {
                assert(block.bbPreds is null);
            }

            if (fgFuncletsCreated)
            {
                if (!reachedFirstFunclet)
                {
                    if (block == fgFirstFuncletBB)
                    {
                        assert(block.hasHndIndex);
                        reachedFirstFunclet = true;
                    }
                    else
                    {
                        assert(!block.hasHndIndex);
                    }
                }
                else
                {
                    assert(block.hasHndIndex);
                }
            }

            if (checkBBRefs)
            {
                assert(fgPredsComputed);
            }

            var checker = new BBPredsChecker(this);
            var blockRefs = checker.CheckBBPreds(block, curTraversalStamp);

            if (block == fgFirstBB)
            {
                blockRefs++;
            }

            if (opts.IsOSR && (block == fgEntryBB))
            {
                blockRefs += fgEntryBBExtraRefs;
            }

            if (checkBBRefs)
            {
                if (block.bbRefs != blockRefs)
                {
                    foreach (ref var HBtab in new EHClauses(this))
                    {
                        if (HBtab.ebdHndBeg == block)
                        {
                            blockRefs++;
                        }

                        if (HBtab.HasFilter && (HBtab.ebdFilter == block))
                        {
                            blockRefs++;
                        }
                    }
                }

                assert(block.bbRefs == blockRefs);
            }

            if (block.hasTryIndex)
            {
                assert(block.TryIndex < compHndBBtabCount);
            }

            if (block.HasTarget)
            {
                assert(block.HasInitializedTarget);
            }

            if (block.Kind is BBJ_COND)
            {
                assert(block.TrueEdge.isHeuristicBased == block.FalseEdge.isHeuristicBased);
            }

            // A callfinally must be reached from its associated try, except when it starts
            // the try or when the predecessor is an internal async resumption.
            foreach (var succBlock in block.Succs)
            {
                if (succBlock.Kind is BBJ_CALLFINALLY)
                {
                    var finallyBlock = succBlock.Target;
                    assert(finallyBlock.hasHndIndex);
                    var finallyIndex = finallyBlock.HndIndex;
                    ref var ehDsc = ref ehGetDsc(finallyIndex);

                    if (ehDsc.ebdTryBeg != succBlock)
                    {
                        assert(bbInTryRegions(finallyIndex, block) || block.HasFlag(BBF_ASYNC_RESUMPTION));
                    }
                }
            }
        }

        assert(fgBBcount == numBlocks);
        assert(fgBBNumMax >= maxBBNum);

        if (genReturnBB is not null)
        {
            assert((genReturnBB.FirstLIRNode is not null) || (genReturnBB.FirstStmt is not null));
            assert(genReturnBB.Kind is BBJ_RETURN);
        }

        if (fgHasAddCodeDscMap)
        {
            foreach (var add in fgAddCodeDscMap.Values)
            {
                if (add.acdUsed)
                {
                    assert(add.acdDstBlk is not null);
                    assert(add.acdDstBlk.bbTraversalStamp == curTraversalStamp);
                }
            }
        }

        if (compIsForInlining)
        {
            return;
        }

#if !JIT32_GCENCODER
        var copiedForGenericsCtxt = (info.compMethodInfo->options & CORINFO_GENERICS_CTXT_FROM_THIS) != 0;
#else
        var copiedForGenericsCtxt = false;
#endif

        if (info.compIsStatic)
        {
            assert(lvaArg0Var == BAD_VAR_NUM);
        }
        else
        {
            assert(info.compThisArg != BAD_VAR_NUM);
            var compThisArgAddrExposedOK = !lvaTable[info.compThisArg].IsAddressExposed;

#if !JIT32_GCENCODER
            compThisArgAddrExposedOK = compThisArgAddrExposedOK || copiedForGenericsCtxt;
#endif

            assert(compThisArgAddrExposedOK && !lvaTable[info.compThisArg].lvHasILStoreOp &&
                   ((lvaArg0Var == info.compThisArg) ||
                    ((lvaArg0Var != info.compThisArg) &&
                     (lvaTable[lvaArg0Var].IsAddressExposed || lvaTable[lvaArg0Var].lvHasILStoreOp || copiedForGenericsCtxt))));
        }
    }

    // Ensure that the forward and reverse block links and switch successor tables agree.
    public void fgDebugCheckBlockLinks()
    {
        assert(fgFirstBB is not null);
        assert(fgFirstBB.IsFirst);

        foreach (var block in Blocks)
        {
            if (block.IsLast)
            {
                assert(block == fgLastBB);
            }
            else
            {
                assert(block.Next.Prev == block);
            }

            if (block.IsFirst)
            {
                assert(block == fgFirstBB);
            }
            else
            {
                assert(block.Prev.Next == block);
            }

            if (block.Kind is BBJ_SWITCH)
            {
                var targets = block.SwitchTargets;

                if (targets.HasDominantCase)
                {
                    assert(block.hasProfileWeight);
                }

                var succBlocks = new HashSet<int>();

                foreach (var caseEdge in targets.Cases)
                {
                    _ = succBlocks.Add(caseEdge.DestinationBlock.bbNum);
                }

                assert(targets.Succs.Length == succBlocks.Count);

                foreach (var succBlock in block.SwitchSuccs)
                {
                    assert(succBlocks.Contains(succBlock.bbNum));
                }
            }
        }
    }

    private bool bbIsFuncletBeg(BasicBlock block)
    {
        assert(fgFuncletsCreated);
        ref var ehDsc = ref ehGetBlockHndDsc(block);
        return !Unsafe.IsNullRef(in ehDsc) &&
               ((block == ehDsc.ebdHndBeg) || (ehDsc.HasFilter && (block == ehDsc.ebdFilter)));
    }

    private bool fgTrysContiguous()
    {
#if TARGET_WASM
        return fgIndexToBlockMap is null;
#else
        return true;
#endif
    }

    private bool ehCallFinallyInCorrectRegion(BasicBlock blockCallFinally, ushort finallyIndex)
    {
        assert(blockCallFinally.Kind is BBJ_CALLFINALLY);
        assert(finallyIndex is not EHblkDsc.NO_ENCLOSING_INDEX);
        assert(finallyIndex < compHndBBtabCount);
        assert(ehGetDsc(finallyIndex).HasFinallyHandler);

        var callFinallyIndex = ehGetCallFinallyRegionIndex(finallyIndex, out var inTryRegion);

        if (callFinallyIndex == EHblkDsc.NO_ENCLOSING_INDEX)
        {
            return !blockCallFinally.hasTryIndex && !blockCallFinally.hasHndIndex;
        }

        if (inTryRegion)
        {
            if (bbInTryRegions(callFinallyIndex, blockCallFinally))
            {
                return true;
            }
        }
        else if (bbInHandlerRegions(callFinallyIndex, blockCallFinally))
        {
            return true;
        }

        return false;
    }

    private readonly struct BBPredsChecker
    {
        private readonly Compiler _compiler;

        public BBPredsChecker(Compiler compiler)
        {
            _compiler = compiler;
        }

        public int CheckBBPreds(BasicBlock block, int curTraversalStamp)
        {
            if (!_compiler.fgPredsComputed)
            {
                assert(block.bbPreds is null);
                return 0;
            }

            var blockRefs = 0;

            foreach (var pred in block.PredEdges)
            {
                blockRefs += pred.DupCount;
                var blockPred = pred.SourceBlock;
                assert(blockPred.bbTraversalStamp == curTraversalStamp);

                ref var ehTryDsc = ref _compiler.ehGetBlockTryDsc(block);

                if (!Unsafe.IsNullRef(in ehTryDsc))
                {
                    assert(CheckEhTryDsc(block, blockPred, ehTryDsc));
                }

                ref var ehHndDsc = ref _compiler.ehGetBlockHndDsc(block);

                if (!Unsafe.IsNullRef(in ehHndDsc))
                {
                    assert(CheckEhHndDsc(block, blockPred, ehHndDsc));
                }

                assert(CheckJump(blockPred, block));
                assert(pred.DestinationBlock == block);
            }

            assert(block.checkPredListOrder());
            return blockRefs;
        }

        private bool CheckEhTryDsc(BasicBlock block, BasicBlock blockPred, in EHblkDsc ehTryDsc)
        {
            if (ehTryDsc.ebdTryBeg == block)
            {
                return true;
            }

            if (_compiler.bbInTryRegions(block.TryIndex, blockPred))
            {
                return true;
            }

            if (_compiler.bbInCatchHandlerRegions(block, blockPred))
            {
                return true;
            }

            var prevBlock = block.Prev;
            assert(prevBlock is not null);

            if (prevBlock.Kind is BBJ_CALLFINALLY && block.Kind is BBJ_CALLFINALLYRET && blockPred.Kind is BBJ_EHFINALLYRET)
            {
                return true;
            }

            if (_compiler.opts.IsOSR && !_compiler.compPostImportationCleanupDone && (blockPred == _compiler.fgFirstBB))
            {
                return true;
            }

            if (blockPred.HasFlag(BBF_ASYNC_RESUMPTION))
            {
                return true;
            }

#if TARGET_WASM
            if (_compiler.fgWasmHasCatchResumptions && blockPred.HasFlag(BBF_CATCH_RESUMPTION))
            {
                return true;
            }
#endif

            JITDUMP($"Jump into the middle of try region: {FMT_BB(blockPred.bbNum)} branches to {FMT_BB(block.bbNum)}\n");
            assert(false, "Jump into middle of try region");
            return false;
        }

        private bool CheckEhHndDsc(BasicBlock block, BasicBlock blockPred, in EHblkDsc ehHndlDsc)
        {
            if (blockPred.Kind is BBJ_EHFINALLYRET)
            {
                return true;
            }

            if (ehHndlDsc.HasFilter && (ehHndlDsc.ebdHndBeg == block) && (blockPred.Kind is BBJ_EHFILTERRET) &&
                ehHndlDsc.InFilterRegionBBRange(blockPred))
            {
                return true;
            }

            if ((block.CatchType is BBCT_FINALLY) && (blockPred.Kind is BBJ_CALLFINALLY) &&
                _compiler.ehCallFinallyInCorrectRegion(blockPred, block.HndIndex))
            {
                return true;
            }

            if (_compiler.bbInHandlerRegions(block.HndIndex, blockPred))
            {
                if (!ehHndlDsc.HasFilter)
                {
                    return true;
                }

                var blockInFilter = ehHndlDsc.InFilterRegionBBRange(block);
                var blockPredInFilter = ehHndlDsc.InFilterRegionBBRange(blockPred);

                if (blockInFilter == blockPredInFilter)
                {
                    return true;
                }

                JITDUMP($"Jump between filter and filter handler regions: {FMT_BB(blockPred.bbNum)} branches to {FMT_BB(block.bbNum)}\n");
                assert(false, "Jump between filter and filter handler regions");
                return false;
            }

            JITDUMP($"Jump into the middle of handler region: {FMT_BB(blockPred.bbNum)} branches to {FMT_BB(block.bbNum)}\n");
            assert(false, "Jump into the middle of handler region");
            return false;
        }

        private bool CheckJump(BasicBlock blockPred, BasicBlock block)
        {
            switch (blockPred.Kind)
            {
                case BBJ_COND:
                {
                    assert((blockPred.FalseTarget == block) || (blockPred.TrueTarget == block));
                    return true;
                }

                case BBJ_ALWAYS:
                case BBJ_CALLFINALLY:
                case BBJ_CALLFINALLYRET:
                case BBJ_EHCATCHRET:
                case BBJ_EHFILTERRET:
                {
                    if (blockPred.Target != block)
                    {
                        var targetNum = blockPred.HasInitializedTarget ? blockPred.Target.bbNum : 0;
                        JITDUMP($"{FMT_BB(blockPred.bbNum)} -> {FMT_BB(block.bbNum)} from pred links does not match {FMT_BB(blockPred.bbNum)} -> {FMT_BB(targetNum)} from succ links\n");
                        assert(false, "Invalid block preds");
                    }

                    assert(blockPred.TargetEdge.Likelihood == 1.0);
                    return true;
                }

                case BBJ_EHFINALLYRET:
                {
                    assert(CheckEHFinallyRet(blockPred, block));
                    return true;
                }

                case BBJ_EHFAULTRET:
                case BBJ_THROW:
                case BBJ_RETURN:
                {
                    assert(false, "EHFAULTRET, THROW, and RETURN block cannot be in the predecessor list!");
                    break;
                }

                case BBJ_SWITCH:
                {
                    foreach (var bTarget in blockPred.SwitchSuccs)
                    {
                        if (block == bTarget)
                        {
                            return true;
                        }
                    }

                    assert(false, "SWITCH in the predecessor list with no jump label to BLOCK!");
                    break;
                }

                case BBJ_LEAVE:
                {
                    if (!_compiler.compPostImportationCleanupDone)
                    {
                        return true;
                    }

                    assert(false, "Unexpected BBJ_LEAVE predecessor");
                    break;
                }

                default:
                {
                    assert(false, "Unexpected bbKind");
                    break;
                }
            }

            return false;
        }

        private bool CheckEHFinallyRet(BasicBlock blockPred, BasicBlock block)
        {
            var found = false;
            var ehfTargets = blockPred.EhfTargets;
            assert(ehfTargets is not null);

            foreach (var succ in ehfTargets.Succs)
            {
                if (block == succ.DestinationBlock)
                {
                    assert(!found);
                    found = true;
                }
            }

            assert(found, "BBJ_EHFINALLYRET successor not found");

            var hndIndex = blockPred.HndIndex;
            ref var ehDsc = ref _compiler.ehGetDsc(hndIndex);
            var finBeg = ehDsc.ebdHndBeg;
            _compiler.ehGetCallFinallyBlockRange(hndIndex, out var firstBlock, out var lastBlock);

            found = false;

            foreach (var bcall in new BasicBlockRangeList(firstBlock, lastBlock))
            {
                if ((bcall.Kind is BBJ_CALLFINALLY) && (bcall.Target == finBeg) && (bcall.Next == block))
                {
                    found = true;
                    break;
                }
            }

            if (!found && _compiler.fgFuncletsCreated)
            {
                for (var bcall = _compiler.fgFirstFuncletBB; bcall is not null; bcall = bcall.Next)
                {
                    if ((bcall.Kind is BBJ_CALLFINALLY) && (bcall.Target == finBeg) && (bcall.Next == block) &&
                        _compiler.ehCallFinallyInCorrectRegion(bcall, hndIndex))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                JITDUMP($"{FMT_BB(block.bbNum)} is successor of finallyret {FMT_BB(blockPred.bbNum)} but prev block is not a callfinally to {FMT_BB(finBeg.bbNum)} (search range was [{FMT_BB(firstBlock.bbNum)}...{FMT_BB(lastBlock.bbNum)}]\n");

                if (!_compiler.fgTrysContiguous())
                {
                    JITDUMP("Tolerating, since try regions are not contiguous\n");
                    return true;
                }

                assert(false, "BBJ_EHFINALLYRET predecessor of block that doesn't follow a BBJ_CALLFINALLY!");
            }

            return found;
        }
    }
#endif
}
