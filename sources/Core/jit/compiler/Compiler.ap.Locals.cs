// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    public static int optCopyProp_LclVarScore(in LclVarDsc local, in LclVarDsc copy, bool preferOp2)
    {
        var score = 0;
        if (local.lvHasExceptionalUsesHint)
        {
            score += 4;
        }

        if (copy.lvHasExceptionalUsesHint)
        {
            score -= 4;
        }

#if TARGET_X86
        if (local.Type is TYP_DOUBLE)
        {
            if (local.lvIsParam)
            {
                score += 2;
            }

            if (copy.lvIsParam)
            {
                score -= 2;
            }
        }
#endif

        return score + (preferOp2 ? 1 : -1);
    }

    public GenTree? optCopyAssertionProp(in AssertionDsc assertion, GenTreeLclVarCommon tree, Statement? statement,
        AssertionIndex index = NO_ASSERTION_INDEX)
    {
        assert(optLocalAssertionProp);
        var first = assertion.Op1.LclNum;
        var second = assertion.Op2.LclNum;
        noway_assert(first != second);
        var localNumber = tree.LclNum;
        if ((first != localNumber) && (second != localNumber))
        {
            return null;
        }

        var copyNumber = first == localNumber ? second : first;
        ref var copy = ref lvaGetDesc(copyNumber);
        ref var local = ref lvaGetDesc(localNumber);
        if (!optAssertionProp_LclVarTypeCheck(tree, local, copy) ||
            (optCopyProp_LclVarScore(local, copy, first == localNumber) <= 0))
        {
            return null;
        }

        if (tree.Oper is GT_LCL_FLD)
        {
            if (copy.IsEnregisterableLcl || copy.lvPromoted)
            {
                return null;
            }

            lvaSetVarDoNotEnregister(copyNumber, DoNotEnregisterReason.LocalField);
        }

        if ((tree.Oper is GT_LCL_VAR) && varTypeIsSimd(tree.Type) && copy.lvPromoted && !copy.lvDoNotEnregister)
        {
            return null;
        }

        if (local.lvOnlyUsedOnSynchronousPath || copy.lvOnlyUsedOnSynchronousPath)
        {
            return null;
        }

        tree.LclNum = copyNumber;
        if (local.lvIsMultiRegRet)
        {
            copy.lvIsMultiRegRet = true;
        }

        // Copy propagation can invalidate the last-use decision made during morph.
        tree.Flags &= ~GTF_VAR_DEATH;
#if DEBUG
        if (verbose)
        {
            assert(compCurBB is not null);
            jitprintf($"\nCopy Assertion prop in {FMT_BB(compCurBB.bbNum)}:\n");
            optPrintAssertion(assertion, index);
            DISPNODE(tree);
        }
#endif
        return optAssertionProp_Update(tree, tree, statement);
    }

    public GenTree? optConstantAssertionProp(in AssertionDsc assertion, GenTreeLclVarCommon tree, Statement? statement,
        AssertionIndex index = NO_ASSERTION_INDEX)
    {
        var local = tree.LclNum;
        if (lclNumIsCSE(local))
        {
            if (optLocalAssertionProp)
            {
                return null;
            }

            assert(vnStore is not null);
            if (!vnStore.IsVNCheckedBound(optConservativeNormalVN(tree)))
            {
                return null;
            }
        }

        GenTree replacement;
        var threading = statement is null ? NodeThreading.None : NodeThreading.AllTrees;
        switch (assertion.Op2.Kind)
        {
            case O2K_CONST_DOUBLE:
            {
                // Equality cannot distinguish positive and negative zero.
                if (assertion.Op2.DoubleConstant == 0.0)
                {
                    return null;
                }

                replacement = new GenTreeDblCon(tree.Type, assertion.Op2.DoubleConstant, tree, threading) {
                    _vnPair = new ValueNumPair(),
                };
                break;
            }

#if FEATURE_HW_INTRINSICS
            case O2K_CONST_VEC:
            {
                if (!varTypeIsSimd(tree.Type) || (tree.Type != lvaGetDesc(local).Type))
                {
                    return null;
                }

#if TARGET_ARM64
                if (tree.Type is TYP_SIMD)
                {
                    throw new NotImplementedException("Scalable vector assertion propagation is not yet ported.");
                }
#endif
                assert(tree.Type.Size == assertion.Op2.SimdSize);
                var vector = gtNewVconNode(tree.Type);
                assertion.Op2.SimdConstant.CopyTo(vector.SimdVal.AsSpan<byte>());
                replacement = vector;
                break;
            }
#endif

            case O2K_CONST_INT:
            {
                if (opts.compReloc && assertion.Op2.HasIconFlag && (assertion.Op2.IntConstant != 0) &&
                    (assertion.Op2.IconFlag is not GTF_ICON_STATIC_HDL))
                {
                    return null;
                }

                assert(tree.Type == lvaGetDesc(local).Type);
                if (assertion.Op2.HasIconFlag)
                {
                    replacement = gtNewIconHandleNode(assertion.Op2.IntConstant, assertion.Op2.IconFlag,
                        assertion.Op2.IconFieldSeq);
                    if (!replacement.IsIntegralConst(0) && replacement.AsIntCon().IsIconHandle(GTF_ICON_OBJ_HDL) && (tree.Type is not TYP_REF))
                    {
                        return null;
                    }

                    replacement.Type = tree.Type;
                }
                else
                {
                    assert(varTypeIsIntegralOrI(tree.Type));
                    var type = tree.Type.ActualType;
                    assert((type.Size > sizeof(int)) || FitsInI32(assertion.Op2.IntConstant));
#if TARGET_64BIT
                    replacement = new GenTreeIntCon(type, assertion.Op2.IntConstant, null, tree, threading);
#else
                    replacement = type is TYP_LONG
                        ? new GenTreeLngCon(assertion.Op2.IntConstant, tree, threading)
                        : new GenTreeIntCon(type, assertion.Op2.IntConstant, null, tree, threading);
#endif
                    replacement._vnPair = new ValueNumPair();
                }
                break;
            }

            default:
            {
                return null;
            }
        }

        if (!optLocalAssertionProp)
        {
            assert(vnStore is not null);
            assert(replacement.Oper.IsConst && vnStore.IsVNConstant(assertion.Op2.VN));
            replacement._vnPair.SetBoth(assertion.Op2.VN);
        }

#if DEBUG
        if (verbose)
        {
            assert(compCurBB is not null);
            jitprintf($"\nConstant Assertion prop in {FMT_BB(compCurBB.bbNum)}:\n");
            optPrintAssertion(assertion, index);
            gtDispTree(replacement, topOnly: true);
        }
#endif
        return optAssertionProp_Update(replacement, tree, statement);
    }

    public GenTree? optAssertionProp_LclVar(ASSERT_TP? assertions, GenTreeLclVarCommon tree, Statement? statement)
    {
        if (((tree.Flags & (GTF_VAR_DEF | GTF_DONT_CSE)) != 0) ||
            (!optLocalAssertionProp && varTypeIsStruct(tree.Type) && !varTypeIsSimd(tree.Type)) || !optCanPropLclVar)
        {
            return null;
        }

        var local = tree.LclNum;
        var vn = optConservativeNormalVN(tree);
        assert(apTraits is not null);
        var filtered = assertions;
        if (optLocalAssertionProp)
        {
            filtered = BitVecOps.Intersection(apTraits, GetAssertionDep(local), filtered);
        }
        else if (!optAssertionHasAssertionsForVN(vn))
        {
            return null;
        }

        GenTree? result = null;
        _ = BitVecOps.VisitBits(apTraits, filtered, bitIndex => {
            var index = GetAssertionIndex((ushort)bitIndex);
            if (index > optAssertionCount)
            {
                return false;
            }

            var assertion = optGetAssertion(index);
            if (!assertion.CanPropLclVar || !(assertion.Op2.IsConstant || assertion.Op2.KindIs(O2K_LCLVAR_COPY)))
            {
                return true;
            }

            if (assertion.Op2.KindIs(O2K_LCLVAR_COPY))
            {
                // Global propagation has no copy kill sets.
                if (optLocalAssertionProp)
                {
                    result = optCopyAssertionProp(assertion, tree, statement, index);
                }

                return result is null;
            }

            if ((varTypeIsStruct(tree.Type) && !varTypeIsSimd(tree.Type)) || (tree.Type != lvaGetDesc(local).Type))
            {
                return true;
            }

            if (optLocalAssertionProp ? assertion.Op1.LclNum == local : assertion.Op1.VN == vn)
            {
                result = optConstantAssertionProp(assertion, tree, statement, index);
                return false;
            }

            return true;
        });

        return result;
    }

    public GenTree? optAssertionProp_LclFld(ASSERT_TP? assertions, GenTreeLclVarCommon tree, Statement? statement)
    {
        if (((tree.Flags & (GTF_VAR_DEF | GTF_DONT_CSE)) != 0) || !optLocalAssertionProp || !optCanPropLclVar)
        {
            return null;
        }

        assert(apTraits is not null);
        var filtered = BitVecOps.Intersection(apTraits, GetAssertionDep(tree.LclNum), assertions);
        GenTree? result = null;
        _ = BitVecOps.VisitBits(apTraits, filtered, bitIndex => {
            var index = GetAssertionIndex((ushort)bitIndex);
            if (index > optAssertionCount)
            {
                return false;
            }

            var assertion = optGetAssertion(index);
            if (assertion.CanPropLclVar && assertion.Op2.KindIs(O2K_LCLVAR_COPY))
            {
                result = optCopyAssertionProp(assertion, tree, statement, index);
            }

            return result is null;
        });

        return result;
    }

    public GenTree? optAssertionProp_LocalStore(ASSERT_TP? assertions, GenTreeLclVarCommon store, Statement? statement)
    {
        if (!optLocalAssertionProp)
        {
            return null;
        }

        ref var value = ref store.Op1Ref;
        var changed = (value.Type is TYP_STRUCT) && optZeroObjAssertionProp(ref value, assertions);
        var local = store.LclNum;
        var isStruct = lvaGetDesc(local).Type is TYP_STRUCT;
        assert(assertions is not null);
        var index = optLocalAssertionIsEqualOrNotEqual(O1K_LCLVAR, local, isStruct ? O2K_ZEROOBJ : O2K_CONST_INT, 0, assertions);
        if (index != NO_ASSERTION_INDEX)
        {
            var assertion = optGetAssertion(index);
            if (assertion.KindIs(OAK_EQUAL) && (assertion.Op2.KindIs(O2K_ZEROOBJ) || (assertion.Op2.IntConstant == 0)) &&
                value.IsIntegralConst(0) && (isStruct || varTypeIsGC(store.Type)))
            {
                // Keep integral-local zero stores visible to loop-bound recognition.
#if DEBUG
                JITDUMP($"[{store.TreeId:D6}] is assigning a constant zero to a struct field or gc local that is already zero\n");
                if (verbose)
                {
                    optPrintAssertion(assertion);
                }
#endif
                store.BashToNOP();
                return optAssertionProp_Update(store, store, statement);
            }
        }

        return changed ? optAssertionProp_Update(store, store, statement) : null;
    }

    public GenTree? optAssertionProp_BlockStore(ASSERT_TP? assertions, GenTreeBlk store, Statement? statement)
    {
        assert(store.Oper is GT_STORE_BLK);
        var zeroObject = optZeroObjAssertionProp(ref store.Op2Ref, assertions);
        var nonNull = optNonNullAssertionProp_Ind(assertions, store);
        var writeBarrier = optWriteBarrierAssertionProp_StoreBlk(assertions, store);
        return (zeroObject || nonNull || writeBarrier) ? optAssertionProp_Update(store, store, statement) : null;
    }
}
