// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LOWER_DECOMPOSE_LONGS
using System;
#if FEATURE_HW_INTRINSICS && TARGET_X86
using System.Runtime.Intrinsics.X86;
#endif

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private sealed partial class DecomposeLongs
    {
        private unsafe GenTree? DecomposeCast(ref LIR.Use use)
        {
            assert(use.IsInitialized());
            assert(use.Def().Oper is GT_CAST);
            var cast = use.Def().AsCast();
            var srcType = cast.CastOp.Type;
            var dstType = cast.CastType;
            if (cast.IsUnsigned)
            {
                srcType = varTypeToUnsigned(srcType);
            }

#if FEATURE_HW_INTRINSICS && TARGET_X86
            if (varTypeIsFloating(srcType) || varTypeIsFloating(dstType))
            {
                assert(!cast.HasOverflowCheck);
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX512));
                var srcOp = cast.CastOp;
                GenTree castResult;
                var castRange = new LIR.Range(null, null);
                var srcVector = _compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD16, srcOp, srcType, 16);
                castRange.InsertAtEnd(srcVector);
                if (srcVector.Oper.IsCnsVec)
                {
                    Range().Remove(srcOp);
                }

                if (varTypeIsFloating(dstType))
                {
                    var intrinsicId = dstType is TYP_FLOAT
                        ? NI_AVX512_ConvertToVector128Single
                        : NI_AVX512_ConvertToVector128Double;
                    castResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsicId, srcType, 16, srcVector);
                }
                else if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX10v2))
                {
                    var intrinsicId = dstType is TYP_ULONG
                        ? NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation
                        : NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation;
                    castResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsicId, srcType, 16, srcVector);
                }
                else if (dstType is TYP_ULONG)
                {
                    // MAX with zero as the second operand also repairs NaN before unsigned conversion.
                    var zero = _compiler.gtNewZeroConNode(TYP_SIMD16);
                    var fixupVal = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_MaxScalar,
                        srcType, 16, srcVector, zero);
                    castRange.InsertAtEnd(zero);
                    castRange.InsertAtEnd(fixupVal);
                    castResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16,
                        NI_AVX512_ConvertToVector128UInt64WithTruncation, srcType, 16, fixupVal);
                }
                else
                {
                    assert(dstType is TYP_LONG);
                    LIR.Use.MakeDummyUse(castRange, srcVector, out var srcUse);
                    _ = srcUse.ReplaceWithLclVar(_compiler);
                    srcVector = srcUse.Def();

                    // EVEX masks repair NaN during conversion and positive overflow afterwards;
                    // the conversion already saturates negative overflow, and 2^63 is exact.
                    var srcClone = _compiler.gtClone(srcVector);
                    assert(srcClone is not null);
                    var compareMode = _compiler.gtNewIconNode(TYP_INT,
                        (int)FloatComparisonMode.OrderedNonSignaling);
                    var nanMask = _compiler.gtNewSimdHWIntrinsicNode(TYP_MASK, NI_AVX512_CompareScalarMask,
                        srcType, 16, srcVector, srcClone, compareMode);
                    castRange.InsertAtEnd(srcClone);
                    castRange.InsertAtEnd(compareMode);
                    castRange.InsertAtEnd(nanMask);

                    compareMode = _compiler.gtNewIconNode(TYP_INT,
                        (int)FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
                    var ovfFloatingValue = _compiler.gtNewVconNode(TYP_SIMD16);
                    ovfFloatingValue.EvaluateBroadcastInPlace(srcType, 9223372036854775808.0);
                    srcClone = _compiler.gtClone(srcVector);
                    assert(srcClone is not null);
                    var ovfMask = _compiler.gtNewSimdHWIntrinsicNode(TYP_MASK, NI_AVX512_CompareScalarMask,
                        srcType, 16, srcClone, ovfFloatingValue, compareMode);
                    castRange.InsertAtEnd(srcClone);
                    castRange.InsertAtEnd(ovfFloatingValue);
                    castRange.InsertAtEnd(compareMode);
                    castRange.InsertAtEnd(ovfMask);

                    var zero = _compiler.gtNewZeroConNode(TYP_SIMD16);
                    srcClone = _compiler.gtClone(srcVector);
                    assert(srcClone is not null);
                    var convert = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16,
                        NI_AVX512_ConvertToVector128Int64WithTruncation, srcType, 16, srcClone);
                    castRange.InsertAtEnd(zero);
                    castRange.InsertAtEnd(srcClone);
                    castRange.InsertAtEnd(convert);

                    var convertMasked = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16,
                        NI_AVX512_BlendVariableMask, dstType, 16, zero, convert, nanMask);
                    var maxLong = _compiler.gtNewVconNode(TYP_SIMD16);
                    maxLong.EvaluateBroadcastInPlace(dstType, long.MaxValue);
                    castRange.InsertAtEnd(convertMasked);
                    castRange.InsertAtEnd(maxLong);
                    castResult = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16,
                        NI_AVX512_BlendVariableMask, dstType, 16, convertMasked, maxLong, ovfMask);
                }

                var toScalar = _compiler.gtNewSimdToScalarNode(dstType.ActualType, castResult, dstType, 16);
                castRange.InsertAtEnd(castResult);
                castRange.InsertAtEnd(toScalar);
                Range().InsertAfter(cast, castRange);
                Range().Remove(cast);
                if (use.IsDummyUse())
                {
                    toScalar.IsUnusedValue = true;
                }

                use.ReplaceWith(toScalar);
                return toScalar;
            }
