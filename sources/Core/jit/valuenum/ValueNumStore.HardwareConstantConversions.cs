// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum VNBroadcastForSimdType(var_types type, var_types baseType, ValueNum operand)
    {
        EnsureSimdSupported();
        assert(varTypeIsSimd(type));
        if (type is TYP_SIMD_ARM64)
        {
            EnsureArm64SimdSupported();
            var value = BroadcastConstantToSimdScalable(baseType, operand);

            return VNForSimdScalableCon(value);
        }

        if (type is TYP_SIMD32 or TYP_SIMD64)
        {
            EnsureXarchSimdSupported();
        }

        switch (type)
        {
            case TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD32 or TYP_SIMD64:
                return VNForGenericCon(type, GetSimdArgumentBytes(type, baseType, operand));
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
        }
    }

    public simdscalable_t GetConstantSimdScalable(var_types baseType, ValueNum operand)
    {
        EnsureHardwareIntrinsicsSupported();
        EnsureArm64SimdSupported();
        assert(IsVNConstant(operand));

        if (TypeOfVN(operand) == TYP_SIMD_ARM64)
        {
            return GetConstantSimdScalable(operand);
        }

        return BroadcastConstantToSimdScalable(baseType, operand);
    }

    private simdscalable_t BroadcastConstantToSimdScalable(var_types baseType, ValueNum operand)
    {
        EnsureArm64SimdSupported();
        assert(IsVNConstant(operand));
        assert(!varTypeIsSimd(TypeOfVN(operand)));
        var index = default(simd8_t);
        var bytes = GetSimdArgumentBytes(TYP_SIMD8, baseType, operand);
        bytes.AsSpan(0, baseType.Size).CopyTo(index.AsSpan<byte>());

        return new simdscalable_t
        {
            BaseType = baseType,
            Kind = SimdScalableKind.SimdScalableRepeated,
            Index = index,
        };
    }

    private ValueNum EvaluateSimdCvtMaskToVectorVN(var_types type, var_types baseType, ValueNum operand)
    {
        EnsureHardwareIntrinsicsSupported();
        EnsureSimdSupported();
        EnsureMaskedHardwareIntrinsicsAvailable();

        var argument = GetConstantSimdMaskValue(operand);
        if (type is TYP_SIMD_ARM64)
        {
            EnsureArm64SimdSupported();
            if (!argument.IsScalable)
            {
                return NoVN;
            }

            var result = default(simdscalable_t);
            if (!EvaluateSimdCvtScalableMaskToVector(baseType, ref result, argument.Scalable))
            {
                return NoVN;
            }
            return VNForSimdScalableCon(result);
        }

        if (argument.IsScalable)
        {
            return NoVN;
        }

        if (type is TYP_SIMD32 or TYP_SIMD64)
        {
            EnsureXarchSimdSupported();
        }

        switch (type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
            case TYP_SIMD32:
            case TYP_SIMD64:
            {
                var result = new byte[type.Size];
                EvaluateSimdCvtMaskToVector(baseType, result, argument.Fixed);

                return VNForGenericCon(type, result);
            }

            default:
            {
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
            }
        }
    }

    private ValueNum EvaluateSimdCvtVectorToMaskVN(var_types type, var_types baseType, ValueNum operand)
    {
        EnsureHardwareIntrinsicsSupported();
        EnsureSimdSupported();
        EnsureMaskedHardwareIntrinsicsAvailable();

        if (type is TYP_SIMD_ARM64)
        {
            EnsureArm64SimdSupported();
            var argument = GetConstantSimdScalable(operand);
            var result = default(simdmaskscalable_t);
            if (!EvaluateSimdCvtScalableVectorToMask(baseType, ref result, argument))
            {
                return NoVN;
            }
            return VNForSimdMaskScalableCon(result);
        }

        if (type is TYP_SIMD32 or TYP_SIMD64)
        {
            EnsureXarchSimdSupported();
        }

        switch (type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
            case TYP_SIMD32:
            case TYP_SIMD64:
            {
                var argument = GetSimdArgumentBytes(type, baseType, operand);
                var result = default(simdmask_t);
                EvaluateSimdCvtVectorToMask(baseType, ref result, argument);

                return VNForSimdMaskCon(result);
            }

            default:
            {
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
            }
        }
    }

    private static void EnsureHardwareIntrinsicsSupported()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.HardwareIntrinsicsAreNotEnabledForThisTarget);
        }
    }

    private static void EnsureMaskedHardwareIntrinsicsAvailable()
    {
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget);
        }
    }
}
