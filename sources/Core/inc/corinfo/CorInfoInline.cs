// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoInline;

namespace RyuJitSharp;

public enum CorInfoInline
{
    /// <summary>Inlining OK.</summary>
    INLINE_PASS = 0,

    /// <summary>Inline check for pre-jit checking usage succeeded</summary>
    INLINE_PREJIT_SUCCESS = 1,

    /// <summary>JIT detected it is permitted to try to actually inline.</summary>
    INLINE_CHECK_CAN_INLINE_SUCCESS = 2,

    /// <summary>VM specified that inline must fail via the CanInline api.</summary>
    INLINE_CHECK_CAN_INLINE_VMFAIL = 3,

    //
    // failures are negative
    //

    /// <summary>Inlining not OK for this case only.</summary>
    INLINE_FAIL = -1,

    /// <summary>This method should never be inlined, regardless of context.</summary>
    INLINE_NEVER = -2,
}
