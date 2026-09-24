// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

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
                throw new NotImplementedException("ARM64 scalable vector constant element lookup is not ported.");
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
