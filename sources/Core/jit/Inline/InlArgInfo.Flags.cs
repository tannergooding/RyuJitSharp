// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct InlArgInfo
{
    private enum Flags
    {
        None = 0,
        IsUsed = 1 << 0,
        IsInvariant = 1 << 1,
        IsLclVar = 1 << 2,
        IsThis = 1 << 3,
        HasSideEff = 1 << 4,
        HasGlobRef = 1 << 5,
        HasCallerLocalRef = 1 << 6,
        HasTmp = 1 << 7,
        HasLdargaOp = 1 << 8,
        HasStargOp = 1 << 9,
        IsByRefToStructLocal = 1 << 10,
        IsExact = 1 << 11,
    }
}
