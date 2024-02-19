// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_THIS_TRANSFORM;

namespace RyuJitSharp;

public enum CORINFO_THIS_TRANSFORM
{
    CORINFO_NO_THIS_TRANSFORM,

    CORINFO_BOX_THIS,

    CORINFO_DEREF_THIS,
}
