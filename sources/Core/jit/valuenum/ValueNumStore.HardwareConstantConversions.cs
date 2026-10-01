// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_SIMD
using System;
using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum VNBroadcastForSimdType(var_types type, var_types baseType, ValueNum operand)
    {
        assert(varTypeIsSimd(type));
        switch (type)
        {
#if TARGET_ARM64
            case TYP_SIMD:
            {
                var value = BroadcastConstantToSimdScalable(baseType, operand);
                return VNForSimdScalableCon(value);
            }
#endif

            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                return VNForGenericCon(type, GetSimdArgumentBytes(type, baseType, operand));
            }

            default:
            {
                throw new UnreachableException();
            }
        }
    }

#if TARGET_ARM64
    private simdscalable_t BroadcastConstantToSimdScalable(var_types baseType, ValueNum operand)
    {
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
#endif

#if FEATURE_HW_INTRINSICS && FEATURE_MASKED_HW_INTRINSICS
    private ValueNum EvaluateSimdCvtMaskToVectorVN(var_types type, var_types baseType, ValueNum operand)
    {
        var argument = GetConstantSimdMaskValue(operand);
#if TARGET_ARM64
        if (argument.IsScalable && (type != TYP_SIMD))
        {
            return NoVN;
        }
        if (type == TYP_SIMD)
        {
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
#endif

        switch (type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                var result = new byte[type.Size];
                EvaluateSimdCvtMaskToVector(baseType, result, argument.Fixed);
                return VNForGenericCon(type, result);
            }

            default:
            {
                throw new UnreachableException();
            }
        }
    }

    private ValueNum EvaluateSimdCvtVectorToMaskVN(var_types type, var_types baseType, ValueNum operand)
    {
#if TARGET_ARM64
        if (type == TYP_SIMD)
        {
            var argument = GetConstantSimdScalable(operand);
            var result = default(simdmaskscalable_t);
            if (!EvaluateSimdCvtScalableVectorToMask(baseType, ref result, argument))
            {
                return NoVN;
            }
            return VNForSimdMaskScalableCon(result);
        }
#endif

        switch (type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                var argument = GetSimdArgumentBytes(type, baseType, operand);
                var result = default(simdmask_t);
                EvaluateSimdCvtVectorToMask(baseType, ref result, argument);
                return VNForSimdMaskCon(result);
            }

            default:
            {
                throw new UnreachableException();
            }
        }
    }
#endif
}
#endif
