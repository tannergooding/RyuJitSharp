// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoAccessAllowedHelperArgType;

namespace RyuJitSharp;

public enum CorInfoAccessAllowedHelperArgType
{
    CORINFO_HELPER_ARG_TYPE_Invalid = 0,

    CORINFO_HELPER_ARG_TYPE_Field = 1,

    CORINFO_HELPER_ARG_TYPE_Method = 2,

    CORINFO_HELPER_ARG_TYPE_Class = 3,

    CORINFO_HELPER_ARG_TYPE_Module = 4,

    CORINFO_HELPER_ARG_TYPE_Const = 5,
}
