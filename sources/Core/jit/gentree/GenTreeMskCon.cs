// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_MASKED_HW_INTRINSICS
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed class GenTreeMskCon : GenTree
{
    private simdmask_t _simdMaskVal;

    public GenTreeMskCon(simdmask_t simdMaskVal)
        : base(GT_CNS_MSK, TYP_MASK)
    {
        _simdMaskVal = simdMaskVal;
#if TARGET_ARM64
        assert(Unsafe.SizeOf<simdmaskscalable_t>() <= Unsafe.SizeOf<simdmask_t>());
#endif
    }

    public bool IsAllBitsSet => IsAllBitsSetForType(TYP_BYTE);

    public bool IsAllBitsSetForType(var_types simdBaseType)
    {
#if TARGET_ARM64 && DEBUG
        if (JitConfig.JitUseScalableVectorT != 0)
        {
            return SimdScalableMaskVal.IsAllBitsSet(simdBaseType);
        }
#endif

        return _simdMaskVal.IsAllBitsSet;
    }

    public ref simdmask_t SimdMaskVal => ref _simdMaskVal;

#if TARGET_ARM64
    // Native overlays the two byte-sized scalable fields on the eight-byte mask payload.
    public ref simdmaskscalable_t SimdScalableMaskVal => ref Unsafe.As<simdmask_t, simdmaskscalable_t>(ref _simdMaskVal);
#endif

    public bool IsZero
    {
        get
        {
#if TARGET_ARM64 && DEBUG
            if (JitConfig.JitUseScalableVectorT != 0)
            {
                return SimdScalableMaskVal.IsZero;
            }
#endif

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
#if TARGET_ARM64 && DEBUG
        if (JitConfig.JitUseScalableVectorT != 0)
        {
            return left.SimdScalableMaskVal == right.SimdScalableMaskVal;
        }
#endif

        return left._simdMaskVal == right._simdMaskVal;
    }

    /// <summary>Is the given node a true mask</summary>
    /// <param name="simdBaseType">the base type of the mask</param>
    /// <returns>Returns true if the node is a true mask for the given simdBaseType.</returns>
#if TARGET_ARM64
    public bool IsTrue(var_types simdBaseType)
    {
#if DEBUG
        if (JitConfig.JitUseScalableVectorT != 0)
        {
            return (SimdScalableMaskVal.Index == 1) && (SimdScalableMaskVal.BaseType.Size <= simdBaseType.Size);
        }
#endif

        return EvaluateSimdMaskToPattern<simd16_t>(simdBaseType, _simdMaskVal) == SveMaskPattern.SveMaskPatternAll;
    }
#else
    public bool IsTrue(var_types simdBaseType) => false;
#endif
}
#endif
