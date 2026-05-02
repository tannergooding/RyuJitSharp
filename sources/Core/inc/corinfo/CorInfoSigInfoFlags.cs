// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoSigInfoFlags;
using System;

namespace RyuJitSharp;

[Flags]
public enum CorInfoSigInfoFlags
{
    CORINFO_SIGFLAG_IS_LOCAL_SIG = 0x01,

    CORINFO_SIGFLAG_IL_STUB = 0x02,

    // unused = 0x04,

    CORINFO_SIGFLAG_FAT_CALL = 0x08,
}
