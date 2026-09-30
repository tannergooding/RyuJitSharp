// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenInstructionFrontendTests
{
    [TestCase(EA_UNKNOWN, "")]
    [TestCase(EA_1BYTE, "byte  ptr ")]
    [TestCase(EA_2BYTE, "word  ptr ")]
    [TestCase(EA_4BYTE, "dword ptr ")]
    [TestCase(EA_8BYTE, "qword ptr ")]
    [TestCase(EA_16BYTE, "xmmword ptr ")]
    [TestCase(EA_GCREF, "gword ptr ")]
    [TestCase(EA_BYREF, "bword ptr ")]
    [TestCase(EA_4BYTE | EA_DSP_RELOC_FLG, "rword ptr ")]
    public static void OperandSizeNamesPreserveNativeSizeAndRelocationPriority(emitAttr attr, string expected)
    {
        Assert.That(CodeGen.genSizeStr(attr), Is.EqualTo(expected));
    }

#if TARGET_XARCH
    [TestCase(EA_32BYTE, "ymmword ptr ")]
    [TestCase(EA_64BYTE, "zmmword ptr ")]
    public static void XarchWideOperandNamesPreserveTheNativeSizeTable(emitAttr attr, string expected)
    {
        Assert.That(CodeGen.genSizeStr(attr), Is.EqualTo(expected));
    }

#if TARGET_X86
    [TestCase(INS_fld, true)]
    [TestCase(INS_fstp, true)]
#endif
    [TestCase(INS_mov, false)]
    [TestCase(INS_addss, false)]
    [TestCase(INS_add, false)]
    public static void XarchFloatingInstructionMetadataMatchesTheTable(instruction ins, bool expected)
    {
        Assert.That(CodeGen.instIsFP(ins), Is.EqualTo(expected));
    }

    [TestCase(INS_cdq, true)]
    [TestCase(INS_cmpps, true)]
    [TestCase(INS_mov, false)]
    public static void PseudoNameFlagMatchesTheNativeInstructionTable(instruction ins, bool expected)
    {
        Assert.That(CodeGen.instHasPseudoName(ins), Is.EqualTo(expected));
    }

#if TARGET_AMD64
    [TestCase(0u)]
    [TestCase(16u)]
    public static void ReturnSelectionKeepsTheImmediateOnlyForCalleePoppedArguments(uint stackArgumentSize)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.instGen_Return(stackArgumentSize);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ret));
            Assert.That(descriptors[0].idInsFmt() == IF_CNS, Is.EqualTo(stackArgumentSize != 0));
            if (stackArgumentSize != 0)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)stackArgumentSize));
            }
        });
    }
#endif
#endif

#if TARGET_ARM
    [TestCase(INS_vldr, true)]
    [TestCase(INS_ldr, false)]
    public static void ArmFloatingInstructionMetadataMatchesTheTable(instruction ins, bool expected)
    {
        Assert.That(CodeGen.instIsFP(ins), Is.EqualTo(expected));
    }
#endif

#if TARGET_ARM64
    [Test]
    public static void Arm64SveInstructionNameKeepsTheNativeMnemonic()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(new CodeGen(compiler).genInsName(INS_sve_mov), Is.EqualTo("mov"));
    }
#endif

#if TARGET_WASM
    [TestCase(INS_i8x16_extract_lane_s, 1)]
    [TestCase(INS_i32x4_extract_lane, 4)]
    [TestCase(INS_i64x2_extract_lane, 8)]
    public static void WasmSimdElementWidthUsesTheTableUpperBits(instruction ins, byte expected)
    {
        Assert.That(CodeGen.instSimdElemSize(ins), Is.EqualTo(expected));
    }
#endif
}
