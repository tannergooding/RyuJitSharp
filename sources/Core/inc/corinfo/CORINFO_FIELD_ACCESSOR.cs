// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_FIELD_ACCESSOR;
using System;

namespace RyuJitSharp;

// getFieldInfo and CORINFO_FIELD_INFO: The EE instructs the JIT about how to access a field
public enum CORINFO_FIELD_ACCESSOR
{
    /// <summary>Regular instance field at given offset from this-ptr.</summary>
    CORINFO_FIELD_INSTANCE,

    /// <summary>Instance field with base offset (used by Ready-to-Run).</summary>
    CORINFO_FIELD_INSTANCE_WITH_BASE,

    /// <summary>Instance field accessed using helper (arguments are this, FieldDesc * and the value).</summary>
    CORINFO_FIELD_INSTANCE_HELPER,

    /// <summary>Instance field accessed using address-of helper (arguments are this and FieldDesc *).</summary>
    CORINFO_FIELD_INSTANCE_ADDR_HELPER,

    /// <summary>Field at given address.</summary>
    CORINFO_FIELD_STATIC_ADDRESS,

    /// <summary>RVA field at given address.</summary>
    CORINFO_FIELD_STATIC_RVA_ADDRESS,

    /// <summary>Static field accessed using the "shared static" helper (arguments are ModuleID + ClassID).</summary>
    CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER,

    /// <summary>Static field access using the "generic static" helper (argument is MethodTable *).</summary>
    CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER,

    /// <summary>Static field accessed using address-of helper (argument is FieldDesc *).</summary>
    CORINFO_FIELD_STATIC_ADDR_HELPER,

    /// <summary>Unmanaged TLS access.</summary>
    CORINFO_FIELD_STATIC_TLS,

    /// <summary>Managed TLS access.</summary>
    CORINFO_FIELD_STATIC_TLS_MANAGED,

    /// <summary>Static field access using a runtime lookup helper.</summary>
    CORINFO_FIELD_STATIC_READYTORUN_HELPER,

    /// <summary>Static field access using relocation (used in AOT).</summary>
    CORINFO_FIELD_STATIC_RELOCATABLE,

    /// <summary>Intrinsic zero (<see cref="IntPtr.Zero" />, <see cref="UIntPtr.Zero" />).</summary>
    CORINFO_FIELD_INTRINSIC_ZERO,

    /// <summary>Intrinsic empty string (<see cref="string.Empty" />).</summary>
    CORINFO_FIELD_INTRINSIC_EMPTY_STRING,

    /// <summary>Intrinsic <see cref="BitConverter.IsLittleEndian" />.</summary>
    CORINFO_FIELD_INTRINSIC_ISLITTLEENDIAN,
}
