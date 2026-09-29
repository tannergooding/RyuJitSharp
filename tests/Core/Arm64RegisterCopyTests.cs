// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64RegisterCopyTests
{
    [TestCase(TYP_INT, INS_mov)]
    [TestCase(TYP_REF, INS_mov)]
    [TestCase(TYP_BYREF, INS_mov)]
    [TestCase(TYP_FLOAT, INS_fmov)]
    [TestCase(TYP_DOUBLE, INS_fmov)]
    [TestCase(TYP_SIMD8, INS_mov)]
    [TestCase(TYP_SIMD12, INS_mov)]
    [TestCase(TYP_SIMD16, INS_mov)]
    [TestCase(TYP_MASK, INS_sve_mov)]
    public static void DestinationTypeSelectsTheNativeCopyInstruction(var_types type, instruction expected)
    {
        Assert.That(CreateCodeGen().ins_Copy(type), Is.EqualTo(expected));
    }

    [TestCase(REG_R1, TYP_INT, INS_mov)]
    [TestCase(REG_V1, TYP_INT, INS_mov)]
    [TestCase(REG_R1, TYP_FLOAT, INS_fmov)]
    [TestCase(REG_V1, TYP_FLOAT, INS_fmov)]
    [TestCase(REG_R1, TYP_DOUBLE, INS_fmov)]
    [TestCase(REG_V1, TYP_DOUBLE, INS_fmov)]
    [TestCase(REG_V1, TYP_SIMD16, INS_mov)]
    [TestCase(REG_R1, TYP_MASK, INS_sve_mov)]
    [TestCase(REG_P1, TYP_MASK, INS_sve_mov)]
    public static void SourceClassSelectsTheNativeCopyInstruction(
        regNumber source, var_types type, instruction expected)
    {
        Assert.That(CreateCodeGen().ins_Copy(source, type), Is.EqualTo(expected));
    }

    [TestCase(TYP_INT, REG_R0, REG_R1, EA_UNKNOWN, EA_4BYTE, INS_mov)]
    [TestCase(TYP_LONG, REG_R0, REG_R1, EA_UNKNOWN, EA_8BYTE, INS_mov)]
    [TestCase(TYP_LONG, REG_R0, REG_R1, EA_4BYTE, EA_4BYTE, INS_mov)]
    [TestCase(TYP_FLOAT, REG_V0, REG_V1, EA_UNKNOWN, EA_4BYTE, INS_fmov)]
    [TestCase(TYP_DOUBLE, REG_V0, REG_V1, EA_UNKNOWN, EA_8BYTE, INS_fmov)]
    [TestCase(TYP_SIMD16, REG_V0, REG_V1, EA_UNKNOWN, EA_16BYTE, INS_mov)]
    public static void MoveRecordsTheSelectedInstructionAndExplicitOrDefaultWidth(
        var_types type, regNumber destination, regNumber source,
        emitAttr requested, emitAttr expectedSize, instruction expected)
    {
        var codeGen = CreateCodeGen();

        codeGen.inst_Mov(type, destination, source, canSkip: false, requested);

        var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing register move.");
        Assert.That(descriptor.idIns(), Is.EqualTo(expected));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(expectedSize));
        Assert.That(descriptor.idReg1(), Is.EqualTo(destination));
        Assert.That(descriptor.idReg2(), Is.EqualTo(source));
        Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
    }

    [TestCase(false, 4)]
    [TestCase(true, 0)]
    public static void MoveForwardsExplicitElisionPermission(bool canSkip, int expectedSize)
    {
        var codeGen = CreateCodeGen();

        codeGen.inst_Mov(TYP_LONG, REG_R0, REG_R0, canSkip);

        Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(expectedSize));
    }

    private static CodeGen CreateCodeGen()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.opts.compMinOptsIsSet = true;
        compiler.opts.compMinOpts = true;
        compiler.opts.canUseAllOpts = false;
        var codeGen = new CodeGen(compiler);
        codeGen.RegSet.rsClearRegsModified();
        var emitter = codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
        return codeGen;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);
}
#endif
