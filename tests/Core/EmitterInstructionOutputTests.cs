// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionOutputTests
{
    [TestCase(INS_ret, EA_8BYTE, "C3")]
    [TestCase(INS_cdq, EA_4BYTE, "99")]
    [TestCase(INS_cdq, EA_8BYTE, "4899")]
    [TestCase(INS_cwde, EA_8BYTE, "4898")]
    [TestCase(INS_int3, EA_4BYTE, "CC")]
    public static void NoOperandDispatchPreservesOpcodeAndImplicitWidths(instruction ins, emitAttr attr, string hex)
    {
        WithOutput((_, emitter) =>
        {
            if (ins is INS_cdq or INS_cwde)
            {
                emitter.emitIns(ins, attr);
            }
            else
            {
                emitter.emitIns(ins);
            }
        }, hex);
    }

    [TestCase(1u, "90")]
    [TestCase(5u, "0F1F440000")]
    [TestCase(15u, "0F1F80000000000F1F840000000000")]
    public static void NopDispatchReturnsTheRecordedDescriptorSize(uint size, string hex)
    {
        WithOutput((_, emitter) => emitter.emitIns_Nop(size), hex);
    }

    [TestCase(INS_inc, EA_8BYTE, REG_RAX, "48FFC0")]
    [TestCase(INS_push, EA_8BYTE, REG_R9, "4151")]
    [TestCase(INS_setne, EA_1BYTE, REG_RAX, "0F95C0")]
    public static void RegisterDispatchUsesSmallDescriptors(instruction ins, emitAttr attr, regNumber reg, string hex)
    {
        WithOutput((_, emitter) => emitter.emitIns_R(ins, attr, reg), hex);
    }

    [TestCase(INS_mov, EA_8BYTE, 0x1122334455667788L, "49B98877665544332211")]
    [TestCase(INS_add, EA_8BYTE, -1L, "4983C1FF")]
    public static void RegisterImmediateDispatchUsesNativeConstantLayouts(instruction ins, emitAttr attr, long value, string hex)
    {
        WithOutput((_, emitter) => emitter.emitIns_R_I(ins, attr, REG_R9, (nint)value), hex);
    }

    [Test]
    public static void RegisterPairAndThreeRegisterDispatchRetainSourceFields()
    {
        WithOutput((compiler, emitter) =>
            _ = emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RAX, REG_RCX, canSkip: false), "488BC1");
        WithOutput((_, emitter) =>
        {
            emitter.UseVexEncodings = true;
            emitter.emitIns_R_R_R(INS_addps, EA_16BYTE, REG_XMM1, REG_XMM2, REG_XMM3);
        }, "C5E858CB");
    }

    [TestCase(false, "660FC2CA03")]
    [TestCase(true, "C5F1C2CA03")]
    public static void RegisterImmediateSimdDispatchPreservesModRmAndVvvv(bool vex, string hex)
    {
        WithOutput((_, emitter) =>
        {
            emitter.UseVexEncodings = vex;
            emitter.emitIns_R_R_I(INS_cmppd, EA_16BYTE, REG_XMM1, REG_XMM2, 3);
        }, hex);
    }

    [TestCase(false, "488B4808")]
    [TestCase(true, "48894808")]
    public static void AddressModeDispatchPlacesTheRegisterField(bool store, string hex)
    {
        WithOutput((_, emitter) =>
        {
            if (store)
            {
                emitter.emitIns_AR_R(INS_mov, EA_8BYTE, REG_RCX, REG_RAX, 8);
            }
            else
            {
                emitter.emitIns_R_AR(INS_mov, EA_8BYTE, REG_RCX, REG_RAX, 8);
            }
        }, hex);
    }

    [TestCase(false, "488B4DF0")]
    [TestCase(true, "48894DF0")]
    public static void StackDispatchPlacesTheRegisterField(bool store, string hex)
    {
        WithOutput((compiler, emitter) =>
        {
            compiler.lvaTable[0].lvOnFrame = true;
            compiler.lvaTable[0].lvFramePointerBased = true;
            compiler.lvaTable[0].StackOffset = -16;
            if (store)
            {
                emitter.emitIns_S_R(INS_mov, EA_8BYTE, REG_RCX, 0, 0);
            }
            else
            {
                emitter.emitIns_R_S(INS_mov, EA_8BYTE, REG_RCX, 0, 0);
            }
        }, hex);
    }

    [TestCase(false, "64488B0510000000")]
    [TestCase(true, "65488B042510000000")]
    public static void StaticDispatchPreservesSegmentPrefixes(bool gs, string hex)
    {
        WithOutput((_, emitter) =>
            emitter.emitIns_R_C(INS_mov, EA_8BYTE, REG_RAX, gs ? FLD_GLOBAL_GS : FLD_GLOBAL_FS, 16), hex);
    }

    [Test]
    public static void ThreeOperandMemoryAndImmediateFormatsReachTheRightEncoders()
    {
        WithOutput((compiler, emitter) =>
        {
            compiler.lvaTable[0].lvOnFrame = true;
            compiler.lvaTable[0].lvFramePointerBased = true;
            compiler.lvaTable[0].StackOffset = -16;
            emitter.UseVexEncodings = true;
            emitter.emitIns_R_R_S(INS_addps, EA_16BYTE, REG_XMM1, REG_XMM2, 0, 0);
        }, "C5E8584DF0");
        WithOutput((_, emitter) =>
        {
            emitter.UseVexEncodings = true;
            emitter.emitIns_R_R_R_I(INS_cmpps, EA_16BYTE, REG_XMM1, REG_XMM2, REG_XMM3, 3);
        }, "C5E8C2CB03");
    }

    [TestCase(false, "")]
    [TestCase(true, "90")]
    public static void RemovedJumpsEmitOnlyRequiredEpilogPaddingAndRestoreDisplayedDescriptors(bool afterCall, string hex)
    {
        WithOutput((compiler, emitter) =>
        {
            var target = new BasicBlock(null, null);
            target.SetFlags(BBF_HAS_LABEL);
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing jump.");
            JumpView.SetRemoved(descriptor, afterCall);
            compiler.opts.disAsm = true;
        }, hex);
    }

    [Test]
    public static void InstructionsBeforeTheLastAlignedGroupRetainOverestimatedSpace()
    {
        WithOutput((_, emitter) =>
        {
            emitter.emitIns_R(INS_inc, EA_8BYTE, REG_RAX);
            var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing instruction.");
            descriptor.idCodeSize(descriptor.idCodeSize() + 2);
            LastAlignedGroup(emitter) = emitter.emitCurIG;
        }, "48FFC06690");
    }

    [TestCase(32)]
    [TestCase(-32)]
    public static void InstructionHexDisplayReadsTheWritableAlias(int aliasOffset)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            WithOutput((compiler, emitter) =>
            {
                emitter.emitIns_R(INS_inc, EA_8BYTE, REG_RAX);
                compiler.opts.disAsm = true;
                compiler.opts.disCodeBytes = true;
            }, "48FFC0", aliasOffset);
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Does.Contain("48FFC0"));
    }

    private static void WithOutput(Action<Compiler, Emitter> record, string hex, int aliasOffset = 32)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = false;
            emitter.UseEvexEncodings = false;
            SyncThisRegister(emitter) = REG_NA;
            record(compiler, emitter);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
            var group = emitter.emitCurIG ?? throw new AssertionException("No current instruction group.");
            var buffer = stackalloc byte[64];
            new Span<byte>(buffer, 64).Fill(0xA5);
            var executable = aliasOffset < 0 ? buffer + 32 : buffer;
            var writable = aliasOffset < 0 ? buffer : buffer + 32;
            emitter.emitCodeBlock = executable;
            emitter.emitTotalHotCodeSize = 32;
            emitter.writeableOffset = aliasOffset;
            var end = executable;
            var originalInstruction = id.idIns();
            var originalFormat = id.idInsFmt();
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var descriptorSize = emitter.emitOutputInstr(group, id, &end);
            var length = checked((int)(end - executable));

            Assert.That(descriptorSize, Is.EqualTo((nuint)id.NativeLogicalSize));
            Assert.That(id.idIns(), Is.EqualTo(originalInstruction));
            Assert.That(id.idInsFmt(), Is.EqualTo(originalFormat));
            Assert.That(length, Is.EqualTo((int)id.idCodeSize()));
            Assert.That(new ReadOnlySpan<byte>(writable, length).ToArray(), Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That(executable[0], Is.EqualTo(0xA5));
            Assert.That(writable[length], Is.EqualTo(0xA5));
        });
    }

    private abstract class JumpView(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static void SetRemoved(instrDesc descriptor, bool afterCall)
        {
            var jump = (instrDescJmp)descriptor;
            jump.idjIsAfterCallBeforeEpilog = afterCall;
            jump.idCodeSize(afterCall ? 1u : 0u);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisRegister(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastAlignedIg")]
    private static extern ref insGroup? LastAlignedGroup(Emitter emitter);
}
