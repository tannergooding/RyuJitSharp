// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH || TARGET_ARM
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenFloatingInstructionSelectionTests
{
#if TARGET_XARCH
    [TestCase(GT_ADD, TYP_FLOAT, INS_addss)]
    [TestCase(GT_ADD, TYP_DOUBLE, INS_addsd)]
    [TestCase(GT_SUB, TYP_FLOAT, INS_subss)]
    [TestCase(GT_SUB, TYP_DOUBLE, INS_subsd)]
    [TestCase(GT_MUL, TYP_FLOAT, INS_mulss)]
    [TestCase(GT_MUL, TYP_DOUBLE, INS_mulsd)]
    [TestCase(GT_DIV, TYP_FLOAT, INS_divss)]
    [TestCase(GT_DIV, TYP_DOUBLE, INS_divsd)]
#elif TARGET_ARM
    [TestCase(GT_ADD, TYP_FLOAT, INS_vadd)]
    [TestCase(GT_SUB, TYP_DOUBLE, INS_vsub)]
    [TestCase(GT_MUL, TYP_FLOAT, INS_vmul)]
    [TestCase(GT_DIV, TYP_DOUBLE, INS_vdiv)]
    [TestCase(GT_NEG, TYP_FLOAT, INS_vneg)]
#endif
    public static void MathInstructionSelectionPreservesTargetOpcode(genTreeOps operation, var_types type,
        instruction expected)
    {
        Assert.That(CodeGen.ins_MathOp(operation, type), Is.EqualTo(expected));
    }

#if TARGET_X86 || TARGET_ARM
#if TARGET_X86
    [TestCase(TYP_INT, TYP_FLOAT, INS_cvtsi2ss32)]
    [TestCase(TYP_INT, TYP_DOUBLE, INS_cvtsi2sd32)]
    [TestCase(TYP_LONG, TYP_FLOAT, INS_cvtsi2ss64)]
    [TestCase(TYP_LONG, TYP_DOUBLE, INS_cvtsi2sd64)]
    [TestCase(TYP_UINT, TYP_FLOAT, INS_vcvtusi2ss32)]
    [TestCase(TYP_UINT, TYP_DOUBLE, INS_vcvtusi2sd32)]
    [TestCase(TYP_ULONG, TYP_FLOAT, INS_vcvtusi2ss64)]
    [TestCase(TYP_ULONG, TYP_DOUBLE, INS_vcvtusi2sd64)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_cvtss2sd)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, INS_cvtsd2ss)]
#else
    [TestCase(TYP_INT, TYP_FLOAT, INS_vcvt_i2f)]
    [TestCase(TYP_INT, TYP_DOUBLE, INS_vcvt_i2d)]
    [TestCase(TYP_UINT, TYP_FLOAT, INS_vcvt_u2f)]
    [TestCase(TYP_UINT, TYP_DOUBLE, INS_vcvt_u2d)]
    [TestCase(TYP_FLOAT, TYP_INT, INS_vcvt_f2i)]
    [TestCase(TYP_FLOAT, TYP_UINT, INS_vcvt_f2u)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_vcvt_f2d)]
    [TestCase(TYP_FLOAT, TYP_FLOAT, INS_vmov)]
    [TestCase(TYP_DOUBLE, TYP_INT, INS_vcvt_d2i)]
    [TestCase(TYP_DOUBLE, TYP_UINT, INS_vcvt_d2u)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, INS_vcvt_d2f)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, INS_vmov)]
#endif
    public static void FloatConversionSelectionPreservesTargetOpcode(var_types from, var_types to,
        instruction expected)
    {
        var codeGen = (CodeGen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        Assert.That(codeGen.ins_FloatConv(to, from), Is.EqualTo(expected));
    }
#endif
}
#endif
