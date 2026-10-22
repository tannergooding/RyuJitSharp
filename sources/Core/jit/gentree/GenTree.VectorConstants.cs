// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public ulong GetIntegralVectorConstElement(int index, var_types baseType)
    {
        if (Configuration.FEATURE_HW_INTRINSICS && Oper.IsCnsVec)
        {
            if (Configuration.TARGET_ARM64 && (Type is TYP_SIMD_ARM64))
            {
                ref var value = ref AsVecCon().SimdScalableVal;
                assert(baseType == value.BaseType);

                // Native uses raw 64-bit arithmetic here, without narrowing to the element width.
                switch (value.Kind)
                {
                    case SimdScalableKind.SimdScalableRepeated:
                    {
                        return value.Index.u64[0];
                    }

                    case SimdScalableKind.SimdScalableSequence:
                    {
                        return unchecked(value.Index.u64[0] + (value.Step.u64[0] * (ulong)index));
                    }

                    case SimdScalableKind.SimdScalableScalar:
                    {
                        return index == 0 ? value.Index.u64[0] : 0;
                    }

                    default:
                    {
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.UnexpectedScalableVectorConstantKind);
                        return default!;
                    }
                }
            }
            var integralType = baseType switch {
                TYP_FLOAT => TYP_INT,
                TYP_DOUBLE => TYP_LONG,
                _ => baseType,
            };
            return unchecked((ulong)AsVecCon().GetElementIntegral(integralType, index));
        }
        return 0;
    }
}
