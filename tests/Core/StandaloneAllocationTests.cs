// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if JIT_STANDALONE_BUILD
using System;
using System.Runtime.InteropServices;
#if DEBUG
using System.Runtime.CompilerServices;
#endif
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StandaloneAllocationTests
{
#if DEBUG
    private static string? s_assertion;
#endif

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public static void AllocationProvidesWritableStorageAndCanBeFreed(int requestedSize)
    {
        var previousHost = CILJit.s_jitHost;
        void* block = null;
        try
        {
            CILJit.s_jitHost = null;
            block = Globals.op_New((nuint)requestedSize);
            Assert.That((nuint)block, Is.Not.EqualTo((nuint)0));

            var backingSize = Math.Max(requestedSize, 1);
            var bytes = new Span<byte>(block, backingSize);
            bytes.Fill(0xA5);
            Assert.That(bytes[0], Is.EqualTo(0xA5));
            Assert.That(bytes[backingSize - 1], Is.EqualTo(0xA5));
        }
        finally
        {
            Globals.op_Delete(block);
            CILJit.s_jitHost = previousHost;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HelpersUseTheNativeMemoryAllocationAndFreePair(bool allocateWithHelper)
    {
        var previousHost = CILJit.s_jitHost;
        void* block = null;
        try
        {
            CILJit.s_jitHost = null;
            block = allocateWithHelper ? Globals.op_New(7) : NativeMemory.Alloc(7);
            Assert.That((nuint)block, Is.Not.EqualTo((nuint)0));
            ((byte*)block)[6] = 0xA5;
        }
        finally
        {
            if (allocateWithHelper)
            {
                NativeMemory.Free(block);
            }
            else
            {
                Globals.op_Delete(block);
            }

            CILJit.s_jitHost = previousHost;
        }
    }

    [Test]
    public static void DeletingNullIsAllowed()
    {
        Globals.op_Delete(null);
    }

    [Test]
    public static void AllocationFailurePropagatesOutOfMemoryRatherThanAJitFailure()
    {
        var previousHost = CILJit.s_jitHost;
        try
        {
            CILJit.s_jitHost = null;
            _ = Assert.Throws<OutOfMemoryException>(() =>
            {
                var block = Globals.op_New(nuint.MaxValue);
                Globals.op_Delete(block);
            });
        }
        finally
        {
            CILJit.s_jitHost = previousHost;
        }
    }

#if DEBUG
    [Test]
    public static void AllocationAfterHostInitializationReportsTheNativeMisuseAssertion()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = new() { doAssert = &RecordAssertion };
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&jitInfo);
        ICorJitHost host = default;
        var previousHost = CILJit.s_jitHost;
        void* block = null;
        s_assertion = null;
        try
        {
            CILJit.s_jitHost = &host;
            block = Globals.op_New(1);
            Assert.That(s_assertion, Is.EqualTo("Global new called; use HostAllocator if long-lived allocation was intended"));
            Assert.That((nuint)block, Is.Not.EqualTo((nuint)0));
        }
        finally
        {
            Globals.op_Delete(block);
            CILJit.s_jitHost = previousHost;
            s_assertion = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* jitInfo, byte* file, int line, byte* expression)
    {
        s_assertion = Marshal.PtrToStringUTF8((nint)expression);

        return 0;
    }
#endif
}
#endif
