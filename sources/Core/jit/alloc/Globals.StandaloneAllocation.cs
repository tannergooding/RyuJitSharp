// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if JIT_STANDALONE_BUILD
using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Globals
{
    // Native new and new[] share this body. This helper does not interpose on global allocation.
    internal static unsafe void* op_New(nuint size)
    {
        assert(CILJit.s_jitHost == null, "Global new called; use HostAllocator if long-lived allocation was intended");

        if (size == 0)
        {
            size = 1;
        }

        var result = NativeMemory.Alloc(size);
        if (result == null)
        {
#pragma warning disable CA2201 // Translate native std::bad_alloc to its managed allocation-failure equivalent.
            throw new OutOfMemoryException();
#pragma warning restore CA2201
        }

        return result;
    }

    // Native delete and delete[] both free the raw allocation.
    internal static unsafe void op_Delete(void* ptr)
    {
        NativeMemory.Free(ptr);
    }
}
#endif
