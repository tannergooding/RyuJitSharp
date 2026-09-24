// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    // Morph only applies local assertions. Keep the VN/SSA-based global phase
    // separate: its range-analysis dependencies are not needed by this path.
    private GenTree? optLocalAssertionPropTree(ASSERT_TP? assertions, GenTree tree)
    {
        assert(optLocalAssertionProp);
        assert(assertions is not null);
        switch (tree.Oper)
        {
            case GT_LCL_VAR:
            {
                return optAssertionProp_LclVar(assertions, tree.AsLclVarCommon(), null);
            }

            case GT_LCL_FLD:
            {
                return optAssertionProp_LclFld(assertions, tree.AsLclVarCommon(), null);
            }

            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                return optAssertionProp_LocalStore(assertions, tree.AsLclVarCommon(), null);
            }

            case GT_STORE_BLK:
            {
                return optAssertionProp_BlockStore(assertions, tree.AsBlk(), null);
            }

            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
            {
                return optAssertionProp_Return(assertions, tree, null);
            }

            case GT_BLK:
            case GT_IND:
            case GT_STOREIND:
            case GT_NULLCHECK:
            {
                return optAssertionProp_Ind(assertions, tree, null);
            }

            case GT_CAST:
            {
                return optLocalAssertionPropCast(assertions, tree.AsCast());
            }

            case GT_CALL:
            {
                return optAssertionProp_Call(assertions, tree.AsCall(), null);
            }

            case GT_EQ:
            case GT_NE:
            {
                return optAssertionPropLocal_RelOp(assertions, tree, null);
            }

            default:
            {
                // The native dispatcher makes no local-mode changes to the other operators.
                return null;
            }
        }
    }

    private GenTree? optLocalAssertionPropCast(ASSERT_TP? assertions, GenTreeCast cast)
    {
        assert(optLocalAssertionProp);
        assert(assertions is not null);
        var operand = cast.CastOp;
        if (!varTypeIsIntegral(cast.Type) || !varTypeIsIntegral(operand.Type))
        {
            return null;
        }

        var local = operand.EffectiveVal;
        if ((local.Oper is GT_LCL_VAR) &&
            (optAssertionIsSubrange(local, IntegralRange.ForCastInput(cast), assertions) != NO_ASSERTION_INDEX))
        {
            ref var descriptor = ref lvaGetDesc(local.AsLclVarCommon().LclNum);
            if (cast.Type.ActualType != local.Type.ActualType)
            {
                if (!cast.HasOverflowCheck)
                {
                    return null;
                }

#if DEBUG
                JITDUMP($"Clearing overflow flag for cast {cast.TreeId:D6} based on assertions.\n");
#endif
                cast.Flags &= ~GTF_OVERFLOW;
                return optAssertionProp_Update(cast, cast, null);
            }

            if (descriptor.lvNormalizeOnLoad)
            {
                // Preserve the narrow-load contract if this local is later spilled.
                if ((descriptor.Type != cast.CastType) || (local.Type is not TYP_INT))
                {
                    return null;
                }

                operand.ChangeType(descriptor.Type);
            }

#if DEBUG
            JITDUMP($"Removing cast {cast.TreeId:D6} as redundant based on assertions.\n");
#endif
            return optAssertionProp_Update(operand, cast, null);
        }

        return null;
    }

    public GenTree? optAssertionPropLocal_RelOp(ASSERT_TP? assertions, GenTree tree, Statement? statement)
    {
        assert(tree.Oper is GT_EQ or GT_NE);
        assert(assertions is not null);
        var left = tree.AsOp().Op1;
        var right = tree.AsOp().Op2;
        if ((left.Oper is not GT_LCL_VAR) || (right.Oper is not GT_CNS_INT))
        {
            return null;
        }

        var value = right.AsIntCon().IconValue;
        var type = left.Type;
        if (varTypeIsFloating(type))
        {
            return null;
        }

        var localNumber = left.AsLclVarCommon().LclNum;
        if (type != lvaGetDesc(localNumber).Type)
        {
            return null;
        }

        noway_assert(localNumber < lvaCount);
        var index = optLocalAssertionIsEqualOrNotEqual(O1K_LCLVAR, localNumber, O2K_CONST_INT, value, assertions);
        if (index == NO_ASSERTION_INDEX)
        {
            return null;
        }

        var assertion = optGetAssertion(index);
        var assertionIsEqual = assertion.KindIs(OAK_EQUAL);
        bool constantIsEqual;
        if (type.Size == TARGET_POINTER_SIZE)
        {
            constantIsEqual = assertion.Op2.IntConstant == value;
        }
#if TARGET_64BIT
        else if (type.Size == sizeof(int))
        {
            constantIsEqual = unchecked((int)assertion.Op2.IntConstant) == unchecked((int)value);
        }
#endif
        else
        {
            return null;
        }

        noway_assert(constantIsEqual || assertionIsEqual);
#if DEBUG
        if (verbose)
        {
            assert(compCurBB is not null);
            jitprintf($"\nAssertion prop for index #{index:D2} in {FMT_BB(compCurBB.bbNum)}:\n");
            gtDispTree(tree, topOnly: true);
        }
#endif

        var result = constantIsEqual == assertionIsEqual;
        if (tree.Oper is GT_NE)
        {
            result = !result;
        }

        var replacement = new GenTreeIntCon(TYP_INT, result ? 1 : 0, null, right) {
            _vnPair = new ValueNumPair(),
        };
        return optAssertionProp_Update(replacement, tree, statement);
    }
}
