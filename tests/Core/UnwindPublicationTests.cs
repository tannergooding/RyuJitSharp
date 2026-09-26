// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class UnwindPublicationTests
{
    [Test]
    public static void EmptyPrologPublishesFourByteHeader()
    {
        WithPublication((compiler, _, context) =>
        {
            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(context->ReservationCount, Is.EqualTo(1));
            Assert.That(context->Reservations[0].Size, Is.EqualTo(4));
            Assert.That(context->AllocationCount, Is.EqualTo(1));
            var record = context->Allocations[0];
            Assert.That(record.Size, Is.EqualTo(4));
            Assert.That(record.Start, Is.Zero);
            Assert.That(record.End, Is.EqualTo(32));
            Assert.That(record.HotCode, Is.EqualTo((nint)context->HotCode));
            Assert.That(record.ColdCode, Is.EqualTo((nint)0));
            Assert.That(record.Kind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_ROOT));
            Assert.That(context->Allocations[0].Bytes[0], Is.EqualTo(1));
            Assert.That(context->Allocations[0].Bytes[1], Is.Zero);
            Assert.That(context->Allocations[0].Bytes[2], Is.Zero);
            Assert.That(context->Allocations[0].Bytes[3], Is.Zero);
        });
    }

    [Test]
    public static void RecordedCodesArePrependedWithHeaderAndCopiedDuringCallback()
    {
        WithPublication((compiler, codeGen, context) =>
        {
            compiler.unwindPush(REG_R12);
            compiler.unwindSetFrameReg(REG_RBP, 32);
            compiler.unwindEndProlog();
            codeGen.Emitter.emitCurIG = new insGroup();

            compiler.unwindReserve();
            var func = compiler.funCurrentFunc();
            Assert.That(func.unwindCodeSlot, Is.EqualTo(506u));
            Assert.That(func.unwindHeader.CountOfUnwindCodes, Is.EqualTo(2));
            Assert.That(context->Reservations[0].Size, Is.EqualTo(8));

            compiler.unwindEmit(context->HotCode, context->ColdCode);
            byte[] expected = [1, 0, 2, 0x25, 0, 3, 0, 0xC0];
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(context->Allocations[0].Bytes[index], Is.EqualTo(expected[index]));
            }
        }, endProlog: false);
    }

    [Test]
    public static void SplitRootReservesAndPublishesEmptyColdRecord()
    {
        WithPublication((compiler, _, context) =>
        {
            var hotEnd = Group(32);
            var coldStart = Group(32);
            var coldEnd = Group(72);
            compiler.fgFirstColdBlock = new BasicBlock(null, null);
            compiler.fgFirstFuncletBB = new BasicBlock(null, null);
            ref var func = ref compiler.compFuncInfos[0];
            func.endLoc = new emitLocation(hotEnd);
            func.coldStartLoc = new emitLocation(coldStart);
            func.coldEndLoc = new emitLocation(coldEnd);
            compiler.info.compNativeCodeSize = 72;
            compiler.info.compTotalHotCodeSize = 32;

            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(context->ReservationCount, Is.EqualTo(2));
            Assert.That(context->Reservations[0].Size, Is.EqualTo(4));
            Assert.That(context->Reservations[1].IsCold, Is.True);
            Assert.That(context->Reservations[1].Size, Is.Zero);
            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->Allocations[0].End, Is.EqualTo(32));
            Assert.That(context->Allocations[1].Start, Is.Zero);
            Assert.That(context->Allocations[1].End, Is.EqualTo(40));
            Assert.That(context->Allocations[1].ColdCode, Is.EqualTo((nint)context->ColdCode));
            Assert.That(context->Allocations[1].Size, Is.Zero);
            Assert.That(context->Allocations[1].UnwindBlock, Is.EqualTo((nint)0));
        });
    }

    [Test]
    public static void ColdFuncletPublishesOwnHeaderAfterRootWithoutRootColdRecord()
    {
        WithPublication((compiler, _, context) =>
        {
            compiler.fgFirstColdBlock = new BasicBlock(null, null);
            compiler.fgFirstFuncletBB = compiler.fgFirstColdBlock;
            compiler.compFuncInfos[0].endLoc = new emitLocation(Group(32));
            compiler.compFuncInfos = [
                compiler.compFuncInfos[0],
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_FILTER,
                    unwindCodes = new byte[514],
                    unwindCodeSlot = 514,
                    unwindHeader = new Amd64UnwindHeader { Version = 1 },
                    coldStartLoc = new emitLocation(Group(32)),
                    coldEndLoc = new emitLocation(Group(72)),
                },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.info.compNativeCodeSize = 72;
            compiler.info.compTotalHotCodeSize = 32;

            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(context->ReservationCount, Is.EqualTo(2));
            Assert.That(context->Reservations[0].IsFunclet, Is.False);
            Assert.That(context->Reservations[1].IsFunclet, Is.True);
            Assert.That(context->Reservations[1].IsCold, Is.True);
            Assert.That(context->Reservations[1].Size, Is.EqualTo(4));
            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->Allocations[1].Kind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_FILTER));
            Assert.That(context->Allocations[1].Start, Is.Zero);
            Assert.That(context->Allocations[1].End, Is.EqualTo(40));
            Assert.That(context->Allocations[1].Size, Is.EqualTo(4));
            Assert.That(context->Allocations[1].Bytes[0], Is.EqualTo(1));
        });
    }

    [Test]
    public static void HotFuncletKeepsOffsetsRelativeToHotCode()
    {
        WithPublication((compiler, _, context) =>
        {
            compiler.compFuncInfos[0].endLoc = new emitLocation(Group(32));
            compiler.compFuncInfos = [
                compiler.compFuncInfos[0],
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_HANDLER,
                    unwindCodes = new byte[514],
                    unwindCodeSlot = 514,
                    unwindHeader = new Amd64UnwindHeader { Version = 1 },
                    startLoc = new emitLocation(Group(32)),
                    endLoc = new emitLocation(Group(48)),
                },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.info.compNativeCodeSize = 48;
            compiler.info.compTotalHotCodeSize = 48;

            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(context->ReservationCount, Is.EqualTo(2));
            Assert.That(context->Reservations[1].IsFunclet, Is.True);
            Assert.That(context->Reservations[1].IsCold, Is.False);
            Assert.That(context->Allocations[1].Start, Is.EqualTo(32));
            Assert.That(context->Allocations[1].End, Is.EqualTo(48));
            Assert.That(context->Allocations[1].ColdCode, Is.EqualTo((nint)0));
            Assert.That(context->Allocations[1].Kind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_HANDLER));
        });
    }

    [Test]
    public static void UnmatchedVmPreparesHeaderWithoutCallingEe()
    {
        WithPublication((compiler, _, context) =>
        {
            compiler.info.compMatchedVM = false;
            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(compiler.funCurrentFunc().unwindCodeSlot, Is.EqualTo(510u));
            Assert.That(context->ReservationCount, Is.Zero);
            Assert.That(context->AllocationCount, Is.Zero);
        });
    }

