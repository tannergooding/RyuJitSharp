// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // 2E4DF063-30B2-4B9F-8A11-1912EE4DB3C1
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x2E4DF063,
        0x30B2,
        0x4B9F,
        0x8A, 0x11, 0x19, 0x12, 0xEE, 0x4D, 0xB3, 0xC1
    );
}
