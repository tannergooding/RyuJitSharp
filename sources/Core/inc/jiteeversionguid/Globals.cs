// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 213C3E7F-F472-4EE7-9003-47E23AFB035F
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x213C3E7F,
        0xF472,
        0x4EE7,
        0x90, 0x03, 0x47, 0xE2, 0x3A, 0xFB, 0x03, 0x5F
    );
}
