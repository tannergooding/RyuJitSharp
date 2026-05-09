// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum AddressExposedReason
{
    NONE,

    /// <summary>This is a promoted field but the parent is exposed.</summary>
    PARENT_EXPOSED,

    /// <summary>Were marked as exposed to be conservative, fix these places.</summary>
    TOO_CONSERVATIVE,

    /// <summary>The address is escaping, for example, passed as call argument.</summary>
    ESCAPE_ADDRESS,

    /// <summary>We access via indirection with wider type.</summary>
    WIDE_INDIR,

    /// <summary>It was exposed in the original method, osr has to repeat it.</summary>
    OSR_EXPOSED,

    /// <summary>Stress mode replaces localVar with localFld and makes them addrExposed.</summary>
    STRESS_LCL_FLD,

    /// <summary>Caller return buffer dispatch.</summary>
    DISPATCH_RET_BUF,

    /// <summary>This is an implicit byref we want to poison.</summary>
    STRESS_POISON_IMPLICIT_BYREFS,

    /// <summary>Local is visible externally without explicit escape in JIT IR.</summary>
    /// <remarks>For example because it is used by GC or is the outgoing arg area that belongs to callees.</remarks>
    EXTERNALLY_VISIBLE_IMPLICITLY,

    /// <summary>A small-typed local has a partial def that doesn't cover the full local, so we must treat it as normalize-on-load.</summary>
    SMALL_TYPE_PARTIAL_DEF,
}
