// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// Represents the calling conventions supported with the extensible calling convention syntax
// as well as the original metadata-encoded calling conventions.
public enum CorInfoCallConvExtension
{
    Managed,

    C,

    Stdcall,

    Thiscall,

    Fastcall,

    // New calling conventions supported with the extensible calling convention encoding go here.
    CMemberFunction,

    StdcallMemberFunction,

    FastcallMemberFunction,

    Swift
}
