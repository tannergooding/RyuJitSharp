// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 33ED917F-A197-4ABD-83AF-DB9FA8186898
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x33ED917F,
        0xA197,
        0x4ABD,
        0x83, 0xAF, 0xDB, 0x9F, 0xA8, 0x18, 0x68, 0x98
    );
}
