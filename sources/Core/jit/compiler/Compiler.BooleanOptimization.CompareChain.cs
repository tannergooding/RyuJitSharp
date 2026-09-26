// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class OptBoolsDsc
    {
        private static bool FindCompareChain(GenTree condition, out bool isTestCondition)
        {
            var left = condition.AsOp().Op1;
            var right = condition.AsOp().Op2;
            isTestCondition = false;

            if ((condition.Oper is GT_EQ or GT_NE) && right.Oper.IsIntegralConst)
            {
                var value = right.AsIntCon().IconValue;
                if (value == 0)
                {
                    if ((left.Oper is GT_AND or GT_OR) &&
                        left.AsOp().Op2.Oper.IsCmpCompare &&
                        varTypeIsIntegralOrI(left.AsOp().Op2.AsOp().Op1.Type))
                    {
                        return true;
                    }

                    isTestCondition = true;
                }
                else if ((left.Oper is GT_AND) && ((value & (value - 1)) == 0) &&
                         (value != 0) && left.AsOp().Op2.IsIntegralConst(value))
                {
                    isTestCondition = true;
                }
            }

            return false;
        }

        public bool optOptimizeCompareChainCondBlock()
        {
            bool endOfOr;
            if ((_first.FalseTarget == _second) && (_second.FalseTarget == _first.TrueTarget))
            {
                endOfOr = true;
            }
            else if ((_first.FalseTarget == _second) && (_first.TrueTarget == _second.TrueTarget))
            {
                endOfOr = false;
            }
            else
            {
                return false;
            }

            var firstStatement = optOptimizeBoolsChkBlkCond();
            if (firstStatement is null)
            {
                return false;
            }

            assert(_test1.TestTree is not null && _test2.TestTree is not null);
            var secondStatement = _second.FirstStmt;
            assert(secondStatement is not null);
            var firstCondition = _test1.TestTree.AsUnOp().Op1;
            var secondCondition = _test2.TestTree.AsUnOp().Op1;
            if (!firstCondition.Oper.IsCompare || !secondCondition.Oper.IsCompare ||
                ((firstCondition.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0) ||
                ((secondCondition.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0) ||
                varTypeIsFloating(firstCondition.AsOp().Op1.Type) ||
                varTypeIsFloating(secondCondition.AsOp().Op1.Type))
            {
                return false;
            }

            var firstChain = FindCompareChain(firstCondition, out var firstTest);
            var secondChain = FindCompareChain(secondCondition, out var secondTest);
            if (firstTest || secondTest)
            {
                return false;
            }

            if (!_compiler.compStressCompile(STRESS_OPT_BOOLS_COMPARE_CHAIN_COST, 25))
            {
                var firstCost = firstCondition.CostEx;
                var secondCost = secondCondition.CostEx;
                if ((firstCost > (firstChain ? 31 : 7)) ||
                    (secondCost > (secondChain ? 31 : 7)))
                {
                    JITDUMP($"Skipping CompareChainCond that will evaluate conditions " +
                        $"unconditionally at costs {firstCost},{secondCost}\n");
                    return false;
                }
            }

            _compiler.fgRemoveStmt(_first, firstStatement, isUnlink: true);
            if (endOfOr)
            {
                var reversed = _compiler.gtReverseCond(firstCondition);
                assert(reversed == firstCondition);
            }

            var chainOp = endOfOr ? GT_AND : GT_OR;
            var chain = _compiler.gtNewBinaryNode(chainOp, TYP_INT, firstCondition, secondCondition);
            firstCondition.Flags &= ~GTF_RELOP_JMP_USED;
            secondCondition.Flags &= ~GTF_RELOP_JMP_USED;
            chain.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
            var test = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, chain,
                _compiler.gtNewIconNode(TYP_INT, 0));
            _test2.SetTestOp(test);
            _test2.TestTree.Flags |= test.Flags & GTF_ALL_EFFECT;
            _ = _compiler.gtSetEvalOrder(_test2.TestTree);
            _compiler.fgSetStmtSeq(secondStatement);

            var removed = _first.TrueEdge;
            var retained = _first.FalseEdge;
            var removedLikelihood = removed.Likelihood;
            var retainedLikelihood = retained.Likelihood;
            var removedTarget = removed.DestinationBlock;
            if (_second.hasProfileWeight)
            {
                _second.increaseBBProfileWeight(removed.LikelyWeight);
            }

            _compiler.fgRemoveRefPred(removed);
            _first.SetKindAndTargetEdge(BBJ_ALWAYS, retained);
            foreach (var edge in new[] { _second.TrueEdge, _second.FalseEdge })
            {
                var combined = retainedLikelihood * edge.Likelihood;
                if (edge.DestinationBlock == removedTarget)
                {
                    combined += removedLikelihood;
                }

                edge.Likelihood = double.Min(1.0, combined);
            }

            _second.CopyFlags(_first, BBF_COPY_PROPAGATE);
            if (_compiler.fgCanCompactBlock(_first))
            {
                _compiler.fgCompactBlock(_first);
            }

#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP($"\nCombined conditions {FMT_BB(_first.bbNum)} and " +
                    $"{FMT_BB(_second.bbNum)} into {(chainOp is GT_OR ? "OR" : "AND")} chain :\n");
                _compiler.fgDumpBlock(_first);
                JITDUMP("\n");
            }
#endif

            return true;
        }

#if DEBUG
        public void optOptimizeBoolsGcStress()
        {
            if (!_compiler.compStressCompile(STRESS_OPT_BOOLS_GC, 20))
            {
                return;
            }

            var statement = _first.LastStmt;
            assert(statement is not null && statement.RootNode.Oper is GT_JTRUE);
            var info = new OptTestInfo { TestStmt = statement, TestTree = statement.RootNode };
            var operand = optIsBoolComp(info);
            if ((operand is null) || !varTypeIsGC(operand.Type) ||
                ((operand.Flags & (GTF_ASG | GTF_CALL | GTF_ORDER_SIDEEFF)) != 0))
            {
                return;
            }

            assert(info.CompTree is not null && info.CompTree.Op1 == operand);
            var clone = _compiler.gtCloneExpr(operand);
            assert(clone is not null);
            var op = _compiler.compStressCompile(STRESS_OPT_BOOLS_GC, 50) ? GT_OR : GT_AND;
            info.CompTree.Op1 = _compiler.gtNewBinaryNode(op, TYP_I_IMPL, operand, clone);
            assert(info.CompTree.Op2.Oper is GT_CNS_INT);
            info.CompTree.Op2.Type = TYP_I_IMPL;
            if (_compiler.fgNodeThreading is not NodeThreading.None)
            {
                _compiler.gtSetStmtInfo(statement);
                _compiler.fgSetStmtSeq(statement);
            }
        }
#endif
    }
}
