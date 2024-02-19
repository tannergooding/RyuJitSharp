// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// getThreadLocalStaticInfo_NativeAOT and CORINFO_THREAD_STATIC_INFO_NATIVEAOT: The EE instructs the JIT about how to access a thread local field
public struct CORINFO_THREAD_STATIC_INFO_NATIVEAOT
{
    public uint offsetOfThreadLocalStoragePointer;

    public CORINFO_CONST_LOOKUP tlsRootObject;

    public CORINFO_CONST_LOOKUP tlsIndexObject;

    public CORINFO_CONST_LOOKUP threadStaticBaseSlow;

    public CORINFO_CONST_LOOKUP tlsGetAddrFtnPtr;
}
