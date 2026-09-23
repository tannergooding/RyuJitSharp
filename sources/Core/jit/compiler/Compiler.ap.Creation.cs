// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe AssertionIndex optCreateAssertion(GenTree op1, GenTree? op2, bool equals)
    {
        if (op2 is null)
        {
            assert(!equals);
            op1 = op1.EffectiveVal;

            // TODO-Cleanup: Replacing this with gtPeelOffset and a proper big-offset check changes native results.
            nint offset = 0;
            while ((op1.Oper is GT_ADD) && (op1.Type is TYP_BYREF))
            {
                var add = op1.AsOp();
                if (add.Op2.Oper.IsCnsIntOrI)
                {
                    offset = unchecked(offset + add.Op2.AsIntCon().IconVal);
                    op1 = add.Op1.EffectiveVal;
                }
                else if (add.Op1.Oper.IsCnsIntOrI)
                {
                    offset = unchecked(offset + add.Op1.AsIntCon().IconVal);
                    op1 = add.Op2.EffectiveVal;
                }
                else
                {
                    break;
                }
            }

            if (!fgIsBigOffset(offset) && (op1.Oper is GT_LCL_VAR) && !lvaGetDesc(op1.AsLclVarCommon().LclNum).IsAddressExposed)
            {
                if (optLocalAssertionProp)
                {
                    return optAddAssertion(AssertionDsc.CreateLclNonNullAssertion(this, op1.AsLclVarCommon().LclNum));
                }

                var vn = optConservativeNormalVN(op1);
                if (vn == ValueNumStore.NoVN)
                {
                    return NO_ASSERTION_INDEX;
                }

                return optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(this, vn));
            }
        }
        else if (op1.Oper.IsScalarLocal)
        {
            var lclNum = op1.AsLclVarCommon().LclNum;
            ref var local = ref lvaGetDesc(lclNum);
            if (local.IsAddressExposed)
            {
                return NO_ASSERTION_INDEX;
            }

            op2 = op2.EffectiveVal;
            switch (op2.Oper)
            {
                case GT_CNS_DBL:
                {
                    var constant = op2.AsDblCon().DconVal;
                    if (double.IsNaN(constant))
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    var vn1 = optConservativeNormalVN(op1);
                    var vn2 = optConservativeNormalVN(op2);
                    if (!optLocalAssertionProp && ((vn1 == ValueNumStore.NoVN) || (vn2 == ValueNumStore.NoVN)))
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    return optAddAssertion(AssertionDsc.CreateConstLclVarAssertion(this, lclNum, vn1, constant, vn2, equals));
                }

#if FEATURE_HW_INTRINSICS
                case GT_CNS_VEC:
                {
                    if (!varTypeIsSimd(op1.Type) || (op1.Type != op2.Type))
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    var vn1 = optConservativeNormalVN(op1);
                    var vn2 = optConservativeNormalVN(op2);
                    if (!optLocalAssertionProp && ((vn1 == ValueNumStore.NoVN) || (vn2 == ValueNumStore.NoVN)))
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    return optAddAssertion(AssertionDsc.CreateConstLclVarAssertion(this, lclNum, vn1, op2.AsVecCon(), vn2, equals));
                }
#endif

                case GT_CNS_INT:
                {
                    var vn1 = optConservativeNormalVN(op1);
                    var vn2 = optConservativeNormalVN(op2);
                    if (!optLocalAssertionProp && ((vn1 == ValueNumStore.NoVN) || (vn2 == ValueNumStore.NoVN)))
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    var constant = op2.AsIntCon().IconVal;
                    if (op1.Type is TYP_STRUCT)
                    {
                        assert(constant == 0);
                        return optAddAssertion(AssertionDsc.CreateConstLclVarAssertion(this, lclNum, vn1, O2K_ZEROOBJ, vn2, equals));
                    }

                    if (varTypeIsSmall(local.Type))
                    {
                        var truncated = optCastConstantSmall(constant, local.Type);
                        if ((op1.Oper is not GT_STORE_LCL_VAR) && (truncated != constant))
                        {
                            // Equality outside the small local's range is impossible, not a usable fact.
                            return NO_ASSERTION_INDEX;
                        }

                        constant = truncated;
                        if (!optLocalAssertionProp)
                        {
                            assert(vnStore is not null);
                            vn2 = vnStore.VNForIntCon((int)constant);
                        }
                    }

                    var icon = op2.AsIntCon();
                    return optAddAssertion(AssertionDsc.CreateConstLclVarAssertion(this, lclNum, vn1, constant, vn2,
                        equals, icon.IconHandleFlag, icon.FieldSeq));
                }

                case GT_LCL_VAR:
                {
                    if (!optLocalAssertionProp)
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    var lclNum2 = op2.AsLclVarCommon().LclNum;
                    ref var other = ref lvaGetDesc(lclNum2);
                    if ((lclNum == lclNum2) || (local.Type != other.Type) ||
                        (other.lvNormalizeOnLoad && !local.lvNormalizeOnLoad) || other.IsAddressExposed)
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    // Locals are processed before their parent; an embedded redefinition could invalidate this copy.
                    if (other.lvRedefinedInEmbeddedStatement)
                    {
                        return NO_ASSERTION_INDEX;
                    }

                    return optAddAssertion(AssertionDsc.CreateLclvarCopy(this, lclNum, lclNum2, equals));
                }

                case GT_CALL:
                {
                    if (optLocalAssertionProp)
                    {
                        var call = op2.AsCall();
                        if (call.IsHelperCall() && call.HelperNum.NonNullReturn)
                        {
                            return optAddAssertion(AssertionDsc.CreateLclNonNullAssertion(this, lclNum));
                        }
                    }
                    break;
                }
            }

            if (optLocalAssertionProp && equals && varTypeIsIntegral(op2.Type))
            {
                var nodeRange = IntegralRange.ForNode(op2, this);
                var typeRange = IntegralRange.ForType(op2.Type.ActualType);
                assert(typeRange.Contains(nodeRange));
                if (!typeRange.Equals(nodeRange))
                {
                    return optAddAssertion(AssertionDsc.CreateSubrange(this, lclNum, nodeRange));
                }
            }
        }
        else if (!optLocalAssertionProp)
        {
            assert(vnStore is not null);
            var vn1 = optConservativeNormalVN(op1);
            var vn2 = optConservativeNormalVN(op2);
            // Native limits this backup to non-handle int32 constants for throughput.
            if ((vn1 != ValueNumStore.NoVN) && (vn2 != ValueNumStore.NoVN) &&
                vnStore.IsVNInt32Constant(vn2) && !vnStore.IsVNHandle(vn2))
            {
                return optAddAssertion(AssertionDsc.CreateInt32ConstantVNAssertion(this, vn1, vn2, equals));
            }
        }

        return NO_ASSERTION_INDEX;
    }

    public unsafe AssertionIndex optCreateJtrueAssertions(GenTree op1, GenTree op2, bool equals)
    {
        var index = optCreateAssertion(op1, op2, equals);
        if (index != NO_ASSERTION_INDEX)
        {
            optCreateComplementaryAssertion(index);
        }

        return index;
    }

    public unsafe AssertionInfo optAssertionGenJtrue(GenTree tree)
    {
        var relop = tree.AsUnOp().Op1;
        if (!relop.Oper.IsCompare)
        {
            return new(NO_ASSERTION_INDEX);
        }

        var info = optCreateJTrueBoundsAssertion(tree);
        if (info.HasAssertion)
        {
            return info;
        }

        if (optLocalAssertionProp && !optCrossBlockLocalAssertionProp)
        {
            return new(NO_ASSERTION_INDEX);
        }

        bool equals;
        switch (relop.Oper)
        {
            case GT_EQ:
            {
                equals = true;
                break;
            }

            case GT_NE:
            {
                equals = false;
                break;
            }

            default:
            {
                // TODO-CQ: Other relops are excluded to avoid unused assertion table entries.
                return new(NO_ASSERTION_INDEX);
            }
        }

        // Look through CSE stores so exact-type checks can still see the indirection.
        var op1 = relop.AsOp().Op1.CommaStoreVal;
        var op2 = relop.AsOp().Op2.CommaStoreVal;
#if FEATURE_HW_INTRINSICS
        if (op1.Oper.IsHWIntrinsic && (op2.IsIntegralConst(0) || op2.IsIntegralConst(1)))
        {
            if (op2.IsIntegralConst(0))
            {
                equals = !equals;
            }

            var intrinsic = op1.AsHWIntrinsic();
            switch (intrinsic.HWIntrinsicId)
            {
                case NI_Vector_op_Equality:
                {
                    break;
                }

                case NI_Vector_op_Inequality:
                {
                    equals = !equals;
                    break;
                }

                default:
                {
                    return new(NO_ASSERTION_INDEX);
                }
            }

            // Floating SIMD equality is not bitwise equality: signed zero and NaNs differ.
            if (!varTypeIsIntegral(intrinsic.SimdBaseType))
            {
                return new(NO_ASSERTION_INDEX);
            }

            assert(intrinsic.Operands.Length == 2);
            op1 = intrinsic.GetOp(1);
            op2 = intrinsic.GetOp(2);
            if (!op2.Oper.IsCnsVec)
            {
                return new(NO_ASSERTION_INDEX);
            }

            assert(varTypeIsSimd(op1.Type) && (op1.Type == op2.Type));
        }
#endif
        if (optLocalAssertionProp && varTypeIsFloating(op1.Type))
        {
            return new(NO_ASSERTION_INDEX);
        }

        if (!optLocalAssertionProp && (op1.Oper is GT_IND) && (op1.AsUnOp().Op1.Type is TYP_REF))
        {
            assert(vnStore is not null);
            var objVN = optConservativeNormalVN(op1.AsUnOp().Op1);
            var typeVN = optConservativeNormalVN(op2);
            if ((objVN != ValueNumStore.NoVN) && vnStore.IsVNTypeHandle(typeVN))
            {
                var index = optAddAssertion(AssertionDsc.CreateSubtype(this, objVN, typeVN, exact: true));
                // The opposite type assertion is not useful; retain only the edge where equality holds.
                return relop.Oper is GT_NE ? AssertionInfo.ForNextEdge(index) : new(index);
            }
        }

        if ((op1.Oper is not GT_LCL_VAR) && (op2.Oper is GT_LCL_VAR))
        {
            (op1, op2) = (op2, op1);
        }

        if ((op1.Oper is GT_LCL_VAR) && (op2.Oper.IsConst || (op2.Oper is GT_LCL_VAR)))
        {
            if ((lvaGetDesc(op1.AsLclVarCommon().LclNum).Type is TYP_LONG) && (op1.Type is not TYP_LONG))
            {
                return new(NO_ASSERTION_INDEX);
            }

            if ((op2.Oper is GT_LCL_VAR) && (lvaGetDesc(op2.AsLclVarCommon().LclNum).Type is TYP_LONG) && (op2.Type is not TYP_LONG))
            {
                return new(NO_ASSERTION_INDEX);
            }

            return new(optCreateJtrueAssertions(op1, op2, equals));
        }
        else if (!optLocalAssertionProp)
        {
            assert(vnStore is not null);
            var vn1 = optConservativeNormalVN(op1);
            var vn2 = optConservativeNormalVN(op2);
            if (vnStore.IsVNCheckedBound(vn1) && vnStore.IsVNInt32Constant(vn2))
            {
                assert(relop.Oper is GT_EQ or GT_NE);
                return new(optCreateJtrueAssertions(op1, op2, equals));
            }
        }

        if (((op1.Oper is not GT_IND) || (op1.AsUnOp().Op1.Oper is not GT_LCL_VAR)) &&
            (op2.Oper is GT_IND) && (op2.AsUnOp().Op1.Oper is GT_LCL_VAR))
        {
            (op1, op2) = (op2, op1);
        }

        if ((op1.Oper is GT_IND) && (op1.AsUnOp().Op1.Oper is GT_LCL_VAR))
        {
            return new(optCreateJtrueAssertions(op1, op2, equals));
        }

        if ((op2.Oper is not GT_CNS_INT) && (op1.Oper is GT_CNS_INT))
        {
            (op1, op2) = (op2, op1);
        }

        if ((op1.Oper is not GT_CALL) || !op1.AsCall().IsHelperCall() || (op1.Type is not TYP_REF) ||
            (op2.Oper is not GT_CNS_INT) || (op2.AsIntCon().IconVal != 0) || optLocalAssertionProp)
        {
            return new(NO_ASSERTION_INDEX);
        }

        var call = op1.AsCall();
        // ReadyToRun helpers do not expose the tested class; CASTCLASS throws instead of returning null.
        if (call.HelperNum is CORINFO_HELP_ISINSTANCEOFINTERFACE or CORINFO_HELP_ISINSTANCEOFARRAY or
            CORINFO_HELP_ISINSTANCEOFCLASS or CORINFO_HELP_ISINSTANCEOFANY)
        {
            var objectArg = call.Args.GetUserArgByIndex(1);
            var typeArg = call.Args.GetUserArgByIndex(0);
            assert((objectArg is not null) && (typeArg is not null));
            var objectNode = objectArg.Node;
            var typeNode = typeArg.Node;
            assert((objectNode.Type is TYP_REF or TYP_I_IMPL) && (typeNode.Type is TYP_I_IMPL));
            var objVN = optConservativeNormalVN(objectNode);
            var typeVN = optConservativeNormalVN(typeNode);
            assert(vnStore is not null);
            if ((objVN != ValueNumStore.NoVN) && vnStore.IsVNTypeHandle(typeVN))
            {
                var index = optAddAssertion(AssertionDsc.CreateSubtype(this, objVN, typeVN, exact: false));
                return relop.Oper is GT_EQ ? AssertionInfo.ForNextEdge(index) : new(index);
            }
        }

        return new(NO_ASSERTION_INDEX);
    }
}
