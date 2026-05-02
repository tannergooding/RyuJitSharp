// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoInitClassResult;
using System;

namespace RyuJitSharp;

[Flags]
public enum CorInfoInitClassResult
{
    /// <summary>No class initialization required, but the class is not actually initialized yet (e.g. we are guaranteed to run the static constructor in method prolog).</summary>
    CORINFO_INITCLASS_NOT_REQUIRED = 0x00,

    /// <summary>Class initialized.</summary>
    CORINFO_INITCLASS_INITIALIZED = 0x01,

    /// <summary>The JIT must insert class initialization helper call.</summary>
    CORINFO_INITCLASS_USE_HELPER = 0x02,

    /// <summary>The JIT should not inline the method requesting the class initialization.</summary>
    /// <remarks>The class initialization requires helper class now, but will not require initialization if the method is compiled standalone. Or the method cannot be inlined due to some requirement around class initialization such as shared generics.</remarks>
    CORINFO_INITCLASS_DONT_INLINE = 0x04,
}
