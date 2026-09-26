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
    private sealed class OptTestInfo
    {
        public Statement? TestStmt;
        public GenTree? TestTree;
        public GenTreeOp? CompTree;
        public bool IsBool;

        public GenTree GetTestOp()
        {
            assert(TestTree is not null);
            return TestTree.AsUnOp().Op1;
        }

        public void SetTestOp(GenTree operand)
        {
            assert(TestTree is not null);
            assert(TestTree.Oper is GT_JTRUE or GT_RETURN or GT_SWIFT_ERROR_RET);
            TestTree.AsUnOp().Op1 = operand;
        }
    }

    private sealed partial class OptBoolsDsc(BasicBlock first, BasicBlock second, Compiler compiler)
    {
        private readonly BasicBlock _first = first;
        private readonly BasicBlock _second = second;
        private readonly Compiler _compiler = compiler;
        private readonly OptTestInfo _test1 = new();
        private readonly OptTestInfo _test2 = new();
        private GenTree? _operand1;
        private GenTree? _operand2;
        private bool _sameTarget;
        private genTreeOps _foldOp;
        private var_types _foldType;
        private genTreeOps _compareOp;

        private Statement? optOptimizeBoolsChkBlkCond()
        {
            if ((_second.CountOfInEdges > 1) || (_first.LastStmt is not Statement firstStatement) ||
                (_second.FirstStmt is not Statement secondStatement) ||
                (secondStatement != _second.LastStmt))
            {
                return null;
            }

            assert(firstStatement.RootNode.Oper is GT_JTRUE);
            assert(secondStatement.RootNode.Oper is GT_JTRUE);
            _test1.TestStmt = firstStatement;
            _test1.TestTree = firstStatement.RootNode;
            _test2.TestStmt = secondStatement;
            _test2.TestTree = secondStatement.RootNode;
            return firstStatement;
        }

        private GenTree? optIsBoolComp(OptTestInfo test)
        {
            test.IsBool = false;
            assert(test.TestTree?.Oper is GT_JTRUE or GT_RETURN or GT_SWIFT_ERROR_RET);
            var condition = test.GetTestOp();
            if (condition.Oper is not (GT_EQ or GT_NE or GT_LT or GT_GT or GT_GE or GT_LE))
            {
                return null;
            }

            var comparison = condition.AsOp();
            test.CompTree = comparison;
            var operand = comparison.Op1;
            var constant = comparison.Op2;
            if ((constant.Oper is not GT_CNS_INT) ||
                (!constant.IsIntegralConst(0) && !constant.IsIntegralConst(1)))
            {
                return null;
            }

            test.IsBool = operand.IsIntegralConst(0) || operand.IsIntegralConst(1);
            if (constant.IsIntegralConst(1))
            {
                if (!test.IsBool)
                {
                    return null;
                }

                _ = _compiler.gtReverseCond(condition);
                constant.AsIntCon().IconValue = 0;
            }

            return operand;
        }

        private bool optOptimizeBoolsChkTypeCostCond()
        {
            assert(_operand1 is not null && _operand2 is not null);
            if (varTypeIsFloating(_operand1.Type) || varTypeIsFloating(_operand2.Type) ||
                (_operand1.Type.Size != _operand2.Type.Size))
            {
                return false;
            }

            assert(_test1.CompTree is not null && _test2.CompTree is not null);
            if ((_test1.CompTree.Type.Size != _test2.CompTree.Type.Size) ||
                ((_operand2.Flags & GTF_GLOB_EFFECT) != 0) ||
                (_operand2.CostEx > 12))
            {
                return false;
            }

            return true;
        }

        public bool optOptimizeBoolsCondBlock()
        {
            if (_first.TrueTarget == _second.TrueTarget)
            {
                _sameTarget = true;
            }
            else if (_second.FalseTarget == _first.TrueTarget)
            {
                _sameTarget = false;
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

            _operand1 = optIsBoolComp(_test1);
            if (_operand1 is null)
            {
                return false;
            }

            _operand2 = optIsBoolComp(_test2);
            if ((_operand2 is null) || !optOptimizeBoolsChkTypeCostCond())
            {
                return false;
            }

            assert(_test1.CompTree is not null && _test2.CompTree is not null);
            var firstOp = _test1.CompTree.Oper;
            var secondOp = _test2.CompTree.Oper;
            var sameLocal = (_operand1.Oper is GT_LCL_VAR) && (_operand2.Oper is GT_LCL_VAR) &&
                (_operand1.AsLclVarCommon().LclNum == _operand2.AsLclVarCommon().LclNum);
            var signed = !_test1.GetTestOp().AsOp().IsUnsigned &&
                !_test2.GetTestOp().AsOp().IsUnsigned;
            _foldType = _operand1.Type.ActualType;
            if (varTypeIsGC(_foldType))
            {
                _foldType = TYP_I_IMPL;
            }

            if (_sameTarget)
            {
                if (sameLocal)
                {
                    if (!signed)
                    {
                        return false;
                    }

                    if (((firstOp is GT_LT) && (secondOp is GT_EQ)) ||
                        ((firstOp is GT_EQ) && (secondOp is GT_LT)))
                    {
                        _compareOp = GT_LE;
                    }
                    else if (((firstOp is GT_GT) && (secondOp is GT_EQ)) ||
                             ((firstOp is GT_EQ) && (secondOp is GT_GT)))
                    {
                        _compareOp = GT_GE;
                    }
                    else
                    {
                        return false;
                    }

                    _foldOp = GT_NONE;
                }
                else if ((firstOp is GT_EQ) && (secondOp is GT_EQ))
                {
                    _foldOp = GT_AND;
                    _compareOp = GT_EQ;
                }
                else if ((firstOp is GT_LT) && (secondOp is GT_LT) && signed)
                {
                    _foldOp = GT_OR;
                    _compareOp = GT_LT;
                }
                else if ((firstOp is GT_NE) && (secondOp is GT_NE))
                {
                    _foldOp = GT_OR;
                    _compareOp = GT_NE;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (sameLocal)
                {
                    if (!signed)
                    {
                        return false;
                    }

                    if (((firstOp is GT_LT) && (secondOp is GT_NE)) ||
                        ((firstOp is GT_EQ) && (secondOp is GT_GE)))
                    {
                        _compareOp = GT_GT;
                    }
                    else if (((firstOp is GT_GT) && (secondOp is GT_NE)) ||
                             ((firstOp is GT_EQ) && (secondOp is GT_LE)))
                    {
                        _compareOp = GT_LT;
                    }
                    else
                    {
                        return false;
                    }

                    _foldOp = GT_NONE;
                }
                else if ((firstOp is GT_EQ) && (secondOp is GT_NE))
                {
                    _foldOp = GT_AND;
                    _compareOp = GT_NE;
                }
                else if ((firstOp is GT_LT) && (secondOp is GT_GE) && signed)
                {
                    _foldOp = GT_OR;
                    _compareOp = GT_GE;
                }
                else if ((firstOp is GT_NE) && (secondOp is GT_EQ))
                {
                    _foldOp = GT_OR;
                    _compareOp = GT_EQ;
                }
                else
                {
                    return false;
                }
            }

            if ((_foldOp is GT_AND) && (!_test1.IsBool || !_test2.IsBool))
            {
                return false;
            }

            optOptimizeBoolsUpdateTrees();
#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP($"Folded {(_operand2.Oper.IsLeaf ? "" : "non-leaf ")}boolean conditions of " +
                    $"{FMT_BB(_first.bbNum)} and {FMT_BB(_second.bbNum)} to :\n");
                _compiler.gtDispStmt(firstStatement);
                JITDUMP("\n");
            }
#endif

            return true;
        }

        private void optOptimizeBoolsUpdateTrees()
        {
            assert(_operand1 is not null && _operand2 is not null);
            assert(_test1.CompTree is not null && _test1.TestStmt is not null);
            var firstOperand = _foldOp is GT_NONE
                ? _operand1
                : _compiler.gtNewBinaryNode(_foldOp, _foldType, _operand1, _operand2);
            _test1.CompTree.SetOper(_compareOp);
            _test1.CompTree.Op1 = firstOperand;
            _test1.CompTree.Op2.Type = _foldType;
            if (_compiler.fgNodeThreading is not NodeThreading.None)
            {
                _compiler.gtSetStmtInfo(_test1.TestStmt);
                _compiler.fgSetStmtSeq(_test1.TestStmt);
                _compiler.gtUpdateStmtSideEffects(_test1.TestStmt);
            }

            var firstTrue = _first.TrueEdge;
            var secondTrue = _second.TrueEdge;
            var secondFalse = _second.FalseEdge;
            var firstLikelihood = firstTrue.Likelihood;
            double newLikelihood;
            if (_sameTarget)
            {
                newLikelihood = firstLikelihood + (1.0 - firstLikelihood) * secondTrue.Likelihood;
            }
            else
            {
                _compiler.fgRedirectEdge(ref _first.TrueEdgeRef, _second.TrueTarget);
                firstTrue.isHeuristicBased = secondTrue.isHeuristicBased;
                newLikelihood = (1.0 - firstLikelihood) * secondTrue.Likelihood;
            }

            firstTrue.Likelihood = newLikelihood;
            _compiler.fgReplacePred(secondFalse, _first);
            _compiler.fgRemoveRefPred(secondTrue);
            _first.FalseEdgeRef = secondFalse;
            secondFalse.Likelihood = 1.0 - newLikelihood;
            secondFalse.isHeuristicBased = firstTrue.isHeuristicBased;
            if (_first.hasProfileWeight)
            {
                var trueTarget = firstTrue.DestinationBlock;
                var falseTarget = secondFalse.DestinationBlock;
                trueTarget.setBBProfileWeight(trueTarget.computeIncomingWeight());
                falseTarget.setBBProfileWeight(falseTarget.computeIncomingWeight());
                if ((trueTarget.NumSucc > 0) || (falseTarget.NumSucc > 0))
                {
                    JITDUMP($"optOptimizeRangeTests: Profile needs to be propagated through " +
                        $"{FMT_BB(_first.bbNum)}'s successors. Data " +
                        $"{(_compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                    _compiler.fgPgoConsistent = false;
                }
            }

            _compiler.fgUnlinkBlockForRemoval(_second);
            _second.SetFlags(BBF_REMOVED);
            _compiler.ehUpdateForDeletedBlock(_second);
            _first.bbCodeOffsEnd = _second.bbCodeOffsEnd;
        }
    }

    public PhaseStatus optOptimizeBools()
    {
#if DEBUG
        if (verbose)
        {
            jitprintf("*************** In optOptimizeBools()\n");
        }
#endif
        var changedCount = 0;
        var passes = 0;
        var stress = false;
        var traits = new BitVecTraits(this, fgBBNumMax + 1);
        var ccmpVec = BitVecOps.MakeEmpty(traits);
        bool changed;
        do
        {
            passes++;
            changed = false;
            for (var first = fgFirstBB; first is not null;)
            {
                var retry = false;
                if ((first.Kind is BBJ_COND) && fgFoldCondToReturnBlock(first))
                {
                    changed = true;
                    changedCount++;
                }

                if (first.Kind is BBJ_COND)
                {
                    var second = first.FalseTarget;
                    if (second is null)
                    {
                        break;
                    }

                    if (!second.HasFlag(BBF_DONT_REMOVE) && (second.Kind is BBJ_COND) &&
                        ((first.TrueTarget == second.TrueTarget) || (second.FalseTarget == first.TrueTarget)))
                    {
                        var descriptor = new OptBoolsDsc(first, second, this);
                        if (descriptor.optOptimizeBoolsCondBlock())
                        {
                            changed = true;
                            changedCount++;
                        }
                        else if (descriptor.optOptimizeRangeTests())
                        {
                            changed = true;
                            retry = true;
                            changedCount++;
                        }
#if TARGET_ARM64
                        else if (descriptor.optOptimizeCompareChainCondBlock())
                        {
                            changed = true;
                            retry = true;
                            changedCount++;
                        }
#elif TARGET_AMD64
                        else if (canUseApxEvexEncoding() && (JitConfig.EnableApxConditionalChaining != 0) &&
                                 !optSwitchDetectForCcmp(first, ccmpVec) &&
                                 descriptor.optOptimizeCompareChainCondBlock())
                        {
                            changed = true;
                            retry = true;
                            changedCount++;
                        }
#endif
                    }
#if DEBUG
                    else if (!second.HasFlag(BBF_DONT_REMOVE) && (second.Kind is not BBJ_COND))
                    {
                        new OptBoolsDsc(first, second, this).optOptimizeBoolsGcStress();
                        stress = true;
                    }
#endif
                }

                if (!retry)
                {
                    first = first.Next;
                }
            }
        } while (changed);

        JITDUMP($"\noptimized {changedCount} BBJ_COND cases in {passes} passes\n");
        return (stress || (changedCount > 0)) ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
