// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterSimdMultioperandInstructionTests
{
    [Test]
    public static void GatherFormsPreserveVsibAndMaskOperands(
        [Values(INS_vpgatherdd, INS_vpgatherdq, INS_vpgatherqd, INS_vpgatherqq,
            INS_vgatherdps, INS_vgatherdpd, INS_vgatherqps, INS_vgatherqpd)] instruction ins,
        [Values(EA_16BYTE, EA_32BYTE)] emitAttr attr, [Values(1, 2, 4, 8)] int scale)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = false;
            emitter.emitIns_R_AR_R(ins, attr, REG_XMM1, REG_XMM2, REG_R12, REG_XMM15, scale, 0);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_ARD_RRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R12));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_XMM15));
            Assert.That(1u << (int)id.idAddr().iiaAddrMode.amScale, Is.EqualTo((uint)scale));
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)0));
            Assert.That(id.idCodeSize(), Is.EqualTo(6u));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(16));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            CheckAccounting(emitter);
        });
    }

    [TestCase(REG_RAX, 1, -129, 10u)]
    [TestCase(REG_RAX, 1, -128, 7u)]
    [TestCase(REG_RAX, 1, 127, 7u)]
    [TestCase(REG_RAX, 1, 128, 10u)]
    [TestCase(REG_RBP, 1, 0, 7u)]
    [TestCase(REG_R13, 1, 0, 7u)]
    [TestCase(REG_RBP, 8, 0, 7u)]
    [TestCase(REG_NA, 8, 0, 10u)]
    [TestCase(REG_RAX, 4, -8192, 10u)]
    [TestCase(REG_RAX, 4, -8191, 10u)]
    [TestCase(REG_RAX, 4, 8191, 10u)]
    [TestCase(REG_RAX, 4, 8192, 10u)]
    [TestCase(REG_RAX, 4, int.MinValue, 10u)]
    [TestCase(REG_RAX, 4, int.MaxValue, 10u)]
    public static void GatherAddressSizingPreservesVectorIndexAndDisplacement(
        regNumber baseReg, int scale, int offset, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = false;
            emitter.emitIns_R_AR_R(INS_vgatherdps, EA_32BYTE,
                REG_XMM1, REG_XMM2, baseReg, REG_XMM3, scale, offset);
            var id = Last(emitter);

            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(baseReg));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_XMM3));
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)offset));
            Assert.That(id.idIsLargeDsp(), Is.EqualTo(offset is < -8191 or > 8191));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(offset is < -8191 or > 8191 ? 24 : 16));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            CheckAccounting(emitter);
        });
    }

#if DEBUG
    [Test]
    public static void GatherDisassemblyRecordsNativeOperands()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
                emitter.emitIns_R_AR_R(INS_vgatherdps, EA_32BYTE, REG_XMM1, REG_XMM2, REG_RAX, REG_XMM3, 4, 0));

            Assert.That(Used(emitter), Is.GreaterThan(used));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(CurrentSize(emitter), Is.GreaterThan(0));
            Assert.That(LastInstruction(emitter)?.idIns(), Is.EqualTo(INS_vgatherdps));
            Assert.That(diagnostic, Does.Contain("vgatherdps"));
        });
    }
