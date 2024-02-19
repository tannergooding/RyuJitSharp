// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public unsafe partial class Globals
{
    // 0fb71692-0ee6-4914-88a8-6446e45f23e8
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0x0FB71692,
        0x0EE6,
        0x4914,
        0X88, 0XA8, 0X64, 0X46, 0XE4, 0X5F, 0X23, 0XE8
    );
}