#if DEBUG
    [TestCase(false, "0x000020")]
    [TestCase(true, "0xd1ffab1e")]
    public static void UnwindDumpUsesDiffableOffsetsWithoutChangingPublication(bool diffable, string endOffset)
    {
        WithPublication((compiler, _, context) =>
        {
            compiler.opts.dspUnwind = true;
            compiler.opts.dspDiffable = diffable;
            compiler.unwindReserve();

            var output = CodeGenLifeTransitionTests.Capture(
                () => compiler.unwindEmit(context->HotCode, context->ColdCode));

            Assert.That(output, Does.Contain("Start offset   : 0x000000"));
            Assert.That(output, Does.Contain($"End offset   : {endOffset}"));
            Assert.That(context->Allocations[0].Start, Is.Zero);
            Assert.That(context->Allocations[0].End, Is.EqualTo(32));
        });
    }

    [Test]
    public static void FakeSplitTreatsColdRootAndFuncletAsHot()
    {
        WithPublication((compiler, _, context) =>
        {
            FakeSplit(ref Globals.JitConfig) = 1;
            compiler.fgFirstColdBlock = new BasicBlock(null, null);
            compiler.fgFirstFuncletBB = new BasicBlock(null, null);
            compiler.compFuncInfos[0].endLoc = new emitLocation(Group(72));
            compiler.compFuncInfos = [
                compiler.compFuncInfos[0],
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_HANDLER,
                    unwindCodes = new byte[514],
                    unwindCodeSlot = 514,
                    unwindHeader = new Amd64UnwindHeader { Version = 1 },
                    startLoc = new emitLocation(Group(72)),
                    endLoc = new emitLocation(Group(80)),
                },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.info.compNativeCodeSize = 80;
            compiler.info.compTotalHotCodeSize = 32;

            compiler.unwindReserve();
            compiler.unwindEmit(context->HotCode, context->ColdCode);

            Assert.That(context->ReservationCount, Is.EqualTo(2));
            Assert.That(context->Reservations[1].IsCold, Is.False);
            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->Allocations[0].End, Is.EqualTo(72));
            Assert.That(context->Allocations[1].Start, Is.EqualTo(72));
            Assert.That(context->Allocations[1].End, Is.EqualTo(80));
            Assert.That(context->Allocations[1].ColdCode, Is.EqualTo((nint)0));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitFakeProcedureSplitting")]
    private static extern ref int FakeSplit(ref JitConfigValues config);
#endif

    private static insGroup Group(uint offset)
    {
        var group = new insGroup { igOffs = offset };
#if DEBUG
        group.igSelf = group;
#endif
        return group;
    }

    private delegate void PublicationAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithPublication(PublicationAction action, bool endProlog = true)
    {
        var previousConfig = Globals.JitConfig;
        try
        {
            Globals.JitConfig = default;
            UnwindPrologRecordingTests.WithProlog((compiler, codeGen) =>
            {
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.reserveUnwindInfo =
                    (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
                    (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&Reserve;
                vtable.allocUnwindInfo = &Allocate;
                var reservations = stackalloc Reservation[8];
                var allocations = stackalloc Allocation[8];
                var hotCode = stackalloc byte[80];
                var coldCode = stackalloc byte[80];
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
                compiler.info.compNativeCodeSize = 32;
                compiler.info.compTotalHotCodeSize = 32;

                if (endProlog)
                {
                    compiler.unwindEndProlog();
                    codeGen.Emitter.emitCurIG = new insGroup();
                }

                action(compiler, codeGen, &context);
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
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
        public nint UnwindBlock;
        public int Start;
        public int End;
        public int Size;
        public CorJitFuncKind Kind;
        public fixed byte Bytes[16];
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
        record->UnwindBlock = (nint)block;
        record->Start = start;
        record->End = end;
        record->Size = size;
        record->Kind = kind;
        for (var index = 0; index < Math.Min(size, 16); index++)
        {
            record->Bytes[index] = block[index];
        }
    }
}
