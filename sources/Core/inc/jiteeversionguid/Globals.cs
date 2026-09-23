// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // CB2FA75D-449D-4EE0-93EF-57011DAEFB8C
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xCB2FA75D,
        0x449D,
        0x4EE0,
        0x93, 0xEF, 0x57, 0x01, 0x1D, 0xAE, 0xFB, 0x8C
    );
}
