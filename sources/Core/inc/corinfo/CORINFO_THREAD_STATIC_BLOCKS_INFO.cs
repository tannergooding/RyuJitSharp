// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// getThreadLocalStaticBlocksInfo and CORINFO_THREAD_STATIC_BLOCKS_INFO: The EE instructs the JIT about how to access a thread local field
public unsafe struct CORINFO_THREAD_STATIC_BLOCKS_INFO
{
    // windows specific
    public CORINFO_CONST_LOOKUP tlsIndex;

    // linux/x64 specific - address of __tls_get_addr() function
    public void* tlsGetAddrFtnPtr;

    // linux/x64 specific - address of tls_index object
    public void* tlsIndexObject;

    // osx x64/arm64 specific - address of __thread_vars section of `t_ThreadStatics`
    public void* threadVarsSection;

    // windows specific
    public uint offsetOfThreadLocalStoragePointer;

    public uint offsetOfMaxThreadStaticBlocks;

    public uint offsetOfThreadStaticBlocks;

    public uint offsetOfGCDataPointer;
}
