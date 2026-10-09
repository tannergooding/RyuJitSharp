// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32InstructionOutputTests
{
    [Test]
    public static void ShortThumbBranchUsesTheNativePcRelativeDisplacement()
    {
        var compiler = (Compiler)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new OutputEmitter(codeGen, compiler);
        var buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0xA5);
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 16;

        var jump = new Emitter.instrDescJmp();
        jump.idIns(INS_b);
        jump.idInsFmt(IF_T1_M);
        jump.idOpSize(EA_4BYTE);
        jump.idInsOpt(INS_OPTS_NONE);
        jump.idSetIsBound();
        jump.idjShort = true;
        jump.idjTargetIG = new insGroup { igOffs = 8 };

        var end = emitter.emitOutputLJ(null, buffer, jump);

        Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 2)));
        Assert.That(buffer[0], Is.EqualTo(0x02));
        Assert.That(buffer[1], Is.EqualTo(0xE0));
        Assert.That(buffer[2], Is.EqualTo(0xA5));
    }

    [Test]
    public static void LongThumbBranchUsesTheNativeJ1J2DisplacementEncoding()
    {
        var compiler = (Compiler)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new OutputEmitter(codeGen, compiler);
        var buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0xA5);
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 16;

        var jump = new Emitter.instrDescJmp();
        jump.idIns(INS_b);
        jump.idInsFmt(IF_T2_J2);
        jump.idOpSize(EA_4BYTE);
        jump.idInsOpt(INS_OPTS_NONE);
        jump.idSetIsBound();
        jump.idjTargetIG = new insGroup { igOffs = 8 };

        var end = emitter.emitOutputLJ(null, buffer, jump);

        Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 4)));
        Assert.That(buffer[0], Is.EqualTo(0x00));
        Assert.That(buffer[1], Is.EqualTo(0xF0));
        Assert.That(buffer[2], Is.EqualTo(0x02));
        Assert.That(buffer[3], Is.EqualTo(0xB8));
        Assert.That(buffer[4], Is.EqualTo(0xA5));
    }

    [Test]
    public static void BackwardLongThumbBranchPreservesUnsignedDisplacementBits()
    {
        var compiler = (Compiler)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new OutputEmitter(codeGen, compiler);
        var buffer = stackalloc byte[4096];
        new Span<byte>(buffer, 4096).Fill(0xA5);
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 4096;

        var jump = new Emitter.instrDescJmp();
        jump.idIns(INS_b);
        jump.idInsFmt(IF_T2_J2);
        jump.idOpSize(EA_4BYTE);
        jump.idInsOpt(INS_OPTS_NONE);
        jump.idSetIsBound();
        jump.idjTargetIG = new insGroup { igOffs = 0 };

        var branch = buffer + 2048;
        var end = emitter.emitOutputLJ(null, branch, jump);

        Assert.That((nuint)end, Is.EqualTo((nuint)(branch + 4)));
        Assert.That(branch[0], Is.EqualTo(0xFF));
        Assert.That(branch[1], Is.EqualTo(0xF7));
        Assert.That(branch[2], Is.EqualTo(0xFE));
        Assert.That(branch[3], Is.EqualTo(0xBB));
        Assert.That(branch[4], Is.EqualTo(0xA5));
    }

    [Test]
    public static void InstructionOutputDispatchRoutesBoundThumbBranches()
    {
        var compiler = (Compiler)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new OutputEmitter(codeGen, compiler);
        var buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0xA5);
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 16;

        var jump = new Emitter.instrDescJmp();
        jump.idIns(INS_b);
        jump.idInsFmt(IF_T1_M);
        jump.idOpSize(EA_4BYTE);
        jump.idInsOpt(INS_OPTS_NONE);
        jump.idSetIsBound();
        jump.idjShort = true;
        jump.idjTargetIG = new insGroup { igOffs = 8 };

        var cursor = buffer;
        var descriptorSize = emitter.emitOutputInstr(new insGroup(), jump, &cursor);

        Assert.That(descriptorSize, Is.EqualTo((nuint)jump.NativeLogicalSize));
        Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 2)));
        Assert.That(buffer[0], Is.EqualTo(0x02));
        Assert.That(buffer[1], Is.EqualTo(0xE0));
        Assert.That(buffer[2], Is.EqualTo(0xA5));
    }

    [Test]
    public static void InstructionOutputDispatchEncodesThumbZeroOperandInstruction()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns(INS_nop);
            var id = LastInstruction(emitter)
                ?? throw new AssertionException("Missing Thumb zero-operand instruction.");
            var buffer = stackalloc byte[8];
            new Span<byte>(buffer, 8).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 8;

            var cursor = buffer;
            var descriptorSize = emitter.emitOutputInstr(new insGroup(), id, &cursor);

            Assert.That(descriptorSize, Is.EqualTo((nuint)id.NativeLogicalSize));
            Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 2)));
            Assert.That(buffer[0], Is.EqualTo(0x00));
            Assert.That(buffer[1], Is.EqualTo(0xBF));
            Assert.That(buffer[2], Is.EqualTo(0xA5));
        });
    }

    [TestCase(INS_add, IF_T1_D0, 0, "add     r0, r1")]
    [TestCase(INS_ldr, IF_T2_H2, -4, "ldr     [r0-0x04]")]
    [TestCase(INS_b, IF_T1_B, 10, "b       ge")]
    public static void InstructionDisplayFormatsArm32Operands(
        instruction ins, Emitter.insFormat format, int immediate, string expected)
    {
        var output = CaptureDisplay((_, emitter) =>
        {
            var id = DisplayEmitter.Descriptor(ins, format, immediate);
            emitter.emitDispIns(id, false, false, true);
        });

        Assert.That(output, Does.Contain(expected));
    }

    [Test]
    public static void DiffableInstructionDisplayUsesTheNativeImmediatePlaceholder()
    {
        var output = CaptureDisplay((compiler, emitter) =>
        {
            compiler.opts.disDiffable = true;
            var id = DisplayEmitter.Descriptor(INS_movw, IF_T2_N, 0x1234);
            emitter.emitDispIns(id, false, false, true);
        });

        Assert.That(output, Does.Contain("movw    r0, 0xd1ff"));
    }

    [TestCase(4, 8)]
    [TestCase(-4, 0)]
    public static void InstructionDisplayReadsEncodedBytesFromTheWritableAlias(int writableOffset, int writableIndex)
    {
        var output = CaptureDisplay((compiler, emitter) =>
        {
            compiler.opts.disCodeBytes = true;
            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            buffer[writableIndex] = 0x34;
            buffer[writableIndex + 1] = 0x12;
            var code = buffer + 4;
            emitter.emitCodeBlock = code;
            emitter.emitTotalHotCodeSize = 2;
            emitter.writeableOffset = writableOffset;
            var id = DisplayEmitter.Descriptor(INS_add, IF_T1_D0, 0);

            emitter.emitDispIns(id, false, false, true, code: code, size: 2);
        });

        Assert.That(output, Does.Contain("1234"));
    }

    [Test]
    public static void ColdInstructionDisplayUsesCombinedOffsetAndWritableAlias()
    {
        var output = CaptureDisplay((compiler, emitter) =>
        {
            compiler.opts.disCodeBytes = true;
            var buffer = stackalloc byte[64];
            new Span<byte>(buffer, 64).Fill(0xA5);
            var coldCode = buffer + 16;
            var writableCode = coldCode + 16;
            writableCode[1] = 0x34;
            writableCode[2] = 0x12;
            emitter.emitCodeBlock = buffer;
            emitter.emitColdCodeBlock = coldCode;
            emitter.emitTotalHotCodeSize = 8;
            emitter.emitTotalColdCodeSize = 8;
            emitter.writeableOffset = 16;
            FirstColdGroup(emitter) = new insGroup();

            var code = coldCode + 1;
            var offset = CurrentCodeOffset(emitter, code);
            var id = DisplayEmitter.Descriptor(INS_add, IF_T1_D0, 0);

            emitter.emitDispIns(id, false, true, true, offset, code, 2);
        });

        Assert.That(output, Does.StartWith("000009  1234     "));
        Assert.That(output, Does.Contain("add     r0, r1"));
    }

    [Test]
    public static void LargeConditionalBranchDisplaysBothSyntheticInstructions()
    {
        var target = new insGroup { igOffs = 16 };
        var output = CaptureDisplay((compiler, emitter) =>
        {
            var jump = new Emitter.instrDescJmp();
            jump.idIns(INS_beq);
            jump.idInsFmt(IF_LARGEJMP);
            jump.idOpSize(EA_4BYTE);
            jump.idInsOpt(INS_OPTS_NONE);
            jump.idSetIsBound();
            jump.idjTargetIG = target;
#if DEBUG
            jump.idDebugOnlyInfo(new Emitter.instrDescDebugInfo());
#endif
            compiler.opts.disCodeBytes = true;
            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            buffer[0] = 0x00;
            buffer[1] = 0xD1;
            buffer[2] = 0x00;
            buffer[3] = 0xF0;
            buffer[4] = 0x00;
            buffer[5] = 0xB8;
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 16;

            emitter.emitDispIns(jump, false, false, true, code: buffer, size: 6);
            Assert.That(jump.idIns(), Is.EqualTo(INS_beq));
            Assert.That(jump.idInsFmt(), Is.EqualTo(IF_LARGEJMP));
            Assert.That(jump.idIsBound(), Is.True);
            Assert.That(jump.idjTargetIG, Is.SameAs(target));
        });

        Assert.That(output, Does.Contain("SHORT pc+1 instructions"));
        Assert.That(output, Does.Contain("b       G_M000_IG"));
        Assert.That(output, Does.Contain("D100"));
        Assert.That(output, Does.Contain("F000 B800"));
        Assert.That(output.Split('\n'), Has.Length.EqualTo(3));
    }

    private static string CaptureDisplay(Action<Compiler, DisplayEmitter> display)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new DisplayEmitter(codeGen, compiler);
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            display(compiler, emitter);
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Emitter.instrDesc? LastInstruction(Emitter emitter) => Last(emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFirstColdIG")]
    private static extern ref insGroup? FirstColdGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitCurCodeOffs")]
    private static extern uint CurrentCodeOffset(Emitter emitter, byte* address);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? Last(Emitter emitter);

    private sealed class OutputEmitter : Emitter
    {
        internal OutputEmitter(CodeGen codeGen, Compiler compiler) : base(codeGen)
        {
            _compiler = compiler;
        }
    }

    private sealed class DisplayEmitter : Emitter
    {
        internal DisplayEmitter(CodeGen codeGen, Compiler compiler) : base(codeGen)
        {
            _compiler = compiler;
        }

        internal static Emitter.instrDesc Descriptor(instruction ins, Emitter.insFormat format, int immediate)
        {
            var id = new Emitter.instrDescBasic();
            id.idIns(ins);
            id.idInsFmt(format);
            id.idOpSize(EA_4BYTE);
            id.idInsOpt(INS_OPTS_NONE);
            id.idReg1(REG_R0);
            id.idReg2(REG_R1);
            id.idSmallCns(immediate);
#if DEBUG
            id.idDebugOnlyInfo(new Emitter.instrDescDebugInfo());
#endif

            return id;
        }
    }
}
#endif
