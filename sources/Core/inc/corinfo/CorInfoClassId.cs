// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoClassId;

namespace RyuJitSharp;

public enum CorInfoClassId
{
    CLASSID_SYSTEM_OBJECT,
    
    CLASSID_TYPED_BYREF,

    CLASSID_TYPE_HANDLE,

    CLASSID_FIELD_HANDLE,

    CLASSID_METHOD_HANDLE,

    CLASSID_STRING,

    CLASSID_ARGUMENT_HANDLE,

    CLASSID_RUNTIME_TYPE,
}
