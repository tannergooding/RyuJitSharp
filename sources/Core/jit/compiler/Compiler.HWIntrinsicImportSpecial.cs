// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.Intrinsics.X86;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_XARCH
    private unsafe GenTree? impSpecialIntrinsic(
        NamedIntrinsic intrinsic,
        CORINFO_CLASS_HANDLE clsHnd,
        CORINFO_METHOD_HANDLE method,
        in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType,
        var_types retType,
        byte simdSize,
        bool mustExpand)
    {
        var isa = HWIntrinsicInfo.lookupIsa(intrinsic);
        if (isa is InstructionSet_Vector)
        {
            return impXplatIntrinsic(intrinsic, clsHnd, method, in sig, in entryPoint,
                simdBaseType, retType, simdSize, mustExpand);
        }

        GenTree? retNode = null;
        GenTree op1;
        GenTree op2;
        GenTree op3;
        GenTree op4;

        if (simdSize != 0)
        {
            assert(varTypeIsArithmetic(simdBaseType));
        }

        switch (intrinsic)
        {
            case NI_AVX2_AndNot:
            {
                if (varTypeIsSimd(retType))
                {
                    intrinsic = NI_AVX2_AndNotVector;
                    simdSize = checked((byte)HWIntrinsicInfo.lookupSimdSize(this, intrinsic, in sig));
                    compFloatingPointUsed = true;
                }
                else
                {
                    intrinsic = NI_AVX2_AndNotScalar;
                }
                goto case NI_X86Base_AndNot;
            }

            case NI_X86Base_AndNot:
            case NI_AVX_AndNot:
            case NI_AVX2_X64_AndNot:
            case NI_AVX512_AndNot:
            {
                assert(sig.numArgs == 2);
                if (simdSize != 0)
                {
                    // The API computes ~op1 & op2; form separate NOT and AND nodes
                    // before LIR so earlier optimizations still see both operations.
                    op2 = impSIMDPopStack();
                    op1 = impSIMDPopStack();
                    op1 = gtFoldExpr(gtNewSimdUnOpNode(GT_NOT, retType, op1, simdBaseType, simdSize));
                    retNode = gtNewSimdBinOpNode(GT_AND, retType, op1, op2, simdBaseType, simdSize);
                }
                else
                {
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    op1 = gtFoldExpr(gtNewUnaryNode(GT_NOT, retType, op1));
                    retNode = gtNewBinaryNode(GT_AND, retType, op1, op2);
                }
                break;
            }

            case NI_AVX512_MoveMask:
            {
                assert(sig.numArgs == 1);
                op1 = impSIMDPopStack();
                op1 = gtFoldExpr(gtNewSimdCvtVectorToMaskNode(TYP_MASK, op1, simdBaseType, simdSize));
                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1);
                break;
            }

            case NI_X86Base_LoadVector128:
            case NI_AVX_LoadVector256:
            case NI_AVX512_LoadVector512:
            {
                assert(sig.numArgs == 1);
                op1 = StripByrefCast(impPopStack().val);
                retNode = gtNewSimdLoadNode(retType, op1, simdBaseType, simdSize);
                break;
            }

            case NI_X86Base_Store:
            case NI_AVX_Store:
            case NI_AVX512_Store:
            {
                assert(retType is TYP_VOID && sig.numArgs == 2);
                op2 = impSIMDPopStack();
                op1 = StripByrefCast(impPopStack().val);
                retNode = gtNewSimdStoreNode(op1, op2, simdBaseType, simdSize);
                break;
            }

            case NI_X86Base_Pause:
            case NI_X86Serialize_Serialize:
            case NI_X86Base_StoreFence:
            case NI_X86Base_LoadFence:
            case NI_X86Base_MemoryFence:
            {
                assert(sig.numArgs == 0 && sig.retType.VarType is TYP_VOID);
                if (intrinsic is not NI_X86Base_StoreFence)
                {
                    assert(simdSize == 0);
                }
                retNode = gtNewScalarHWIntrinsicNode(TYP_VOID, intrinsic);
                break;
            }

            case NI_X86Base_DivRem:
            case NI_X86Base_X64_DivRem:
            {
                assert(sig.numArgs == 3);
                assert(HWIntrinsicInfo.IsMultiReg(intrinsic));
                assert(retType is TYP_STRUCT && simdBaseType is not TYP_UNDEF);
                op3 = impPopStack().val;
                op2 = impPopStack().val;
                op1 = impPopStack().val;
                var node = gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2, op3);
                node.SimdBaseType = simdBaseType;
                retNode = impStoreMultiRegValueToVar(node, sig.retTypeSigClass, CorInfoCallConvExtension.Managed);
                break;
            }

            case NI_X86Base_X64_BigMul:
            {
                assert(sig.numArgs == 2);
                assert(HWIntrinsicInfo.IsMultiReg(intrinsic));
                assert(retType is TYP_STRUCT && simdBaseType is not TYP_UNDEF);
                op2 = impPopStack().val;
                op1 = impPopStack().val;
                var node = gtNewScalarHWIntrinsicNode(retType, intrinsic, op1, op2);
                node.SimdBaseType = simdBaseType;
                retNode = impStoreMultiRegValueToVar(node, sig.retTypeSigClass, CorInfoCallConvExtension.Managed);
                break;
            }

            case NI_X86Base_CompareScalarGreaterThan:
            case NI_X86Base_CompareScalarGreaterThanOrEqual:
            case NI_X86Base_CompareScalarNotGreaterThan:
            case NI_X86Base_CompareScalarNotGreaterThanOrEqual:
            {
                assert(sig.numArgs == 2);
                var supportsAvx = compOpportunisticallyDependsOn(InstructionSet_AVX);
                if (!supportsAvx)
                {
                    impSpillSideEffect(true, stackState.esStackDepth - 2,
                        "Spilling op1 side effects for HWIntrinsic");
                }
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                assert(varTypeIsFloating(simdBaseType));

                if (supportsAvx)
                {
                    var immediate = HWIntrinsicInfo.lookupIval(this, intrinsic, simdBaseType);
                    retNode = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX_CompareScalar,
                        simdBaseType, simdSize, op1, op2, gtNewIconNode(TYP_INT, immediate));
                }
                else
                {
                    op1 = impCloneExpr(op1, out var clonedOp1, CHECK_SPILL_ALL,
                        "Clone op1 for CompareScalarGreaterThan");
                    intrinsic = intrinsic switch {
                        NI_X86Base_CompareScalarGreaterThan => NI_X86Base_CompareScalarLessThan,
                        NI_X86Base_CompareScalarGreaterThanOrEqual => NI_X86Base_CompareScalarLessThanOrEqual,
                        NI_X86Base_CompareScalarNotGreaterThan => NI_X86Base_CompareScalarNotLessThan,
                        NI_X86Base_CompareScalarNotGreaterThanOrEqual => NI_X86Base_CompareScalarNotLessThanOrEqual,
                        _ => throw new FatalJitException("Unexpected scalar comparison."),
                    };
                    var comparison = gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsic,
                        simdBaseType, simdSize, op2, op1);
                    retNode = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_MoveScalar,
                        simdBaseType, simdSize, clonedOp1, comparison);
                }
                break;
            }

            case NI_X86Base_Prefetch0:
            case NI_X86Base_Prefetch1:
            case NI_X86Base_Prefetch2:
            case NI_X86Base_PrefetchNonTemporal:
            {
                assert(sig.numArgs == 1 && sig.retType.VarType is TYP_VOID);
                op1 = impPopStack().val;
                retNode = gtNewSimdHWIntrinsicNode(TYP_VOID, intrinsic, TYP_UBYTE, 0, op1);
                break;
            }

            case NI_X86Base_StoreNonTemporal:
            {
                assert(sig.numArgs == 2 && sig.retType.VarType is TYP_VOID);
                fixed (CORINFO_SIG_INFO* signature = &sig)
                {
                    var arg = info.compCompHnd->getArgNext(sig.args);
                    CORINFO_CLASS_HANDLE argClass;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg, &argClass)).PreciseVarType;
                    op2 = impPopStack().val;
                    op1 = impPopStack().val;
                    retNode = gtNewSimdHWIntrinsicNode(TYP_VOID, NI_X86Base_StoreNonTemporal,
                        argType, 0, op1, op2);
                }
                break;
            }

            case NI_AVX2_PermuteVar8x32:
            case NI_AVX512_PermuteVar4x64:
            case NI_AVX512_PermuteVar8x16:
            case NI_AVX512_PermuteVar8x64:
            case NI_AVX512_PermuteVar16x16:
            case NI_AVX512_PermuteVar16x32:
            case NI_AVX512_PermuteVar32x16:
            case NI_AVX512v2_PermuteVar16x8:
            case NI_AVX512v2_PermuteVar32x8:
            case NI_AVX512v2_PermuteVar64x8:
            {
                simdBaseType = getBaseTypeOfSimdType(sig.retTypeSigClass);
                impSpillSideEffect(true, stackState.esStackDepth - 2,
                    "Spilling op1 side effects for HWIntrinsic");
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                retNode = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op2, op1);
                break;
            }

            case NI_AVX512_Fixup:
            case NI_AVX512_FixupScalar:
            {
                assert(sig.numArgs == 4);
                op4 = impPopStack().val;
                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                var fixup = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize,
                    op1, op2, op3, op4);
                if (!fixup.IsRmwHWIntrinsic(this) && !op1.IsVectorZero)
                {
                    var zero = gtNewZeroConNode(retType);
                    op1 = (op1.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0
                        ? gtNewBinaryNode(GT_COMMA, retType, op1, zero) : zero;
                    fixup.SetOp(1, op1);
                }
                retNode = fixup;
                break;
            }

            case NI_AVX512_TernaryLogic:
            {
                assert(sig.numArgs == 4);
                op4 = impPopStack().val;
                retNode = impSpecialTernaryLogic(retType, simdBaseType, simdSize, op4);
                break;
            }

            default:
            {
                retNode = impSpecialIntrinsicRemaining(ref intrinsic, in sig,
                    ref simdBaseType, ref retType, simdSize);
                break;
            }
        }

        if (retType is TYP_MASK)
        {
            retType = GetSimdTypeForSize(simdSize);
            assert(retType == GetSimdTypeForSize(GetSimdTypeSizeInBytes(sig.retTypeSigClass)));
            assert(retNode is not null);
            retNode = gtNewSimdCvtMaskToVectorNode(retType, gtFoldExpr(retNode), simdBaseType, simdSize);
        }
        return retNode;
    }

    private static GenTree StripByrefCast(GenTree node)
    {
        if ((node.Oper is GT_CAST) && (node.AsCast().CastOp.Type is TYP_BYREF))
        {
            return node.AsCast().CastOp;
        }
        return node;
    }

    private unsafe GenTreeHWIntrinsic? impSpecialIntrinsicRemaining(ref NamedIntrinsic intrinsic,
        in CORINFO_SIG_INFO sig, ref var_types simdBaseType, ref var_types retType, byte simdSize)
    {
        GenTree op1;
        GenTree op2;
        GenTree op3;
        GenTree op4;

        switch (intrinsic)
        {
            case NI_X86Base_BlendVariable:
            case NI_AVX_BlendVariable:
            case NI_AVX2_BlendVariable:
            case NI_AVX512_BlendVariable:
            {
                assert(sig.numArgs == 3);
                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                if ((simdSize == 64) || canUseEvexEncoding())
                {
                    if ((intrinsic is not NI_AVX512_BlendVariable) && varTypeIsIntegral(simdBaseType))
                    {
                        simdBaseType = varTypeIsSigned(simdBaseType) ? TYP_BYTE : TYP_UBYTE;
                    }
                    intrinsic = NI_AVX512_BlendVariableMask;
                    op3 = gtFoldExpr(gtNewSimdCvtVectorToMaskNode(TYP_MASK, op3, simdBaseType, simdSize));
                }
                return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
            }

            case NI_AVX512_Classify:
            case NI_AVX512_ClassifyScalar:
            {
                assert(sig.numArgs == 2);
                intrinsic = intrinsic is NI_AVX512_Classify
                    ? NI_AVX512_ClassifyMask : NI_AVX512_ClassifyScalarMask;
                retType = TYP_MASK;
                op2 = impPopStack().val;
                op1 = impSIMDPopStack();
                return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
            }

            case NI_AVX_Compare:
            case NI_AVX512_Compare:
            {
                if ((simdSize == 64) || canUseEvexEncoding())
                {
                    intrinsic = NI_AVX512_CompareMask;
                    retType = TYP_MASK;
                }
                goto case NI_AVX_CompareScalar;
            }

            case NI_AVX_CompareScalar:
            {
                assert(sig.numArgs == 3);
                var upperBound = HWIntrinsicInfo.lookupImmUpperBound(intrinsic);
                op3 = impPopStack().val;
                op3 = addRangeCheckIfNeeded(intrinsic, op3, 0, upperBound);
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                if (op3.Oper.IsCnsIntOrI)
                {
                    var mode = (FloatComparisonMode)op3.AsIntConCommon().IntegralValue;
                    var id = HWIntrinsicInfo.lookupIdForFloatComparisonMode(
                        intrinsic, mode, simdBaseType, simdSize);
                    if (id != intrinsic)
                    {
                        intrinsic = id;
                        return gtNewSimdHWIntrinsicNode(retType, intrinsic,
                            simdBaseType, simdSize, op1, op2);
                    }
                }
                return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
            }

            case NI_X86Base_CompareEqual:
            case NI_AVX_CompareEqual:
            case NI_AVX2_CompareEqual:
            case NI_AVX512_CompareEqual:
            case NI_X86Base_CompareGreaterThan:
            case NI_AVX_CompareGreaterThan:
            case NI_AVX2_CompareGreaterThan:
            case NI_AVX512_CompareGreaterThan:
            case NI_X86Base_CompareGreaterThanOrEqual:
            case NI_AVX_CompareGreaterThanOrEqual:
            case NI_AVX512_CompareGreaterThanOrEqual:
            case NI_X86Base_CompareLessThan:
            case NI_AVX_CompareLessThan:
            case NI_AVX2_CompareLessThan:
            case NI_AVX512_CompareLessThan:
            case NI_X86Base_CompareLessThanOrEqual:
            case NI_AVX_CompareLessThanOrEqual:
            case NI_AVX512_CompareLessThanOrEqual:
            case NI_X86Base_CompareNotEqual:
            case NI_AVX_CompareNotEqual:
            case NI_AVX512_CompareNotEqual:
            case NI_X86Base_CompareNotGreaterThan:
            case NI_AVX_CompareNotGreaterThan:
            case NI_AVX512_CompareNotGreaterThan:
            case NI_X86Base_CompareNotGreaterThanOrEqual:
            case NI_AVX_CompareNotGreaterThanOrEqual:
            case NI_AVX512_CompareNotGreaterThanOrEqual:
            case NI_X86Base_CompareNotLessThan:
            case NI_AVX_CompareNotLessThan:
            case NI_AVX512_CompareNotLessThan:
            case NI_X86Base_CompareNotLessThanOrEqual:
            case NI_AVX_CompareNotLessThanOrEqual:
            case NI_AVX512_CompareNotLessThanOrEqual:
            case NI_X86Base_CompareOrdered:
            case NI_AVX_CompareOrdered:
            case NI_AVX512_CompareOrdered:
            case NI_X86Base_CompareUnordered:
            case NI_AVX_CompareUnordered:
            case NI_AVX512_CompareUnordered:
            {
                assert(sig.numArgs == 2);
                if ((simdSize == 64) || canUseEvexEncoding())
                {
                    intrinsic = intrinsic switch {
                        NI_X86Base_CompareEqual or NI_AVX_CompareEqual or
                            NI_AVX2_CompareEqual or NI_AVX512_CompareEqual => NI_AVX512_CompareEqualMask,
                        NI_X86Base_CompareGreaterThan or NI_AVX_CompareGreaterThan or
                            NI_AVX2_CompareGreaterThan or NI_AVX512_CompareGreaterThan => NI_AVX512_CompareGreaterThanMask,
                        NI_X86Base_CompareGreaterThanOrEqual or NI_AVX_CompareGreaterThanOrEqual or
                            NI_AVX512_CompareGreaterThanOrEqual => NI_AVX512_CompareGreaterThanOrEqualMask,
                        NI_X86Base_CompareLessThan or NI_AVX_CompareLessThan or
                            NI_AVX2_CompareLessThan or NI_AVX512_CompareLessThan => NI_AVX512_CompareLessThanMask,
                        NI_X86Base_CompareLessThanOrEqual or NI_AVX_CompareLessThanOrEqual or
                            NI_AVX512_CompareLessThanOrEqual => NI_AVX512_CompareLessThanOrEqualMask,
                        NI_X86Base_CompareNotEqual or NI_AVX_CompareNotEqual or
                            NI_AVX512_CompareNotEqual => NI_AVX512_CompareNotEqualMask,
                        NI_X86Base_CompareNotGreaterThan or NI_AVX_CompareNotGreaterThan or
                            NI_AVX512_CompareNotGreaterThan => NI_AVX512_CompareNotGreaterThanMask,
                        NI_X86Base_CompareNotGreaterThanOrEqual or NI_AVX_CompareNotGreaterThanOrEqual or
                            NI_AVX512_CompareNotGreaterThanOrEqual => NI_AVX512_CompareNotGreaterThanOrEqualMask,
                        NI_X86Base_CompareNotLessThan or NI_AVX_CompareNotLessThan or
                            NI_AVX512_CompareNotLessThan => NI_AVX512_CompareNotLessThanMask,
                        NI_X86Base_CompareNotLessThanOrEqual or NI_AVX_CompareNotLessThanOrEqual or
                            NI_AVX512_CompareNotLessThanOrEqual => NI_AVX512_CompareNotLessThanOrEqualMask,
                        NI_X86Base_CompareOrdered or NI_AVX_CompareOrdered or
                            NI_AVX512_CompareOrdered => NI_AVX512_CompareOrderedMask,
                        NI_X86Base_CompareUnordered or NI_AVX_CompareUnordered or
                            NI_AVX512_CompareUnordered => NI_AVX512_CompareUnorderedMask,
                        _ => throw new FatalJitException("Unexpected special comparison."),
                    };
                    retType = TYP_MASK;
                }
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2);
            }

            case NI_AVX512_Compress:
            case NI_AVX512v3_Compress:
            case NI_AVX512_Expand:
            case NI_AVX512v3_Expand:
            {
                assert(sig.numArgs == 3);
                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();
                intrinsic = intrinsic is NI_AVX512_Compress or NI_AVX512v3_Compress
                    ? NI_AVX512_CompressMask : NI_AVX512_ExpandMask;
                op2 = gtNewSimdCvtVectorToMaskNode(TYP_MASK, op2, simdBaseType, simdSize);
                return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
            }

            case NI_AVX512_CompressStore:
            case NI_AVX512v3_CompressStore:
            case NI_AVX512_ExpandLoad:
            case NI_AVX512v3_ExpandLoad:
            case NI_AVX512_MaskLoad:
            case NI_AVX512_MaskLoadAligned:
            case NI_AVX512_MaskStore:
            case NI_AVX512_MaskStoreAligned:
            {
                assert(sig.numArgs == 3);
                op3 = impSIMDPopStack();
                op2 = impSIMDPopStack();
                op1 = StripByrefCast(impPopStack().val);
                intrinsic = intrinsic switch {
                    NI_AVX512_CompressStore or NI_AVX512v3_CompressStore => NI_AVX512_CompressStoreMask,
                    NI_AVX512_ExpandLoad or NI_AVX512v3_ExpandLoad => NI_AVX512_ExpandLoadMask,
                    NI_AVX512_MaskLoad => NI_AVX512_MaskLoadMask,
                    NI_AVX512_MaskLoadAligned => NI_AVX512_MaskLoadAlignedMask,
                    NI_AVX512_MaskStore => NI_AVX512_MaskStoreMask,
                    NI_AVX512_MaskStoreAligned => NI_AVX512_MaskStoreAlignedMask,
                    _ => throw new FatalJitException("Unexpected masked memory intrinsic."),
                };
                op2 = gtNewSimdCvtVectorToMaskNode(TYP_MASK, op2, simdBaseType, simdSize);
                return gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
            }

            case NI_AVXVNNIINT_MultiplyWideningAndAdd:
            case NI_AVXVNNIINT_MultiplyWideningAndAddSaturate:
            case NI_AVXVNNIINT_V512_MultiplyWideningAndAdd:
            case NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate:
            {
                assert(sig.numArgs == 3);
                fixed (CORINFO_SIG_INFO* signature = &sig)
                {
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    CORINFO_CLASS_HANDLE argClass;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    var op3BaseType = getBaseTypeOfSimdType(argClass);
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    node.AuxiliaryType = op3BaseType;
                    return node;
                }
            }

            case NI_AVX2_GatherMaskVector128:
            case NI_AVX2_GatherMaskVector256:
            {
                assert(sig.numArgs == 5);
                simdBaseType = getBaseTypeAndSizeOfSimdType(sig.retTypeSigClass, out var sizeBytes);
                retType = GetSimdTypeForSize(sizeBytes);
                fixed (CORINFO_SIG_INFO* signature = &sig)
                {
                    var arg1 = sig.args;
                    var arg2 = info.compCompHnd->getArgNext(arg1);
                    var arg3 = info.compCompHnd->getArgNext(arg2);
                    var arg4 = info.compCompHnd->getArgNext(arg3);
                    var arg5 = info.compCompHnd->getArgNext(arg4);
                    CORINFO_CLASS_HANDLE argClass;
                    var argType = strip(info.compCompHnd->getArgType(signature, arg5, &argClass)).VarType;
                    var op5 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg4, &argClass)).VarType;
                    op4 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg3, &argClass)).VarType;
                    var indexBaseType = getBaseTypeOfSimdType(argClass);
                    op3 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg2, &argClass)).VarType;
                    op2 = getArgForHWIntrinsic(argType, argClass);
                    argType = strip(info.compCompHnd->getArgType(signature, arg1, &argClass)).VarType;
                    op1 = getArgForHWIntrinsic(argType, argClass);
                    var node = gtNewSimdHWIntrinsicNode(retType, intrinsic, simdBaseType, simdSize,
                        op1, op2, op3, op4, op5);
                    node.AuxiliaryType = indexBaseType;
                    return node;
                }
            }

            case NI_AVX2_ZeroHighBits:
            case NI_AVX2_X64_ZeroHighBits:
            case NI_AVX2_BitFieldExtract:
            case NI_AVX2_X64_BitFieldExtract:
            {
                if ((intrinsic is NI_AVX2_BitFieldExtract or NI_AVX2_X64_BitFieldExtract) &&
                    (sig.numArgs == 3))
                {
                    return null;
                }
                assert(sig.numArgs == 2);
                impSpillSideEffect(true, stackState.esStackDepth - 2,
                    intrinsic is NI_AVX2_ZeroHighBits or NI_AVX2_X64_ZeroHighBits
                        ? "Spilling op1 for ZeroHighBits" : "Spilling op1 for BitFieldExtract");
                op2 = impPopStack().val;
                op1 = impPopStack().val;
                return gtNewScalarHWIntrinsicNode(retType, intrinsic, op2, op1);
            }

            default:
            {
                return null;
            }
        }
    }

    private GenTree impSpecialTernaryLogic(var_types retType, var_types simdBaseType,
        byte simdSize, GenTree controlNode)
    {
        if (controlNode.Oper.IsIntegralConst)
        {
            var control = unchecked((byte)controlNode.AsIntCon().IconVal);
            var info = TernaryLogicInfo.Lookup(control);
            var useFlags = info.GetAllUseFlags();

            if (useFlags is not TernaryLogicUseFlags.ABC)
            {
                // Normalize unused operands to a single order so downstream morphing
                // only needs to recognize one form of each unary or binary operation.
                assert(info.Oper2 is not (TernaryLogicOperKind.Select or TernaryLogicOperKind.True or
                    TernaryLogicOperKind.False or TernaryLogicOperKind.Cond or TernaryLogicOperKind.Major or
                    TernaryLogicOperKind.Minor));
                assert(info.Oper3 is TernaryLogicOperKind.None &&
                    info.Oper3Use is TernaryLogicUseFlags.None);

                var positions = new[] { 0, 1, 2 };
                var spillOp1 = false;
                var spillOp2 = false;
                var unusedVal1 = false;
                var unusedVal2 = false;
                var unusedVal3 = false;

                switch (useFlags)
                {
                    case TernaryLogicUseFlags.A:
                    {
                        spillOp1 = true;
                        (positions[0], positions[1]) = (positions[1], positions[0]);
                        (positions[1], positions[2]) = (positions[2], positions[1]);
                        unusedVal1 = true;
                        unusedVal2 = true;
                        break;
                    }

                    case TernaryLogicUseFlags.B:
                    {
                        spillOp1 = true;
                        spillOp2 = true;
                        (positions[1], positions[2]) = (positions[2], positions[1]);
                        unusedVal1 = true;
                        unusedVal2 = true;
                        break;
                    }

                    case TernaryLogicUseFlags.C:
                    {
                        unusedVal1 = true;
                        unusedVal2 = true;
                        break;
                    }

                    case TernaryLogicUseFlags.AB:
                    {
                        spillOp1 = true;
                        spillOp2 = true;
                        (positions[0], positions[2]) = (positions[2], positions[0]);
                        (positions[1], positions[2]) = (positions[2], positions[1]);
                        unusedVal1 = true;
                        break;
                    }

                    case TernaryLogicUseFlags.AC:
                    {
                        spillOp1 = true;
                        (positions[0], positions[1]) = (positions[1], positions[0]);
                        unusedVal1 = true;
                        break;
                    }

                    case TernaryLogicUseFlags.BC:
                    {
                        unusedVal1 = true;
                        break;
                    }

                    case TernaryLogicUseFlags.None:
                    {
                        unusedVal1 = true;
                        unusedVal2 = true;
                        unusedVal3 = true;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                if (info.Oper1 is TernaryLogicOperKind.Not)
                {
                    assert(info.Oper1Use is not TernaryLogicUseFlags.None);
                    var additionalSpill = false;
                    if (info.Oper2 is TernaryLogicOperKind.And)
                    {
                        assert(info.Oper2Use is not TernaryLogicUseFlags.None);
                        if ((control == unchecked((byte)(~0xCC & 0xF0))) ||
                            (control == unchecked((byte)(~0xAA & 0xF0))) ||
                            (control == unchecked((byte)(~0xAA & 0xCC))))
                        {
                            (positions[1], positions[2]) = (positions[2], positions[1]);
                            additionalSpill = control == unchecked((byte)(~0xAA & 0xCC));
                        }
                    }
                    else if (info.Oper2 is TernaryLogicOperKind.Or)
                    {
                        assert(info.Oper2Use is not TernaryLogicUseFlags.None);
                        if ((control == unchecked((byte)(~0xCC | 0xF0))) ||
                            (control == unchecked((byte)(~0xAA | 0xF0))) ||
                            (control == unchecked((byte)(~0xAA | 0xCC))))
                        {
                            (positions[1], positions[2]) = (positions[2], positions[1]);
                            additionalSpill = control == unchecked((byte)(~0xAA | 0xCC));
                        }
                    }
                    if (additionalSpill)
                    {
                        spillOp1 = true;
                        spillOp2 = true;
                    }
                }

                if (spillOp1)
                {
                    impSpillSideEffect(true, stackState.esStackDepth - 3,
                        "Spilling op1 side effects for HWIntrinsic");
                }
                if (spillOp2)
                {
                    impSpillSideEffect(true, stackState.esStackDepth - 2,
                        "Spilling op2 side effects for HWIntrinsic");
                }

                var op3 = impSIMDPopStack();
                var op2 = impSIMDPopStack();
                var op1 = impSIMDPopStack();
                var operands = new[] { op1, op2, op3 };
                ref var val1 = ref operands[positions[0]];
                ref var val2 = ref operands[positions[1]];
                ref var val3 = ref operands[positions[2]];

                if (unusedVal1 && !val1.IsVectorZero)
                {
                    _ = impAppendTree(gtUnusedValNode(val1), CHECK_SPILL_ALL, impCurStmtDI);
                }
                if (unusedVal2 && !val2.IsVectorZero)
                {
                    _ = impAppendTree(gtUnusedValNode(val2), CHECK_SPILL_ALL, impCurStmtDI);
                }
                if (unusedVal3 && !val3.IsVectorZero)
                {
                    _ = impAppendTree(gtUnusedValNode(val3), CHECK_SPILL_ALL, impCurStmtDI);
                }

                switch (info.Oper1)
                {
                    case TernaryLogicOperKind.Select:
                    {
                        assert(info.Oper1Use is not TernaryLogicUseFlags.None);
                        assert(info.Oper2 is TernaryLogicOperKind.None &&
                            info.Oper2Use is TernaryLogicUseFlags.None);
                        assert(control is 0xF0 or 0xCC or 0xAA);
                        assert(unusedVal1 && unusedVal2 && !unusedVal3);
                        return val3;
                    }

                    case TernaryLogicOperKind.True:
                    {
                        assert(info.Oper1Use is TernaryLogicUseFlags.None);
                        assert(info.Oper2 is TernaryLogicOperKind.None &&
                            info.Oper2Use is TernaryLogicUseFlags.None);
                        assert(control == 0xFF && unusedVal1 && unusedVal2 && unusedVal3);
                        return gtNewAllBitsSetConNode(retType);
                    }

                    case TernaryLogicOperKind.False:
                    {
                        assert(info.Oper1Use is TernaryLogicUseFlags.None);
                        assert(info.Oper2 is TernaryLogicOperKind.None &&
                            info.Oper2Use is TernaryLogicUseFlags.None);
                        assert(control == 0 && unusedVal1 && unusedVal2 && unusedVal3);
                        return gtNewZeroConNode(retType);
                    }

                    case TernaryLogicOperKind.Not:
                    {
                        assert(info.Oper1Use is not TernaryLogicUseFlags.None);
                        if (info.Oper2 is TernaryLogicOperKind.None)
                        {
                            assert(info.Oper2Use is TernaryLogicUseFlags.None);
                            assert(control is 0x0F or 0x33 or 0x55);
                            assert(unusedVal1 && unusedVal2 && !unusedVal3);
                            if (!val1.IsVectorZero)
                            {
                                val1 = gtNewZeroConNode(retType);
                            }
                            if (!val2.IsVectorZero)
                            {
                                val2 = gtNewZeroConNode(retType);
                            }
                            controlNode.AsIntCon().IconVal = unchecked((byte)~0xAA);
                            break;
                        }

                        assert(info.Oper2Use is not TernaryLogicUseFlags.None);
                        if (info.Oper2 is TernaryLogicOperKind.And)
                        {
                            assert(control is 0x30 or 0x50 or 0x44 or 0x0C or 0x0A or 0x22);
                            assert(unusedVal1 && !unusedVal2 && !unusedVal3);
                            return gtNewSimdBinOpNode(GT_AND_NOT, retType, val3, val2,
                                simdBaseType, simdSize);
                        }

                        assert(info.Oper2 is TernaryLogicOperKind.Or);
                        assert(unusedVal1 && !unusedVal2 && !unusedVal3);
                        if (!val1.IsVectorZero)
                        {
                            val1 = gtNewZeroConNode(retType);
                        }
                        controlNode.AsIntCon().IconVal = unchecked((byte)(~0xCC | 0xAA));
                        break;
                    }

                    case TernaryLogicOperKind.And:
                    case TernaryLogicOperKind.Or:
                    case TernaryLogicOperKind.Xor:
                    {
                        assert(info.Oper1Use is not TernaryLogicUseFlags.None);
                        assert(info.Oper2 is TernaryLogicOperKind.None &&
                            info.Oper2Use is TernaryLogicUseFlags.None);
                        assert(unusedVal1 && !unusedVal2 && !unusedVal3);
                        var oper = info.Oper1 switch {
                            TernaryLogicOperKind.And => GT_AND,
                            TernaryLogicOperKind.Or => GT_OR,
                            _ => GT_XOR,
                        };
                        return gtNewSimdBinOpNode(oper, retType, val2, val3, simdBaseType, simdSize);
                    }

                    case TernaryLogicOperKind.Nand:
                    case TernaryLogicOperKind.Nor:
                    case TernaryLogicOperKind.Xnor:
                    {
                        assert(info.Oper1Use is not TernaryLogicUseFlags.None);
                        assert(info.Oper2 is TernaryLogicOperKind.None &&
                            info.Oper2Use is TernaryLogicUseFlags.None);
                        assert(unusedVal1 && !unusedVal2 && !unusedVal3);
                        if (!val1.IsVectorZero)
                        {
                            val1 = gtNewZeroConNode(retType);
                        }
                        controlNode.AsIntCon().IconVal = info.Oper1 switch {
                            TernaryLogicOperKind.Nand => unchecked((byte)~(0xCC & 0xAA)),
                            TernaryLogicOperKind.Nor => unchecked((byte)~(0xCC | 0xAA)),
                            _ => unchecked((byte)~(0xCC ^ 0xAA)),
                        };
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                return gtNewSimdTernaryLogicNode(retType, val1, val2, val3,
                    controlNode, simdBaseType, simdSize);
            }
        }

        var third = impSIMDPopStack();
        var second = impSIMDPopStack();
        var first = impSIMDPopStack();
        return gtNewSimdTernaryLogicNode(retType, first, second, third,
            controlNode, simdBaseType, simdSize);
    }
#endif
}
