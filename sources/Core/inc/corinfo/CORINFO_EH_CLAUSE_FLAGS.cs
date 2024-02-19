// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_EH_CLAUSE_FLAGS;
using System;

namespace RyuJitSharp;

/// <summary>These are the flags set on an <see cref="CORINFO_EH_CLAUSE" />.</summary>
[Flags]
public enum CORINFO_EH_CLAUSE_FLAGS
{
    CORINFO_EH_CLAUSE_NONE = 0,

    /// <summary>If this bit is on, then this EH entry is for a filter.</summary>
    CORINFO_EH_CLAUSE_FILTER = 0x0001,

    /// <summary>This clause is a finally clause.</summary>
    CORINFO_EH_CLAUSE_FINALLY = 0x0002,

    /// <summary>This clause is a fault clause.</summary>
    CORINFO_EH_CLAUSE_FAULT = 0x0004,

    /// <summary>Duplicated clause.</summary>
    /// <remarks>This clause was duplicated to a funclet which was pulled out of line.</remarks>
    CORINFO_EH_CLAUSE_DUPLICATE = 0x0008,

    /// <summary>This clause covers same try block as the previous one.</summary>
    CORINFO_EH_CLAUSE_SAMETRY = 0x0010,
}
