// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenStoreSelectionTests
{
    [TestCase(TYP_BYTE, false, INS_mov)]
    [TestCase(TYP_USHORT, true, INS_mov)]
    [TestCase(TYP_INT, false, INS_mov)]
    [TestCase(TYP_LONG, false, INS_mov)]
    [TestCase(TYP_REF, false, INS_mov)]
    [TestCase(TYP_BYREF, false, INS_mov)]
    [TestCase(TYP_FLOAT, false, INS_movss)]
    [TestCase(TYP_DOUBLE, true, INS_movsd_simd)]
    [TestCase(TYP_SIMD8, true, INS_movsd_simd)]
    [TestCase(TYP_SIMD12, false, INS_movups)]
    [TestCase(TYP_SIMD12, true, INS_movaps)]
    [TestCase(TYP_SIMD16, false, INS_movups)]
    [TestCase(TYP_SIMD16, true, INS_movaps)]
    [TestCase(TYP_SIMD32, false, INS_movups)]
    [TestCase(TYP_SIMD64, true, INS_movaps)]
    [TestCase(TYP_MASK, false, INS_kmovq_msk)]
    public static void StoreOpcodePreservesRegisterClassWidthAndAlignment(
        var_types type, bool aligned, instruction expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);

        Assert.That(codeGen.ins_Store(type, aligned), Is.EqualTo(expected));
    }

    [TestCase(TYP_INT, false, 0, 0, false)]
    [TestCase(TYP_SIMD8, true, 0, -8, true)]
    [TestCase(TYP_SIMD12, true, 0, -16, true)]
    [TestCase(TYP_SIMD16, true, 0, -8, false)]
    [TestCase(TYP_SIMD16, true, 0, 16, true)]
    [TestCase(TYP_SIMD16, false, 0, 0, false)]
    [TestCase(TYP_SIMD16, false, 0, 8, true)]
    [TestCase(TYP_SIMD16, false, 8, 0, true)]
    [TestCase(TYP_SIMD16, false, 16, 0, false)]
    [TestCase(TYP_SIMD16, false, 24, 0, true)]
    [TestCase(TYP_SIMD32, true, 0, 32, false)]
    [TestCase(TYP_SIMD64, true, 0, 64, false)]
    public static void LocalAlignmentUsesTheFrameBaseAndReturnAddressBias(
        var_types type, bool fpBased, int frameSize, int offset, bool expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.codeGen = new CodeGen(compiler) { IsFramePointerUsed = fpBased };
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc
        {
            Type = type,
            lvOnFrame = true,
            lvFramePointerBased = fpBased,
            StackOffset = offset,
        }];
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.compCalleeRegsPushed = 0;
        compiler.compLclFrameSize = frameSize;

        Assert.That(compiler.isSIMDTypeLocalAligned(0), Is.EqualTo(expected));
    }
}
