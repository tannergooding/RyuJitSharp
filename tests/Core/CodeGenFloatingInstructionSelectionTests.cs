// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenFloatingInstructionSelectionTests
{
    [TestCase(GT_ADD, TYP_FLOAT, INS_addss, true)]
    [TestCase(GT_ADD, TYP_DOUBLE, INS_addsd, true)]
    [TestCase(GT_SUB, TYP_FLOAT, INS_subss, true)]
    [TestCase(GT_SUB, TYP_DOUBLE, INS_subsd, true)]
    [TestCase(GT_MUL, TYP_FLOAT, INS_mulss, true)]
    [TestCase(GT_MUL, TYP_DOUBLE, INS_mulsd, true)]
    [TestCase(GT_DIV, TYP_FLOAT, INS_divss, true)]
    [TestCase(GT_DIV, TYP_DOUBLE, INS_divsd, true)]
    [TestCase(GT_ADD, TYP_FLOAT, INS_vadd, false)]
    [TestCase(GT_SUB, TYP_DOUBLE, INS_vsub, false)]
    [TestCase(GT_MUL, TYP_FLOAT, INS_vmul, false)]
    [TestCase(GT_DIV, TYP_DOUBLE, INS_vdiv, false)]
    [TestCase(GT_NEG, TYP_FLOAT, INS_vneg, false)]
    public static void MathInstructionSelectionPreservesTargetOpcode(genTreeOps operation, var_types type,
        instruction expected, bool xarchTarget)
    {
        if (xarchTarget ? !Configuration.TARGET_XARCH : !Configuration.TARGET_ARM)
        {
            Assert.Ignore("The expected opcode is specific to another target.");
        }

        Assert.That(CodeGen.ins_MathOp(operation, type), Is.EqualTo(expected));
    }

    [TestCase(TYP_INT, TYP_FLOAT, INS_cvtsi2ss32, false)]
    [TestCase(TYP_INT, TYP_DOUBLE, INS_cvtsi2sd32, false)]
    [TestCase(TYP_LONG, TYP_FLOAT, INS_cvtsi2ss64, false)]
    [TestCase(TYP_LONG, TYP_DOUBLE, INS_cvtsi2sd64, false)]
    [TestCase(TYP_UINT, TYP_FLOAT, INS_vcvtusi2ss32, false)]
    [TestCase(TYP_UINT, TYP_DOUBLE, INS_vcvtusi2sd32, false)]
    [TestCase(TYP_ULONG, TYP_FLOAT, INS_vcvtusi2ss64, false)]
    [TestCase(TYP_ULONG, TYP_DOUBLE, INS_vcvtusi2sd64, false)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_cvtss2sd, false)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, INS_cvtsd2ss, false)]
    [TestCase(TYP_INT, TYP_FLOAT, INS_vcvt_i2f, true)]
    [TestCase(TYP_INT, TYP_DOUBLE, INS_vcvt_i2d, true)]
    [TestCase(TYP_UINT, TYP_FLOAT, INS_vcvt_u2f, true)]
    [TestCase(TYP_UINT, TYP_DOUBLE, INS_vcvt_u2d, true)]
    [TestCase(TYP_FLOAT, TYP_INT, INS_vcvt_f2i, true)]
    [TestCase(TYP_FLOAT, TYP_UINT, INS_vcvt_f2u, true)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_vcvt_f2d, true)]
    [TestCase(TYP_FLOAT, TYP_FLOAT, INS_vmov, true)]
    [TestCase(TYP_DOUBLE, TYP_INT, INS_vcvt_d2i, true)]
    [TestCase(TYP_DOUBLE, TYP_UINT, INS_vcvt_d2u, true)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, INS_vcvt_d2f, true)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, INS_vmov, true)]
    public static void FloatConversionSelectionPreservesTargetOpcode(var_types from, var_types to,
        instruction expected, bool armTarget)
    {
        if (armTarget ? !Configuration.TARGET_ARM : !Configuration.TARGET_X86)
        {
            Assert.Ignore("The expected conversion opcode is specific to another target.");
        }

        var codeGen = (CodeGen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        Assert.That(codeGen.ins_FloatConv(to, from), Is.EqualTo(expected));
    }
}
