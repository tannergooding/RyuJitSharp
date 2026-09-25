// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class UnwindPrologRecordingTests
{
    [Test]
    public static void BeginningPrologInitializesFunctionLocationsAndHeader()
    {
        WithProlog((compiler, codeGen) =>
        {
            var func = compiler.funCurrentFunc();

            Assert.That(func.startLoc, Is.Null);
            Assert.That(func.endLoc, Is.Null);
            Assert.That(func.coldStartLoc, Is.Null);
            Assert.That(func.unwindCodeSlot, Is.EqualTo(514u));
            Assert.That(func.unwindCodes, Has.Length.EqualTo(514));
            Assert.That(func.unwindHeader.Version, Is.EqualTo(1));
            Assert.That(func.unwindHeader.Flags, Is.Zero);
            Assert.That(func.unwindHeader.CountOfUnwindCodes, Is.Zero);
            Assert.That(func.unwindHeader.FrameRegister, Is.Zero);
            Assert.That(func.unwindHeader.FrameOffset, Is.Zero);
            Assert.That(Unsafe.SizeOf<Amd64UnwindHeader>(), Is.EqualTo(4));
            byte[] expectedHeader = [1, 0, 0, 0];
            Assert.That(HeaderBytes(in func), Is.EqualTo(expectedHeader));
            Assert.That(compiler.compGeneratingUnwindProlog, Is.True);

            compiler.unwindEndProlog();
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);
        });
    }

    [TestCase(8u, 2u)]
    [TestCase(128u, 2u)]
    [TestCase(136u, 4u)]
    [TestCase(0x7FFF8u, 4u)]
    [TestCase(0x80000u, 6u)]
    [TestCase(0xFFFFFFF8u, 6u)]
    public static void AllocationUsesNativeBoundaryEncodings(uint size, uint codeBytes)
    {
        WithProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 23;
            compiler.unwindAllocStack(size);

            var func = compiler.funCurrentFunc();
            byte[] expected = size switch
            {
                8 => [23, 0x02],
                128 => [23, 0xF2],
                136 => [23, 0x01, 17, 0],
                0x7FFF8 => [23, 0x01, 0xFF, 0xFF],
                0x80000 => [23, 0x11, 0, 0, 8, 0],
                0xFFFFFFF8 => [23, 0x11, 0xF8, 0xFF, 0xFF, 0xFF],
                _ => throw new AssertionException("Unexpected stack allocation size."),
            };
            Assert.That(func.unwindCodeSlot, Is.EqualTo(514u - codeBytes));
            Assert.That(Codes(in func), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void PushesRecordCalleeSavedAndVolatileRegistersInReverseStorageOrder()
    {
        WithProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 2;
            compiler.unwindPush(REG_R12);
            CurrentSize(codeGen.Emitter) = 5;
            compiler.unwindPush2(REG_RAX, REG_RBP);

            var func = compiler.funCurrentFunc();
            byte[] expected = [5, 0x50, 5, 0x02, 2, 0xC0];
            Assert.That(Codes(in func), Is.EqualTo(expected));
            Assert.That(func.unwindCodeSlot, Is.EqualTo(508u));
        });
    }

    [TestCase(0u, (byte)0x00)]
    [TestCase(240u, (byte)0x0F)]
    public static void FrameRegisterUsesScaledOffsetAndSetFrameCode(uint offset, byte expectedOffset)
    {
        WithProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 7;
            compiler.unwindSetFrameReg(REG_RBP, offset);

            var func = compiler.funCurrentFunc();
            byte[] expected = [7, 3];
            Assert.That(Codes(in func), Is.EqualTo(expected));
            Assert.That(func.unwindHeader.FrameRegister, Is.EqualTo((byte)REG_RBP));
            Assert.That(func.unwindHeader.FrameOffset, Is.EqualTo(expectedOffset));
            byte[] expectedHeader = [1, 0, 0, (byte)(((int)expectedOffset << 4) | (int)REG_RBP)];
            Assert.That(HeaderBytes(in func), Is.EqualTo(expectedHeader));
        });
    }

    [TestCase(REG_R12, 0x7FFF8u)]
    [TestCase(REG_R12, 0x80000u)]
    [TestCase(REG_XMM6, 0x7FFF0u)]
    [TestCase(REG_XMM6, 0x80000u)]
    public static void SavedRegistersUseNearAndFarWidths(regNumber reg, uint offset)
    {
        WithProlog((compiler, codeGen) =>
        {
            CurrentSize(codeGen.Emitter) = 19;
            compiler.unwindSaveReg(reg, offset);

            var func = compiler.funCurrentFunc();
            byte[] expected = (reg, offset) switch
            {
                (REG_R12, 0x7FFF8) => [19, 0xC4, 0xFF, 0xFF],
                (REG_R12, 0x80000) => [19, 0xC5, 0, 0, 8, 0],
                (REG_XMM6, 0x7FFF0) => [19, 0x68, 0xFF, 0x7F],
                (REG_XMM6, 0x80000) => [19, 0x69, 0, 0, 8, 0],
                _ => throw new AssertionException("Unexpected saved register."),
            };
            Assert.That(Codes(in func), Is.EqualTo(expected));
            Assert.That(func.unwindCodeSlot, Is.EqualTo(514u - (uint)expected.Length));
        });
    }

    [Test]
    public static void SavingVolatileRegisterRecordsNoUnwindCode()
    {
        WithProlog((compiler, _) =>
        {
            compiler.unwindSaveReg(REG_R11, 64);
            var func = compiler.funCurrentFunc();
            Assert.That(func.unwindCodeSlot, Is.EqualTo(514u));
        });
    }

    [Test]
    public static void PrologOffsetAccumulatesEarlierGroupsInSameProlog()
    {
        WithProlog((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var first = emitter.emitGetFirstPrologIG();
            var second = first.igNext ?? throw new AssertionException("Missing second group.");
            first.igSize = 9;
            second.igFlags |= InsGroupFlags.Prolog;
            emitter.emitCurIG = second;
            CurrentSize(emitter) = 4;

            compiler.unwindAllocStack(8);

            var func = compiler.funCurrentFunc();
            byte[] expected = [13, 2];
            Assert.That(Codes(in func), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void HotAndColdFunctionLocationsAreRecordedWithoutFabricatingOffsets()
    {
        WithProlog((compiler, _) =>
        {
            var coldGroup = new insGroup();
            var funcletGroup = new insGroup();
            var coldBlock = new BasicBlock(null, null) { bbEmitCookie = coldGroup };
            var funcletBlock = new BasicBlock(null, null) { bbEmitCookie = funcletGroup };
            compiler.fgFirstColdBlock = coldBlock;
            compiler.fgFirstFuncletBB = funcletBlock;
            compiler.unwindEndProlog();
            compiler.unwindBegProlog();

            var func = compiler.funCurrentFunc();
#if DEBUG
            var hotEnd = JitConfig.JitFakeProcedureSplitting != 0 ? funcletGroup : coldGroup;
#else
            var hotEnd = coldGroup;
#endif
            Assert.That(func.endLoc?.GetIG(), Is.SameAs(hotEnd));
            Assert.That(func.coldStartLoc?.GetIG(), Is.SameAs(coldGroup));
            Assert.That(func.coldEndLoc?.GetIG(), Is.SameAs(funcletGroup));
        });
    }

    [Test]
    public static void FuncletLocationsUseHandlerRangeAndDoNotRelativeOffsetRoot()
    {
        WithProlog((compiler, codeGen) =>
        {
            var beginGroup = codeGen.Emitter.emitGetFirstPrologIG();
            var endGroup = new insGroup();
            var beginBlock = new BasicBlock(null, null) { bbEmitCookie = beginGroup };
            var endBlock = new BasicBlock(null, null) { bbEmitCookie = endGroup };
            var lastBlock = new BasicBlock(null, null) { Next = endBlock };
            compiler.compHndBBtab = [new EHblkDsc { ebdHndBeg = beginBlock, ebdHndLast = lastBlock }];
            compiler.compHndBBtabCount = 1;
            compiler.funCurrentFunc().funKind = FuncKind.FUNC_HANDLER;
            compiler.funCurrentFunc().funEHIndex = 0;

            compiler.unwindEndProlog();
            compiler.unwindBegProlog();

            var func = compiler.funCurrentFunc();
            Assert.That(func.startLoc?.GetIG(), Is.SameAs(beginGroup));
            Assert.That(func.endLoc?.GetIG(), Is.SameAs(endGroup));
            compiler.unwindPush(REG_R12);
            byte[] expected = [0, 0xC0];
            Assert.That(Codes(in compiler.funCurrentFunc()), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void EpilogStateTransitionsRequireEpilogGroup()
    {
        WithProlog((compiler, codeGen) =>
        {
            var epilog = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing group.");
            epilog.igFlags = InsGroupFlags.Epilog;
            compiler.unwindBegEpilog();
            Assert.That(compiler.compGeneratingUnwindEpilog, Is.True);
            compiler.unwindEndEpilog();
            Assert.That(compiler.compGeneratingUnwindEpilog, Is.False);
        });
    }

    private static byte[] Codes(in FuncInfoDsc func)
    {
        var storage = func.unwindCodes ?? throw new AssertionException("Unwind storage was not initialized.");
        return storage.AsSpan((int)func.unwindCodeSlot).ToArray();
    }

    private static byte[] HeaderBytes(in FuncInfoDsc func)
    {
        var header = func.unwindHeader;
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref header, 1)).ToArray();
    }

    internal static void WithProlog(Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            CurrentSize(codeGen.Emitter) = 0;
            compiler.unwindBegProlog();
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);
}
