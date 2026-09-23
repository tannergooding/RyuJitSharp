// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void optAssertionGen(GenTree tree)
    {
        tree.ClearAssertion();
        if (optLocalAssertionProp && ((tree.Flags & GTF_COLON_COND) != 0))
        {
            return;
        }

#if DEBUG
        optAssertionPropCurrentTree = tree;
#endif
        AssertionInfo assertionInfo = default;
        switch (tree.Oper)
        {
            case GT_STORE_LCL_VAR:
            {
                // VN takes care of non-local assertions for data flow.
                if (optLocalAssertionProp)
                {
                    assertionInfo = new(optCreateAssertion(tree, tree.AsLclVar().Data, equals: true));
                }

                break;
            }

            case GT_LCL_VAR:
            {
                if (!optLocalAssertionProp && (tree.Type == TYP_INT) && lvaGetDesc(tree.AsLclVarCommon().LclNum).IsNeverNegative)
                {
                    var vn = optConservativeNormalVN(tree);
                    if (vn != ValueNumStore.NoVN)
                    {
                        assert(vnStore is not null);
                        assertionInfo = new(optAddAssertion(AssertionDsc.CreateConstantBound(this, VNF_GE, vn, vnStore.VNZeroForType(TYP_INT))));
                    }
                }

                break;
            }

            case GT_IND:
            case GT_XAND:
            case GT_XORR:
            case GT_XADD:
            case GT_XCHG:
            case GT_CMPXCHG:
            case GT_BLK:
            case GT_STOREIND:
            case GT_STORE_BLK:
            case GT_NULLCHECK:
            case GT_ARR_LENGTH:
            case GT_MDARR_LENGTH:
            case GT_MDARR_LOWER_BOUND:
            {
                if (tree.IndirMayFault(this))
                {
                    assertionInfo = new(optCreateAssertion(tree.IndirOrArrMetaDataAddr, null, equals: false));
                }
                else if ((tree.Oper == GT_IND) && (tree.Type == TYP_INT) && IntegralRange.ForNode(tree, this).IsNonNegative)
                {
                    var vn = optConservativeNormalVN(tree);
                    if (vn != ValueNumStore.NoVN)
                    {
                        assert(vnStore is not null);
                        assertionInfo = new(optAddAssertion(AssertionDsc.CreateConstantBound(this, VNF_GE, vn, vnStore.VNZeroForType(TYP_INT))));
                    }
                }

                break;
            }

            case GT_INTRINSIC:
            {
                if (tree.AsIntrinsic().IntrinsicName == NI_System_Object_GetType)
                {
                    assertionInfo = new(optCreateAssertion(tree.AsIntrinsic().Op1, null, equals: false));
                }

                break;
            }

            case GT_BOUNDS_CHECK:
            {
                if (!optLocalAssertionProp)
                {
                    var check = tree.AsBoundsChk();
                    var indexVN = optConservativeNormalVN(check.Index);
                    var lengthVN = optConservativeNormalVN(check.ArrayLength);
                    if ((indexVN != ValueNumStore.NoVN) && (lengthVN != ValueNumStore.NoVN))
                    {
                        // A successful bounds check establishes index < length and a nonnegative length.
                        assertionInfo = new(optAddAssertion(AssertionDsc.CreateNoThrowArrBnd(this, indexVN, lengthVN)));
                    }
                }

                break;
            }

            case GT_ARR_ELEM:
            {
                assertionInfo = new(optCreateAssertion(tree.AsArrElem().ArrObj, null, equals: false));
                break;
            }

            case GT_CALL:
            {
                var call = tree.AsCall();
                // Tail calls carry 'this' in the regular argument list and already perform an implicit null check.
                if (call.NeedsNullCheck || (call.IsVirtual && !call.IsTailCall))
                {
                    var thisArg = call.Args.ThisArg;
                    assert(thisArg is not null);
                    assertionInfo = new(optCreateAssertion(thisArg.Node, null, equals: false));
                }
                else if (!optLocalAssertionProp)
                {
                    assert(vnStore is not null);
                    var length = getArrayLengthFromAllocation(call);
                    if (length is not null)
                    {
                        var lengthVN = vnStore.VNIgnoreIntToLongCast(optConservativeNormalVN(length));
                        if ((lengthVN != ValueNumStore.NoVN) && !vnStore.IsVNConstant(lengthVN) && (vnStore.TypeOfVN(lengthVN) == TYP_INT))
                        {
                            assertionInfo = new(optAddAssertion(AssertionDsc.CreateConstantBound(this, VNF_GE, lengthVN, vnStore.VNZeroForType(TYP_INT))));
                            break;
                        }
                    }

                    if (call.HelperNum is CORINFO_HELP_ARRADDR_ST or CORINFO_HELP_LDELEMA_REF)
                    {
                        assert(call.Args.CountUserArgs() == 3);
                        var array = call.Args.GetUserArgByIndex(0);
                        var index = call.Args.GetUserArgByIndex(1);
                        assert((array is not null) && (index is not null));
                        var indexVN = vnStore.VNIgnoreIntToLongCast(optConservativeNormalVN(index.Node));
                        if ((indexVN != ValueNumStore.NoVN) && (vnStore.TypeOfVN(indexVN) == TYP_INT))
                        {
                            var arrayVN = optConservativeNormalVN(array.Node);
                            if (arrayVN != ValueNumStore.NoVN)
                            {
                                var lengthVN = vnStore.VNForFunc(TYP_INT, VNF_ARR_LENGTH, arrayVN);
                                assertionInfo = new(optAddAssertion(AssertionDsc.CreateNoThrowArrBnd(this, indexVN, lengthVN)));
                            }
                        }
                    }
                }

                break;
            }

            case GT_DIV:
            case GT_UDIV:
            case GT_MOD:
            case GT_UMOD:
            {
                if (!optLocalAssertionProp)
                {
                    var divisorVN = optConservativeNormalVN(tree.AsOp().Op2);
                    assert(vnStore is not null);
                    if ((divisorVN != ValueNumStore.NoVN) && !vnStore.IsVNConstant(divisorVN))
                    {
                        var divisorType = vnStore.TypeOfVN(divisorVN);
                        if (varTypeIsIntegral(divisorType))
                        {
                            assertionInfo = new(optAddAssertion(AssertionDsc.CreateConstantBound(this, VNF_NE, divisorVN,
                                vnStore.VNZeroForType(divisorType))));
                        }
                    }
                }

                break;
            }

            case GT_JTRUE:
            {
                assertionInfo = optAssertionGenJtrue(tree);
                break;
            }
        }

        if (assertionInfo.HasAssertion)
        {
            tree.AssertionInfo = assertionInfo;
        }
    }
}
