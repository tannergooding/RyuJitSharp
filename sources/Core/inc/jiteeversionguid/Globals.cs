// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 57112612-E99E-4311-A95F-A13F0C8FE9E4
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x57112612,
        0xE99E,
        0x4311,
        0xA9, 0x5F, 0xA1, 0x3F, 0x0C, 0x8F, 0xE9, 0xE4
    );
}
