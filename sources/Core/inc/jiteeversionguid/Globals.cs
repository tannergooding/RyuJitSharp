// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 31A04B06-915E-42A0-BBD2-C9C397677AE5
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x31A04B06,
        0x915E,
        0x42A0,
        0xBB, 0xD2, 0xC9, 0xC3, 0x97, 0x67, 0x7A, 0xE5
    );
}
