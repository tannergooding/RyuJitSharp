// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoHelpSig;

namespace RyuJitSharp;

/// <summary>Describes the signature for a helper method.</summary>
public enum CorInfoHelpSig
{
    CORINFO_HELP_SIG_UNDEF,

    CORINFO_HELP_SIG_NO_ALIGN_STUB,

    CORINFO_HELP_SIG_NO_UNWIND_STUB,

    CORINFO_HELP_SIG_REG_ONLY,

    CORINFO_HELP_SIG_4_STACK,

    CORINFO_HELP_SIG_8_STACK,

    CORINFO_HELP_SIG_12_STACK,

    CORINFO_HELP_SIG_16_STACK,

    /// <summary>Special calling convention that uses EDX and EBP as arguments.</summary>
    CORINFO_HELP_SIG_EBPCALL,

    CORINFO_HELP_SIG_CANNOT_USE_ALIGN_STUB,

    CORINFO_HELP_SIG_COUNT,
}
