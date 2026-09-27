// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus optUnrollLoops()
    {
        if (compCodeOpt is SMALL_CODE)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (_loops is null)
        {
            throw new FatalJitException("Loop unrolling requires discovered natural loops.");
        }
        if (_loops.NumLoops == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (JitConfig.JitNoUnroll != 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        var unrollCount = 0;
        var anyIRchange = false;
        var passes = 0;
        while (true)
        {
            var loopsWithUnrolledDescendant = new HashSet<int>();
            foreach (var loop in _loops.InPostOrder())
            {
                if (loopsWithUnrolledDescendant.Contains(loop.Index) ||
                    !optTryUnrollLoop(loop, ref anyIRchange))
                {
                    continue;
                }

                unrollCount++;
                for (var ancestor = loop.Parent; ancestor is not null; ancestor = ancestor.Parent)
                {
                    loopsWithUnrolledDescendant.Add(ancestor.Index);
                }
            }

            if (unrollCount == 0 || loopsWithUnrolledDescendant.Count == 0 || passes >= 10)
            {
                break;
            }

            JITDUMP($"A nested loop was unrolled. Doing another pass (pass {passes + 1})\n");
            fgInvalidateDfsTree();
            _dfsTree = fgComputeDfs();
            _loops = FlowGraphNaturalLoops.Find(_dfsTree);
            passes++;
        }

        if (unrollCount > 0)
        {
            assert(anyIRchange);
            Metrics.LoopsUnrolled += unrollCount;
#if DEBUG
            if (verbose)
            {
                jitprintf($"\nFinished unrolling {unrollCount} loops in {passes} passes\n");
            }
#endif
            _ = fgDfsBlocksAndRemove();
            assert(_dfsTree is not null);
            _loops = FlowGraphNaturalLoops.Find(_dfsTree);
            if (optCanonicalizeLoops())
            {
                fgInvalidateDfsTree();
                _dfsTree = fgComputeDfs();
                _loops = FlowGraphNaturalLoops.Find(_dfsTree);
            }

#if DEBUG
            if (verbose)
            {
                fgDispBasicBlocks();
            }
#endif
        }

#if DEBUG
        fgDebugCheckBBlist();
#endif
        return anyIRchange ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool optTryUnrollLoop(FlowGraphNaturalLoop loop, ref bool changedIR)
    {
        var iterLimit = compCodeOpt switch
        {
            BLENDED_CODE => 10u,
            SMALL_CODE => 0u,
            FAST_CODE => 20u,
            _ => 0u,
        };
#if DEBUG
        if (compStressCompile(STRESS_UNROLL_LOOPS, 50))
        {
            iterLimit *= 10;
        }
#endif
        var unrollLimitSz = compCodeOpt switch
        {
            BLENDED_CODE => 300,
            SMALL_CODE => 0,
            FAST_CODE => 600,
            _ => 0,
        };
        if (loop.Header.isRunRarely)
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: Loop is cold.\n");
            return false;
        }

        if (!loop.AnalyzeIteration(out var iterInfo))
        {
            return false;
        }
        if (!iterInfo.HasConstInit || !iterInfo.HasConstLimit)
        {
            return false;
        }

        var testBlock = iterInfo.TestBlock
            ?? throw new FatalJitException("An analyzed loop must have a test block.");
        assert(loop.ContainsBlock(testBlock.TrueTarget) != loop.ContainsBlock(testBlock.FalseTarget));
        if (testBlock.TrueTarget != loop.Header && testBlock.FalseTarget != loop.Header)
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: test block is not a backedge\n");
            return false;
        }

        var lbeg = iterInfo.ConstInitValue;
        var llim = iterInfo.ConstLimit();
        var testOper = iterInfo.TestOper();
        var lvar = iterInfo.IterVar;
        var iterInc = iterInfo.IterConst();
        var iterOper = iterInfo.IterOper();
        var iterOperType = iterInfo.IterOperType();
        var testTree = iterInfo.TestTree
            ?? throw new FatalJitException("An analyzed loop must have a test tree.");
        var unsTest = (testTree.Flags & GTF_UNSIGNED) != 0;
        assert(!lvaGetDesc(lvar).IsAddressExposed && !lvaGetDesc(lvar).lvIsStructField);

        JITDUMP("Analyzing candidate for loop unrolling:\n");
#if DEBUG
        if (verbose)
        {
            FlowGraphNaturalLoop.Dump(loop);
        }
#endif
        if (!optComputeLoopRep(lbeg, llim, iterInc, iterOper, iterOperType,
            testOper, unsTest, out var totalIter))
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: not a constant iteration count\n");
            return false;
        }

        JITDUMP($"Computed loop repetition count (number of test block executions) to be {totalIter}\n");
        if (totalIter > iterLimit)
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: too many iterations ({totalIter} > {iterLimit}) (heuristic)\n");
            return false;
        }

