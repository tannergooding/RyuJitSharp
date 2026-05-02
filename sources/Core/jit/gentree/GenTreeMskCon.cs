// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_MASKED_HW_INTRINSICS
namespace RyuJitSharp;

public sealed class GenTreeMskCon : GenTree
{
    public GenTreeMskCon(var_types type)
        : base(GT_CNS_MSK, type)
    {
        assert(varTypeIsMask(type));
    }
}
#endif