#endif

    [Test]
    public static void MaskStoresPreserveMaskAndDataRegisterOrder(
        [Values(INS_vmaskmovps, INS_vmaskmovpd, INS_vpmaskmovd, INS_vpmaskmovq)] instruction ins,
        [Values(EA_16BYTE, EA_32BYTE)] emitAttr attr, [Values(false, true)] bool sameRegister)
    {
        WithEmitter((_, emitter) =>
        {
            var data = sameRegister ? REG_XMM1 : REG_XMM15;
            emitter.emitIns_AR_R_R(ins, attr, REG_XMM1, data, REG_RAX, 0);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_AWR_RRD_RRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(id.idReg2(), Is.EqualTo(data));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)0));
            Assert.That(id.idCodeSize(), Is.EqualTo(5u));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(16));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            CheckAccounting(emitter);
        });
    }

    [TestCase(REG_RAX, -129, false, 9u)]
    [TestCase(REG_RAX, -128, false, 6u)]
    [TestCase(REG_RAX, 127, false, 6u)]
    [TestCase(REG_RAX, 128, false, 9u)]
    [TestCase(REG_RSP, 0, false, 6u)]
    [TestCase(REG_RBP, 0, false, 6u)]
    [TestCase(REG_R12, 0, false, 6u)]
    [TestCase(REG_R13, 0, false, 6u)]
    [TestCase(REG_NA, 0, false, 10u)]
    [TestCase(REG_RAX, 0, true, 9u)]
    [TestCase(REG_NA, 0, true, 9u)]
    [TestCase(REG_RAX, -8192, false, 9u)]
    [TestCase(REG_RAX, -8191, false, 9u)]
    [TestCase(REG_RAX, 8191, false, 9u)]
    [TestCase(REG_RAX, 8192, false, 9u)]
    [TestCase(REG_RAX, int.MinValue, false, 9u)]
    [TestCase(REG_RAX, int.MaxValue, false, 9u)]
    public static void MaskStoresRetainNativeAddressAndDescriptorBoundaries(
        regNumber baseReg, int offset, bool relocation, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var attr = relocation ? EA_32BYTE | EA_DSP_RELOC_FLG : EA_32BYTE;
            emitter.emitIns_AR_R_R(INS_vpmaskmovd, attr, REG_XMM1, REG_XMM2, baseReg, offset, INS_OPTS_NONE);
            var id = Last(emitter);

            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(baseReg));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)offset));
            Assert.That(id.idIsDspReloc(), Is.EqualTo(relocation));
            Assert.That(id.idIsLargeDsp(), Is.EqualTo(offset is < -8191 or > 8191));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(offset is < -8191 or > 8191 ? 24 : 16));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            CheckAccounting(emitter);
        });
    }

#if DEBUG
    [Test]
    public static void MaskStoreDisassemblyRecordsNativeOperands()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
                emitter.emitIns_AR_R_R(INS_vmaskmovps, EA_32BYTE, REG_XMM1, REG_XMM2, REG_RAX, 0));

            Assert.That(Used(emitter), Is.GreaterThan(used));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(CurrentSize(emitter), Is.GreaterThan(0));
            Assert.That(LastInstruction(emitter)?.idIns(), Is.EqualTo(INS_vmaskmovps));
            Assert.That(diagnostic, Does.Contain("vmaskmovps"));
        });
    }
