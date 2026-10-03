// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using System.Runtime.CompilerServices;
using NUnit.Framework;
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
        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var tree = MakeAddTree(TYP_INT, 7);

#if TARGET_LOONGARCH64
        Assert.That(codeGen.genGetInsForOper(tree), Is.EqualTo(INS_addi_w));
#else
        Assert.That(codeGen.genGetInsForOper(tree), Is.EqualTo(INS_addiw));
#endif
    }

    [Test]
    public static void AddNativeIntegerImmediateUsesTargetPointerInstruction()
    {
        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var tree = MakeAddTree(TYP_I_IMPL, 7);

#if TARGET_LOONGARCH64
        Assert.That(codeGen.genGetInsForOper(tree), Is.EqualTo(INS_addi_d));
#else
        Assert.That(codeGen.genGetInsForOper(tree), Is.EqualTo(INS_addi));
#endif
    }

    private static GenTreeOp MakeAddTree(var_types type, nint value)
    {
        var operand1 = new GenTreePhysReg(REG_R0, type) { RegNum = REG_R0 };
        var operand2 = new GenTreeIntCon(type, value) { IsContained = true };

        return new GenTreeOp(GT_ADD, type, operand1, operand2);
    }
}
#endif
