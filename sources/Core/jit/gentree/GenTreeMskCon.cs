// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed class GenTreeMskCon : GenTree
{
    private simdmask_t _simdMaskVal;

    public GenTreeMskCon(simdmask_t simdMaskVal)
        : base(GT_CNS_MSK, TYP_MASK)
    {
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget);
        }

        _simdMaskVal = simdMaskVal;
        if (Configuration.TARGET_ARM64)
        {
            assert(Unsafe.SizeOf<simdmaskscalable_t>() <= Unsafe.SizeOf<simdmask_t>());
        }
    }

    public bool IsAllBitsSet => IsAllBitsSetForType(TYP_BYTE);

    public bool IsAllBitsSetForType(var_types simdBaseType)
    {
        if (Configuration.DEBUG && Configuration.TARGET_ARM64 && (JitConfig.JitUseScalableVectorT != 0))
        {
            return SimdScalableMaskVal.IsAllBitsSet(simdBaseType);
        }

        return _simdMaskVal.IsAllBitsSet;
    }

    public ref simdmask_t SimdMaskVal => ref _simdMaskVal;

    // Native overlays the two byte-sized scalable fields on the eight-byte mask payload.
    public ref simdmaskscalable_t SimdScalableMaskVal => ref Unsafe.As<simdmask_t, simdmaskscalable_t>(ref _simdMaskVal);

    public bool IsZero
    {
        get
        {
            if (Configuration.DEBUG && Configuration.TARGET_ARM64 && (JitConfig.JitUseScalableVectorT != 0))
            {
                return SimdScalableMaskVal.IsZero;
            }

            return _simdMaskVal.IsZero;
        }
    }

    public void EvaluateUnaryInPlace(genTreeOps oper, bool scalar, var_types baseType, int simdSize)
    {
        simdmask_t result = default;
        EvaluateUnaryMask(oper, scalar, baseType, simdSize, ref result, _simdMaskVal);
        _simdMaskVal = result;
    }

    public void EvaluateBinaryInPlace(genTreeOps oper, bool scalar, var_types baseType, int simdSize, GenTreeMskCon other)
    {
        simdmask_t result = default;
        EvaluateBinaryMask(oper, scalar, baseType, simdSize, ref result, _simdMaskVal, other._simdMaskVal);
        _simdMaskVal = result;
    }

    public static bool Equals(GenTreeMskCon left, GenTreeMskCon right)
    {
        if (Configuration.DEBUG && Configuration.TARGET_ARM64 && (JitConfig.JitUseScalableVectorT != 0))
        {
            return left.SimdScalableMaskVal == right.SimdScalableMaskVal;
        }

        return left._simdMaskVal == right._simdMaskVal;
    }

    /// <summary>Is the given node a true mask</summary>
    /// <param name="simdBaseType">the base type of the mask</param>
    /// <returns>Returns true if the node is a true mask for the given simdBaseType.</returns>
    public bool IsTrue(var_types simdBaseType)
    {
        if (!Configuration.TARGET_ARM64)
        {
            return false;
        }

        if (Configuration.DEBUG && (JitConfig.JitUseScalableVectorT != 0))
        {
            return (SimdScalableMaskVal.Index == 1) && (SimdScalableMaskVal.BaseType.Size <= simdBaseType.Size);
        }

        return EvaluateSimdMaskToPattern<simd16_t>(simdBaseType, _simdMaskVal) == SveMaskPattern.SveMaskPatternAll;
    }
}
