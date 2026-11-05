// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 6FE71AB2-CEDB-4F39-815D-2EAFF70271FF
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x6FE71AB2,
        0xCEDB,
        0x4F39,
        0x81, 0x5D, 0x2E, 0xAF, 0xF7, 0x02, 0x71, 0xFF
    );
}