#endif

    [TestCase(INS_vpermi2b, true)]
    [TestCase(INS_vpermi2d, true)]
    [TestCase(INS_vpermi2pd, true)]
    [TestCase(INS_vpermi2ps, true)]
    [TestCase(INS_vpermi2q, true)]
    [TestCase(INS_vpermi2w, true)]
    [TestCase(INS_vpermt2b, true)]
    [TestCase(INS_vpermt2d, true)]
    [TestCase(INS_vpermt2pd, true)]
    [TestCase(INS_vpermt2ps, true)]
    [TestCase(INS_vpermt2q, true)]
    [TestCase(INS_vpermt2w, true)]
    [TestCase(INS_vfmadd132pd, true)]
    [TestCase(INS_vfnmsub231ss, true)]
    [TestCase(INS_vpdpbusd, true)]
    [TestCase(INS_vpdpwssds, true)]
    [TestCase(INS_vpdpwsud, true)]
    [TestCase(INS_vpdpwuuds, true)]
    [TestCase(INS_vpdpbssd, true)]
    [TestCase(INS_vpdpbuuds, true)]
    [TestCase(INS_vbmacor16x16x16, true)]
    [TestCase(INS_vbitrev, true)]
    [TestCase(INS_vpmadd52huq, true)]
    [TestCase(INS_vpmadd52luq, true)]
    [TestCase(INS_vfmadd132ph, true)]
    [TestCase(INS_vfnmsub231sh, true)]
    [TestCase(INS_addps, false)]
    [TestCase(INS_blendvps, false)]
    [TestCase(INS_vblendvps, false)]
    [TestCase(INS_vblendmps, false)]
    [TestCase(INS_vpternlogd, false)]
    public static void RmwClassificationRetainsAllNativeFamilies(instruction ins, bool expected)
    {
        Assert.That(Emitter.Is3OpRmwInstruction(ins), Is.EqualTo(expected));
    }

    [Test]
    public static void RmwFormsCopyOnlyWhenTheFirstOperandDiffers(
        [Values(0, 1, 2, 3)] int kind, [Values(false, true)] bool sameDestination)
    {
        WithEmitter((compiler, emitter) =>
        {
            var first = sameDestination ? REG_XMM3 : REG_XMM1;
            EmitRmw(compiler, emitter, kind, INS_vfmadd132ps, EA_16BYTE, first, INS_OPTS_NONE);
            var id = Last(emitter);

            Assert.That(CurrentCount(emitter), Is.EqualTo(sameDestination ? 1 : 2));
            Assert.That(id.idIns(), Is.EqualTo(INS_vfmadd132ps));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM3));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idInsFmt(), Is.EqualTo(kind switch
            {
                0 => IF_RRW_RRD_ARD,
                1 => IF_RRW_RRD_MRD,
                2 => IF_RRW_RRD_SRD,
                _ => IF_RRW_RRD_RRD,
            }));
            if (kind == 3)
            {
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM4));
            }
            else
            {
                CheckMemory(emitter, id, kind, 0);
            }

            if (!sameDestination)
            {
                CheckCopy(Instructions(emitter)[0], REG_XMM3, REG_XMM1);
            }
            CheckAccounting(emitter);
        });
    }

    [TestCase(REG_XMM3, REG_XMM3)]
    [TestCase(REG_XMM3, REG_XMM4)]
    [TestCase(REG_XMM2, REG_XMM3)]
    public static void RmwRegisterAliasesAreSafeWhenTheFirstOperandIsTheDestination(
        regNumber second, regNumber third)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_SIMD_R_R_R_R(INS_vfmadd132ps, EA_16BYTE,
                REG_XMM3, REG_XMM3, second, third, INS_OPTS_NONE);
            var id = Last(emitter);
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(id.idReg2(), Is.EqualTo(second));
            Assert.That(id.idReg3(), Is.EqualTo(third));
        });
    }

    [Test]
    public static void RmwFormsForwardEmbeddedMasks([Values(0, 1, 2, 3)] int kind)
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitRmw(compiler, emitter, kind, INS_vfmadd132ps, EA_64BYTE, REG_XMM3,
                INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero);
            var id = Last(emitter);
            Assert.That(id.idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(id.idIsEvexZContextSet(), Is.True);
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void RmwRegisterFormForwardsEmbeddedRounding()
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitRmw(compiler, emitter, 3, INS_vfmadd132ps, EA_64BYTE, REG_XMM3, INS_OPTS_EVEX_er_ru);
            Assert.That(Last(emitter).idGetEvexbContext(), Is.EqualTo(2u));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));
        });
    }

    [Test]
    public static void RmwAndImmediateMemoryFormsForwardBroadcast(
        [Values(0, 1, 2)] int kind, [Values(false, true)] bool immediate)
    {
        WithEmitter((compiler, emitter) =>
        {
            if (immediate)
            {
                EmitImmediate(compiler, emitter, kind, REG_XMM3, 7, INS_OPTS_EVEX_eb);
            }
            else
            {
                EmitRmw(compiler, emitter, kind, INS_vfmadd132ps, EA_64BYTE, REG_XMM3, INS_OPTS_EVEX_eb);
            }

            Assert.That(Last(emitter).idGetEvexbContext(), Is.EqualTo(kind == 1 ? 1u : 3u));
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void VexBlendFormsConvertLegacyOpcodesWithoutCopies(
        [Values(0, 1, 2, 3)] int kind,
        [Values(INS_blendvps, INS_blendvpd, INS_pblendvb)] instruction ins)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.UseEvexEncodings = false;
            EmitBlend(compiler, emitter, kind, ins, EA_16BYTE, REG_XMM3, REG_XMM1, REG_XMM15, 0);
            var id = Last(emitter);

            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(id.idIns(), Is.EqualTo(ins switch
            {
                INS_blendvps => INS_vblendvps,
                INS_blendvpd => INS_vblendvpd,
                _ => INS_vpblendvb,
            }));
            Assert.That(id.idInsFmt(), Is.EqualTo(BlendFormat(kind)));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM3));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            Assert.That(id.idCodeSize(), Is.EqualTo(kind switch { 1 => 10u, 2 => 7u, _ => 6u }));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(kind == 3 ? 16 : 24));
            if (kind == 3)
            {
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM2));
                Assert.That(id.idReg4(), Is.EqualTo(REG_XMM15));
            }
            else
            {
                Assert.That(Constant(emitter, id), Is.EqualTo((nint)REG_XMM15));
                CheckMemory(emitter, id, kind, 0);
            }
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void LegacyBlendFormsMoveTheMaskBeforeTheDestination(
        [Values(0, 1, 2, 3)] int kind, [Values(false, true)] bool maskAlreadyInXmm0,
        [Values(false, true)] bool sameDestination)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.UseVexEncodings = false;
            emitter.UseEvexEncodings = false;
            var first = sameDestination ? REG_XMM3 : REG_XMM1;
            EmitBlend(compiler, emitter, kind, INS_blendvps, EA_16BYTE, REG_XMM3, first,
                maskAlreadyInXmm0 ? REG_XMM0 : REG_XMM4, 0);
            var id = Last(emitter);
            var copyCount = 0;
            if (!maskAlreadyInXmm0)
            {
                CheckCopy(Instructions(emitter)[copyCount++], REG_XMM0, REG_XMM4);
            }
            if (!sameDestination)
            {
                CheckCopy(Instructions(emitter)[copyCount++], REG_XMM3, REG_XMM1);
            }

            Assert.That(CurrentCount(emitter), Is.EqualTo(copyCount + 1));
            Assert.That(id.idIns(), Is.EqualTo(INS_blendvps));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM3));
            Assert.That(id.idInsFmt(), Is.EqualTo(kind switch
            {
                0 => IF_RWR_ARD,
                1 => IF_RWR_MRD,
                2 => IF_RWR_SRD,
                _ => IF_RWR_RRD,
            }));
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void LegacyBlendCanReuseXmm0WhenTheFirstOperandIsTheMask([Values(0, 1, 2, 3)] int kind)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.UseVexEncodings = false;
            emitter.UseEvexEncodings = false;
            EmitBlend(compiler, emitter, kind, INS_blendvps, EA_16BYTE, REG_XMM0, REG_XMM4, REG_XMM4, 0);
            Assert.That(Last(emitter).idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(Last(emitter).idIns(), Is.EqualTo(INS_blendvps));
            CheckCopy(Instructions(emitter)[0], REG_XMM0, REG_XMM4);
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void EvexBlendStoresTheExplicitMaskAndUsesNoRegisterImmediateByte(
        [Values(0, 1, 2, 3)] int kind,
        [Values(INS_vblendmps, INS_vblendmpd, INS_vpblendmb, INS_vpblendmd, INS_vpblendmq, INS_vpblendmw)] instruction ins)
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitBlend(compiler, emitter, kind, ins, EA_64BYTE, REG_XMM3, REG_XMM1, REG_K7, 0);
            var id = Last(emitter);
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(BlendFormat(kind)));
            Assert.That(id.idGetEvexAaaContext(), Is.Zero);
            if (kind == 3)
            {
                Assert.That(id.idReg4(), Is.EqualTo(REG_K7));
                Assert.That(id.idCodeSize(), Is.EqualTo(6u));
                Assert.That(id.NativeLogicalSize, Is.EqualTo(16));
            }
            else
            {
                Assert.That(Constant(emitter, id), Is.EqualTo((nint)REG_K7));
                Assert.That(id.idCodeSize(), Is.EqualTo(kind == 0 ? 7u : 11u));
                Assert.That(id.NativeLogicalSize, Is.EqualTo(24));
            }
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void BlendMemoryRetainsBroadcastAndDisplacementMetadata([Values(0, 1, 2)] int kind)
    {
        WithEmitter((compiler, emitter) =>
        {
            var offset = kind == 2 ? 0 : 16;
            EmitBlend(compiler, emitter, kind, INS_vblendmps, EA_64BYTE,
                REG_XMM3, REG_XMM1, REG_K3, offset, INS_OPTS_EVEX_eb | INS_OPTS_EVEX_em_zero);
            var id = Last(emitter);
            Assert.That(id.idGetEvexbContext(), Is.EqualTo(kind == 1 ? 1u : 3u));
            Assert.That(id.idIsEvexZContextSet(), Is.True);
            Assert.That(id.idGetEvexAaaContext(), Is.Zero);
            CheckMemory(emitter, id, kind, offset);
            CheckAccounting(emitter);
        });
    }

    [Test]
    public static void BlendMemoryUsesLargeDisplacementDescriptors([Values(0, 1)] int kind)
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitBlend(compiler, emitter, kind, INS_vblendvps, EA_16BYTE,
                REG_XMM3, REG_XMM1, REG_XMM15, 65536);
            var id = Last(emitter);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(32));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)REG_XMM15));
            CheckMemory(emitter, id, kind, 65536);
            CheckAccounting(emitter);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GlobalBlendFieldsKeepOnlyExplicitRelocation(bool relocation)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_SIMD_R_R_C_R(INS_vblendvps, relocation ? EA_16BYTE | EA_DSP_RELOC_FLG : EA_16BYTE,
                REG_XMM3, REG_XMM1, REG_XMM4, FLD_GLOBAL_DS, -4, INS_OPTS_NONE);
            var id = Last(emitter);
            Assert.That(id.idIsDspReloc(), Is.EqualTo(relocation));
            Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.EqualTo((nuint)FLD_GLOBAL_DS));
            Assert.That(FieldDisplacement(emitter, id), Is.EqualTo((nint)(-4)));
        });
    }

    [Test]
    public static void ImmediateFormsPreserveSignedValuesCopiesAndMasks(
        [Values(0, 1, 2, 3)] int kind, [Values(-128, -16, 15, 16, 127)] int value,
        [Values(false, true)] bool sameDestination)
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitImmediate(compiler, emitter, kind, sameDestination ? REG_XMM3 : REG_XMM1, value,
                INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero);
            var id = Last(emitter);
            Assert.That(CurrentCount(emitter), Is.EqualTo(sameDestination ? 1 : 2));
            Assert.That(id.idIns(), Is.EqualTo(INS_vpternlogd));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM3));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM2));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)value));
            Assert.That(id.NativeLogicalSize, Is.EqualTo((value is >= -16 and <= 15) ? 16 : 24));
            Assert.That(id.idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(id.idIsEvexZContextSet(), Is.True);
            Assert.That(id.idInsFmt(), Is.EqualTo(kind switch
            {
                0 => IF_RWR_RRD_ARD_CNS,
                1 => IF_RWR_RRD_MRD_CNS,
                2 => IF_RWR_RRD_SRD_CNS,
                _ => IF_RWR_RRD_RRD_CNS,
            }));
            if (kind == 3)
            {
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM4));
            }
            else
            {
                CheckMemory(emitter, id, kind, 0, immediate: true);
            }
            if (!sameDestination)
            {
                CheckCopy(Instructions(emitter)[0], REG_XMM3, REG_XMM1);
            }
            CheckAccounting(emitter);
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsSimdFamiliesAndRequiredCopies(
        [Values(0, 1, 2, 3)] int kind, [Values(0, 1, 2)] int family)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                switch (family)
                {
                    case 0:
                    {
                        EmitRmw(compiler, emitter, kind, INS_vfmadd132ps, EA_16BYTE, REG_XMM1, INS_OPTS_NONE);
                        break;
                    }

                    case 1:
                    {
                        EmitBlend(compiler, emitter, kind, INS_blendvps, EA_16BYTE,
                            REG_XMM3, REG_XMM1, REG_XMM4, 0);
                        break;
                    }

                    default:
                    {
                        EmitImmediate(compiler, emitter, kind, REG_XMM1, 7, INS_OPTS_NONE);
                        break;
                    }
                }
            });
            Assert.That(Used(emitter), Is.GreaterThan(used));
            Assert.That(CurrentCount(emitter), Is.GreaterThan(0));
            Assert.That(CurrentSize(emitter), Is.GreaterThan(0));
            Assert.That(LastInstruction(emitter), Is.Not.Null);
            Assert.That(diagnostic, Does.Contain(family switch
            {
                0 => "fmadd",
                1 => "blend",
                _ => "vpternlogd",
            }));
        });
    }
