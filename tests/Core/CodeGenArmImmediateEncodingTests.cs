// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenArmImmediateEncodingTests
{
    [TestCase(INS_ldr, true)]
    [TestCase(INS_str, true)]
    [TestCase(INS_vldr, true)]
    [TestCase(INS_vstr, true)]
    [TestCase(INS_add, false)]
    [TestCase(INS_mov, false)]
    [TestCase(INS_invalid, false)]
    public static void MemoryClassifierUsesArmLoadAndStoreFlags(instruction ins, bool expected)
    {
        var codeGen = NewCodeGen();
        Assert.That(EmitInsIsLoadOrStore(codeGen.Emitter, ins), Is.EqualTo(expected));
    }

    [Test]
    public static void SyntheticAndUnknownInstructionsAreNotMemoryOperations()
    {
        var codeGen = NewCodeGen();
        Assert.That(EmitInsIsLoadOrStore(codeGen.Emitter, INS_lea), Is.False);
        Assert.That(EmitInsIsLoadOrStore(codeGen.Emitter, (instruction)CodeGen.instInfo.Length), Is.False);
        Assert.That(EmitInsIsLoadOrStore(codeGen.Emitter, (instruction)int.MaxValue), Is.False);
        Assert.That(EmitInsIsLoadOrStore(codeGen.Emitter, (instruction)(-1)), Is.False);
    }

    [TestCase(INS_ldr, -255, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_ldr, -256, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_str, 4095, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_str, 4096, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_vldr, 1020, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_vstr, 1024, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_vldr, -4, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_cmp, -4096, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_cmn, 0x12345678, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_and, unchecked((int)0xffffff00), INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_orr, 0x12345678, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_mov, 0xffff, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_mov, 0x12345678, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_addw, 4095, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_addw, 4095, INS_FLAGS_SET, false)]
    [TestCase(INS_subw, 4096, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_add, 4095, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_add, 4095, INS_FLAGS_SET, false)]
    [TestCase(INS_sub, -4096, INS_FLAGS_SET, true)]
    [TestCase(INS_lsl, 0, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_lsl, 32, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_lsl, 33, INS_FLAGS_DONT_CARE, false)]
    [TestCase(INS_ror, 1, INS_FLAGS_DONT_CARE, true)]
    [TestCase(INS_invalid, 1, INS_FLAGS_DONT_CARE, false)]
    public static void InstructionImmediateValidityKeepsOpcodeAndMemoryRules(instruction ins, int immediate,
        insFlags flags, bool expected)
    {
        var codeGen = NewCodeGen();
        Assert.That(codeGen.arm_Valid_Imm_For_Instr(ins, immediate, flags), Is.EqualTo(expected));
    }

    [TestCase(0, true)]
    [TestCase(0xff, true)]
    [TestCase(0x00ff00ff, true)]
    [TestCase(unchecked((int)0xff00ff00), true)]
    [TestCase(-1, true)]
    [TestCase(int.MinValue, true)]
    [TestCase(0x12345678, false)]
    public static void ModifiedImmediateRecognizesThumbReplicatedAndShiftedBytes(int immediate, bool expected)
    {
        Assert.That(Emitter.isModImmConst(immediate), Is.EqualTo(expected));
        Assert.That(Emitter.emitIns_valid_imm_for_alu(immediate), Is.EqualTo(expected));
    }

    [TestCase(0xffff, true)]
    [TestCase(unchecked((int)0xffff0000), true)]
    [TestCase(0x12345678, false)]
    public static void MoveImmediateAllowsHalfwordsAndModifiedInverses(int immediate, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_mov(immediate), Is.EqualTo(expected));
        var codeGen = (CodeGen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        Assert.That(codeGen.validImmForMov(immediate), Is.EqualTo(expected));
    }

    [TestCase(4095, INS_FLAGS_DONT_CARE, true)]
    [TestCase(4095, INS_FLAGS_SET, false)]
    [TestCase(4096, INS_FLAGS_SET, true)]
    [TestCase(int.MinValue, INS_FLAGS_SET, true)]
    public static void AddImmediateRetainsTwelveBitAndFlagsRules(int immediate, insFlags flags, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_add(immediate, flags), Is.EqualTo(expected));
        var codeGen = (CodeGen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        Assert.That(codeGen.arm_Valid_Imm_For_Add(immediate, flags), Is.EqualTo(expected));
    }

    [TestCase(0, true)]
    [TestCase(4, true)]
    [TestCase(1020, true)]
    [TestCase(2, false)]
    [TestCase(1024, false)]
    [TestCase(-4, false)]
    public static void StackPointerAdditionRequiresAlignedTenBitOffset(int immediate, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_add_sp(immediate), Is.EqualTo(expected));
        var codeGen = (CodeGen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        Assert.That(codeGen.arm_Valid_Imm_For_Add_SP(immediate), Is.EqualTo(expected));
    }

    [TestCase(TYP_INT, -255, true)]
    [TestCase(TYP_INT, -256, false)]
    [TestCase(TYP_INT, 4095, true)]
    [TestCase(TYP_INT, 4096, false)]
    [TestCase(TYP_FLOAT, 1020, true)]
    [TestCase(TYP_FLOAT, 1024, false)]
    [TestCase(TYP_FLOAT, -4, false)]
    public static void LoadStoreDisplacementsKeepIntegerAndFloatingLimits(var_types type, int displacement,
        bool expected)
    {
        var codeGen = (CodeGen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        Assert.That(codeGen.validDispForLdSt(displacement, type), Is.EqualTo(expected));
    }

    [TestCase(REG_R0, true)]
    [TestCase(REG_R7, true)]
    [TestCase(REG_R8, false)]
    public static void LowRegisterSelectionMatchesThumbEncoding(regNumber reg, bool expected)
    {
        Assert.That(Emitter.isLowRegister(reg), Is.EqualTo(expected));
    }

    private static CodeGen NewCodeGen()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        return new CodeGen(compiler);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsIsLoadOrStore")]
    private static extern bool EmitInsIsLoadOrStore(Emitter emitter, instruction ins);
}
#endif
