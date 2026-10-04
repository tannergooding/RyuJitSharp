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
using static RyuJitSharp.instruction;

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

    [TestCase(INS_mov, true)]
    [TestCase(INS_jirl, true)]
    [TestCase(INS_movfcsr2gr, true)]
    [TestCase(INS_movcf2gr, true)]
    [TestCase(INS_addi_d, false)]
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
        Assert.That(failure?.Message, Does.Contain("not implemented"));
    }

    private static Emitter.instrDesc NewDescriptor(instruction ins, bool isLocal)
    {
        var descriptorType = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic);
        if (descriptorType is null)
        {
            throw new InvalidOperationException("The basic LoongArch instruction descriptor type is unavailable.");
        }

        var descriptor = (Emitter.instrDesc)RuntimeHelpers.GetUninitializedObject(descriptorType);
        descriptor.idIns(ins);
        if (isLocal)
        {
            descriptor.idSetIsLclVar();
        }

        return descriptor;
    }
}
#endif