#endif

    private static void EmitRmw(Compiler compiler, Emitter emitter, int kind, instruction ins,
        emitAttr attr, regNumber first, insOpts options)
    {
        switch (kind)
        {
            case 0:
            {
                emitter.emitIns_SIMD_R_R_R_A(ins, attr, REG_XMM3, first, REG_XMM2, Address(compiler, 0), options);
                break;
            }

            case 1:
            {
                emitter.emitIns_SIMD_R_R_R_C(ins, attr, REG_XMM3, first, REG_XMM2,
                    Compiler.eeFindJitDataOffs(64), 0, options);
                break;
            }

            case 2:
            {
                emitter.emitIns_SIMD_R_R_R_S(ins, attr, REG_XMM3, first, REG_XMM2, 0, 0, options);
                break;
            }

            default:
            {
                emitter.emitIns_SIMD_R_R_R_R(ins, attr, REG_XMM3, first, REG_XMM2, REG_XMM4, options);
                break;
            }
        }
    }

    private static void EmitBlend(Compiler compiler, Emitter emitter, int kind, instruction ins,
        emitAttr attr, regNumber target, regNumber first, regNumber mask, int offset,
        insOpts options = INS_OPTS_NONE)
    {
        switch (kind)
        {
            case 0:
            {
                emitter.emitIns_SIMD_R_R_A_R(ins, attr, target, first, mask, Address(compiler, offset), options);
                break;
            }

            case 1:
            {
                emitter.emitIns_SIMD_R_R_C_R(ins, attr, target, first, mask,
                    Compiler.eeFindJitDataOffs(64), offset, options);
                break;
            }

            case 2:
            {
                emitter.emitIns_SIMD_R_R_S_R(ins, attr, target, first, mask, 0, offset, options);
                break;
            }

            default:
            {
                emitter.emitIns_SIMD_R_R_R_R(ins, attr, target, first, REG_XMM2, mask, options);
                break;
            }
        }
    }

    private static void EmitImmediate(Compiler compiler, Emitter emitter, int kind,
        regNumber first, int value, insOpts options)
    {
        switch (kind)
        {
            case 0:
            {
                emitter.emitIns_SIMD_R_R_R_A_I(INS_vpternlogd, EA_16BYTE,
                    REG_XMM3, first, REG_XMM2, Address(compiler, 0), value, options);
                break;
            }

            case 1:
            {
                emitter.emitIns_SIMD_R_R_R_C_I(INS_vpternlogd, EA_16BYTE,
                    REG_XMM3, first, REG_XMM2, Compiler.eeFindJitDataOffs(64), 0, value, options);
                break;
            }

            case 2:
            {
                emitter.emitIns_SIMD_R_R_R_S_I(INS_vpternlogd, EA_16BYTE,
                    REG_XMM3, first, REG_XMM2, 0, 0, value, options);
                break;
            }

            default:
            {
                emitter.emitIns_SIMD_R_R_R_R_I(INS_vpternlogd, EA_16BYTE,
                    REG_XMM3, first, REG_XMM2, REG_XMM4, value, options);
                break;
            }
        }
    }

    private static Emitter.insFormat BlendFormat(int kind)
    {
        return kind switch
        {
            0 => IF_RWR_RRD_ARD_RRD,
            1 => IF_RWR_RRD_MRD_RRD,
            2 => IF_RWR_RRD_SRD_RRD,
            _ => IF_RWR_RRD_RRD_RRD,
        };
    }

    private static void CheckCopy(Emitter.instrDesc id, regNumber destination, regNumber source)
    {
        Assert.That(id.idIns(), Is.EqualTo(INS_movaps));
        Assert.That(id.idReg1(), Is.EqualTo(destination));
        Assert.That(id.idReg2(), Is.EqualTo(source));
    }

    private static void CheckMemory(Emitter emitter, Emitter.instrDesc id,
        int kind, int offset, bool immediate = false)
    {
        switch (kind)
        {
            case 0:
            {
                Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
                Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)offset));
                break;
            }

            case 1:
            {
                Assert.That(id.idIsDspReloc(), Is.True);
                Assert.That(id.idAddr().iiaGetJitDataOffset(), Is.EqualTo(64));
                Assert.That(FieldDisplacement(emitter, id), Is.EqualTo((nint)offset));
                break;
            }

            default:
            {
                Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
                Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
#if DEBUG
                if (id.idInsFmt() == IF_RWR_RRD_SRD_RRD || immediate)
                {
                    var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
                    Assert.That(info.idVarRefOffs, Is.EqualTo(immediate ? 0x1234u : 0u));
                }
#endif
                break;
            }
        }
    }

    private static void CheckAccounting(Emitter emitter)
    {
        var codeSize = 0;
        nuint logicalSize = 0;
        for (var i = 0; i < CurrentCount(emitter); i++)
        {
            var id = Instructions(emitter)[i];
            codeSize += (int)id.idCodeSize();
            logicalSize += (nuint)id.NativeLogicalSize;
#if DEBUG
            var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
            Assert.That(info.idSize, Is.EqualTo((nuint)id.NativeLogicalSize));
            logicalSize += (nuint)IntPtr.Size;
#endif
        }

        Assert.That(CurrentSize(emitter), Is.EqualTo(codeSize));
        Assert.That(Used(emitter), Is.EqualTo(logicalSize));
    }

    private static GenTreeIndir Address(Compiler compiler, int offset)
    {
        var reg = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
        reg.RegNum = REG_RAX;
        GenTree address = reg;
        if (offset != 0)
        {
            address = new GenTreeAddrMode(TYP_BYREF, reg, null, 0, offset)
            {
                RegNum = REG_NA,
                IsContained = true,
            };
        }

        return new GenTreeIndir(GT_IND, TYP_SIMD16, address);
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
            codeGen.Emitter.emitVarRefOffs = 0x1234;
#endif
            action(compiler, codeGen.Emitter);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* _, void* address) => CorInfoReloc.RELATIVE32;

    private static Emitter.instrDesc Last(Emitter emitter) =>
        LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");

    private static List<Emitter.instrDesc> Instructions(Emitter emitter) =>
        Buffer(emitter) ?? throw new AssertionException("Missing recording buffer.");

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint Constant(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint AddressDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsDsp")]
    private static extern nint FieldDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);
}
