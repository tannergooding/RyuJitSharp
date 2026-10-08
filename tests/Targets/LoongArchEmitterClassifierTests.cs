// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class LoongArchEmitterClassifierTests
{
    [TestCase(INS_mov)]
    [TestCase(INS_fmov_s)]
    [TestCase(INS_fmov_d)]
    [TestCase(INS_movgr2fr_w)]
    [TestCase(INS_movgr2fr_d)]
    [TestCase(INS_movfr2gr_s)]
    [TestCase(INS_movfr2gr_d)]
    public static void IsMovInstructionRecognizesEveryLoongArchMove(instruction ins)
    {
        Assert.That(Emitter.IsMovInstruction(ins), Is.True);
    }

    [TestCase(INS_add_d)]
    [TestCase(INS_fadd_s)]
    [TestCase(INS_movfcsr2gr)]
    public static void IsMovInstructionRejectsNonElidableMoveLikeInstructions(instruction ins)
    {
        Assert.That(Emitter.IsMovInstruction(ins), Is.False);
    }

    [TestCase(REG_R0, "zero")]
    [TestCase(REG_RA, "ra")]
    [TestCase(REG_F0, "f0")]
    [TestCase(REG_F31, "f31")]
    public static void RegisterNamesMatchTheLoongArchAbi(regNumber reg, string expected)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));

        Assert.That(emitter.emitRegName(reg, EA_4BYTE, varName: false), Is.EqualTo(expected));
    }

#if DEBUG || LATE_DISASM
    [TestCase(INS_nop, 4.0f)]
    [TestCase(INS_b, 1.0f)]
    [TestCase(INS_ld_d, 3.0f)]
    [TestCase(INS_st_d, 2.0f)]
#if FEATURE_SIMD
    [TestCase(INS_vadd_b, 4.0f)]
    [TestCase(INS_xvadd_b, 4.0f)]
    [TestCase(INS_xvbitsel_v, 2.0f)]
#endif
    public static void ExecutionCostUsesLoongArchInstructionCharacteristics(instruction ins, float expectedCost)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        var descriptor = NewDescriptor(ins, isLocal: false);
        descriptor.idInsOpt(INS_OPTS_NONE);
        descriptor.idCodeSize(4);

        Assert.That(emitter.insEvaluateExecutionCost(descriptor), Is.EqualTo(expectedCost));
    }

    [TestCase(INS_b, INS_OPTS_RC, 8u, 6.0f)]
    [TestCase(INS_b, INS_OPTS_RC, 12u, 9.0f)]
    [TestCase(INS_jirl, INS_OPTS_JIRL, 8u, 5.5f)]
    public static void MergedInstructionCostsUseLoongArchInstructionCount(
        instruction ins,
        insOpts options,
        uint codeSize,
        float expectedCost)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        var descriptor = NewDescriptor(ins, isLocal: false);
        descriptor.idInsOpt(options);
        descriptor.idCodeSize(codeSize);

        Assert.That(emitter.insEvaluateExecutionCost(descriptor), Is.EqualTo(expectedCost));
    }