#if DEBUG
        if (compStressCompile(STRESS_UNROLL_LOOPS, 50))
        {
            unrollLimitSz *= 4;
        }
        else
#endif
        if (totalIter <= 1)
        {
            unrollLimitSz = int.MaxValue;
        }
        else if (totalIter > opts.compJitUnrollLoopMaxIterationCount && !iterInfo.HasSimdLimit)
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: insufficiently simple loop (heuristic)\n");
            return false;
        }

        var increment = iterInfo.IterTree
            ?? throw new FatalJitException("An analyzed loop must have an increment.");
        if (increment.Oper is not GT_STORE_LCL_VAR)
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: unknown increment op ({increment.Oper.Name})\n");
            return false;
        }
        increment = increment.AsLclVarCommon().Data;
        assert(testBlock.Kind is BBJ_COND);
        if (increment.Oper is not (GT_ADD or GT_SUB) ||
            increment.AsOp().Op1.Oper is not GT_LCL_VAR ||
            increment.AsOp().Op1.AsLclVarCommon().LclNum != lvar ||
            increment.AsOp().Op2.Oper is not GT_CNS_INT ||
            increment.AsOp().Op2.AsIntCon().IconValue != iterInc ||
            testBlock.LastStmt?.RootNode.AsUnOp().Op1 != testTree)
        {
            throw new FatalJitException("Bad precondition in Compiler::optUnrollLoops().");
        }

        var unrollLoopsWithEH = false;
#if DEBUG
        unrollLoopsWithEH = JitConfig.JitUnrollLoopsWithEH > 0;
#endif
        if (!optCanDuplicateLoop(loop, unrollLoopsWithEH, out var reason))
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: {reason}\n");
            return false;
        }

        // gtSetStmtInfo can change the IR even when the cost rejects duplication.
        changedIR = true;
        ulong loopCostSz = 0;
        var costOverflow = false;
        _ = loop.VisitLoopBlocksReversePostOrder(block => {
            foreach (var statement in block.Statements)
            {
                gtSetStmtInfo(statement);
                loopCostSz += statement.CostSz;
                costOverflow |= loopCostSz > uint.MaxValue;
            }
            return BasicBlockVisit.Continue;
        });

        // Both unsigned safe values are converted to ClrSafeInt<int> before subtraction.
        var costExceeded = costOverflow || loopCostSz > int.MaxValue - 8 ||
            loopCostSz * totalIter > int.MaxValue;
        var duplicationCost = costExceeded ? 0L :
            (long)(loopCostSz * totalIter) - (long)loopCostSz - 8;
        if (costExceeded || duplicationCost < int.MinValue || duplicationCost > int.MaxValue ||
            duplicationCost > unrollLimitSz)
        {
            JITDUMP($"Failed to unroll loop L{loop.Index:D2}: size constraint ({duplicationCost} > {unrollLimitSz}) (heuristic)\n");
            return false;
        }

        JITDUMP($"\nUnrolling loop L{loop.Index:D2} unrollCostSz = {duplicationCost}\n");
#if DEBUG
        if (verbose)
        {
            FlowGraphNaturalLoop.Dump(loop);
        }
