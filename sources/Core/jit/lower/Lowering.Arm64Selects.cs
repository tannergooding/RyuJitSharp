// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void TryLowerCselToCSOp(GenTreeOp select, GenTree condition)
    {
        assert(select.Oper is GT_SELECT or GT_SELECTCC);
        var trueValue = select.Op1;
        var falseValue = select.Op2;
        genTreeOps resultingOper;
        bool reverse;
        if ((trueValue.Oper is GT_NEG) || (falseValue.Oper is GT_NEG))
        {
            resultingOper = GT_SELECT_NEG;
            reverse = trueValue.Oper is GT_NEG;
        }
        else if ((trueValue.Oper is GT_NOT) || (falseValue.Oper is GT_NOT))
        {
            resultingOper = GT_SELECT_INV;
            reverse = trueValue.Oper is GT_NOT;
        }
        else
        {
            assert((trueValue.Oper is GT_ADD) || (falseValue.Oper is GT_ADD));
            resultingOper = GT_SELECT_INC;
            reverse = trueValue.Oper is GT_ADD;
        }

        var nodeToRemove = reverse ? trueValue : falseValue;
        var operatedValue = nodeToRemove.AsUnOp().Op1;
        var nonOperatedValue = reverse ? falseValue : trueValue;
        if (reverse && !condition.Oper.IsCompare && (select.Oper is GT_SELECT))
        {
            return;
        }

        if (nodeToRemove.HasOverflowCheckEx)
        {
            return;
        }

        if ((resultingOper is GT_SELECT_INC) && !nodeToRemove.AsOp().Op2.IsIntegralConst(1))
        {
            return;
        }

        if (!IsInvariantInRange(operatedValue, select) || !IsInvariantInRange(nonOperatedValue, select))
        {
            return;
        }

        if (resultingOper is GT_SELECT_INC)
        {
            BlockRange().Remove(nodeToRemove.AsOp().Op2);
            nodeToRemove.AsOp().Op2Ref = null;
        }

        BlockRange().Remove(nodeToRemove);
        operatedValue.IsContained = false;
        select.Op1 = nonOperatedValue;
        select.Op2 = operatedValue;
        if (select.Oper is GT_SELECT)
        {
            if (reverse)
            {
                var reversed = CompilerInstance.gtReverseCond(condition);
                assert(reversed == condition);
            }

            select.SetOper(resultingOper);
        }
        else
        {
            var selectCC = select.AsOpCC();
            if (reverse)
            {
                selectCC.Condition = GenCondition.Reverse(selectCC.Condition);
            }

            resultingOper = resultingOper switch {
                GT_SELECT_NEG => GT_SELECT_NEGCC,
                GT_SELECT_INV => GT_SELECT_INVCC,
                GT_SELECT_INC => GT_SELECT_INCCC,
                _ => throw new System.Diagnostics.UnreachableException(),
            };
            selectCC.SetOper(resultingOper);
        }

        JITDUMP("Converted to ");
#if DEBUG
        if (CompilerInstance.verbose)
        {
            CompilerInstance.gtDispNodeName(select);
        }
#endif
        JITDUMP(":\n");
        DISPTREERANGE(BlockRange(), select);
        JITDUMP("\n");
    }

    private void TryLowerCnsIntCselToCinc(GenTreeOp select, GenTree condition)
    {
        assert(select.Oper is GT_SELECT or GT_SELECTCC);
        var trueValue = select.Op1;
        var falseValue = select.Op2;
        if (trueValue.Oper.IsCnsIntOrI && falseValue.Oper.IsCnsIntOrI)
        {
            var op1Value = unchecked((nuint)trueValue.AsIntCon().IconValue);
            var op2Value = unchecked((nuint)falseValue.AsIntCon().IconValue);
            if ((unchecked(op1Value + 1) == op2Value) || (unchecked(op2Value + 1) == op1Value))
            {
                var reverse = unchecked(op1Value + 1) == op2Value;
                if (select.Oper is GT_SELECT)
                {
                    if (reverse)
                    {
                        if (!condition.Oper.IsCompare)
                        {
                            return;
                        }

                        var reversed = CompilerInstance.gtReverseCond(condition);
                        assert(reversed == condition);
                    }

                    BlockRange().Remove(select.Op2, markOperandsUnused: true);
                    select.Op2Ref = null;
                    select.SetOper(GT_SELECT_INC);
                    JITDUMP("Converted to: GT_SELECT_INC\n");
                    DISPTREERANGE(BlockRange(), select);
                    JITDUMP("\n");
                }
                else
                {
                    var selectCC = select.AsOpCC();
                    if (reverse)
                    {
                        selectCC.Condition = GenCondition.Reverse(selectCC.Condition);
                    }
                    else
                    {
                        (selectCC.Op1, selectCC.Op2) = (selectCC.Op2, selectCC.Op1);
                    }

                    BlockRange().Remove(selectCC.Op2, markOperandsUnused: true);
                    selectCC.Op2Ref = null;
                    selectCC.SetOper(GT_SELECT_INCCC);
                    JITDUMP("Converted to: GT_SELECT_INCCC\n");
                    DISPTREERANGE(BlockRange(), selectCC);
                    JITDUMP("\n");
                }
            }
        }
        else
        {
            if ((condition.Oper is not GT_CMP) || (select.Oper is not GT_SELECTCC))
            {
                return;
            }

            var compare = condition.AsOp();
            if ((!compare.Op1.Oper.IsIntegralConst && !compare.Op2.Oper.IsIntegralConst) ||
                ((compare.Op1.Oper is not GT_LCL_VAR) && (compare.Op2.Oper is not GT_LCL_VAR)))
            {
                return;
            }

            var code = select.AsOpCC().Condition.Code;
            var constant = compare.Op1.Oper.IsIntegralConst ? compare.Op1 : compare.Op2;
            var local = compare.Op1.Oper.IsIntegralConst ? compare.Op2 : compare.Op1;
            var constantValue = constant.AsIntCon().IntegralValue;
            var localNumber = local.AsLclVar().LclNum;
            if (code is not (GenCondition.EQ or GenCondition.NE))
            {
                return;
            }

            if ((code is GenCondition.EQ) && (!trueValue.Oper.IsIntegralConst ||
                (unchecked(trueValue.AsIntCon().IntegralValue - constantValue) != 1)))
            {
                return;
            }
            if ((code is GenCondition.NE) && (!falseValue.Oper.IsIntegralConst ||
                (unchecked(falseValue.AsIntCon().IntegralValue - constantValue) != 1)))
            {
                return;
            }

            var localType = local.Type.ActualType;
            if (localType != select.Type.ActualType)
            {
                return;
            }

            if (code is GenCondition.EQ)
            {
                var selectCC = select.AsOpCC();
                selectCC.Condition = GenCondition.Reverse(selectCC.Condition);
                (selectCC.Op1, selectCC.Op2) = (selectCC.Op2, selectCC.Op1);
                falseValue = selectCC.Op2;
            }

            var newLocal = CompilerInstance.gtNewLclvNode(localType, localNumber);
            BlockRange().InsertBefore(falseValue, newLocal);
            BlockRange().Remove(falseValue);
            select.Op2 = newLocal;
            select.SetOper(GT_SELECT_INCCC);
            JITDUMP("Converted to: GT_SELECT_INCCC\n");
            DISPTREERANGE(BlockRange(), select);
            JITDUMP("\n");
        }
    }
}
#endif
