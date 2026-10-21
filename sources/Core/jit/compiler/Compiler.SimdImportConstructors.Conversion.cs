// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree gtNewSimdCvtNode(var_types type, GenTree op1, var_types simdTargetBaseType,
        var_types simdSourceBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "SIMD conversion requires hardware intrinsics.");
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdSourceBaseType) && varTypeIsIntegral(simdTargetBaseType));

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            return gtNewSimdCvtNativeNode(type, op1, simdTargetBaseType, simdSourceBaseType, simdSize);
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException("gtNewSimdCvtNode requires its target-specific implementation.");
            return default!;
        }

        assert(compIsaSupportedDebugOnly(InstructionSet_AVX512) || simdTargetBaseType == TYP_INT);
        if (compOpportunisticallyDependsOn(InstructionSet_AVX10v2))
        {
            NamedIntrinsic intrinsic;
            switch (simdTargetBaseType)
            {
                case TYP_INT:
                {
                    intrinsic = NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation;
                    break;
                }

                case TYP_UINT:
                {
                    intrinsic = NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation;
                    break;
                }

                case TYP_LONG:
                {
                    intrinsic = NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation;
                    break;
                }

                case TYP_ULONG:
                {
                    intrinsic = NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation;
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage124
                    );
                    return default!;
                }
            }

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
    }

    public GenTree gtNewSimdCvtNativeNode(var_types type, GenTree op1, var_types simdTargetBaseType,
        var_types simdSourceBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Native SIMD conversion requires hardware intrinsics.");
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdSourceBaseType) && varTypeIsIntegral(simdTargetBaseType));

        if (Configuration.TARGET_XARCH)
        {
            assert(compIsaSupportedDebugOnly(InstructionSet_AVX512) || simdTargetBaseType == TYP_INT);
            NamedIntrinsic intrinsic;
            switch (simdSourceBaseType)
            {
                case TYP_FLOAT:
                {
                    switch (simdTargetBaseType)
                    {
                        case TYP_INT:
                        {
                            switch (simdSize)
                            {
                                case 64:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector512Int32WithTruncation;
                                    break;
                                }

                                case 32:
                                {
                                    intrinsic = NI_AVX_ConvertToVector256Int32WithTruncation;
                                    break;
                                }

                                case 16:
                                {
                                    intrinsic = NI_X86Base_ConvertToVector128Int32WithTruncation;
                                    break;
                                }

                                default:
                                {
                                    FatalJitException.ThrowFatalJitException(
                                        CORJIT_INTERNALERROR,
                                        FatalJitErrorCode.DirectFailureMessage192
                                    );
                                    return default!;
                                }
                            }

                            break;
                        }

                        case TYP_UINT:
                        {
                            switch (simdSize)
                            {
                                case 64:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector512UInt32WithTruncation;
                                    break;
                                }

                                case 32:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector256UInt32WithTruncation;
                                    break;
                                }

                                case 16:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector128UInt32WithTruncation;
                                    break;
                                }

                                default:
                                {
                                    FatalJitException.ThrowFatalJitException(
                                        CORJIT_INTERNALERROR,
                                        FatalJitErrorCode.DirectFailureMessage192
                                    );
                                    return default!;
                                }
                            }

                            break;
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage124
                            );
                            return default!;
                        }
                    }
                    break;
                }

                case TYP_DOUBLE:
                {
                    switch (simdTargetBaseType)
                    {
                        case TYP_LONG:
                        {
                            switch (simdSize)
                            {
                                case 64:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector512Int64WithTruncation;
                                    break;
                                }

                                case 32:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector256Int64WithTruncation;
                                    break;
                                }

                                case 16:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector128Int64WithTruncation;
                                    break;
                                }

                                default:
                                {
                                    FatalJitException.ThrowFatalJitException(
                                        CORJIT_INTERNALERROR,
                                        FatalJitErrorCode.DirectFailureMessage192
                                    );
                                    return default!;
                                }
                            }

                            break;
                        }

                        case TYP_ULONG:
                        {
                            switch (simdSize)
                            {
                                case 64:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector512UInt64WithTruncation;
                                    break;
                                }

                                case 32:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector256UInt64WithTruncation;
                                    break;
                                }

                                case 16:
                                {
                                    intrinsic = NI_AVX512_ConvertToVector128UInt64WithTruncation;
                                    break;
                                }

                                default:
                                {
                                    FatalJitException.ThrowFatalJitException(
                                        CORJIT_INTERNALERROR,
                                        FatalJitErrorCode.DirectFailureMessage192
                                    );
                                    return default!;
                                }
                            }

                            break;
                        }

                        default:
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage124
                            );
                            return default!;
                        }
                    }
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage193
                    );
                    return default!;
                }
            }

            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdSourceBaseType, simdSize, op1);
        }
        else if (Configuration.TARGET_ARM64)
        {
            assert(simdSize is 8 or 16);
            var intrinsic = NI_Illegal;

            switch (simdSourceBaseType)
            {
                case TYP_FLOAT:
                {
                    switch (simdTargetBaseType)
                    {
                        case TYP_INT:
                        {
                            intrinsic = NI_AdvSimd_ConvertToInt32RoundToZero;
                            break;
                        }

                        case TYP_UINT:
                        {
                            intrinsic = NI_AdvSimd_ConvertToUInt32RoundToZero;
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                    break;
                }

                case TYP_DOUBLE:
                {
                    switch (simdTargetBaseType)
                    {
                        case TYP_LONG:
                        {
                            intrinsic = simdSize == 8 ? NI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar
                                : NI_AdvSimd_Arm64_ConvertToInt64RoundToZero;
                            break;
                        }

                        case TYP_ULONG:
                        {
                            intrinsic = simdSize == 8 ? NI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar
                                : NI_AdvSimd_Arm64_ConvertToUInt64RoundToZero;
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
            assert(intrinsic != NI_Illegal);

            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdSourceBaseType, simdSize, op1);
        }
        else if (Configuration.TARGET_WASM)
        {
            var intrinsic = NI_Illegal;

            switch (simdTargetBaseType)
            {
                case TYP_INT:
                {
                    assert(simdSourceBaseType == TYP_FLOAT);
                    intrinsic = NI_PackedSimd_ConvertToInt32Saturate;
                    break;
                }

                case TYP_UINT:
                {
                    assert(simdSourceBaseType == TYP_FLOAT);
                    intrinsic = NI_PackedSimd_ConvertToUInt32Saturate;
                    break;
                }

                default:
                {
                    // Float/double -> LONG and ULONG conversions are not natively supported on Wasm.
                    unreached();
                    break;
                }
            }
            assert(intrinsic != NI_Illegal);

            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdSourceBaseType, simdSize, op1);
        }

        FatalJitException.ThrowFatalJitException("gtNewSimdCvtNativeNode requires its target-specific implementation.");
        return default!;
    }
}
