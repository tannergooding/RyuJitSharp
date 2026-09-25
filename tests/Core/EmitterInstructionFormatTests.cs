// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.ID_OPS;
using static RyuJitSharp.IS_INFO;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insUpdateModes;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class EmitterInstructionFormatTests
{
    [Test]
    public static void GeneratedFormatOrdinalsAndWidthsMatchThePinnedNativeTable()
    {
        Assert.That(Unsafe.SizeOf<Emitter.insFormat>(), Is.EqualTo(4));
        Assert.That(Unsafe.SizeOf<insUpdateModes>(), Is.EqualTo(4));
        Assert.That((uint)IF_NONE, Is.Zero);
        Assert.That((uint)IF_RRD, Is.EqualTo(7u));
        Assert.That((uint)IF_RRD_RRD_RRD, Is.EqualTo(22u));
        Assert.That((uint)IF_MRD, Is.EqualTo(28u));
        Assert.That((uint)IF_SRD, Is.EqualTo(60u));
        Assert.That((uint)IF_SWR_RRD, Is.EqualTo(68u));
        Assert.That((uint)IF_ARD, Is.EqualTo(90u));
        Assert.That((uint)IF_RWR_RRD_ARD_RRD, Is.EqualTo(119u));
        Assert.That((uint)IF_COUNT, Is.EqualTo(120u));
        Assert.That(Emitter.emitFmtToOps.Length, Is.EqualTo((int)IF_COUNT));
        Assert.That((int)IUM_RD, Is.Zero);
        Assert.That((int)IUM_WR, Is.EqualTo(1));
        Assert.That((int)IUM_RW, Is.EqualTo(2));
    }

    [Test]
    public static void EveryInstructionHasOneByteOfUpdateMetadataInInstructionOrder()
    {
        var emitter = CreateEmitter();
        Assert.That(Emitter.emitInsModeFmtTab.Length, Is.EqualTo((int)INS_count));
        for (var index = 0; index < (int)INS_count; index++)
        {
            Assert.That(Emitter.emitInsModeFmtTab[index], Is.InRange((byte)IUM_RD, (byte)IUM_RW));
            Assert.That(emitter.emitInsUpdateMode((instruction)index),
                Is.EqualTo((insUpdateModes)Emitter.emitInsModeFmtTab[index]));
        }
    }

    [TestCase(INS_push, IUM_RD)]
    [TestCase(INS_pop, IUM_WR)]
    [TestCase(INS_inc, IUM_RW)]
    [TestCase(INS_cmp, IUM_RD)]
    [TestCase(INS_mov, IUM_WR)]
    [TestCase(INS_add, IUM_RW)]
    [TestCase(INS_imul_AX, IUM_RD)]
    [TestCase(INS_push2, IUM_RD)]
    [TestCase(INS_kmovb_msk, IUM_WR)]
    [TestCase(INS_vpternlogd, IUM_RW)]
    [TestCase(INS_ret, IUM_RD)]
    [TestCase(INS_align, IUM_RD)]
    public static void UpdateModesMatchNativeScalarMaskSimdAndPseudoInstructions(instruction ins, insUpdateModes mode)
    {
        Assert.That(CreateEmitter().emitInsUpdateMode(ins), Is.EqualTo(mode));
    }

    [TestCase(INS_cmp, IF_SRD_RRD, IF_SRD_RRD)]
    [TestCase(INS_mov, IF_SRD_RRD, IF_SWR_RRD)]
    [TestCase(INS_add, IF_SRD_RRD, IF_SRW_RRD)]
    [TestCase(INS_push, IF_RRD, IF_RRD)]
    [TestCase(INS_pop, IF_RRD, IF_RWR)]
    [TestCase(INS_inc, IF_RRD, IF_RRW)]
    [TestCase(INS_cmp, IF_MRD_CNS, IF_MRD_CNS)]
    [TestCase(INS_mov, IF_MRD_CNS, IF_MWR_CNS)]
    [TestCase(INS_add, IF_MRD_CNS, IF_MRW_CNS)]
    [TestCase(INS_cmp, IF_RRD_ARD, IF_RRD_ARD)]
    [TestCase(INS_mov, IF_RRD_ARD, IF_RWR_ARD)]
    [TestCase(INS_add, IF_RRD_ARD, IF_RRW_ARD)]
    public static void OrdinaryFormatSelectionAddsTheNativeUpdateMode(instruction ins, Emitter.insFormat baseFormat,
        Emitter.insFormat expected)
    {
        Assert.That(CreateEmitter().emitInsModeFormat(ins, baseFormat), Is.EqualTo(expected));
    }

    [TestCase(IF_RRD_RRD_RRD, IF_RWR_RRD_RRD)]
    [TestCase(IF_RRD_RRD_ARD, IF_RWR_RRD_ARD)]
    [TestCase(IF_RRD_RRD_CNS, IF_RWR_RRD_CNS)]
    [TestCase(IF_RRD_RRD_SRD, IF_RWR_RRD_SRD)]
    [TestCase(IF_RRD_RRD, IF_RWR_RRD)]
    public static void ApxNddWritesANewDestinationForEverySupportedBase(Emitter.insFormat baseFormat,
        Emitter.insFormat expected)
    {
        var emitter = CreateEmitter();
        emitter.UsePromotedEvexEncodings = true;

        Assert.That(emitter.emitInsModeFormat(INS_add, baseFormat, useNDD: true), Is.EqualTo(expected));
    }

    [TestCase(INS_rcl_N)]
    [TestCase(INS_rcr_N)]
    [TestCase(INS_rol_N)]
    [TestCase(INS_ror_N)]
    [TestCase(INS_shl_N)]
    [TestCase(INS_shr_N)]
    [TestCase(INS_sar_N)]
    public static void ApxImmediateShiftsOverrideTheOrdinaryBaseFormat(instruction ins)
    {
        var emitter = CreateEmitter();
        emitter.UsePromotedEvexEncodings = true;

        Assert.That(emitter.emitInsModeFormat(ins, IF_RRD_RRD_CNS, useNDD: true), Is.EqualTo(IF_RWR_RRD_SHF));
    }

    [Test]
    public static void ApxClassificationUsesInstructionFlagsAndThePromotedEncodingSwitch()
    {
        var emitter = CreateEmitter();
        Assert.That(Emitter.IsApxNddCompatibleInstruction(INS_add), Is.True);
        Assert.That(Emitter.IsApxNddCompatibleInstruction(INS_mov), Is.False);
        Assert.That(emitter.IsApxNddEncodableInstruction(INS_add), Is.False);

        emitter.UsePromotedEvexEncodings = true;

        Assert.That(emitter.IsApxNddEncodableInstruction(INS_add), Is.True);
        Assert.That(emitter.IsApxNddEncodableInstruction(INS_mov), Is.False);
        Assert.That(() => emitter.emitInsModeFormat(INS_add, IF_MRD, useNDD: true),
            Throws.TypeOf<FatalJitException>());
    }

    [TestCase(IF_NONE, ID_OP_NONE, IS_NONE)]
    [TestCase(IF_LABEL, ID_OP_JMP, IS_NONE)]
    [TestCase(IF_SWR_LABEL, ID_OP_LBL, IS_SF_WR)]
    [TestCase(IF_METHOD, ID_OP_CALL, IS_NONE)]
    [TestCase(IF_RRW_SHF, ID_OP_SCNS, IS_R1_RW)]
    [TestCase(IF_MRD, ID_OP_SPEC, IS_GM_RD)]
    [TestCase(IF_SRD, ID_OP_SPEC, IS_SF_RD)]
    [TestCase(IF_ARD, ID_OP_SPEC, IS_AM_RD)]
    [TestCase(IF_SWR_RRD, ID_OP_NONE, IS_SF_WR | IS_R1_RD)]
    [TestCase(IF_MRW_RRW, ID_OP_DSP, IS_GM_RW | IS_R1_RW)]
    [TestCase(IF_SRW_RRD_CNS, ID_OP_CNS, IS_SF_RW | IS_R1_RD)]
    [TestCase(IF_RWR_RRD_MRD_CNS, ID_OP_DSP_CNS, IS_R1_WR | IS_R2_RD | IS_GM_RD)]
    [TestCase(IF_RWR_RRD_ARD_CNS, ID_OP_AMD_CNS, IS_R1_WR | IS_R2_RD | IS_AM_RD)]
    [TestCase(IF_RWR_RRD_RRD_RRD, ID_OP_SCNS, IS_R1_WR | IS_R2_RD | IS_R3_RD | IS_R4_RD)]
    public static void OperandAndSchedulingMetadataRetainNativeSpecialCases(Emitter.insFormat format,
        ID_OPS operands, IS_INFO scheduling)
    {
        Assert.That((ID_OPS)Emitter.emitFmtToOps[(int)format], Is.EqualTo(operands));
        Assert.That(Emitter.emitGetSchedInfo(format), Is.EqualTo(scheduling));
    }

    [Test]
    public static void EveryFormatRoundTripsWithoutChangingOtherDescriptorFieldsOrLogicalSizes()
    {
        var descriptor = CreateDescriptor();
        descriptor.idIns(INS_add);
        descriptor.idOpSize(EA_8BYTE);
        descriptor.idCodeSize(7);
        descriptor.idReg1(REG_R8);
        descriptor.idReg2(REG_R9);
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_NONE));

        for (uint value = 0; value < (uint)IF_COUNT; value++)
        {
            var format = (Emitter.insFormat)value;
            descriptor.idInsFmt(format);
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(7u));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R9));
            Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(16));
        }

        descriptor.idSetIsSmallDsc();
        descriptor.idInsFmt(IF_RWR_RRD);
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(8));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_RWR_RRD));
    }

    [Test]
    public static void SchedulingQueriesCoverAllRegisterAndMemoryBanks()
    {
        var descriptor = CreateDescriptor();
        descriptor.idInsFmt(IF_RWR_RRD_RRD_RRD);
        Assert.That(descriptor.idHasReg1(), Is.True);
        Assert.That(descriptor.idIsReg1Read(), Is.False);
        Assert.That(descriptor.idIsReg1Write(), Is.True);
        Assert.That(descriptor.idHasReg2(), Is.True);
        Assert.That(descriptor.idIsReg2Read(), Is.True);
        Assert.That(descriptor.idIsReg2Write(), Is.False);
        Assert.That(descriptor.idHasReg3(), Is.True);
        Assert.That(descriptor.idIsReg3Read(), Is.True);
        Assert.That(descriptor.idIsReg3Write(), Is.False);
        Assert.That(descriptor.idHasReg4(), Is.True);
        Assert.That(descriptor.idIsReg4Read(), Is.True);
        Assert.That(descriptor.idIsReg4Write(), Is.False);
        Assert.That(descriptor.idHasMem(), Is.False);
        Assert.That(descriptor.idHasMemRead(), Is.False);
        Assert.That(descriptor.idHasMemWrite(), Is.False);

        descriptor.idInsFmt(IF_MRW_RRW);
        Assert.That(descriptor.idHasMemGen(), Is.True);
        Assert.That(descriptor.idHasMemGenRead(), Is.True);
        Assert.That(descriptor.idHasMemGenWrite(), Is.True);
        Assert.That(descriptor.idHasMemRead(), Is.True);
        Assert.That(descriptor.idHasMemWrite(), Is.True);
        Assert.That(descriptor.idIsReg1Read(), Is.True);
        Assert.That(descriptor.idIsReg1Write(), Is.True);
        Assert.That(descriptor.idHasReg2(), Is.False);

        descriptor.idInsFmt(IF_SWR_RRD);
        Assert.That(descriptor.idHasMemStk(), Is.True);
        Assert.That(descriptor.idHasMemStkRead(), Is.False);
        Assert.That(descriptor.idHasMemStkWrite(), Is.True);
        Assert.That(descriptor.idHasMem(), Is.True);
        Assert.That(descriptor.idHasMemGen(), Is.False);

        descriptor.idInsFmt(IF_RWR_RRD_ARD);
        Assert.That(descriptor.idHasMemAdr(), Is.True);
        Assert.That(descriptor.idHasMemAdrRead(), Is.True);
        Assert.That(descriptor.idHasMemAdrWrite(), Is.False);
        Assert.That(descriptor.idHasMemStk(), Is.False);
        Assert.That(descriptor.idHasReg3(), Is.False);
        Assert.That(descriptor.idHasReg4(), Is.False);
    }

    [TestCase(IF_RWR_CNS, false)]
    [TestCase(IF_SWR_RRD, false)]
    [TestCase(IF_SWR_CNS, true)]
    [TestCase(IF_MWR_CNS, true)]
    [TestCase(IF_AWR_CNS, true)]
    [TestCase(IF_RWR_RRD_ARD_RRD, true)]
    public static void MemoryAndConstantQueriesUseDescriptorOperandClassification(Emitter.insFormat format, bool expected)
    {
        var descriptor = CreateDescriptor();
        descriptor.idInsFmt(format);

        Assert.That(descriptor.idHasMemAndCns(), Is.EqualTo(expected));
    }

#if !DEBUG
    [Test]
    public static void DescriptorFormatStorageRetainsTheNativeSevenBitTruncation()
    {
        var descriptor = CreateDescriptor();
        descriptor.idInsFmt((Emitter.insFormat)(0x180u | (uint)IF_SWR_RRD));

        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_SWR_RRD));
        Assert.That(Emitter.emitGetSchedInfo(IF_COUNT), Is.EqualTo(IS_NONE));
    }
#endif

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        return new CodeGen(compiler).Emitter;
    }

    private static Emitter.instrDesc CreateDescriptor()
    {
        var type = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)!;
        return (Emitter.instrDesc)Activator.CreateInstance(type, nonPublic: true)!;
    }
}
