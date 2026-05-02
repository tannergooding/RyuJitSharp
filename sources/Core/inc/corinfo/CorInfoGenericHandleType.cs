// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoGenericHandleType;

namespace RyuJitSharp;

public enum CorInfoGenericHandleType
{
    CORINFO_HANDLETYPE_UNKNOWN,

    CORINFO_HANDLETYPE_CLASS,

    CORINFO_HANDLETYPE_METHOD,

    CORINFO_HANDLETYPE_FIELD
}
