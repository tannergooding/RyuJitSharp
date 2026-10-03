// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal readonly unsafe struct HostAllocator
{
    public T* allocate<T>(nuint count)
        where T : unmanaged
    {
        var elementSize = (nuint)sizeof(T);
        if (count > nuint.MaxValue / elementSize)
        {
            return null;
        }

        return (T*)allocateHostMemory(count * elementSize);
    }

    public void deallocate(void* block)
    {
        freeHostMemory(block);
    }

    public static HostAllocator getHostAllocator() => default;

    private static void* allocateHostMemory(nuint size)
    {
        assert(CILJit.s_jitHost != null);
        // The managed ABI uses nint for size_t; preserve the unsigned size bits.
        return CILJit.s_jitHost->allocateMemory(unchecked((nint)size));
    }

    private static void freeHostMemory(void* block)
    {
        assert(CILJit.s_jitHost != null);
        CILJit.s_jitHost->freeMemory(block);
    }
}
