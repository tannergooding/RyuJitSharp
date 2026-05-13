// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const UnrollKind Memset = UnrollKind.Memset;
    public const UnrollKind Memcpy = UnrollKind.Memcpy;
    public const UnrollKind Memmove = UnrollKind.Memmove;
    public const UnrollKind MemcmpU16 = UnrollKind.MemcmpU16;
    public const UnrollKind ProfiledMemmove = UnrollKind.ProfiledMemmove;
    public const UnrollKind ProfiledMemcmp = UnrollKind.ProfiledMemcmp;

    public enum UnrollKind
    {
        Memset,
        Memcpy,
        Memmove,
        MemcmpU16,
        ProfiledMemmove,
        ProfiledMemcmp,
    }
}
