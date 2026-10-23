// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // D55CA4F5-C533-4C6C-A211-521303884BDB
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xD55CA4F5,
        0xC533,
        0x4C6C,
        0xA2, 0x11, 0x52, 0x13, 0x03, 0x88, 0x4B, 0xDB
    );
}
