// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_SIMD
namespace RyuJitSharp;

public sealed class GenTreeVecCon : GenTree
{
    private simd_t _SimdVal;

    public GenTreeVecCon(var_types type, simd_t simdVal)
        : base(GT_CNS_VEC, type)
    {
        assert(varTypeIsSimd(type));
        _SimdVal = simdVal;
    }

    public ref simd_t simdVal => ref _SimdVal;
}
#endif
