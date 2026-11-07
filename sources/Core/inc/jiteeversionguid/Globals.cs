// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 01882B0B-A86E-434D-9F0E-DF84F3032062
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x01882B0B,
        0xA86E,
        0x434D,
        0x9F, 0x0E, 0xDF, 0x84, 0xF3, 0x03, 0x20, 0x62
    );
}
