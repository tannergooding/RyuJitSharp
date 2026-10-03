// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HostAllocatorTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public static void AllocationAndFreePreserveTheHostSizeAndPointer(int count)
    {
        var buffer = stackalloc byte[32];
        ICorJitHost.Vtbl vtable = new() { allocateMemory = &Allocate, freeMemory = &Free };
        HostContext context = new() { Host = new ICorJitHost { lpVtbl = &vtable }, Result = buffer };
        var previous = CILJit.s_jitHost;
        try
        {
            CILJit.s_jitHost = &context.Host;
            var allocator = HostAllocator.getHostAllocator();

            var block = allocator.allocate<int>((nuint)count);
            Assert.That((nuint)block, Is.EqualTo((nuint)buffer));
            Assert.That(context.RequestedSize, Is.EqualTo((nint)(count * sizeof(int))));
            Assert.That(context.Allocations, Is.EqualTo(1));

            allocator.deallocate(block);
            Assert.That((nuint)context.Freed, Is.EqualTo((nuint)buffer));
            Assert.That(context.Frees, Is.EqualTo(1));
        }
        finally
        {
            CILJit.s_jitHost = previous;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TheLargestSafeProductPreservesAllSizeBitsAndOverflowSkipsTheHost(bool overflow)
    {
        ICorJitHost.Vtbl vtable = new() { allocateMemory = &Allocate };
        HostContext context = new() { Host = new ICorJitHost { lpVtbl = &vtable } };
        var previous = CILJit.s_jitHost;
        try
        {
            CILJit.s_jitHost = &context.Host;
            var count = nuint.MaxValue / sizeof(ushort);
            if (overflow)
            {
                count++;
            }

            var block = HostAllocator.getHostAllocator().allocate<ushort>(count);

            Assert.That((nuint)block, Is.EqualTo((nuint)0));
            Assert.That(context.Allocations, Is.EqualTo(overflow ? 0 : 1));
            if (!overflow)
            {
                Assert.That(unchecked((nuint)context.RequestedSize), Is.EqualTo(nuint.MaxValue - 1));
            }
        }
        finally
        {
            CILJit.s_jitHost = previous;
        }
    }

    [Test]
    public static void AByteAllocationCanForwardTheLargestHostSize()
    {
        ICorJitHost.Vtbl vtable = new() { allocateMemory = &Allocate, freeMemory = &Free };
        HostContext context = new() { Host = new ICorJitHost { lpVtbl = &vtable } };
        var previous = CILJit.s_jitHost;
        try
        {
            CILJit.s_jitHost = &context.Host;
            var allocator = HostAllocator.getHostAllocator();

            Assert.That((nuint)allocator.allocate<byte>(nuint.MaxValue), Is.EqualTo((nuint)0));
            Assert.That(unchecked((nuint)context.RequestedSize), Is.EqualTo(nuint.MaxValue));
            allocator.deallocate(null);
            Assert.That(context.Frees, Is.EqualTo(1));
            Assert.That((nuint)context.Freed, Is.EqualTo((nuint)0));
        }
        finally
        {
            CILJit.s_jitHost = previous;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HostContext
    {
        public ICorJitHost Host;
        public void* Result;
        public void* Freed;
        public nint RequestedSize;
        public int Allocations;
        public int Frees;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Allocate(ICorJitHost* host, nint size)
    {
        var context = (HostContext*)host;
        context->RequestedSize = size;
        context->Allocations++;
        return context->Result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Free(ICorJitHost* host, void* block)
    {
        var context = (HostContext*)host;
        context->Freed = block;
        context->Frees++;
    }
}
