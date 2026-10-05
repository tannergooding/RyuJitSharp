#if TARGET_ARM
using System;
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class ArmEmitterPredicateTests
{
    [TestCase(REG_R0, true)]
    [TestCase(REG_R15, true)]
    [TestCase(REG_F0, false)]
    public static void GeneralRegisterClassificationMatchesTheArmRegisterRange(regNumber reg, bool expected)
    {
        Assert.That(Emitter.isGeneralRegister(reg), Is.EqualTo(expected));
    }

    [TestCase(REG_F0, true)]
    [TestCase(REG_F31, true)]
    [TestCase(REG_R0, false)]
    public static void FloatRegisterClassificationMatchesTheArmRegisterRange(regNumber reg, bool expected)
    {
        Assert.That(Emitter.isFloatReg(reg), Is.EqualTo(expected));
    }

    [TestCase(REG_F0, true)]
    [TestCase(REG_F1, false)]
    [TestCase(REG_F30, true)]
    [TestCase(REG_F31, false)]
    public static void DoubleRegisterClassificationUsesEvenFloatRegisters(regNumber reg, bool expected)
    {
        Assert.That(Emitter.isDoubleReg(reg), Is.EqualTo(expected));
    }

    [TestCase(INS_FLAGS_SET, true)]
    [TestCase(INS_FLAGS_NOT_SET, false)]
    public static void FlagPredicatesPreserveNativeValues(insFlags flags, bool expectedSet)
    {
        Assert.That(Emitter.insSetsFlags(flags), Is.EqualTo(expectedSet));
        Assert.That(Emitter.insDoesNotSetFlags(flags), Is.EqualTo(!expectedSet));
        Assert.That(Emitter.insMustSetFlags(flags), Is.EqualTo(expectedSet ? INS_FLAGS_SET : INS_FLAGS_NOT_SET));
        Assert.That(Emitter.insMustNotSetFlags(flags), Is.EqualTo(expectedSet ? INS_FLAGS_SET : INS_FLAGS_NOT_SET));
    }

    [TestCase(INS_OPTS_NONE, true, false)]
    [TestCase(INS_OPTS_LDST_PRE_DEC, false, true)]
    [TestCase(INS_OPTS_LDST_POST_INC, false, true)]
    [TestCase(INS_OPTS_RRX, false, false)]
    public static void IncrementAndNoneOptionPredicates(insOpts option, bool expectedNone, bool expectedIncrement)
    {
        Assert.That(Emitter.insOptsNone(option), Is.EqualTo(expectedNone));
        Assert.That(Emitter.insOptAnyInc(option), Is.EqualTo(expectedIncrement));
        Assert.That(Emitter.insOptsPreDec(option), Is.EqualTo(option == INS_OPTS_LDST_PRE_DEC));
        Assert.That(Emitter.insOptsPostInc(option), Is.EqualTo(option == INS_OPTS_LDST_POST_INC));
    }

    [TestCase(INS_OPTS_RRX, true)]
    [TestCase(INS_OPTS_LSL, true)]
    [TestCase(INS_OPTS_LSR, true)]
    [TestCase(INS_OPTS_ASR, true)]
    [TestCase(INS_OPTS_ROR, true)]
    [TestCase(INS_OPTS_NONE, false)]
    [TestCase(INS_OPTS_LDST_PRE_DEC, false)]
    public static void ShiftOptionPredicatesMatchTheArmOptionRange(insOpts option, bool expectedShift)
    {
        Assert.That(Emitter.insOptAnyShift(option), Is.EqualTo(expectedShift));
        Assert.That(Emitter.insOptsRRX(option), Is.EqualTo(option == INS_OPTS_RRX));
        Assert.That(Emitter.insOptsLSL(option), Is.EqualTo(option == INS_OPTS_LSL));
        Assert.That(Emitter.insOptsLSR(option), Is.EqualTo(option == INS_OPTS_LSR));
        Assert.That(Emitter.insOptsASR(option), Is.EqualTo(option == INS_OPTS_ASR));
        Assert.That(Emitter.insOptsROR(option), Is.EqualTo(option == INS_OPTS_ROR));
    }

    [TestCase(EA_1BYTE, 8u)]
    [TestCase(EA_2BYTE, 16u)]
    [TestCase(EA_4BYTE, 32u)]
    [TestCase(EA_8BYTE, 64u)]
    public static void BitWidthScalesTheEmitSize(emitAttr size, uint expectedWidth)
    {
        Assert.That(Emitter.getBitWidth(size), Is.EqualTo(expectedWidth));
    }

    [TestCase("emitIsCondJump", Emitter.insFormat.IF_T2_J1, true)]
    [TestCase("emitIsCondJump", Emitter.insFormat.IF_T1_K, true)]
    [TestCase("emitIsCondJump", Emitter.insFormat.IF_LARGEJMP, true)]
    [TestCase("emitIsCondJump", Emitter.insFormat.IF_NONE, false)]
    [TestCase("emitIsCmpJump", Emitter.insFormat.IF_T1_I, true)]
    [TestCase("emitIsCmpJump", Emitter.insFormat.IF_NONE, false)]
    [TestCase("emitIsUncondJump", Emitter.insFormat.IF_T2_J2, true)]
    [TestCase("emitIsUncondJump", Emitter.insFormat.IF_T1_M, true)]
    [TestCase("emitIsUncondJump", Emitter.insFormat.IF_NONE, false)]
    [TestCase("emitIsLoadLabel", Emitter.insFormat.IF_T2_M1, true)]
    [TestCase("emitIsLoadLabel", Emitter.insFormat.IF_T1_J3, true)]
    [TestCase("emitIsLoadLabel", Emitter.insFormat.IF_T2_N1, true)]
    [TestCase("emitIsLoadLabel", Emitter.insFormat.IF_NONE, false)]
    public static void JumpFormatClassifiersMatchArmFormats(
        string methodName,
        Emitter.insFormat format,
        bool expected)
    {
        var descriptorType = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("instrDescBasic was not found.");
        var descriptor = (Emitter.instrDesc)(Activator.CreateInstance(descriptorType, nonPublic: true)
            ?? throw new InvalidOperationException("Could not create instrDescBasic."));
        descriptor.idIns(instruction.INS_nop);
        descriptor.idInsFmt(format);
        var classifier = typeof(Emitter).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} was not found.");

        Assert.That(classifier.Invoke(null, [descriptor]), Is.EqualTo(expected));
    }
}
#endif
