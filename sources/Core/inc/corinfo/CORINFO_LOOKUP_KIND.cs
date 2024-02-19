// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe struct CORINFO_LOOKUP_KIND
{
    public bool needsRuntimeLookup;

    public CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind;

    /// <summary>Just for internal VM / ZAP communication, not to be used by the JIT.</summary>
    public ushort runtimeLookupFlags;

    /// <summary>Just for internal VM / ZAP communication, not to be used by the JIT.</summary>
    public void* runtimeLookupArgs;
}
