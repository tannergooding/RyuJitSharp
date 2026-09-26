// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private struct OptInvertCountTreeInfoType
    {
        public int sharedStaticHelperCount;
        public int arrayLengthCount;
    }

    public static bool IsSharedStaticHelper(GenTree tree)
    {
        if ((tree.Oper is not GT_CALL) || !tree.AsCall().IsHelperCall())
        {
            return false;
        }

        var helper = tree.AsCall().HelperNum;

        // Native also includes helpers with similar hoisting benefits that are
        // not literally shared-static helpers, including boxing and TLS access.
        return (helper is CORINFO_HELP_BOX or CORINFO_HELP_GETSTATICFIELDADDR_TLS) ||
            ((helper >= CORINFO_HELP_GET_GCSTATIC_BASE) &&
             (helper <= CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT))
#if FEATURE_READYTORUN
            || (helper is CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE or
                CORINFO_HELP_READYTORUN_GCSTATIC_BASE or CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE or
                CORINFO_HELP_READYTORUN_THREADSTATIC_BASE or CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR or
                CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE)
#endif
            || (helper is CORINFO_HELP_INITCLASS);
    }

    private OptInvertCountTreeInfoType optInvertCountTreeInfo(GenTree tree)
    {
        var walker = new InvertCountTreeInfoVisitor();
        _ = walker.WalkTree(ref tree, null);
        return walker.Result;
    }

    private struct InvertCountTreeInfoVisitor : IGenTreeVisitor<InvertCountTreeInfoVisitor>
    {
        public static bool DoPreOrder => true;
        private readonly GenTreeStack _ancestors;
        public OptInvertCountTreeInfoType Result;

        public InvertCountTreeInfoVisitor()
        {
            _ancestors = [];
            Result = default;
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (IsSharedStaticHelper(use))
            {
                Result.sharedStaticHelperCount++;
            }
            if (use.Oper.IsArrLength)
            {
                Result.arrayLengthCount++;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<InvertCountTreeInfoVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private bool optTryInvertWhileLoop(FlowGraphNaturalLoop loop)
    {
        assert(loop.EntryEdges.Length == 1);
        var preheader = loop.EntryEdge(0).SourceBlock;
        List<BasicBlock> duplicatedBlocks = [];
        var condBlock = loop.Header;

        while (true)
        {
            if (!BasicBlock.sameEHRegion(preheader, condBlock))
            {
                JITDUMP($"No loop-inversion for L{loop.Index:D2} since we could not find a condition block " +
                    "in the same EH region as the preheader\n");
                return false;
            }

            duplicatedBlocks.Add(condBlock);
            if (condBlock.Kind is BBJ_ALWAYS)
            {
                condBlock = condBlock.Target;
                if (!loop.ContainsBlock(condBlock) || (condBlock == loop.Header))
                {
                    JITDUMP($"No loop-inversion for L{loop.Index:D2}; ran out of blocks following BBJ_ALWAYS blocks\n");
                    return false;
                }

                continue;
            }

            if (condBlock.Kind is not BBJ_COND)
            {
                JITDUMP($"No loop-inversion for L{loop.Index:D2} since we could not find any BBJ_COND block\n");
                return false;
            }

            break;
        }

        var trueExits = !loop.ContainsBlock(condBlock.TrueTarget);
        var falseExits = !loop.ContainsBlock(condBlock.FalseTarget);
        if (trueExits == falseExits)
        {
            JITDUMP($"No loop-inversion for L{loop.Index:D2} since we could not find any exiting BBJ_COND block\n");
            return false;
        }

        var exit = trueExits ? condBlock.TrueTarget : condBlock.FalseTarget;
        var stayInLoopSucc = trueExits ? condBlock.FalseTarget : condBlock.TrueTarget;
        if (stayInLoopSucc == loop.Header)
        {
            JITDUMP($"No loop-inversion for L{loop.Index:D2} since it is already inverted\n");
            return false;
        }

        // Splitting an exit that enters a try region would create a jump into
        // its middle. Native currently excludes this rather than moving the split.
        if (!BasicBlock.sameEHRegion(preheader, exit))
        {
            JITDUMP($"No loop-inversion for L{loop.Index:D2} since the preheader {FMT_BB(preheader.bbNum)} " +
                $"and exit {FMT_BB(exit.bbNum)} are in different EH regions\n");
            return false;
        }

        bool IsExitingCondLatch(BasicBlock block)
        {
            if ((block == condBlock) || (block.Kind is not BBJ_COND))
            {
                return false;
            }

            foreach (var edge in loop.ExitEdges)
            {
                if (edge.SourceBlock == block)
                {
                    return true;
                }
            }

            return false;
        }

        // Only analyze the IV when an exiting latch could already bottom-test
        // the loop; an arbitrary early-exit condition is not sufficient.
        var analyzedIteration = false;
        BasicBlock? ivTestBlock = null;
        bool IsIvTest(BasicBlock candidate)
        {
            if (!analyzedIteration)
            {
                analyzedIteration = true;
                if (loop.AnalyzeIteration(out var iterInfo))
                {
                    ivTestBlock = iterInfo.TestBlock;
                }
            }

            return (ivTestBlock is not null) && (candidate == ivTestBlock);
        }

        foreach (var backEdge in loop.BackEdges)
        {
            var latch = backEdge.SourceBlock;
            if (IsExitingCondLatch(latch) && IsIvTest(latch))
            {
                JITDUMP($"No loop-inversion for L{loop.Index:D2}; IV-test latch {FMT_BB(latch.bbNum)} " +
                    "already makes it bottom-tested\n");
                return false;
            }

            if ((latch.Kind is BBJ_ALWAYS) && latch.isEmpty())
            {
                foreach (var predEdge in latch.PredEdges)
                {
                    var pred = predEdge.SourceBlock;
                    if (loop.ContainsBlock(pred) && IsExitingCondLatch(pred) && IsIvTest(pred))
                    {
                        JITDUMP($"No loop-inversion for L{loop.Index:D2}; IV-test predecessor {FMT_BB(pred.bbNum)} " +
                            $"of canonical latch {FMT_BB(latch.bbNum)} already makes it bottom-tested\n");
                        return false;
                    }
                }
            }
        }

        JITDUMP($"Condition in block {FMT_BB(condBlock.bbNum)} of loop L{loop.Index:D2} " +
            "is a candidate for duplication to invert the loop\n");

        var loopIterations = BB_LOOP_WEIGHT_SCALE;
        var haveProfileWeights = fgIsUsingProfileWeights;
        var weightPreheader = preheader.bbWeight;
        var weightCond = condBlock.bbWeight;
        var weightStayInLoopSucc = stayInLoopSucc.bbWeight;
        if (haveProfileWeights)
        {
            assert(preheader.hasProfileWeight);
            assert(condBlock.hasProfileWeight);
            assert(stayInLoopSucc.hasProfileWeight);
            if (weightStayInLoopSucc == BB_ZERO_WEIGHT)
            {
                JITDUMP($"No loop-inversion for L{loop.Index:D2} since the in-loop successor " +
                    $"{FMT_BB(preheader.bbNum)} has 0 weight\n");
                return false;
            }

            // Bound the inferred entry count by the preheader's weight when
            // profile counts disagree.
            var inferredEntries = weightCond - weightStayInLoopSucc;
            var loopEntries = weightPreheader < inferredEntries ? inferredEntries : weightPreheader;
            loopIterations = weightStayInLoopSucc / loopEntries;
        }

        var mightBenefitFromCloning = false;
        var invertSizeLimit = JitConfig.JitLoopInversionSizeLimit;
        if (invertSizeLimit >= 0)
        {
            var cloneSizeLimit = JitConfig.JitCloneLoopsSizeLimit;
            var sizeLimit = (uint)Math.Max(invertSizeLimit, cloneSizeLimit);
            var loopSize = 0u;
            var loopHasBoundsCheck = false;
            var nestedHasBoundsCheck = false;

            // A nested loop with its own bounds checks is inverted/cloned
            // independently; duplicating its parent can harm later LICM/CSE.
            _ = loop.VisitLoopBlocks(block => {
                var inNestedLoop = false;
                for (var child = loop.Child; child is not null; child = child.Sibling)
                {
                    if (child.ContainsBlock(block))
                    {
                        inNestedLoop = true;
                        break;
                    }
                }

                var slack = sizeLimit > loopSize ? sizeLimit - loopSize : 0;
                var exceeded = block.ComplexityExceeds(this, slack, tree => {
                    if (tree.Oper is GT_BOUNDS_CHECK)
                    {
                        if (inNestedLoop)
                        {
                            nestedHasBoundsCheck = true;
                        }
                        else
                        {
                            loopHasBoundsCheck = true;
                        }
                    }

                    loopSize = unchecked(loopSize + 1);
                    return 1;
                });

                return exceeded ? BasicBlockVisit.Abort : BasicBlockVisit.Continue;
            });

            mightBenefitFromCloning = loopHasBoundsCheck || nestedHasBoundsCheck;
            if (loopSize > (uint)invertSizeLimit)
            {
                JITDUMP($"L{loop.Index:D2} exceeds inversion size limit of {invertSizeLimit}\n");
                if (!mightBenefitFromCloning)
                {
                    JITDUMP($"No inversion for L{loop.Index:D2}: unlikely to benefit from cloning\n");
                    return false;
                }
                if (nestedHasBoundsCheck)
                {
                    JITDUMP($"No inversion for L{loop.Index:D2}: nested loops have their own bounds checks; " +
                        "they will be inverted independently\n");
                    return false;
                }

                JITDUMP($"L{loop.Index:D2} might benefit from cloning. Continuing.\n");
            }
        }

        var estDupCostSz = 0u;
        foreach (var block in duplicatedBlocks)
        {
            foreach (var statement in block.Statements)
            {
                var tree = statement.RootNode;
                gtPrepareCost(tree);
                estDupCostSz = unchecked(estDupCostSz + tree.CostSz);
            }
        }

        var maxDupCostSz = 34u;
        if ((compCodeOpt is FAST_CODE) || compStressCompile(STRESS_DO_WHILE_LOOPS, 30))
        {
            maxDupCostSz *= 4;
        }
        if (loopIterations >= 12.0)
        {
            maxDupCostSz *= 2;
            if (loopIterations >= 96.0)
            {
                maxDupCostSz *= 2;
            }
        }

        var costIsTooHigh = estDupCostSz > maxDupCostSz;
        OptInvertCountTreeInfoType totalInfo = default;
        var hasSplitIVTestAndIncrement = false;
        if (costIsTooHigh)
        {
            if (mightBenefitFromCloning)
            {
                foreach (var backEdge in loop.BackEdges)
                {
                    var latchBlock = backEdge.SourceBlock;
                    if (latchBlock == condBlock)
                    {
                        continue;
                    }

                    foreach (var statement in latchBlock.Statements)
                    {
                        if (optIsLoopIncrTree(statement.RootNode) != BAD_VAR_NUM)
                        {
                            hasSplitIVTestAndIncrement = true;
                            break;
                        }
                    }
                    if (hasSplitIVTestAndIncrement)
                    {
                        break;
                    }
                }
            }

            for (var i = 0; (i < duplicatedBlocks.Count) && costIsTooHigh; i++)
            {
                var block = duplicatedBlocks[i];
                foreach (var statement in block.Statements)
                {
                    var tree = statement.RootNode;
                    var info = optInvertCountTreeInfo(tree);
                    totalInfo.sharedStaticHelperCount += info.sharedStaticHelperCount;
                    totalInfo.arrayLengthCount += info.arrayLengthCount;

                    if ((info.sharedStaticHelperCount > 0) || (info.arrayLengthCount > 0) ||
                        hasSplitIVTestAndIncrement)
                    {
                        // Match the native host conversion, not C#'s saturating
                        // floating-to-integer cast for inconsistent profile counts.
                        var iterationLimit = double.ConvertToIntegerNative<int>(loopIterations + 1.5);
                        var newMaxDupCostSz = unchecked(maxDupCostSz +
                            (uint)((24 * Math.Min(totalInfo.sharedStaticHelperCount, iterationLimit)) +
                                (8 * totalInfo.arrayLengthCount) + (hasSplitIVTestAndIncrement ? 24 : 0)));
                        costIsTooHigh = estDupCostSz > newMaxDupCostSz;
                        if (!costIsTooHigh)
                        {
#if DEBUG
                            JITDUMP($"Decided to duplicate loop condition block after counting helpers in tree " +
                                $"[{tree.TreeId:D6}] in block {FMT_BB(block.bbNum)}");
#endif
                            maxDupCostSz = newMaxDupCostSz;
                            break;
                        }
                    }
                }
            }
        }

#if DEBUG
        if (verbose)
        {
            var conditionStatement = condBlock.LastStmt;
            assert(conditionStatement is not null);
            jitprintf($"\nDuplication of loop condition [{conditionStatement.RootNode.TreeId:D6}] is " +
                $"{(costIsTooHigh ? "not done" : "performed")}, because the cost of duplication ({estDupCostSz}) is " +
                $"{(costIsTooHigh ? "greater" : "less or equal")} than {maxDupCostSz}," +
                FormattableString.Invariant($"\n   loopIterations = {loopIterations,7:F3}, optInvertTotalInfo.sharedStaticHelperCount >= ") +
                $"{totalInfo.sharedStaticHelperCount}, hasSplitIVTestAndIncrement = {dspBool(hasSplitIVTestAndIncrement)}, " +
                $"haveProfileWeights = {dspBool(haveProfileWeights)}\n");
        }
#endif
        if (costIsTooHigh)
        {
            // Preparing costs above can change IR metadata even without inversion.
            return true;
        }

        var newPreheader = fgSplitBlockAtEnd(preheader);
        var nonEnterBlock = fgSplitBlockAtBeginning(exit);
        JITDUMP($"New preheader is {FMT_BB(newPreheader.bbNum)}\n");
        JITDUMP($"Duplicated condition block is {FMT_BB(preheader.bbNum)}\n");
        JITDUMP($"Old exit is {FMT_BB(exit.bbNum)}, new non-enter block is {FMT_BB(nonEnterBlock.bbNum)}\n");

        var newCondToNewPreheader = preheader.TargetEdge;
        var newCondToNewExit = fgAddRefPred(nonEnterBlock, preheader);
        preheader.SetCond(trueExits ? newCondToNewExit : newCondToNewPreheader,
            trueExits ? newCondToNewPreheader : newCondToNewExit);
        preheader.TrueEdge.Likelihood = condBlock.TrueEdge.Likelihood;
        preheader.FalseEdge.Likelihood = condBlock.FalseEdge.Likelihood;
        fgRedirectEdge(ref newPreheader.TargetEdgeRef, stayInLoopSucc);

        foreach (var block in duplicatedBlocks)
        {
            foreach (var statement in block.Statements)
            {
                var clonedTree = gtCloneExpr(statement.RootNode);
                var clonedStatement = gtNewStmt(clonedTree, statement.DebugInfo);
                fgInsertStmtAtEnd(preheader, clonedStatement);
                if (statement == condBlock.LastStmt)
                {
                    // Native retains this reversal because dropping it changes
                    // downstream code quality despite equivalent control flow.
                    assert(clonedStatement.RootNode.Oper is GT_JTRUE);
                    var jump = clonedStatement.RootNode.AsUnOp();
                    jump.Op1 = gtReverseCond(jump.Op1);
                    preheader.SetCond(preheader.FalseEdge, preheader.TrueEdge);
                }

                DISPSTMT(clonedStatement);
            }

            preheader.CopyFlags(block, BBF_COPY_PROPAGATE);
        }

        if (haveProfileWeights)
        {
            newPreheader.setBBProfileWeight(newCondToNewPreheader.LikelyWeight);
            for (var i = 0; i < duplicatedBlocks.Count - 1; i++)
            {
                var block = duplicatedBlocks[i];
                block.setBBProfileWeight(block.computeIncomingWeight());
            }

            condBlock.setBBProfileWeight(condBlock.computeIncomingWeight());
            exit.setBBProfileWeight(exit.computeIncomingWeight());
        }

        // Compaction preserves the pattern expected by downstream IV analysis.
        var condPred = condBlock.GetUniquePred(this);
        if (condPred is not null)
        {
            JITDUMP($"Cond block {FMT_BB(condBlock.bbNum)} has a unique pred now, seeing if we can compact...\n");
            if (fgCanCompactBlock(condPred))
            {
                JITDUMP("  ..we can!\n");
                fgCompactBlock(condPred);
                condBlock = condPred;
            }
            else
            {
                JITDUMP("  ..we cannot\n");
            }
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"\nDuplicated loop exit block at {FMT_BB(preheader.bbNum)} for loop L{loop.Index:D2}\n");
            jitprintf($"Estimated code size expansion is {estDupCostSz}\n");
            fgDumpBlock(preheader);
            fgDumpBlock(condBlock);
        }
#endif
        Metrics.LoopsInverted++;
        return true;
    }

    public PhaseStatus optInvertLoops()
    {
#if OPT_CONFIG
        if (JitConfig.JitDoLoopInversion == 0)
        {
            JITDUMP("Loop inversion disabled\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif
        if (compCodeOpt is SMALL_CODE)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert(_loops is not null);
        var madeChanges = false;
        foreach (var loop in _loops.InPostOrder())
        {
            madeChanges |= optTryInvertWhileLoop(loop);
        }

        if (Metrics.LoopsInverted > 0)
        {
            assert(madeChanges);
            fgInvalidateDfsTree();
            _dfsTree = fgComputeDfs();
            _loops = FlowGraphNaturalLoops.Find(_dfsTree);

            // An inner loop's duplicated condition can create new parent exits.
            if (optCanonicalizeLoops())
            {
                fgInvalidateDfsTree();
                _dfsTree = fgComputeDfs();
                _loops = FlowGraphNaturalLoops.Find(_dfsTree);
            }
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
