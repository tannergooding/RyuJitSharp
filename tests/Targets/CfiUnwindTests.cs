// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CfiUnwindTests
{
    [TestCase(REG_RAX, 0)]
    [TestCase(REG_RCX, 2)]
    [TestCase(REG_RDX, 1)]
    [TestCase(REG_RBX, 3)]
    [TestCase(REG_RSP, 7)]
    [TestCase(REG_RBP, 6)]
    [TestCase(REG_RSI, 4)]
    [TestCase(REG_RDI, 5)]
    [TestCaseSource(nameof(HighIntegerRegisters))]
    public static void FrameRegisterUsesDwarfNumber(regNumber reg, int dwarfReg)
    {
        WithCfiProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 19;
            compiler.unwindSetFrameReg(reg, 0);
            AssertCodes(compiler, new CFI_CODE(19, 1, (short)dwarfReg, 0));
        });
    }

    private static IEnumerable<TestCaseData> HighIntegerRegisters()
    {
        for (var index = 8; index <= 31; index++)
        {
            yield return new TestCaseData((regNumber)index, index);
        }
    }

    [Test]
    public static void PairedPushesPreserveInstructionOffsetAndRecordOrder()
    {
        WithCfiProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 5;
            compiler.unwindPush2(REG_RBX, REG_R12);
            CurrentSize(codeGen.Emitter) = 9;
            compiler.unwindPush(REG_RAX);
            AssertCodes(compiler,
                new CFI_CODE(5, 0, -1, 8),
                new CFI_CODE(5, 2, 3, 0),
                new CFI_CODE(5, 0, -1, 8),
                new CFI_CODE(5, 2, 12, 0),
                new CFI_CODE(9, 0, -1, 8));
        });
    }

    [TestCase(0u)]
    [TestCase(240u)]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public static void FrameOffsetPreservesSignedAdjustment(uint offset)
    {
        WithCfiProlog((compiler, _) =>
        {
            compiler.unwindSetFrameReg(REG_RBP, offset);
            var codes = compiler.funCurrentFunc().cfiCodes ?? throw new AssertionException("Missing CFI codes.");
            Assert.That(codes.Count, Is.EqualTo(offset == 0 ? 1 : 2));
            Assert.That(codes[0].CfiOpCode, Is.EqualTo(1));
            Assert.That(codes[0].DwarfReg, Is.EqualTo(6));
            if (offset != 0)
            {
                Assert.That(codes[1].CfiOpCode, Is.Zero);
                Assert.That(codes[1].Offset, Is.EqualTo(unchecked(-(int)offset)));
            }
        });
    }

    [TestCase(0u)]
    [TestCase(16u)]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public static void StackAllocationPreservesNativeIntConversion(uint size)
    {
        WithCfiProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = byte.MaxValue;
            compiler.unwindAllocStack(size);
            AssertCodes(compiler, new CFI_CODE(byte.MaxValue, 0, -1, unchecked((int)size)));
        });
    }

    [Test]
    public static void SavesIgnoreVolatileRegistersAndPreserveUnsignedOffsetBits()
    {
        WithCfiProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 11;
            compiler.unwindSaveReg(REG_RDI, 64);
            compiler.unwindSaveReg(REG_RBX, uint.MaxValue);
            AssertCodes(compiler, new CFI_CODE(11, 2, 3, -1));
        });
    }

    [Test]
    public static void MaskPushesRecordDescendingRegisters()
    {
        WithCfiProlog((compiler, _) =>
        {
            PushMask(compiler, genRegMask(REG_RBX) | genRegMask(REG_R12), false);
            AssertCodes(compiler,
                new CFI_CODE(0, 0, -1, 8),
                new CFI_CODE(0, 2, 12, 0),
                new CFI_CODE(0, 0, -1, 8),
                new CFI_CODE(0, 2, 3, 0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MaskPushesHandleEmptyMaskAndLowestRegister(bool empty)
    {
        WithCfiProlog((compiler, _) =>
        {
            PushMask(compiler, empty ? default : genRegMask(REG_RAX), false);
            if (empty)
            {
                AssertCodes(compiler);
            }
            else
            {
                AssertCodes(compiler, new CFI_CODE(0, 0, -1, 8));
            }
        });
    }

    [Test]
    public static void BeginningAnotherPrologReplacesTheVectorAndRecordsFuncletLocations()
    {
        WithCfiProlog((compiler, codeGen) =>
        {
            compiler.unwindPush(REG_RBX);
            var oldCodes = compiler.funCurrentFunc().cfiCodes;
            compiler.unwindEndProlog();
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing group.");
            var beginBlock = new BasicBlock(null, null) { bbEmitCookie = group };
            var lastBlock = new BasicBlock(null, null);
            compiler.compHndBBtab = [new EHblkDsc { ebdHndBeg = beginBlock, ebdHndLast = lastBlock }];
            compiler.compHndBBtabCount = 1;
            ref var func = ref compiler.funCurrentFunc();
            func.funKind = FuncKind.FUNC_HANDLER;
            func.funEHIndex = 0;
            compiler.unwindBegProlog();

            Assert.That(func.cfiCodes, Is.Empty);
            Assert.That(func.cfiCodes, Is.Not.SameAs(oldCodes));
            Assert.That(func.startLoc?.GetIG(), Is.SameAs(group));
            Assert.That(func.endLoc, Is.Null);
            compiler.unwindPush(REG_RBX);
            AssertCodes(compiler, new CFI_CODE(0, 0, -1, 8), new CFI_CODE(0, 2, 3, 0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PublicationReservesExactVectorSizeAndPinsCallbackBytes(bool empty)
    {
        WithCfiProlog((compiler, codeGen) =>
        {
            if (!empty)
            {
                CurrentSize(codeGen.Emitter) = 7;
                compiler.unwindSaveReg(REG_RBX, 32);
            }

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.reserveUnwindInfo =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&Reserve;
            vtable.allocUnwindInfo = &Allocate;
            var context = new PublicationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            compiler.info.compNativeCodeSize = 32;
            compiler.info.compTotalHotCodeSize = 32;
            compiler.unwindEndProlog();
            codeGen.Emitter.emitCurIG = new insGroup();

            compiler.unwindReserve();
            compiler.unwindEmit((void*)0x1000, null);

            Assert.That(context.Reservations, Is.EqualTo(1));
            Assert.That(context.ReservedBytes, Is.EqualTo(empty ? 0 : 8));
            Assert.That(context.Allocations, Is.EqualTo(1));
            Assert.That(context.AllocatedBytes, Is.EqualTo(context.ReservedBytes));
            Assert.That(context.Start, Is.Zero);
            Assert.That(context.End, Is.EqualTo(32));
            Assert.That(context.HotCode, Is.EqualTo((nint)0x1000));
            Assert.That(context.ColdCode, Is.EqualTo(IntPtr.Zero));
            Assert.That(context.NullBlock, Is.EqualTo(empty));
            if (!empty)
            {
                byte[] expected = [7, 2, 3, 0, 32, 0, 0, 0];
                Assert.That(new ReadOnlySpan<byte>(context.Bytes, 8).ToArray(), Is.EqualTo(expected));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedPublicationKeepsHotPayloadAndUsesEmptyRelativeColdRecord(bool split)
    {
        WithCfiProlog((compiler, _) =>
        {
            compiler.unwindSaveReg(REG_RBX, 32);
            ref var func = ref compiler.funCurrentFunc();
            var hotEnd = new insGroup { igOffs = 32 };
#if DEBUG
            hotEnd.igSelf = hotEnd;
#endif
            func.endLoc = new emitLocation(hotEnd);
            func.coldStartLoc = new emitLocation(hotEnd);
            compiler.fgFirstColdBlock = new BasicBlock(null, null);
            compiler.info.compNativeCodeSize = split ? 72 : 32;
            compiler.info.compTotalHotCodeSize = 32;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.allocUnwindInfo = &Allocate;
            var context = new PublicationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;

            PublishSharedCfi(compiler, in func, (void*)0x1000, split ? (void*)0x2000 : null);

            Assert.That(context.Allocations, Is.EqualTo(split ? 2 : 1));
            Assert.That(context.FirstAllocatedBytes, Is.EqualTo(8));
            Assert.That(context.FirstColdCode, Is.EqualTo(IntPtr.Zero));
            Assert.That(context.FirstEnd, Is.EqualTo(32));
            Assert.That(context.AllocatedBytes, Is.EqualTo(split ? 0 : 8));
            Assert.That(context.Start, Is.Zero);
            Assert.That(context.End, Is.EqualTo(split ? 40 : 32));
            Assert.That(context.ColdCode, Is.EqualTo(split ? (nint)0x2000 : 0));
            Assert.That(context.NullBlock, Is.EqualTo(split));
            byte[] expected = [0, 2, 3, 0, 32, 0, 0, 0];
            Assert.That(new ReadOnlySpan<byte>(context.Bytes, 8).ToArray(), Is.EqualTo(expected));
        });
    }

    private static void WithCfiProlog(Action<Compiler, CodeGen> action)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            CurrentSize(codeGen.Emitter) = 0;
            compiler.unwindBegProlog();
            action(compiler, codeGen);
        });
    }

    private static void AssertCodes(Compiler compiler, params CFI_CODE[] expected)
    {
        var actual = compiler.funCurrentFunc().cfiCodes ?? throw new AssertionException("Missing CFI codes.");
        Assert.That(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(actual)).ToArray(),
            Is.EqualTo(MemoryMarshal.AsBytes(expected.AsSpan()).ToArray()));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unwindPushPopMaskCFI")]
    private static extern void PushMask(Compiler compiler, regMaskTP mask, bool isFloat);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unwindEmitFuncCFI")]
    private static extern void PublishSharedCfi(Compiler compiler, in FuncInfoDsc func, void* hotCode, void* coldCode);

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public int Reservations;
        public int ReservedBytes;
        public int Allocations;
        public int FirstAllocatedBytes;
        public int FirstEnd;
        public nint FirstColdCode;
        public int AllocatedBytes;
        public int Start;
        public int End;
        public nint HotCode;
        public nint ColdCode;
        public bool NullBlock;
        public fixed byte Bytes[8];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Reserve(ICorJitInfo* jitInfo, byte isFunclet, byte isCold, int size)
    {
        var context = (PublicationContext*)jitInfo;
        context->Reservations++;
        context->ReservedBytes = size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* jitInfo, byte* hotCode, byte* coldCode, int start, int end,
        int size, byte* block, CorJitFuncKind kind)
    {
        var context = (PublicationContext*)jitInfo;
        context->Allocations++;
        if (context->Allocations == 1)
        {
            context->FirstAllocatedBytes = size;
            context->FirstEnd = end;
            context->FirstColdCode = (nint)coldCode;
        }
        context->AllocatedBytes = size;
        context->Start = start;
        context->End = end;
        context->HotCode = (nint)hotCode;
        context->ColdCode = (nint)coldCode;
        context->NullBlock = block == null;
        for (var index = 0; index < Math.Min(size, 8); index++)
        {
            context->Bytes[index] = block[index];
        }
    }
}
#endif
