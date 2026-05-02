// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using System;

namespace RyuJitSharp;

public sealed class GenTreeHWIntrinsic : GenTreeJitIntrinsic
{
    public GenTreeHWIntrinsic(var_types type, NamedIntrinsic hwIntrinsicId, var_types simdBaseType, byte simdSize, params ReadOnlySpan<GenTree> operands)
        : base(GT_HWINTRINSIC, type, simdBaseType, simdSize, operands)
    {
        Initialize(hwIntrinsicId);
    }

    public NamedIntrinsic HWIntrinsicId => _hwIntrinsicId;

    private void Initialize(NamedIntrinsic intrinsicId)
    {
        // TODO: Port Initialize
    }
}
#endif
