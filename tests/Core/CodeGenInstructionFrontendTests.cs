// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenInstructionFrontendTests
{
    private static IEnumerable<TestCaseData> OperandSizeCases()
    {
        yield return new TestCaseData(EA_UNKNOWN, "");
        yield return new TestCaseData(EA_1BYTE, "byte  ptr ");
        yield return new TestCaseData(EA_2BYTE, "word  ptr ");
        yield return new TestCaseData(EA_4BYTE, "dword ptr ");
        yield return new TestCaseData(EA_8BYTE, "qword ptr ");
        yield return new TestCaseData(EA_16BYTE, "xmmword ptr ");
        yield return new TestCaseData(EA_GCREF, "gword ptr ");
        yield return new TestCaseData(EA_BYREF, "bword ptr ");
        yield return new TestCaseData(EA_4BYTE | EA_DSP_RELOC_FLG, "rword ptr ");

        if (Configuration.TARGET_XARCH)
        {
            yield return new TestCaseData(EA_32BYTE, "ymmword ptr ");
            yield return new TestCaseData(EA_64BYTE, "zmmword ptr ");
        }
    }

    [TestCaseSource(nameof(OperandSizeCases))]
    public static void OperandSizeNamesPreserveNativeSizeAndRelocationPriority(emitAttr attr, string expected)
    {
        Assert.That(CodeGen.genSizeStr(attr), Is.EqualTo(expected));
    }

    [TestCase(INS_fld, true)]
    [TestCase(INS_fstp, true)]
    [TestCase(INS_mov, false)]
    [TestCase(INS_addss, false)]
    [TestCase(INS_add, false)]
    public static void XarchFloatingInstructionMetadataMatchesTheTable(instruction ins, bool expected)
    {
        Assume.That(Configuration.TARGET_XARCH);
        if (ins is INS_fld or INS_fstp)
        {
            Assume.That(Configuration.TARGET_X86);
        }

        Assert.That(CodeGen.instIsFP(ins), Is.EqualTo(expected));
    }

    [TestCase(INS_cdq, true)]
    [TestCase(INS_cmpps, true)]
    [TestCase(INS_mov, false)]
    public static void PseudoNameFlagMatchesTheNativeInstructionTable(instruction ins, bool expected)
    {
        Assume.That(Configuration.TARGET_XARCH);

        Assert.That(CodeGen.instHasPseudoName(ins), Is.EqualTo(expected));
    }

    [TestCase(0u)]
    [TestCase(16u)]
    public static void ReturnSelectionKeepsTheImmediateOnlyForCalleePoppedArguments(uint stackArgumentSize)
    {
        Assume.That(Configuration.TARGET_AMD64);

        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.instGen_Return(stackArgumentSize);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ret));
            Assert.That(descriptors[0].idInsFmt() == IF_XARCH_CNS, Is.EqualTo(stackArgumentSize != 0));
            if (stackArgumentSize != 0)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)stackArgumentSize));
            }
        });
    }

    [TestCase(INS_vldr, true)]
    [TestCase(INS_ldr, false)]
    public static void ArmFloatingInstructionMetadataMatchesTheTable(instruction ins, bool expected)
    {
        Assume.That(Configuration.TARGET_ARM);

        Assert.That(CodeGen.instIsFP(ins), Is.EqualTo(expected));
    }

    [Test]
    public static void Arm64SveInstructionNameKeepsTheNativeMnemonic()
    {
        Assume.That(Configuration.TARGET_ARM64);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(new CodeGen(compiler).genInsName(INS_sve_mov).ToString(), Is.EqualTo("mov"));
    }

    [TestCase(INS_i8x16_extract_lane_s, 1)]
    [TestCase(INS_i32x4_extract_lane, 4)]
    [TestCase(INS_i64x2_extract_lane, 8)]
    public static void WasmSimdElementWidthUsesTheTableUpperBits(instruction ins, byte expected)
    {
        Assume.That(Configuration.TARGET_WASM);

        Assert.That(CodeGen.instSimdElemSize(ins), Is.EqualTo(expected));
    }
}
