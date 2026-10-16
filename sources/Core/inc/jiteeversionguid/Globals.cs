// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 406B139F-B910-4579-A9A8-91EB153AB024
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x406B139F,
        0xB910,
        0x4579,
        0xA9, 0xA8, 0x91, 0xEB, 0x15, 0x3A, 0xB0, 0x24
    );
}