#endif

            GenTree loResult;
            GenTree hiResult;
            if (varTypeIsLong(srcType))
            {
                if (cast.HasOverflowCheck && (varTypeIsUnsigned(srcType) != varTypeIsUnsigned(dstType)))
                {
                    var srcOp = cast.CastOp.AsOp();
                    noway_assert(srcOp.Oper is GT_LONG);
                    var loSrcOp = srcOp.Op1;
                    var hiSrcOp = srcOp.Op2;
                    // Both signedness changes only need to check whether the high half is negative.
                    loResult = EnsureIntSized(loSrcOp, !cast.IsUnsigned);
                    hiResult = cast;
                    cast.Type = TYP_INT;
                    cast.CastType = TYP_UINT;
                    cast.IsUnsigned = false;
                    cast.Op1 = hiSrcOp;
                    Range().Remove(srcOp);
                }
                else
                {
                    NYI("Unimplemented long->long no-op cast decomposition");
                    throw new NotImplementedException("Unimplemented long->long no-op cast decomposition");
                }
            }
            else if (varTypeIsIntegralOrI(srcType))
            {
                if (cast.HasOverflowCheck && !varTypeIsUnsigned(srcType) && varTypeIsUnsigned(dstType))
                {
                    loResult = cast;
                    cast.CastType = TYP_UINT;
                    cast.Type = TYP_INT;
                    hiResult = _compiler.gtNewZeroConNode(TYP_INT);
                    Range().InsertAfter(loResult, hiResult);
                }
                else if (!use.IsDummyUse() && (use.User().Oper is GT_MUL))
                {
                    // MUL_LONG consumes the int operands directly, so avoid creating dead high halves.
                    assert(use.User().AsOp().Is64RsltMul);
                    return cast.Next;
                }
                else if (varTypeIsUnsigned(srcType))
                {
                    loResult = EnsureIntSized(cast.Op1, !cast.IsUnsigned);
                    hiResult = _compiler.gtNewZeroConNode(TYP_INT);
                    Range().InsertAfter(cast, hiResult);
                    Range().Remove(cast);
                }
                else
                {
                    var src = new LIR.Use(Range(), ref cast.Op1Ref, cast);
                    var lclNum = src.ReplaceWithLclVar(_compiler);
                    loResult = src.Def();
                    var loCopy = _compiler.gtNewLclvNode(TYP_INT, lclNum);
                    var shiftBy = _compiler.gtNewIconNode(TYP_INT, 31);
                    hiResult = _compiler.gtNewBinaryNode(GT_RSH, TYP_INT, loCopy, shiftBy);
                    Range().InsertAfter(cast, loCopy, shiftBy, hiResult);
                    Range().Remove(cast);
                }
            }
            else
            {
                NYI("Unimplemented cast decomposition");
                throw new NotImplementedException("Unimplemented cast decomposition");
            }

            return FinalizeDecomposition(ref use, loResult, hiResult, hiResult);
        }
    }
}
#endif
