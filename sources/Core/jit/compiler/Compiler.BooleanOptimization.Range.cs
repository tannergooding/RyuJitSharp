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
    private static genTreeOps SwapBooleanRelop(genTreeOps oper) => oper switch
    {
        GT_LE => GT_GE,
        GT_LT => GT_GT,
        GT_GE => GT_LE,
        GT_GT => GT_LT,
        GT_EQ => GT_EQ,
        GT_NE => GT_NE,
        _ => throw new System.ArgumentOutOfRangeException(nameof(oper)),
    };

    private static genTreeOps ReverseBooleanRelop(genTreeOps oper) => oper switch
    {
        GT_LE => GT_GT,
        GT_LT => GT_GE,
        GT_GE => GT_LT,
        GT_GT => GT_LE,
        GT_EQ => GT_NE,
        GT_NE => GT_EQ,
        _ => throw new System.ArgumentOutOfRangeException(nameof(oper)),
    };

    private static bool GetBooleanRangeIntersection(var_types type, genTreeOps firstOp, genTreeOps secondOp,
        nint firstConstant, nint secondConstant, out nint start, out nint end)
    {
        start = 0;
        end = 0;
        if ((firstConstant < 0) || (secondConstant < 0))
        {
            return false;
        }

        if (firstOp is GT_GT)
        {
            if (firstConstant == nint.MaxValue)
            {
                return false;
            }

            firstConstant++;
            firstOp = GT_GE;
        }
        else if (firstOp is GT_LT)
        {
            firstConstant--;
            firstOp = GT_LE;
        }

        if (secondOp is GT_GT)
        {
            if (secondConstant == nint.MaxValue)
            {
                return false;
            }

            secondConstant++;
            secondOp = GT_GE;
        }
        else if (secondOp is GT_LT)
        {
            secondConstant--;
            secondOp = GT_LE;
        }

        if (firstOp == secondOp)
        {
            return false;
        }

        if (firstOp is GT_GE)
        {
            start = firstConstant;
            end = secondConstant;
        }
        else
        {
            assert(firstOp is GT_LE);
            start = secondConstant;
            end = firstConstant;
        }

        return (start < end) && (start >= 0) && (end >= 0) &&
            FitsIn(type, start) && FitsIn(type, end);
    }

    private static bool IsBooleanConstantRangeTest(GenTreeOp tree, out GenTree? variable,
        out GenTreeIntCon? constant, out genTreeOps comparison)
    {
        variable = null;
        constant = null;
        comparison = GT_NONE;
        if ((tree.Oper is not (GT_LE or GT_LT or GT_GE or GT_GT)) ||
            !varTypeIsIntegral(tree.Op1.Type) || !varTypeIsIntegral(tree.Op2.Type) ||
            (tree.Op1.Type != tree.Op2.Type))
        {
            return false;
        }

        if (tree.Op2.Oper.IsCnsIntOrI)
        {
            variable = tree.Op1;
            constant = tree.Op2.AsIntCon();
            comparison = tree.Oper;
            return true;
        }

        if (tree.Op1.Oper.IsCnsIntOrI)
        {
            variable = tree.Op2;
            constant = tree.Op1.AsIntCon();
            comparison = SwapBooleanRelop(tree.Oper);
            return true;
        }

        return false;
    }

    private bool FoldNeverNegativeRangeTest(GenTreeOp first, bool firstReversed,
        GenTreeOp second, bool secondReversed)
    {
        if (first.IsUnsigned ||
            !IsBooleanConstantRangeTest(first, out var variable, out var constant, out var firstOp))
        {
            return false;
        }

        assert(variable is not null && constant is not null);
        firstOp = firstReversed ? ReverseBooleanRelop(firstOp) : firstOp;
        var secondOp = secondReversed ? ReverseBooleanRelop(second.Oper) : second.Oper;
        if ((firstOp is not GT_GE) || !constant.IsIntegralConst(0))
        {
            return false;
        }

        GenTree upper;
        if ((second.Op1.Oper is GT_LCL_VAR or GT_LCL_FLD) &&
            GenTree.Compare(variable.EffectiveVal, second.Op1))
        {
            upper = second.Op2;
        }
        else if ((second.Op2.Oper is GT_LCL_VAR or GT_LCL_FLD) &&
                 GenTree.Compare(variable.EffectiveVal, second.Op2))
        {
            upper = second.Op1;
            secondOp = SwapBooleanRelop(secondOp);
        }
        else
        {
            return false;
        }

        if (!upper.IsNeverNegative(this) || (upper.Type != variable.Type) ||
            ((upper.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0) ||
            (secondOp is not (GT_LT or GT_LE)))
        {
            return false;
        }

        first.Op1 = variable;
        first.Op2 = upper;
        first.SetOper(secondReversed ? ReverseBooleanRelop(secondOp) : secondOp);
        first.Flags |= GTF_UNSIGNED;
        return true;
    }

    private bool FoldBooleanRangeTests(GenTreeOp first, bool firstReversed,
        GenTreeOp second, bool secondReversed)
    {
        if ((first.IsUnsigned != second.IsUnsigned) ||
            !IsBooleanConstantRangeTest(first, out var firstVariable, out var firstConstant, out var firstOp) ||
            !IsBooleanConstantRangeTest(second, out var secondVariable, out var secondConstant, out var secondOp))
        {
            return FoldNeverNegativeRangeTest(first, firstReversed, second, secondReversed);
        }

        assert(firstVariable is not null && secondVariable is not null);
        assert(firstConstant is not null && secondConstant is not null);
        firstOp = firstReversed ? ReverseBooleanRelop(firstOp) : firstOp;
        secondOp = secondReversed ? ReverseBooleanRelop(secondOp) : secondOp;
        if ((secondVariable.Oper is not GT_LCL_VAR) ||
            !GenTree.Compare(firstVariable.EffectiveVal, secondVariable) ||
            !GetBooleanRangeIntersection(firstVariable.Type, firstOp, secondOp,
                firstConstant.IconValue, secondConstant.IconValue, out var start, out var end))
        {
            return false;
        }

        var oldRight = first.Op2;
        first.Op1 = start == 0
            ? firstVariable
            : gtNewBinaryNode(GT_SUB, firstVariable.Type, firstVariable,
                gtNewIconNode(firstVariable.Type, start));
        if (oldRight is GenTreeIntCon oldConstant)
        {
            oldConstant.Type = firstVariable.Type;
            oldConstant.IconValue = end - start;
        }
        else
        {
            first.Op2 = new GenTreeIntCon(firstVariable.Type, end - start, null,
                oldRight, fgNodeThreading);
        }
        first.SetOper(secondReversed ? GT_GT : GT_LE);
        first.Flags |= GTF_UNSIGNED;
        return true;
    }

    private sealed partial class OptBoolsDsc
    {
        public bool optOptimizeRangeTests()
        {
            assert(_first.Kind is BBJ_COND && _second.Kind is BBJ_COND &&
                _first.FalseTarget == _second);
            if (_second.isRunRarely || !BasicBlock.sameEHRegion(_first, _second) ||
                _second.HasFlag(BBF_DONT_REMOVE) ||
                (_first.TrueTarget == _first) || (_first.TrueTarget == _second) ||
                (_second.TrueTarget == _second) || (_second.TrueTarget == _first))
            {
                return false;
            }

            var notInRange = _first.TrueTarget;
            BasicBlock inRange;
            var likelihood = _first.FalseEdge.Likelihood;
            bool secondReversed;
            if (_second.TrueTarget == notInRange)
            {
                inRange = _second.FalseTarget;
                likelihood *= _second.FalseEdge.Likelihood;
                secondReversed = true;
            }
            else if (_second.FalseTarget == notInRange)
            {
                inRange = _second.TrueTarget;
                likelihood *= _second.TrueEdge.Likelihood;
                secondReversed = false;
            }
            else
            {
                return false;
            }

            if ((_second.FirstStmt is null) || (_second.FirstStmt != _second.LastStmt) ||
                (_second.GetUniquePred(_compiler) != _first))
            {
                return false;
            }

            var firstStatement = _first.LastStmt;
            var secondStatement = _second.LastStmt;
            assert(firstStatement is not null && secondStatement is not null);
            var firstCompare = firstStatement.RootNode.AsUnOp().Op1.AsOp();
            var secondCompare = secondStatement.RootNode.AsUnOp().Op1.AsOp();
            if (!_compiler.FoldBooleanRangeTests(firstCompare, true, secondCompare, secondReversed))
            {
                return false;
            }

            var newEdge = _compiler.fgAddRefPred(inRange, _first);
            var oldFalse = _first.FalseEdge;
            var oldTrue = _first.TrueEdge;
            newEdge.isHeuristicBased = oldTrue.isHeuristicBased;
            newEdge.Likelihood = likelihood;
            oldTrue.Likelihood = 1.0 - likelihood;
            if (!secondReversed)
            {
                _first.FalseEdgeRef = oldTrue;
                _first.TrueEdgeRef = newEdge;
            }
            else
            {
                _first.FalseEdgeRef = newEdge;
            }

            _compiler.fgRemoveRefPred(oldFalse);
            _ = _compiler.fgRemoveBlock(_second, true);
            if (_first.hasProfileWeight)
            {
                var trueTarget = _first.TrueTarget;
                var falseTarget = _first.FalseTarget;
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

            _compiler.gtSetStmtInfo(firstStatement);
            _compiler.fgSetStmtSeq(firstStatement);
            _compiler.gtUpdateStmtSideEffects(firstStatement);
            return true;
        }
    }
}
