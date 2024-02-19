// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoIndirectCallReason;
using System;

namespace RyuJitSharp;

public enum CorInfoIndirectCallReason
{
    CORINFO_INDIRECT_CALL_UNKNOWN,

    CORINFO_INDIRECT_CALL_EXOTIC,

    CORINFO_INDIRECT_CALL_PINVOKE,

    CORINFO_INDIRECT_CALL_GENERIC,

    CORINFO_INDIRECT_CALL_NO_CODE,

    CORINFO_INDIRECT_CALL_FIXUPS,

    CORINFO_INDIRECT_CALL_STUB,

    CORINFO_INDIRECT_CALL_REMOTING,

    CORINFO_INDIRECT_CALL_CER,

    CORINFO_INDIRECT_CALL_RESTORE_METHOD,

    CORINFO_INDIRECT_CALL_RESTORE_FIRST_CALL,

    CORINFO_INDIRECT_CALL_RESTORE_VALUE_TYPE,

    CORINFO_INDIRECT_CALL_RESTORE,

    CORINFO_INDIRECT_CALL_CANT_PATCH,

    CORINFO_INDIRECT_CALL_PROFILING,

    CORINFO_INDIRECT_CALL_OTHER_LOADER_MODULE,

    CORINFO_INDIRECT_CALL_COUNT,
}
