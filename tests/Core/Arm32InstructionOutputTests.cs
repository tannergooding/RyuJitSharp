// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

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

    private sealed class OutputEmitter : Emitter
    {
        internal OutputEmitter(CodeGen codeGen, Compiler compiler) : base(codeGen)
        {
            _compiler = compiler;
        }
    }
}
#endif
