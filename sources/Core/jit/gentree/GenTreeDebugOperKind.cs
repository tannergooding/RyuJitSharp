// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.GenTreeDebugOperKind;
using System;

namespace RyuJitSharp;

// The following enum defines a set of bit flags that describe opers for the purposes
// of DEBUG-only checks. This is separate from the above "GenTreeOperKind"s to avoid
// making the table for those larger in Release builds. However, it resides in the same
// "namespace" and so all values here must be distinct from those in "GenTreeOperKind".
[Flags]
public enum GenTreeDebugOperKind
{
    DBK_NONE = 0,

    DBK_FIRST_FLAG = GTK_MASK + 1,

    // This oper is not supported in HIR (before rationalization).
    DBK_NOTHIR = DBK_FIRST_FLAG,

    // This oper is not supported in LIR (after rationalization).
    DBK_NOTLIR = DBK_FIRST_FLAG << 1,

    // This oper produces a value, but may not be contained.
    DBK_NOCONTAIN = DBK_FIRST_FLAG << 2,

    DBK_MASK = ~GTK_MASK
}