#endif

        var blockMap = new Dictionary<BasicBlock, BasicBlock>();
        var bottom = loop.GetLexicallyBottomMostBlock();
        var insertAfter = bottom;
        BasicBlock? prevTestBlock = null;
        var exiting = testBlock;
        var exit = loop.ContainsBlock(exiting.TrueTarget) ? exiting.FalseTarget : exiting.TrueTarget;
        var lval = lbeg;
        for (var remaining = totalIter; remaining > 0; remaining--)
        {
            var scaleWeight = 1.0 / BB_LOOP_WEIGHT_SCALE;
            optDuplicateLoop(loop, ref insertAfter, blockMap, scaleWeight, unrollLoopsWithEH);
            _ = loop.VisitLoopBlocks(block => {
                optReplaceScalarUsesWithConst(blockMap[block], lvar, lval);
                return BasicBlockVisit.Continue;
            });

            var clonedTest = blockMap[testBlock];
            optRedirectPrevUnrollIteration(loop, prevTestBlock, blockMap[loop.Header]);
            prevTestBlock = clonedTest;
            lval = iterOper switch
            {
                GT_ADD => unchecked(lval + iterInc),
                GT_SUB => unchecked(lval - iterInc),
                _ => throw new FatalJitException("Unexpected loop increment operator."),
            };
        }

        optRedirectPrevUnrollIteration(loop, prevTestBlock, exit);

#if DEBUG
        if (verbose)
        {
            jitprintf("Whole unrolled loop:\n");
            gtDispTree(iterInfo.InitTree);
            jitprintf("\n");
            fgDumpTrees(bottom.Next, insertAfter);
        }
#endif

        return true;
    }

    private void optRedirectPrevUnrollIteration(FlowGraphNaturalLoop loop,
        BasicBlock? prevTestBlock, BasicBlock target)
    {
        if (prevTestBlock is not null)
        {
            assert(prevTestBlock.Kind is BBJ_COND);
            var testCopyStmt = prevTestBlock.LastStmt!;
            var testCopyExpr = testCopyStmt.RootNode;
            assert(testCopyExpr.Oper is GT_JTRUE);
            GenTree? sideEffects = null;
            gtExtractSideEffList(testCopyExpr, ref sideEffects, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF);
            if (sideEffects is null)
            {
                fgRemoveStmt(prevTestBlock, testCopyStmt);
            }
            else
            {
                testCopyStmt.RootNode = sideEffects;
            }

            fgRedirectEdge(ref prevTestBlock.TrueEdgeRef, target);
            fgRemoveRefPred(prevTestBlock.FalseEdge);
            prevTestBlock.SetKindAndTargetEdge(BBJ_ALWAYS, prevTestBlock.TrueEdge);
            JITDUMP($"Redirecting previously created exiting {FMT_BB(prevTestBlock.bbNum)} -> {FMT_BB(target.bbNum)}\n");
        }
        else
        {
            foreach (var entryEdge in loop.EntryEdges)
            {
                var entering = entryEdge.SourceBlock;
                JITDUMP($"Redirecting {FMT_BB(entering.bbNum)} -> {FMT_BB(loop.Header.bbNum)} to {FMT_BB(entering.bbNum)} -> {FMT_BB(target.bbNum)}\n");
                assert(entering.Kind is not BBJ_COND);
                fgReplaceJumpTarget(entering, loop.Header, target);
            }
        }
    }

    private struct LoopUnrollReplaceVisitor : IGenTreeVisitor<LoopUnrollReplaceVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool DoLclVarsOnly => true;

        private readonly Compiler _compiler;
        private readonly int _lclNum;
        private readonly nint _value;
        private readonly GenTreeStack _ancestors;
        public bool MadeChanges;

        public LoopUnrollReplaceVisitor(Compiler compiler, int lclNum, nint value)
        {
            _compiler = compiler;
            _lclNum = lclNum;
            _value = value;
            _ancestors = [];
            MadeChanges = false;
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.Oper is GT_LCL_VAR && use.AsLclVarCommon().LclNum == _lclNum)
            {
                use = _compiler.gtNewIconNode(use.Type.ActualType, _value);
                MadeChanges = true;
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<LoopUnrollReplaceVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private void optReplaceScalarUsesWithConst(BasicBlock block, int lclNum, nint value)
    {
        var visitor = new LoopUnrollReplaceVisitor(this, lclNum, value);
        foreach (var statement in block.Statements)
        {
            _ = visitor.WalkTree(ref statement.RootNodeRef, null);
            if (visitor.MadeChanges)
            {
                gtUpdateStmtSideEffects(statement);
                visitor.MadeChanges = false;
            }
        }
    }
}
