// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public GenTree gtNewSimdCvtNode(var_types type, GenTree op1, var_types simdTargetBaseType,
        var_types simdSourceBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdSourceBaseType) && varTypeIsIntegral(simdTargetBaseType));

#if TARGET_XARCH
        assert(compIsaSupportedDebugOnly(InstructionSet_AVX512) || simdTargetBaseType == TYP_INT);
        if (compOpportunisticallyDependsOn(InstructionSet_AVX10v2))
        {
            var intrinsic = simdTargetBaseType switch
            {
                TYP_INT => NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation,
                TYP_UINT => NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation,
                TYP_LONG => NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation,
                TYP_ULONG => NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation,
                _ => throw new FatalJitException("Unsupported SIMD conversion target."),
            };
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdSourceBaseType, simdSize, op1);
        }

        GenTree fixupVal;
        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            // The VFIXUPIMM control table maps NaNs to zero; unsigned conversions also map negative inputs to zero.
            var iconVal = varTypeIsUnsigned(simdTargetBaseType) ? 0x08080088 : 0x00000088;
            var tblCon = gtNewSimdCreateBroadcastNode(type, gtNewIconNode(TYP_INT, iconVal),
                simdTargetBaseType, simdSize);
            var op1Clone = fgMakeMultiUse(ref op1);
            fixupVal = gtNewSimdHWIntrinsicNode(type, NI_AVX512_Fixup, simdSourceBaseType, simdSize,
                op1, op1Clone, tblCon, gtNewIconNode(TYP_INT, 0));
        }
        else
        {
            var op1Clone = fgMakeMultiUse(ref op1);
            var mask = gtNewSimdIsNaNNode(type, op1Clone, simdSourceBaseType, simdSize);
            fixupVal = gtNewSimdBinOpNode(GT_AND_NOT, type, op1, mask, simdSourceBaseType, simdSize);
        }

        if (varTypeIsSigned(simdTargetBaseType))
        {
            GenTree maxVal;
            GenTree maxValDup;
            if (varTypeIsLong(simdTargetBaseType))
            {
                maxVal = gtNewDconNode(simdSourceBaseType, (double)long.MaxValue);
                maxVal = gtNewSimdCreateBroadcastNode(type, maxVal, simdSourceBaseType, simdSize);
                maxValDup = gtNewSimdCreateBroadcastNode(type, gtNewLconNode(long.MaxValue),
                    simdTargetBaseType, simdSize);
            }
            else
            {
                maxVal = gtNewDconNode(simdSourceBaseType, (double)int.MaxValue);
                maxVal = gtNewSimdCreateBroadcastNode(type, maxVal, simdSourceBaseType, simdSize);
                maxValDup = gtNewSimdCreateBroadcastNode(type, gtNewIconNode(TYP_INT, int.MaxValue),
                    simdTargetBaseType, simdSize);
            }

            var fixupValDup = fgMakeMultiUse(ref fixupVal);
            var overMax = gtNewSimdCmpOpNode(GT_GE, type, fixupVal, maxVal, simdSourceBaseType, simdSize);
            var converted = gtNewSimdCvtNativeNode(type, fixupValDup, simdTargetBaseType,
                simdSourceBaseType, simdSize);
            return gtNewSimdCndSelNode(type, overMax, maxValDup, converted, simdTargetBaseType, simdSize);
        }
        return gtNewSimdCvtNativeNode(type, fixupVal, simdTargetBaseType, simdSourceBaseType, simdSize);
#else
        throw new FatalJitException("gtNewSimdCvtNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdCvtNativeNode(var_types type, GenTree op1, var_types simdTargetBaseType,
        var_types simdSourceBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdSourceBaseType) && varTypeIsIntegral(simdTargetBaseType));

#if TARGET_XARCH
        assert(compIsaSupportedDebugOnly(InstructionSet_AVX512) || simdTargetBaseType == TYP_INT);
        NamedIntrinsic intrinsic;
        switch (simdSourceBaseType)
        {
            case TYP_FLOAT:
            {
                intrinsic = simdTargetBaseType switch
                {
                    TYP_INT => simdSize switch
                    {
                        64 => NI_AVX512_ConvertToVector512Int32WithTruncation,
                        32 => NI_AVX_ConvertToVector256Int32WithTruncation,
                        16 => NI_X86Base_ConvertToVector128Int32WithTruncation,
                        _ => throw new FatalJitException("Unsupported SIMD conversion width."),
                    },
                    TYP_UINT => simdSize switch
                    {
                        64 => NI_AVX512_ConvertToVector512UInt32WithTruncation,
                        32 => NI_AVX512_ConvertToVector256UInt32WithTruncation,
                        16 => NI_AVX512_ConvertToVector128UInt32WithTruncation,
                        _ => throw new FatalJitException("Unsupported SIMD conversion width."),
                    },
                    _ => throw new FatalJitException("Unsupported SIMD conversion target."),
                };
                break;
            }

            case TYP_DOUBLE:
            {
                intrinsic = simdTargetBaseType switch
                {
                    TYP_LONG => simdSize switch
                    {
                        64 => NI_AVX512_ConvertToVector512Int64WithTruncation,
                        32 => NI_AVX512_ConvertToVector256Int64WithTruncation,
                        16 => NI_AVX512_ConvertToVector128Int64WithTruncation,
                        _ => throw new FatalJitException("Unsupported SIMD conversion width."),
                    },
                    TYP_ULONG => simdSize switch
                    {
                        64 => NI_AVX512_ConvertToVector512UInt64WithTruncation,
                        32 => NI_AVX512_ConvertToVector256UInt64WithTruncation,
                        16 => NI_AVX512_ConvertToVector128UInt64WithTruncation,
                        _ => throw new FatalJitException("Unsupported SIMD conversion width."),
                    },
                    _ => throw new FatalJitException("Unsupported SIMD conversion target."),
                };
                break;
            }

            default:
            {
                throw new FatalJitException("Unsupported SIMD conversion source.");
            }
        }
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdSourceBaseType, simdSize, op1);
#else
        throw new FatalJitException("gtNewSimdCvtNativeNode requires its target-specific implementation.");
#endif
    }
#endif
}
