// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // C77C1A0F-C8C6-4A68-90DB-4D8C1253C5EA
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xC77C1A0F,
        0xC8C6,
        0x4A68,
        0x90, 0xDB, 0x4D, 0x8C, 0x12, 0x53, 0xC5, 0xEA
    );
}
