// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // FA0C6A6F-B219-4B60-B928-042C72667EB3
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xFA0C6A6F,
        0xB219,
        0x4B60,
        0xB9, 0x28, 0x04, 0x2C, 0x72, 0x66, 0x7E, 0xB3
    );
}
