// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LoongArchRiscVCodeGenBinaryTests
{
    [Test]
    public static void AddImmediateUsesTargetWordInstruction()
    {
        Assume.That(Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64,
            "requires LoongArch64 or RISC-V64 target semantics");

        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var tree = MakeAddTree(TYP_INT, 7);
        var expected = Configuration.TARGET_LOONGARCH64 ? INS_addi_w : INS_addiw;
        Assert.That(codeGen.genGetInsForOper(tree), Is.EqualTo(expected));
    }

    [Test]
    public static void AddNativeIntegerImmediateUsesTargetPointerInstruction()
    {
        Assume.That(Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64,
            "requires LoongArch64 or RISC-V64 target semantics");

        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var tree = MakeAddTree(TYP_I_IMPL, 7);
        var expected = Configuration.TARGET_LOONGARCH64 ? INS_addi_d : INS_addi;
        Assert.That(codeGen.genGetInsForOper(tree), Is.EqualTo(expected));
    }

    private static GenTreeOp MakeAddTree(var_types type, nint value)
    {
        var register0 = Configuration.TARGET_LOONGARCH64 ? REG_LOONGARCH64_R0 : REG_RISCV64_R0;
        var operand1 = new GenTreePhysReg(register0, type) { RegNum = register0 };
        var operand2 = new GenTreeIntCon(type, value) { IsContained = true };

        return new GenTreeOp(GT_ADD, type, operand1, operand2);
    }
}
