// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // D9636771-EB8F-4E21-8A6F-D97F21E658F7
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xD9636771,
        0xEB8F,
        0x4E21,
        0x8A, 0x6F, 0xD9, 0x7F, 0x21, 0xE6, 0x58, 0xF7
    );
}
