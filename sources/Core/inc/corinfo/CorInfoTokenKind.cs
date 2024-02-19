// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoTokenKind;

namespace RyuJitSharp;

/// <summary>Used for JIT to tell EE where this token comes from.</summary>
/// <remarks>Depending on different opcodes, we might allow/disallow certain types of tokens or return different types of handles (e.g. boxed vs. regular entrypoints)</remarks>
public enum CorInfoTokenKind
{
    CORINFO_TOKENKIND_Class = 0x01,

    CORINFO_TOKENKIND_Method = 0x02,

    CORINFO_TOKENKIND_Field = 0x04,

    CORINFO_TOKENKIND_Mask = 0x07,

    /// <summary>Token comes from CEE_LDTOKEN.</summary>
    CORINFO_TOKENKIND_Ldtoken = 0x10 | CORINFO_TOKENKIND_Class | CORINFO_TOKENKIND_Method | CORINFO_TOKENKIND_Field,

    /// <summary>Token comes from CEE_CASTCLASS or CEE_ISINST.</summary>
    CORINFO_TOKENKIND_Casting = 0x20 | CORINFO_TOKENKIND_Class,

    /// <summary>Token comes from CEE_NEWARR.</summary>
    CORINFO_TOKENKIND_Newarr = 0x40 | CORINFO_TOKENKIND_Class,

    /// <summary>Token comes from CEE_BOX.</summary>
    CORINFO_TOKENKIND_Box = 0x80 | CORINFO_TOKENKIND_Class,

    /// <summary>Token comes from CEE_CONSTRAINED.</summary>
    CORINFO_TOKENKIND_Constrained = 0x100 | CORINFO_TOKENKIND_Class,

    /// <summary>Token comes from CEE_CONSTRAINED.</summary>
    CORINFO_TOKENKIND_NewObj = 0x200 | CORINFO_TOKENKIND_Method,

    /// <summary>Token comes from CEE_LDVIRTFTN.</summary>
    CORINFO_TOKENKIND_Ldvirtftn = 0x400 | CORINFO_TOKENKIND_Method,

    /// <summary>Token comes from devirtualizing a method.</summary>
    CORINFO_TOKENKIND_DevirtualizedMethod = 0x800 | CORINFO_TOKENKIND_Method,
}
