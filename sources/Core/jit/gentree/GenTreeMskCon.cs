// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_MASKED_HW_INTRINSICS
namespace RyuJitSharp;

public sealed class GenTreeMskCon : GenTree
{
    private simdmask_t _simdMaskVal;

    public GenTreeMskCon(simdmask_t simdMaskVal)
        : base(GT_CNS_MSK, TYP_MASK)
    {
        _simdMaskVal = simdMaskVal;
    }

    public bool IsAllBitsSet => _simdMaskVal.IsAllBitsSet;

    public ref simdmask_t SimdMaskVal => ref _simdMaskVal;

    public bool IsZero => _simdMaskVal.IsZero;

    /// <summary>Is the given node a true mask</summary>
    /// <param name="simdBaseType">the base type of the mask</param>
    /// <returns>Returns true if the node is a true mask for the given simdBaseType.</returns>
#if TARGET_ARM64
    public bool IsTrue(var_types simdBaseType)
    {
        // Note that a byte true mask (1111...) is different to an int true mask (10001000...), therefore the simdBaseType of the mask needs to be taken into account.
        return SveMaskPatternAll == EvaluateSimdMaskToPattern<simd16_t>(simdBaseType, AsMskCon()->gtSimdMaskVal);
    }
#else
    public bool IsTrue(var_types simdBaseType) => false;
#endif
}
#endif
