// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenInstructionSelectionClosureTests
{
    [TestCase(EJ_XARCH_js, INS_sets)]
    [TestCase(EJ_XARCH_jne, INS_setne)]
    [TestCase(EJ_XARCH_jle, INS_setle)]
    [TestCase(EJ_XARCH_jae, INS_setae)]
    [TestCase(EJ_XARCH_jp, INS_setp)]
    [TestCase(EJ_XARCH_jnp, INS_setnp)]
    public static void Amd64ConditionAdapterRecordsTheMatchingByteSet(emitJumpKind condition, instruction expected)
    {
        Assume.That(Configuration.TARGET_AMD64);
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_SET(condition, REG_X64_RAX);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_1BYTE));
        });
    }

    [TestCase(TYP_BYTE, INS_movsx, EA_4BYTE)]
    [TestCase(TYP_UBYTE, INS_movzx, EA_4BYTE)]
    [TestCase(TYP_INT, INS_mov, EA_4BYTE)]
    [TestCase(var_types.TYP_LONG, INS_mov, EA_8BYTE)]
    public static void Amd64ExtendedMoveRecordsSignAndActualWidth(
        var_types sourceType, instruction expected, emitAttr size)
    {
        Assume.That(Configuration.TARGET_AMD64);
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_Mov_Extend(sourceType, srcInReg: true, REG_X64_R8, REG_X64_RAX, canSkip: false, EA_UNKNOWN);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(size));
        });
    }
    [TestCase(TYP_BYTE, true, INS_sxtb)]
    [TestCase(TYP_UBYTE, true, INS_uxtb)]
    [TestCase(TYP_SHORT, true, INS_sxth)]
    [TestCase(TYP_USHORT, true, INS_uxth)]
    [TestCase(TYP_INT, true, INS_mov)]
    [TestCase(TYP_BYTE, false, INS_ldrsb)]
    [TestCase(TYP_UBYTE, false, INS_ldrb)]
    [TestCase(TYP_FLOAT, true, INS_mov)]
    [TestCase(TYP_FLOAT, false, INS_ldr)]
    public static void Arm64ExtensionSelectionKeepsRegisterAndMemoryForms(
        var_types sourceType, bool srcInReg, instruction expected)
    {
        Assume.That(Configuration.TARGET_ARM64);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);

        Assert.That(codeGen.ins_Move_Extend(sourceType, srcInReg), Is.EqualTo(expected));
    }
}
