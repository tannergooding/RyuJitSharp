// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmUnwindTests
{
    [Test]
    public static void StackAllocationsAreAccumulatedAndPublishedForEveryFunction()
    {
        WithPublication((compiler, context) =>
        {
            compiler.compFuncInfos =
            [
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_ROOT,
                    funWasmFrameSize = 120,
                    startVirtualIP = 10,
                    endVirtualIP = 128,
                    startLoc = new emitLocation(Group(0)),
                    endLoc = new emitLocation(Group(32)),
                },
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_FILTER,
                    funWasmFrameSize = 127,
                    startVirtualIP = 100,
                    endVirtualIP = 228,
                    startLoc = new emitLocation(Group(32)),
                    endLoc = new emitLocation(Group(48)),
                },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.fgFuncletsCreated = true;

            compiler.unwindBegProlog();
            compiler.unwindAllocStack(8);
            compiler.unwindEndProlog();
            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(compiler.compFuncInfos[0].funWasmFrameSize, Is.EqualTo(128u));
            Assert.That(context->ReservationCount, Is.EqualTo(2));
            Assert.That(context->Reservations[0].IsFunclet, Is.False);
            Assert.That(context->Reservations[0].IsCold, Is.False);
            Assert.That(context->Reservations[0].Size, Is.EqualTo(3));
            Assert.That(context->Reservations[1].IsFunclet, Is.True);
            Assert.That(context->Reservations[1].IsCold, Is.False);
            Assert.That(context->Reservations[1].Size, Is.EqualTo(3));

            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->Allocations[0].Start, Is.Zero);
            Assert.That(context->Allocations[0].End, Is.EqualTo(32));
            Assert.That(context->Allocations[0].HotCode, Is.EqualTo((nint)context->HotCode));
            Assert.That(context->Allocations[0].ColdCode, Is.EqualTo(IntPtr.Zero));
            Assert.That(context->Allocations[0].Size, Is.EqualTo(3));
            Assert.That(context->Allocations[0].Kind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_ROOT));
            Assert.That(context->Allocations[0].Bytes[0], Is.EqualTo(0x80));
            Assert.That(context->Allocations[0].Bytes[1], Is.EqualTo(0x01));
            Assert.That(context->Allocations[0].Bytes[2], Is.EqualTo(0x76));

            Assert.That(context->Allocations[1].Start, Is.EqualTo(32));
            Assert.That(context->Allocations[1].End, Is.EqualTo(48));
            Assert.That(context->Allocations[1].HotCode, Is.EqualTo((nint)context->HotCode));
            Assert.That(context->Allocations[1].ColdCode, Is.EqualTo(IntPtr.Zero));
            Assert.That(context->Allocations[1].Size, Is.EqualTo(3));
            Assert.That(context->Allocations[1].Kind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_FILTER));
            Assert.That(context->Allocations[1].Bytes[0], Is.EqualTo(0x7F));
            Assert.That(context->Allocations[1].Bytes[1], Is.EqualTo(0x80));
            Assert.That(context->Allocations[1].Bytes[2], Is.EqualTo(0x01));
        });
    }

    [Test]
    public static void Uleb128BoundariesAndWrappedFrameSizesArePublished()
    {
        WithPublication((compiler, context) =>
        {
            compiler.compFuncInfos =
            [
                CreateFunction(FuncKind.FUNC_ROOT, 0x7F, 0, 0x7F, 0, 16),
                CreateFunction(FuncKind.FUNC_HANDLER, 0x3FFF, 0x100, 0x4100, 16, 32),
                CreateFunction(FuncKind.FUNC_FILTER, 0x1FFFFF, 0x200, 0x200200, 32, 48),
                CreateFunction(FuncKind.FUNC_HANDLER, 0x10000000, 0x300, 0x10000300, 48, 64),
                CreateFunction(FuncKind.FUNC_FILTER, uint.MaxValue, 0, uint.MaxValue, 64, 80),
                CreateFunction(FuncKind.FUNC_HANDLER, uint.MaxValue - 7, 0x400, 0x480, 80, 96),
            ];
            compiler.compFuncInfoCount = 6;
            compiler.fgFuncletsCreated = true;

            compiler.unwindBegProlog();
            compiler.funSetCurrentFunc(0);
            compiler.unwindAllocStack(1);
            compiler.funSetCurrentFunc(5);
            compiler.unwindAllocStack(8);
            compiler.unwindEndProlog();
            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(compiler.compFuncInfos[0].funWasmFrameSize, Is.EqualTo(0x80u));
            Assert.That(compiler.compFuncInfos[5].funWasmFrameSize, Is.Zero);
            Assert.That(context->ReservationCount, Is.EqualTo(6));
            Assert.That(context->AllocationCount, Is.EqualTo(6));
            for (var index = 0; index < context->ReservationCount; index++)
            {
                Assert.That(context->Reservations[index].IsFunclet, Is.EqualTo(index != 0));
                Assert.That(context->Reservations[index].IsCold, Is.False);
            }

            Assert.That(context->Reservations[0].Size, Is.EqualTo(3));
            Assert.That(context->Reservations[1].Size, Is.EqualTo(5));
            Assert.That(context->Reservations[2].Size, Is.EqualTo(7));
            Assert.That(context->Reservations[3].Size, Is.EqualTo(10));
            Assert.That(context->Reservations[4].Size, Is.EqualTo(10));
            Assert.That(context->Reservations[5].Size, Is.EqualTo(3));

            AssertAllocation(context, 0, 0, 16, CorJitFuncKind.CORJIT_FUNC_ROOT, 0x80, 0x01, 0x7F);
            AssertAllocation(
                context, 1, 16, 32, CorJitFuncKind.CORJIT_FUNC_HANDLER,
                0xFF, 0x7F, 0x80, 0x80, 0x01);
            AssertAllocation(
                context, 2, 32, 48, CorJitFuncKind.CORJIT_FUNC_FILTER,
                0xFF, 0xFF, 0x7F, 0x80, 0x80, 0x80, 0x01);
            AssertAllocation(
                context, 3, 48, 64, CorJitFuncKind.CORJIT_FUNC_HANDLER,
                0x80, 0x80, 0x80, 0x80, 0x01, 0x80, 0x80, 0x80, 0x80, 0x01);
            AssertAllocation(
                context, 4, 64, 80, CorJitFuncKind.CORJIT_FUNC_FILTER,
                0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F);
            AssertAllocation(context, 5, 80, 96, CorJitFuncKind.CORJIT_FUNC_HANDLER, 0x00, 0x80, 0x01);
        });
    }

    private delegate void PublicationAction(Compiler compiler, PublicationContext* context);

    private static void WithPublication(PublicationAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.reserveUnwindInfo =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&Reserve;
        vtable.allocUnwindInfo = &Allocate;

        var reservations = stackalloc Reservation[8];
        var allocations = stackalloc Allocation[8];
        var hotCode = stackalloc byte[1];
        var coldCode = stackalloc byte[1];
        var context = new PublicationContext
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            Reservations = reservations,
            Allocations = allocations,
            HotCode = hotCode,
            ColdCode = coldCode,
        };

        compiler.info.compCompHnd = &context.JitInfo;
        compiler.info.compMatchedVM = true;
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
        compiler.compFuncInfoCount = 1;
        compiler.fgFuncletsCreated = true;
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            action(compiler, &context);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static insGroup Group(uint offset)
    {
        var group = new insGroup { igOffs = offset };
#if DEBUG
        group.igSelf = group;
#endif
        return group;
    }

    private static FuncInfoDsc CreateFunction(
        FuncKind kind, uint frameSize, uint startVirtualIP, uint endVirtualIP, uint startOffset, uint endOffset)
    {
        return new FuncInfoDsc
        {
            funKind = kind,
            funWasmFrameSize = frameSize,
            startVirtualIP = startVirtualIP,
            endVirtualIP = endVirtualIP,
            startLoc = new emitLocation(Group(startOffset)),
            endLoc = new emitLocation(Group(endOffset)),
        };
    }

    private static void AssertAllocation(
        PublicationContext* context, int index, int start, int end, CorJitFuncKind kind, params byte[] bytes)
    {
        ref var allocation = ref context->Allocations[index];
        Assert.That(allocation.Start, Is.EqualTo(start));
        Assert.That(allocation.End, Is.EqualTo(end));
        Assert.That(allocation.HotCode, Is.EqualTo((nint)context->HotCode));
        Assert.That(allocation.ColdCode, Is.EqualTo(IntPtr.Zero));
        Assert.That(allocation.Size, Is.EqualTo(bytes.Length));
        Assert.That(allocation.Kind, Is.EqualTo(kind));
        for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
            Assert.That(allocation.Bytes[byteIndex], Is.EqualTo(bytes[byteIndex]));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Reserve(ICorJitInfo* jitInfo, byte isFunclet, byte isCold, int size)
    {
        var context = (PublicationContext*)jitInfo;
        context->Reservations[context->ReservationCount++] = new Reservation
        {
            IsFunclet = isFunclet != 0,
            IsCold = isCold != 0,
            Size = size,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* jitInfo, byte* hotCode, byte* coldCode, int start, int end,
        int size, byte* block, CorJitFuncKind kind)
    {
        var context = (PublicationContext*)jitInfo;
        var record = &context->Allocations[context->AllocationCount++];
        record->HotCode = (nint)hotCode;
        record->ColdCode = (nint)coldCode;
        record->Start = start;
        record->End = end;
        record->Size = size;
        record->Kind = kind;
        for (var index = 0; index < size; index++)
        {
            record->Bytes[index] = block[index];
        }
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public Reservation* Reservations;
        public Allocation* Allocations;
        public byte* HotCode;
        public byte* ColdCode;
        public int ReservationCount;
        public int AllocationCount;
    }

    private struct Reservation
    {
        public bool IsFunclet;
        public bool IsCold;
        public int Size;
    }

    private struct Allocation
    {
        public nint HotCode;
        public nint ColdCode;
        public int Start;
        public int End;
        public int Size;
        public CorJitFuncKind Kind;
        public fixed byte Bytes[10];
    }
}
#endif
