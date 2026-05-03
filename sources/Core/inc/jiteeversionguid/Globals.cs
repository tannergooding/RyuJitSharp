// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 1516ACB8-AC41-4DCB-9840-F39EE25FFA73
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x1516ACB8,
        0xAC41,
        0x4DCB,
        0x98, 0x40, 0xF3, 0x9E, 0xE2, 0x5F, 0xFA, 0x73
    );
}
