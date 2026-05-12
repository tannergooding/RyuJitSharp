// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // BF284EFA-A3FB-4420-A62F-6E1A0A1B2CFC
    public static readonly Guid JITEEVersionIdentifier = new Guid(
        0xBF284EFA,
        0xA3FB,
        0x4420,
        0xA6, 0x2F, 0x6E, 0x1A, 0x0A, 0x1B, 0x2C, 0xFC
    );
}
