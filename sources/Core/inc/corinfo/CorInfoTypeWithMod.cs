// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoTypeWithMod;

namespace RyuJitSharp;

public enum CorInfoTypeWithMod
{
    /// <summary>Lower 6 bits are type mask.</summary>
    CORINFO_TYPE_MASK = 0x3F,

    /// <summary>Can be applied to CLASS, or BYREF to indicate pinned.</summary>
    CORINFO_TYPE_MOD_PINNED = 0x40,

    /// <summary>Can be applied to VALUECLASS to indicate 'needs helper to copy'</summary>
    CORINFO_TYPE_MOD_COPY_WITH_HELPER = 0x80,
}
