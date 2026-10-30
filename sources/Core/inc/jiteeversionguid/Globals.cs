// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // AEC48FC3-2735-42A2-ACCF-36202A1EC5C3
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xAEC48FC3,
        0x2735,
        0x42A2,
        0xAC, 0xCF, 0x36, 0x20, 0x2A, 0x1E, 0xC5, 0xC3
    );
}
