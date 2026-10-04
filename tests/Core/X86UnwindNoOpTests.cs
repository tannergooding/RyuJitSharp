// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

#if TARGET_X86
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class X86UnwindNoOpTests
{
    [Test]
    public static void X86PrologAndEpilogRecordingRemainsANoOp()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        compiler.unwindBegProlog();
        compiler.unwindEndProlog();
        compiler.unwindBegEpilog();
        compiler.unwindEndEpilog();
        compiler.unwindPush(default);
        compiler.unwindAllocStack(0);
        compiler.unwindSetFrameReg(default, 0);
        compiler.unwindSaveReg(default, 0);
    }

    [Test]
    public static void EmptyMethodReservesAndPublishesX86FunctionLength()
    {
        WithPublication((compiler, context, hotCode, _) =>
        {
            compiler.unwindReserve();
            compiler.unwindEmit(hotCode, null);

            Assert.That(context->ReservationCount, Is.EqualTo(1));
            Assert.That(context->Reservations[0].IsFunclet, Is.False);
            Assert.That(context->Reservations[0].IsCold, Is.False);
            Assert.That(context->Reservations[0].Size, Is.EqualTo(sizeof(uint)));
            Assert.That(context->AllocationCount, Is.EqualTo(1));
            Assert.That(context->Allocations[0].HotCode, Is.EqualTo((nint)hotCode));
            Assert.That(context->Allocations[0].ColdCode, Is.Zero);
            Assert.That(context->Allocations[0].Start, Is.Zero);
            Assert.That(context->Allocations[0].End, Is.EqualTo(32));
            Assert.That(context->Allocations[0].Size, Is.EqualTo(sizeof(uint)));
            Assert.That(context->Allocations[0].FuncKind, Is.EqualTo(CorJitFuncKind.CORJIT_FUNC_ROOT));
            Assert.That(context->Allocations[0].FunctionLength, Is.EqualTo(32u));
        });
    }

    [Test]
    public static void SplitRootPublishesHotAndColdFunctionLengths()
    {
        WithPublication((compiler, context, hotCode, coldCode) =>
        {
            var coldStart = new BasicBlock(null, null) { bbEmitCookie = Group(32) };
            var coldEnd = new BasicBlock(null, null) { bbEmitCookie = Group(64) };
            compiler.fgFirstColdBlock = coldStart;
            compiler.fgFirstFuncletBB = coldEnd;
            compiler.info.compNativeCodeSize = 64;
            compiler.info.compTotalHotCodeSize = 32;

            compiler.unwindReserve();
            compiler.unwindEmit(hotCode, coldCode);

            Assert.That(context->ReservationCount, Is.EqualTo(2));
            Assert.That(context->Reservations[0].IsCold, Is.False);
            Assert.That(context->Reservations[0].Size, Is.EqualTo(sizeof(uint)));
            Assert.That(context->Reservations[1].IsCold, Is.True);
            Assert.That(context->Reservations[1].Size, Is.EqualTo(sizeof(uint)));
            Assert.That(context->AllocationCount, Is.EqualTo(2));
            Assert.That(context->Allocations[0].HotCode, Is.EqualTo((nint)hotCode));
            Assert.That(context->Allocations[0].ColdCode, Is.Zero);
            Assert.That(context->Allocations[0].Start, Is.Zero);
            Assert.That(context->Allocations[0].End, Is.EqualTo(32));
            Assert.That(context->Allocations[0].FunctionLength, Is.EqualTo(32u));
            Assert.That(context->Allocations[1].HotCode, Is.EqualTo((nint)hotCode));
            Assert.That(context->Allocations[1].ColdCode, Is.EqualTo((nint)coldCode));
            Assert.That(context->Allocations[1].Start, Is.Zero);
            Assert.That(context->Allocations[1].End, Is.EqualTo(32));
            Assert.That(context->Allocations[1].FunctionLength, Is.EqualTo(32u));
        });
    }

#if DEBUG
    [Test]
    public static void FakeSplitKeepsColdRootInHotSection()
    {
        WithPublication((compiler, context, hotCode, coldCode) =>
        {
            FakeSplit(ref Globals.JitConfig) = 1;
            var coldStart = new BasicBlock(null, null) { bbEmitCookie = Group(32) };
            var coldEnd = new BasicBlock(null, null) { bbEmitCookie = Group(64) };
            compiler.fgFirstColdBlock = coldStart;
            compiler.fgFirstFuncletBB = coldEnd;
            compiler.info.compNativeCodeSize = 64;
            compiler.info.compTotalHotCodeSize = 32;

            compiler.unwindReserve();
            compiler.unwindEmit(hotCode, coldCode);

            Assert.That(context->ReservationCount, Is.EqualTo(1));
            Assert.That(context->Reservations[0].IsCold, Is.False);
            Assert.That(context->AllocationCount, Is.EqualTo(1));
            Assert.That(context->Allocations[0].ColdCode, Is.Zero);
            Assert.That(context->Allocations[0].Start, Is.Zero);
            Assert.That(context->Allocations[0].End, Is.EqualTo(64));
            Assert.That(context->Allocations[0].FunctionLength, Is.EqualTo(64u));
        });
    }
#endif

    [Test]
    public static void FuncKindValuesMatchEeUnwindContract()
    {
        Assert.That((int)FuncKind.FUNC_ROOT, Is.EqualTo((int)CorJitFuncKind.CORJIT_FUNC_ROOT));
        Assert.That((int)FuncKind.FUNC_HANDLER, Is.EqualTo((int)CorJitFuncKind.CORJIT_FUNC_HANDLER));
        Assert.That((int)FuncKind.FUNC_FILTER, Is.EqualTo((int)CorJitFuncKind.CORJIT_FUNC_FILTER));
    }

    private static void WithPublication(PublicationAction action)
    {
        var previousConfig = Globals.JitConfig;
        try
        {
            Globals.JitConfig = default;

            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
            EmitterField(ref codeGen) = new Emitter(codeGen);
            compiler.codeGen = codeGen;
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.info.compNativeCodeSize = 32;
            compiler.info.compTotalHotCodeSize = 32;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.reserveUnwindInfo =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&Reserve;
            vtable.allocUnwindInfo = &Allocate;

            var reservations = stackalloc Reservation[4];
            var allocations = stackalloc Allocation[4];
            byte* hotCode = stackalloc byte[1];
            byte* coldCode = stackalloc byte[1];
            var context = new PublicationContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Reservations = reservations,
                Allocations = allocations,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;

            action(compiler, &context, hotCode, coldCode);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    private delegate void PublicationAction(
        Compiler compiler, PublicationContext* context, byte* hotCode, byte* coldCode);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitFakeProcedureSplitting")]
    private static extern ref int FakeSplit(ref JitConfigValues config);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cgEmitter")]
    private static extern ref Emitter EmitterField(ref CodeGen codeGen);

    private static insGroup Group(uint offset)
    {
        var group = new insGroup { igOffs = offset };
#if DEBUG
        group.igSelf = group;
#endif
        return group;
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
        context->Allocations[context->AllocationCount++] = new Allocation
        {
            HotCode = (nint)hotCode,
            ColdCode = (nint)coldCode,
            Start = start,
            End = end,
            Size = size,
            FuncKind = kind,
            FunctionLength = *(uint*)block,
        };
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public Reservation* Reservations;
        public Allocation* Allocations;
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
        public CorJitFuncKind FuncKind;
        public uint FunctionLength;
    }
}
#endif
