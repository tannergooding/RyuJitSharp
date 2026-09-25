// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe bool fgUpdateFlowGraph(bool doTailDuplication = false, bool isPhase = false)
    {
#if DEBUG
        if (verbose && !isPhase)
        {
            jitprintf("\n*************** In fgUpdateFlowGraph()");
        }
#endif
        noway_assert(opts.OptimizationEnabled);
        assert(fgFirstColdBlock is null);

#if DEBUG
        if (verbose && !isPhase)
        {
            jitprintf("\nBefore updating the flow graph:\n");
            fgDispBasicBlocks(verboseTrees);
            jitprintf("\n");
        }
#endif
        // Conditional cycles can rotate forever under tail duplication. Block IDs are stable,
        // so admitting each source/target pair once bounds the work without changing traversal.
        var tailDupPairs = new HashSet<ulong>();
        var modified = false;
        bool change;

        do
        {
            change = false;
            BasicBlock? bPrev = null;

            for (var block = fgFirstBB; block is not null; block = block.Next)
            {
                if (block.HasFlag(BBF_REMOVED))
                {
                    if (bPrev is not null)
                    {
                        assert(!block.IsLast);
                        bPrev.Next = block.Next;
                    }
                    else
                    {
                        noway_assert(false, "First basic block marked as BBF_REMOVED???");
                        fgFirstBB = block.Next;
                    }
                    continue;
                }

            REPEAT:
                var bNext = block.Next;
                BasicBlock? bDest = null;
                BasicBlock? bFalseDest = null;

                if (doTailDuplication && (block.Kind is BBJ_RETURN) && fgDedupReturnComparison(block))
                {
                    assert(block.Kind is BBJ_COND);
                    change = true;
                    modified = true;
                    bNext = block.Next;
                }

                if (block.Kind is BBJ_ALWAYS)
                {
                    bDest = block.Target;
                    var tailDupKey = ((ulong)unchecked((uint)block.bbID) << 32) | unchecked((uint)bDest.bbID);
                    if (doTailDuplication && !tailDupPairs.Contains(tailDupKey) &&
                        fgOptimizeUncondBranchToSimpleCond(block, bDest))
                    {
                        _ = tailDupPairs.Add(tailDupKey);
                        assert(block.Kind is BBJ_COND);
                        assert(bNext == block.Next);
                        change = true;
                        modified = true;

                        if (fgFoldSimpleCondByForwardSub(block))
                        {
                            var otherPred = bDest.GetUniquePred(this);
                            if (otherPred is not null)
                            {
                                JITDUMP($"Trying to compact last pred {FMT_BB(otherPred.bbNum)} of " +
                                    $"{FMT_BB(bDest.bbNum)} that we now bypass\n");
                                if (fgCanCompactBlock(otherPred))
                                {
                                    fgCompactBlock(otherPred);
                                    _ = fgFoldSimpleCondByForwardSub(otherPred);
                                    bPrev = block.Prev;
                                    bNext = block.Next;
                                }
                            }

                            assert(block.Kind is BBJ_ALWAYS);
                            bDest = block.Target;
                        }
                    }
                }

                if (block.Kind is BBJ_ALWAYS or BBJ_CALLFINALLYRET)
                {
                    bDest = block.Target;
                    if (bDest == bNext)
                    {
                        bDest = null;
                    }
                }
                else if (block.Kind is BBJ_COND)
                {
                    bDest = block.TrueTarget;
                    bFalseDest = block.FalseTarget;
                    if (bDest == bFalseDest)
                    {
                        fgRemoveConditionalJump(block);
                        assert(block.Kind is BBJ_ALWAYS);
                        change = true;
                        modified = true;
                        bFalseDest = null;
                    }
                }

                if (bDest is not null)
                {
                    if ((bDest.Kind is BBJ_ALWAYS) && (bDest.Target != bDest) && bDest.isEmpty())
                    {
                        if (!bDest.JumpsToNext && fgOptimizeBranchToEmptyUnconditional(block, bDest))
                        {
                            change = true;
                            modified = true;
                            goto REPEAT;
                        }
                    }

                    if ((block.Kind is BBJ_COND) && (bFalseDest == bNext))
                    {
                        assert(bNext is not null);
                        if ((bNext.bbRefs == 1) && (bNext.Kind is BBJ_ALWAYS) && !bNext.JumpsToNext &&
                            bNext.isEmpty() && (bNext.Target != bNext))
                        {
                            assert(block.FalseTarget == bNext);
                            var isJumpAroundEmpty = bNext.Next == bDest;
                            var bNextJumpDest = bNext.Target;

                            // Asymmetric join counts prevent reversing and re-reversing the same flow.
                            var isJumpToJoinFree = !isJumpAroundEmpty && (bDest.bbRefs == 1) &&
                                (bNextJumpDest.bbRefs > 1) && (bDest.bbNum > block.bbNum) &&
                                (block.isRunRarely == bDest.isRunRarely);
                            var optimizeJump = isJumpAroundEmpty || isJumpToJoinFree;

#if TARGET_WASM
                            var lastNode = block.GetLastNode();
                            assert(lastNode is not null);
                            if (lastNode.Oper is GT_WASM_JEXCEPT)
                            {
                                optimizeJump = false;
                            }
#endif
                            if (bDest.hasTryIndex && !BasicBlock.sameTryRegion(block, bDest))
                            {
                                optimizeJump = false;
                            }
                            if (bNext.hasTryIndex && !BasicBlock.sameTryRegion(block, bNext))
                            {
                                optimizeJump = false;
                            }

                            if (optimizeJump && isJumpToJoinFree)
                            {
                                if (!BasicBlock.sameEHRegion(bNext, bDest) || bbIsTryBeg(bDest) ||
                                    bbIsHandlerBeg(bDest) || bDest.isBBCallFinallyPair)
                                {
                                    optimizeJump = false;
                                }
                                else
                                {
                                    assert(bNext.Next != bDest);
                                    JITDUMP($"\nMoving {FMT_BB(bDest.bbNum)} after {FMT_BB(bNext.bbNum)} to enable reversal\n");
                                    if (ehIsBlockEHLast(bDest))
                                    {
                                        assert(bDest.Prev is not null);
                                        ehUpdateLastBlocks(bDest, bDest.Prev);
                                    }

                                    fgUnlinkBlock(bDest);
                                    fgInsertBBafter(bNext, bDest);
                                    if (ehIsBlockEHLast(bNext))
                                    {
                                        ehUpdateLastBlocks(bNext, bDest);
                                    }
                                }
                            }

                            if (optimizeJump)
                            {
                                JITDUMP("\nReversing a conditional jump around an unconditional jump " +
                                    $"({FMT_BB(block.bbNum)} -> {FMT_BB(bDest.bbNum)}, " +
                                    $"{FMT_BB(bNext.bbNum)} -> {FMT_BB(bNextJumpDest.bbNum)})\n");

                                var test = block.GetLastNode();
                                assert(test is not null);
                                noway_assert(test.Oper.IsConditionalJump);
                                if (test.Oper is GT_JTRUE)
                                {
                                    var cond = gtReverseCond(test.AsUnOp().Op1);
                                    assert(cond == test.AsUnOp().Op1);
                                    test.AsUnOp().Op1 = cond;
                                }
                                else
                                {
                                    _ = gtReverseCond(test);
                                }

                                (block.TrueEdgeRef, block.FalseEdgeRef) = (block.FalseEdge, block.TrueEdge);
                                fgRedirectEdge(ref block.TrueEdgeRef, bNext.Target);
                                block.CopyFlags(bNext, BBF_ASYNC_RESUMPTION);
                                fgRemoveRefPred(bNext.TargetEdge);
                                fgUnlinkBlockForRemoval(bNext);
                                bNext.SetFlags(BBF_REMOVED);

                                foreach (ref var clause in new EHClauses(this))
                                {
                                    if ((clause.ebdTryLast == bNext) || (clause.ebdHndLast == bNext))
                                    {
                                        fgSkipRmvdBlocks(ref clause);
                                    }
                                }

                                change = true;
                                modified = true;
#if DEBUG
                                if (verbose)
                                {
                                    jitprintf("\nAfter reversing the jump:\n");
                                    fgDispBasicBlocks(verboseTrees);
                                }
#endif
                                // The removed block's other predecessors have not been redirected yet.
                                // Do not mistake their eventual self-loop target for unreachable code.
                                if ((bNext.bbRefs > 0) && (bNext.Target == block) && (block.bbRefs == 1))
                                {
                                    continue;
                                }
                                goto REPEAT;
                            }
                        }
                    }
                }

                if ((block.Kind is BBJ_SWITCH) && fgOptimizeSwitchBranches(block))
                {
                    change = true;
                    modified = true;
                    goto REPEAT;
                }
                noway_assert(!block.HasFlag(BBF_REMOVED));

                if (fgCanCompactBlock(block))
                {
                    fgCompactBlock(block);
                    change = true;
                    modified = true;
                    bPrev = block.Prev;
                    goto REPEAT;
                }

                if (block.HasFlag(BBF_DONT_REMOVE))
                {
                    bPrev = block;
                    continue;
                }

                assert(!bbIsTryBeg(block));
                noway_assert(block.CatchType == BBCT_NONE);

                if (block.CountOfInEdges == 0)
                {
                    fgRemoveBlock(block, unreachable: true);
                    change = true;
                    modified = true;
                    continue;
                }
                else if (block.CountOfInEdges == 1)
                {
                    switch (block.Kind)
                    {
                        case BBJ_COND:
                        {
                            if ((block.TrueTarget == block) || (block.FalseTarget == block))
                            {
                                fgRemoveBlock(block, unreachable: true);
                                change = true;
                                modified = true;
                                continue;
                            }
                            break;
                        }

                        case BBJ_ALWAYS:
                        {
                            if (block.Target == block)
                            {
                                fgRemoveBlock(block, unreachable: true);
                                change = true;
                                modified = true;
                                continue;
                            }
                            break;
                        }
                    }
                }

                noway_assert(!block.HasFlag(BBF_REMOVED));
                if (block.isEmpty())
                {
                    assert(block.Prev == bPrev);
                    if (fgOptimizeEmptyBlock(block))
                    {
                        change = true;
                        modified = true;
                    }
                    if (block.HasFlag(BBF_REMOVED))
                    {
                        continue;
                    }
                }

                noway_assert(!block.HasFlag(BBF_REMOVED));
                bPrev = block;
            }
        }
        while (change);

        if (modified && opts.IsOSR)
        {
            JITDUMP("fgUpdateFlowGraph: Inconsistent OSR entry weight may have been propagated. " +
                $"Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            fgPgoConsistent = false;
        }

#if DEBUG
        if (!isPhase)
        {
            if (verbose && modified)
            {
                jitprintf("\nAfter updating the flow graph:\n");
                fgDispBasicBlocks(verboseTrees);
                fgDispHandlerTab();
            }
            fgVerifyHandlerTab();
            fgDebugCheckBBlist();
            fgDebugCheckUpdate();
        }
#endif
        return modified;
    }

    public ref EHblkDsc ehIsBlockTryLast(BasicBlock block)
    {
        ref var clause = ref ehGetBlockTryDsc(block);
        if (!Unsafe.IsNullRef(in clause) && (clause.ebdTryLast == block))
        {
            return ref clause;
        }

        return ref Unsafe.NullRef<EHblkDsc>();
    }

    public ref EHblkDsc ehIsBlockHndLast(BasicBlock block)
    {
        ref var clause = ref ehGetBlockHndDsc(block);
        if (!Unsafe.IsNullRef(in clause) && (clause.ebdHndLast == block))
        {
            return ref clause;
        }

        return ref Unsafe.NullRef<EHblkDsc>();
    }

    public bool ehIsBlockEHLast(BasicBlock block) =>
        !Unsafe.IsNullRef(in ehIsBlockTryLast(block)) || !Unsafe.IsNullRef(in ehIsBlockHndLast(block));
}
