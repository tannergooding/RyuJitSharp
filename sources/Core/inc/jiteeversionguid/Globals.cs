// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 69033DC2-A177-4B8B-B218-91B24BD2A4E4
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x69033DC2,
        0xA177,
        0x4B8B,
        0xB2, 0x18, 0x91, 0xB2, 0x4B, 0xD2, 0xA4, 0xE4
    );
}
