// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class JitMemKindTraitsTests
{
    [Test]
    public static void MemoryKindNamesMatchEveryPinnedOrdinal()
    {
        string[] expected =
        [
            "ABI", "AssertionProp", "ASTNode", "InstDesc", "ImpStack", "BasicBlock", "CallArgs", "FlowEdge",
            "DepthFirstSearch", "Loops", "TreeStatementList", "SiScope", "DominatorMemory", "Lower", "LSRA",
            "LSRA_Interval", "LSRA_RefPosition", "Reachability", "RedundantBranch", "SSA", "ValueNumber",
            "LvaTable", "UnwindInfo", "hashBv", "bitset", "FixedBitVect", "Generic", "LocalAddressVisitor",
            "FieldSeqStore", "MemorySsaMap", "MemoryPhiArg", "CSE", "GC", "CorTailCallInfo", "Inlining",
            "ArrayStack", "DebugInfo", "DebugOnly", "Codegen", "LoopOpt", "LoopClone", "LoopUnroll",
            "LoopHoist", "LoopIVOpts", "Unknown", "RangeCheck", "CopyProp", "Promotion", "SideEffects",
            "ObjectAllocator", "VariableLiveRanges", "ClassLayout", "EarlyProp", "ZeroInit", "Pgo",
            "MaskConversionOpt", "TryRegionClone", "Async", "RangeCheckCloning", "WasmSccTransform",
            "WasmCfgLowering", "WasmEH", "WasmSpillRefs",
        ];

        Assert.That(JitMemKindTraits.Count, Is.EqualTo(63));
        Assert.That(JitMemKindTraits.Names.ToArray(), Is.EqualTo(expected));
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.That(Enum.GetName((CompMemKind)i), Is.EqualTo($"CMK_{expected[i]}"));
        }

        Assert.That((int)CompMemKind.CMK_Count, Is.EqualTo(expected.Length));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public static void HostSlabsPreserveTheRequestActualSizeAndOwnership(int requestedSize)
    {
        var buffer = stackalloc byte[32];
        ICorJitHost.Vtbl vtable = new()
        {
            allocateMemory = &AllocateMemory,
            freeMemory = &FreeMemory,
            allocateSlab = &AllocateSlab,
            freeSlab = &FreeSlab,
        };
        HostContext context = new()
        {
            Host = new ICorJitHost { lpVtbl = &vtable },
            Result = buffer,
            ActualSize = 32,
        };
        var previousHost = CILJit.s_jitHost;
        var previousConfig = Globals.JitConfig;
        try
        {
            CILJit.s_jitHost = &context.Host;
            Globals.JitConfig = default;
            nuint actualSize = 0;

            var block = JitMemKindTraits.allocateHostMemory((nuint)requestedSize, &actualSize);
            Assert.That((nuint)block, Is.EqualTo((nuint)buffer));
            Assert.That(context.RequestedSize, Is.EqualTo((nint)requestedSize));
            Assert.That(actualSize, Is.EqualTo((nuint)32));
            Assert.That(context.SlabAllocations, Is.EqualTo(1));

            JitMemKindTraits.freeHostMemory(block, actualSize);
            Assert.That((nuint)context.Freed, Is.EqualTo((nuint)buffer));
            Assert.That(context.FreedSize, Is.EqualTo((nint)32));
            Assert.That(context.SlabFrees, Is.EqualTo(1));
            Assert.That(context.MemoryAllocations, Is.Zero);
            Assert.That(context.MemoryFrees, Is.Zero);
        }
        finally
        {
            CILJit.s_jitHost = previousHost;
            Globals.JitConfig = previousConfig;
        }
    }

    [Test]
    public static void HostFailureAndUnsignedSizeBitsAreForwardedWithoutChangingTheContract()
    {
        ICorJitHost.Vtbl vtable = new() { allocateSlab = &AllocateSlab, freeSlab = &FreeSlab };
        HostContext context = new()
        {
            Host = new ICorJitHost { lpVtbl = &vtable },
            ActualSize = unchecked((nint)(nuint.MaxValue - 1)),
        };
        var previousHost = CILJit.s_jitHost;
        var previousConfig = Globals.JitConfig;
        try
        {
            CILJit.s_jitHost = &context.Host;
            Globals.JitConfig = default;
            nuint actualSize = 0;

            var block = JitMemKindTraits.allocateHostMemory(nuint.MaxValue, &actualSize);
            Assert.That((nuint)block, Is.EqualTo((nuint)0));
            Assert.That(unchecked((nuint)context.RequestedSize), Is.EqualTo(nuint.MaxValue));
            Assert.That(actualSize, Is.EqualTo(nuint.MaxValue - 1));
            Assert.That(context.SlabAllocations, Is.EqualTo(1));

            JitMemKindTraits.freeHostMemory(null, actualSize);
            Assert.That((nuint)context.Freed, Is.EqualTo((nuint)0));
            Assert.That(unchecked((nuint)context.FreedSize), Is.EqualTo(actualSize));
            Assert.That(context.SlabFrees, Is.EqualTo(1));
        }
        finally
        {
            CILJit.s_jitHost = previousHost;
            Globals.JitConfig = previousConfig;
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public static void UninitializedFillRespectsTheBuildAndBlockBoundaries(int size)
    {
        var buffer = stackalloc byte[9];
        new Span<byte>(buffer, 9).Fill(0xA5);

        JitMemKindTraits.fillWithUninitializedPattern(buffer + 1, (nuint)size);

        Assert.That(buffer[0], Is.EqualTo(0xA5));
        for (var i = 0; i < size; i++)
        {
#if DEBUG
            Assert.That(buffer[i + 1], Is.EqualTo(0xCD));
#else
            Assert.That(buffer[i + 1], Is.EqualTo(0xA5));
#endif
        }

        Assert.That(buffer[size + 1], Is.EqualTo(0xA5));
        JitMemKindTraits.fillWithUninitializedPattern(null, 0);
    }

    [Test]
    public static void OutOfMemoryUsesTheJitFailureCode()
    {
        var previousConfig = Globals.JitConfig;
        try
        {
            Globals.JitConfig = default;
            var failure = Assert.Throws<FatalJitException>(JitMemKindTraits.outOfMemory);
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_OUTOFMEM));
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(-1)]
    public static void DebugPredicatesPreserveNonzeroConfigSemantics(int value)
    {
        var previousConfig = Globals.JitConfig;
        try
        {
            Globals.JitConfig = default;
            DirectAlloc(ref Globals.JitConfig) = value;
            Assert.That(JitMemKindTraits.bypassHostAllocator(), Is.EqualTo(value != 0));
            Assert.That(JitMemKindTraits.shouldInjectFault(), Is.False);

            DirectAlloc(ref Globals.JitConfig) = 0;
            InjectFault(ref Globals.JitConfig) = value;
            Assert.That(JitMemKindTraits.bypassHostAllocator(), Is.False);
            Assert.That(JitMemKindTraits.shouldInjectFault(), Is.EqualTo(value != 0));
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public static void DirectSlabsDoNotUseTheHostAndAccountForTheRequestedSize(int requestedSize)
    {
        var previousHost = CILJit.s_jitHost;
        var previousConfig = Globals.JitConfig;
        void* block = null;
        try
        {
            CILJit.s_jitHost = null;
            Globals.JitConfig = default;
            DirectAlloc(ref Globals.JitConfig) = 1;
            var actualSize = nuint.MaxValue;

            block = JitMemKindTraits.allocateHostMemory((nuint)requestedSize, &actualSize);
            Assert.That((nuint)block, Is.Not.EqualTo((nuint)0));
            Assert.That(actualSize, Is.EqualTo((nuint)requestedSize));
            ((byte*)block)[0] = 0xA5;
        }
        finally
        {
            JitMemKindTraits.freeHostMemory(block, (nuint)requestedSize);
            CILJit.s_jitHost = previousHost;
            Globals.JitConfig = previousConfig;
        }
    }

    [Test]
    public static void DirectAllocationFailurePreservesActualSizeAndUsesTheJitFailureCode()
    {
        var previousHost = CILJit.s_jitHost;
        var previousConfig = Globals.JitConfig;
        nuint actualSize = 0;
        try
        {
            CILJit.s_jitHost = null;
            Globals.JitConfig = default;
            DirectAlloc(ref Globals.JitConfig) = 1;
            try
            {
                var block = JitMemKindTraits.allocateHostMemory(nuint.MaxValue, &actualSize);
                JitMemKindTraits.freeHostMemory(block, actualSize);
                Assert.Fail("An allocation of SIZE_MAX bytes must fail.");
            }
            catch (FatalJitException failure)
            {
                Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_OUTOFMEM));
                Assert.That(actualSize, Is.EqualTo(nuint.MaxValue));
            }
        }
        finally
        {
            CILJit.s_jitHost = previousHost;
            Globals.JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDirectAlloc")]
    private static extern ref int DirectAlloc(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_shouldInjectFault")]
    private static extern ref int InjectFault(ref JitConfigValues config);
#else
    [Test]
    public static void RetailPredicatesDoNotEnableDebugBehavior()
    {
        Assert.That(JitMemKindTraits.bypassHostAllocator(), Is.False);
        Assert.That(JitMemKindTraits.shouldInjectFault(), Is.False);
    }
#endif

    [StructLayout(LayoutKind.Sequential)]
    private struct HostContext
    {
        public ICorJitHost Host;
        public void* Result;
        public void* Freed;
        public nint RequestedSize;
        public nint ActualSize;
        public nint FreedSize;
        public int SlabAllocations;
        public int SlabFrees;
        public int MemoryAllocations;
        public int MemoryFrees;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateSlab(ICorJitHost* host, nint size, nint* actualSize)
    {
        var context = (HostContext*)host;
        context->RequestedSize = size;
        *actualSize = context->ActualSize;
        context->SlabAllocations++;

        return context->Result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void FreeSlab(ICorJitHost* host, void* block, nint actualSize)
    {
        var context = (HostContext*)host;
        context->Freed = block;
        context->FreedSize = actualSize;
        context->SlabFrees++;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateMemory(ICorJitHost* host, nint size)
    {
        var context = (HostContext*)host;
        context->MemoryAllocations++;

        return context->Result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void FreeMemory(ICorJitHost* host, void* block)
    {
        var context = (HostContext*)host;
        context->MemoryFrees++;
    }
}
