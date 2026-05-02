// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_RUNTIME_LOOKUP_KIND;

namespace RyuJitSharp;

public enum CORINFO_RUNTIME_LOOKUP_KIND
{
    CORINFO_LOOKUP_THISOBJ,

    CORINFO_LOOKUP_METHODPARAM,

    CORINFO_LOOKUP_CLASSPARAM,

    /// <summary>Returned for attempts to inline dictionary lookups.</summary>
    CORINFO_LOOKUP_NOT_SUPPORTED,
}
