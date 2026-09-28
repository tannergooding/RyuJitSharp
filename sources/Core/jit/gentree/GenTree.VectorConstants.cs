// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public ulong GetIntegralVectorConstElement(int index, var_types baseType)
    {
#if FEATURE_HW_INTRINSICS
        if (Oper.IsCnsVec)
        {
#if TARGET_ARM64
            if (Type is TYP_SIMD)
            {
                ref var value = ref AsVecCon().SimdScalableVal;
                assert(baseType == value.BaseType);

                // Native uses raw 64-bit arithmetic here, without narrowing to the element width.
                return value.Kind switch {
                    SimdScalableKind.SimdScalableRepeated => value.Index.u64[0],
                    SimdScalableKind.SimdScalableSequence =>
                        unchecked(value.Index.u64[0] + (value.Step.u64[0] * (ulong)index)),
                    SimdScalableKind.SimdScalableScalar => index == 0 ? value.Index.u64[0] : 0,
                    _ => throw new FatalJitException("Unexpected scalable vector constant kind."),
                };
            }
#endif
            var integralType = baseType switch {
                TYP_FLOAT => TYP_INT,
                TYP_DOUBLE => TYP_LONG,
                _ => baseType,
            };
            return unchecked((ulong)AsVecCon().GetElementIntegral(integralType, index));
        }
#endif
        return 0;
    }
}
