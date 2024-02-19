// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe struct CORINFO_FIELD_INFO
{
    public CORINFO_FIELD_ACCESSOR fieldAccessor;

    public uint fieldFlags;

    /// <summary>Helper to use if the field access requires it.</summary>
    public CorInfoHelpFunc helper;

    /// <summary>Field offset if there is one.</summary>
    public uint offset;

    public CorInfoType fieldType;

    /// <summary>Possibly null.</summary>
    public CORINFO_CLASS_HANDLE structType;

    /// <summary>See <see cref="CORINFO_CALL_INFO.accessAllowed" />.</summary>
    public CorInfoIsAccessAllowedResult accessAllowed;

    public CORINFO_HELPER_DESC accessCalloutHelper;

    /// <summary>Used by Ready-to-Run.</summary>
    public CORINFO_CONST_LOOKUP fieldLookup;
}
