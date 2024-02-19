// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

// ICorJitHost provides the interface that the JIT uses to access some functionality that
// would normally be provided by the operating system. This is intended to allow for
// host-specific policies re: memory allocation, configuration value access, etc. It is
// expected that the `ICorJitHost` value provided to `jitStartup` lives at least as
// long as the JIT itself.
public unsafe partial struct ICorJitHost : ICorJitHost.Interface
{
    internal Vtbl* lpVtbl;

    public void* allocateMemory(nuint size) => lpVtbl->allocateMemory((ICorJitHost*)Unsafe.AsPointer(ref this), size);

    public void freeMemory(void* block) => lpVtbl->freeMemory((ICorJitHost*)Unsafe.AsPointer(ref this), block);

    public int getIntConfigValue(char* name, int defaultValue) => lpVtbl->getIntConfigValue((ICorJitHost*)Unsafe.AsPointer(ref this), name, defaultValue);

    public char* getStringConfigValue(char* name) => lpVtbl->getStringConfigValue((ICorJitHost*)Unsafe.AsPointer(ref this), name);

    public void freeStringConfigValue(char* value) => lpVtbl->freeStringConfigValue((ICorJitHost*)Unsafe.AsPointer(ref this), value);

    public void* allocateSlab(nuint size, nuint* pActualSize) => lpVtbl->allocateSlab((ICorJitHost*)Unsafe.AsPointer(ref this), size, pActualSize);

    public void freeSlab(void* slab, nuint actualSize) => lpVtbl->freeSlab((ICorJitHost*)Unsafe.AsPointer(ref this), slab, actualSize);

    public interface Interface
    {
        // Allocate memory of the given size in bytes.
        void* allocateMemory(nuint size);

        // Frees memory previous obtained by a call to `ICorJitHost::allocateMemory`.
        void freeMemory(void* block);

        // Return an integer config value for the given key, if any exists.
        int getIntConfigValue(char* name, int defaultValue);

        // Return a string config value for the given key, if any exists.
        char* getStringConfigValue(char* name);

        // Free a string ConfigValue returned by the runtime.
        // JITs using the getStringConfigValue query are required
        // to return the string values to the runtime for deletion.
        // This avoids leaking the memory in the JIT.
        void freeStringConfigValue(char* value);

        // Allocate memory slab of the given size in bytes. The host is expected to pool
        // these for a good performance.
        void* allocateSlab(nuint size, nuint* pActualSize)
        {
            *pActualSize = size;
            return allocateMemory(size);
        }

        // Free memory slab of the given size in bytes.
        void freeSlab(void* slab, nuint actualSize)
        {
            freeMemory(slab);
        }
    }

    public struct Vtbl
    {
        public delegate* unmanaged[MemberFunction]<ICorJitHost*, nuint, void*> allocateMemory;

        public delegate* unmanaged[MemberFunction]<ICorJitHost*, void*, void> freeMemory;

        public delegate* unmanaged[MemberFunction]<ICorJitHost*, char*, int, int> getIntConfigValue;

        public delegate* unmanaged[MemberFunction]<ICorJitHost*, char*, char*> getStringConfigValue;

        public delegate* unmanaged[MemberFunction]<ICorJitHost*, char*, void> freeStringConfigValue;

        public delegate* unmanaged[MemberFunction]<ICorJitHost*, nuint, nuint*, void*> allocateSlab;

        public delegate* unmanaged[MemberFunction]<ICorJitHost*, void*, nuint, void> freeSlab;
    }
}
