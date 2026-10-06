// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32InstructionSelectionTests
{
    [TestCase(12, INS_add)]
    [TestCase(-12, INS_sub)]
    public static void ConstantInstructionNormalizesNegativeImmediates(int immediate, instruction expectedInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var immediateFits = InstructionWithConstant(
                codeGen, INS_add, EA_PTRSIZE, REG_R3, REG_R4, immediate, REG_R7, INS_FLAGS_DONT_CARE);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing immediate instruction.");
            Assert.That(immediateFits, Is.True);
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_L0));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R4));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)12));
        });
    }

    [Test]
    public static void ConstantInstructionUsesNativeDefaultFlagsWhenRecordingImmediate()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var immediateFits = InstructionWithConstant(
                codeGen, INS_add, EA_PTRSIZE, REG_R3, REG_R4, 1, REG_R7, INS_FLAGS_NOT_SET);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing immediate instruction.");
            Assert.That(immediateFits, Is.True);
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T1_G));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_SET));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)1));
        });
    }

    [Test]
    public static void ConstantInstructionMaterializesValuesThatDoNotFit()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var immediateFits = InstructionWithConstant(
                codeGen, INS_add, EA_PTRSIZE, REG_R3, REG_R4, 0x12345678, REG_R7, INS_FLAGS_DONT_CARE);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing register-based instruction.");
            Assert.That(immediateFits, Is.False);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R4));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R7));
        });
    }

    [Test]
    public static void StackPointerAdjustmentUsesTheNormalizedImmediateInstruction()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var immediateFits = StackPointerAdjustment(codeGen, -12, REG_R7);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing stack-pointer adjustment.");
            Assert.That(immediateFits, Is.True);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_sub));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_SPBASE));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_SPBASE));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)12));
        });
    }

    private static Emitter.instrDesc? LastInstruction(Emitter emitter)
        => Last(emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? Last(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genInstrWithConstant")]
    private static extern bool InstructionWithConstant(CodeGen codeGen, instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, nint imm, regNumber tmpReg, insFlags flags);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genStackPointerAdjustment")]
    private static extern bool StackPointerAdjustment(CodeGen codeGen, nint spDelta, regNumber tmpReg);
}
#endif