#endif

    [TestCase(INS_nop, 0x03400000u, 0xffffffffu, "DF_G_ALIAS")]
    [TestCase(INS_addi_d, 0x02c00000u, 0xffc00000u, "DF_G_2R12I")]
    public static void InstructionCodeAndDisassemblyTablesStayAligned(
        instruction ins,
        uint expectedCode,
        uint expectedMask,
        string expectedFormat)
    {
        var codeLookup = typeof(Emitter).GetMethod("emitInsCode", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The LoongArch instruction-code lookup is unavailable.");
        var maskLookup = typeof(Emitter).GetMethod("emitGetInsMask", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The LoongArch instruction-mask lookup is unavailable.");
        var formatLookup = typeof(Emitter).GetMethod("emitGetInsFmt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The LoongArch disassembly-format lookup is unavailable.");

        Assert.That(codeLookup.Invoke(null, [ins]), Is.EqualTo(expectedCode));
        Assert.That(maskLookup.Invoke(null, [ins]), Is.EqualTo(expectedMask));
        Assert.That(formatLookup.Invoke(null, [ins])?.ToString(), Is.EqualTo(expectedFormat));
    }

    [TestCase(0x12345678L, 0)]
    [TestCase(0xffffffffL, 1)]
    [TestCase(long.MaxValue, 1)]
    public static unsafe void EightByteImmediateOutputUsesTheSpecialSequenceOnlyWhenMarked(
        long immediateValue,
        int specialSequenceMarker)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        var descriptor = NewDescriptor(INS_lu12i_w, isLocal: false);
        var immediate = unchecked((nint)immediateValue);
        descriptor.idInsOpt(INS_OPTS_I);
        descriptor.idCodeSize(8);
        descriptor.idReg1(REG_R21);
        descriptor.idReg2((regNumber)specialSequenceMarker);
        descriptor.idAddr().iiaAddr = unchecked((byte*)immediate);

        var output = stackalloc uint[2];
        EmitOutputLoongArch64Immediate(emitter, (byte*)output, descriptor);

        uint expectedFirst;
        uint expectedSecond;
        if (specialSequenceMarker != 0)
        {
            expectedFirst = InstructionCode(INS_addi_d) |
                unchecked((uint)REG_R21) |
                (unchecked((uint)REG_R0) << 5) |
                (0xfff << 10);
            var shift = immediate == nint.MaxValue ? 1u : 32u;
            expectedSecond = InstructionCode(INS_srli_d) |
                unchecked((uint)REG_R21) |
                (unchecked((uint)REG_R21) << 5) |
                (shift << 10);
        }
        else
        {
            expectedFirst = InstructionCode(INS_lu12i_w) |
                unchecked((uint)REG_R21) |
                ((unchecked((uint)(immediate >> 12)) & 0xfffff) << 5);
            expectedSecond = InstructionCode(INS_ori) |
                unchecked((uint)REG_R21) |
                (unchecked((uint)REG_R21) << 5) |
                ((unchecked((uint)immediate) & 0xfff) << 10);
        }

        Assert.That(output[0], Is.EqualTo(expectedFirst));
        Assert.That(output[1], Is.EqualTo(expectedSecond));
    }

    [TestCase(INS_mov, true)]
    [TestCase(INS_jirl, true)]
    [TestCase(INS_movfcsr2gr, true)]
    [TestCase(INS_movcf2gr, true)]
    [TestCase(INS_addi_d, true)]
    [TestCase(INS_fadd_s, false)]
#if FEATURE_SIMD
    [TestCase(INS_vpickve2gr_d, true)]
    [TestCase(INS_vpickve2gr_wu, true)]
    [TestCase(INS_vpickve2gr_h, true)]
    [TestCase(INS_vpickve2gr_bu, true)]
    [TestCase(INS_xvpickve2gr_d, true)]
    [TestCase(INS_xvpickve2gr_wu, true)]
    [TestCase(INS_vadd_b, false)]
#endif
    public static void MayWriteToGCRegisterMatchesLoongArchInstructionRanges(instruction ins, bool expected)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));

        Assert.That(emitter.emitInsMayWriteToGCReg(ins), Is.EqualTo(expected));
    }

    [TestCase(INS_st_d, true, true)]
    [TestCase(INS_st_w, true, true)]
    [TestCase(INS_st_b, true, true)]
    [TestCase(INS_st_h, true, true)]
    [TestCase(INS_stptr_d, true, true)]
    [TestCase(INS_stx_d, true, true)]
    [TestCase(INS_stx_w, true, true)]
    [TestCase(INS_stx_b, true, true)]
    [TestCase(INS_stx_h, true, true)]
    [TestCase(INS_st_d, false, false)]
    [TestCase(INS_ld_d, true, false)]
    [TestCase(INS_fst_d, true, false)]
    public static void LocalStackWriteClassificationRequiresAnIntegerStoreToALocal(
        instruction ins,
        bool isLocal,
        bool expected)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        var descriptor = NewDescriptor(ins, isLocal);

        Assert.That(emitter.emitInsWritesToLclVarStackLoc(descriptor), Is.EqualTo(expected));
    }

    [Test]
    public static void InstructionToJumpKindLookupRemainsATerminatingUnsupportedStub()
    {
        var failure = Assert.Throws<FatalJitException>(() => Emitter.emitInsToJumpKind(INS_beq));

        Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(failure?.Message, Is.EqualTo("emitInsToJumpKind-----unimplemented on LOONGARCH64 yet----"));
    }

    private static Emitter.instrDesc NewDescriptor(instruction ins, bool isLocal)
    {
        var descriptorType = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The basic LoongArch instruction descriptor type is unavailable.");

        var descriptor = (Emitter.instrDesc)RuntimeHelpers.GetUninitializedObject(descriptorType);
        descriptor.idIns(ins);
        if (isLocal)
        {
            descriptor.idSetIsLclVar();
        }

        return descriptor;
    }

    private static uint InstructionCode(instruction ins)
    {
        var codeLookup = typeof(Emitter).GetMethod("emitInsCode", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The LoongArch instruction-code lookup is unavailable.");
        return (uint)(codeLookup.Invoke(null, [ins])
            ?? throw new InvalidOperationException("The LoongArch instruction-code lookup returned no value."));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EmitOutputLoongArch64Immediate")]
    private static extern unsafe void EmitOutputLoongArch64Immediate(
        Emitter emitter,
        byte* dst,
        Emitter.instrDesc id);
}
#endif
