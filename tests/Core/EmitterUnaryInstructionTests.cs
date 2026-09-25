// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterUnaryInstructionTests
{
    [TestCase(INS_neg, EA_4BYTE, REG_RAX, IF_RRW, 2u)]
    [TestCase(INS_not, EA_8BYTE, REG_RCX, IF_RRW, 3u)]
    [TestCase(INS_inc, EA_1BYTE, REG_RSP, IF_RRW, 3u)]
    [TestCase(INS_dec, EA_2BYTE, REG_R8, IF_RRW, 4u)]
    [TestCase(INS_inc, EA_4BYTE, REG_R8, IF_RRW, 3u)]
    [TestCase(INS_neg, EA_8BYTE, REG_R16, IF_RRW, 4u)]
    [TestCase(INS_shl_1, EA_8BYTE, REG_RAX, IF_RRW, 3u)]
    [TestCase(INS_seto, EA_1BYTE, REG_RAX, IF_RWR, 3u)]
    [TestCase(INS_setg, EA_1BYTE, REG_RSP, IF_RWR, 4u)]
    [TestCase(INS_sete, EA_1BYTE, REG_R16, IF_RWR, 4u)]
    public static void SingleRegisterInstructionsRetainNativeFormatsAndWidths(
        instruction ins, emitAttr attr, regNumber reg, Emitter.insFormat format, uint size)
    {
        WithEmitter(emitter =>
        {
            emitter.UseRex2Encodings = true;
            emitter.emitIns_R(ins, attr, reg);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idIsSmallDsc(), Is.True);
            Assert.That(id.NativeLogicalSize, Is.EqualTo(8));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [TestCase(INS_push, REG_RAX, INS_OPTS_NONE, IF_RRD, 1u, false)]
    [TestCase(INS_pop, REG_R8, INS_OPTS_NONE, IF_RWR, 2u, false)]
    [TestCase(INS_push, REG_R16, INS_OPTS_NONE, IF_RRD, 3u, false)]
    [TestCase(INS_push, REG_RAX, INS_OPTS_APX_ppx, IF_RRD, 3u, true)]
    [TestCase(INS_pop, REG_RAX, INS_OPTS_APX_ppx, IF_RWR, 3u, true)]
    [TestCase(INS_push_hide, REG_RAX, INS_OPTS_APX_ppx, IF_RRD, 1u, false)]
    [TestCase(INS_pop_hide, REG_R8, INS_OPTS_APX_ppx, IF_RWR, 2u, false)]
    public static void PushPopVariantsPreservePpxEligibilityAndFixedOutgoingStackDepth(
        instruction ins, regNumber reg, insOpts options, Emitter.insFormat format, uint size, bool ppx)
    {
        WithEmitter(emitter =>
        {
            emitter.UseRex2Encodings = true;
            var depth = emitter.emitCntStackDepth;
            var maximum = emitter.emitMaxStackDepth;
            emitter.emitIns_R(ins, EA_8BYTE, reg, options);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idIsApxPpxContextSet(), Is.EqualTo(ppx));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(emitter.emitCntStackDepth, Is.EqualTo(depth));
            Assert.That(emitter.emitMaxStackDepth, Is.EqualTo(maximum));
        });
    }

    [Test]
    public static void SingleRegisterRecordingRetainsGcAttributes()
    {
        WithEmitter(emitter =>
        {
            emitter.emitIns_R(INS_push, EA_GCREF, REG_RAX);
            Assert.That(Last(emitter).idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(Last(emitter).idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(1u));
        });
    }

    [Test]
    public static void ApxNoFlagsAndZeroUpperOptionsAreRecordedBeforeSizing()
    {
        WithEmitter(emitter =>
        {
            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R(INS_neg, EA_8BYTE, REG_RAX, INS_OPTS_EVEX_nf);
            Assert.That(Last(emitter).idIsEvexNfContextSet(), Is.True);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));

            emitter.emitIns_R(INS_seto_apx, EA_4BYTE, REG_RAX, INS_OPTS_EVEX_zu);
            Assert.That(Last(emitter).idIsEvexZuContextSet(), Is.True);
            Assert.That(Last(emitter).idInsFmt(), Is.EqualTo(IF_RWR));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));

            emitter.emitIns_R(INS_setg_apx, EA_4BYTE, REG_RAX);
            Assert.That(Last(emitter).idIsEvexZuContextSet(), Is.False);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));
        });
    }

    [TestCase(INS_neg, false, false, false, 2, false)]
    [TestCase(INS_neg, true, false, false, 2, false)]
    [TestCase(INS_neg, false, true, false, 2, false)]
    [TestCase(INS_neg, true, true, false, 1, true)]
    [TestCase(INS_not, true, true, false, 1, true)]
    [TestCase(INS_neg, true, true, true, 1, false)]
    [TestCase(INS_neg, false, false, true, 1, false)]
    [NonParallelizable]
    public static void BaseUnaryWrappersSelectNddOnlyWhenEnabledCompatibleAndNeeded(
        instruction ins, bool enabled, bool promoted, bool sameRegister, int count, bool ndd)
    {
        var saved = ApxNdd(ref JitConfig);
        ApxNdd(ref JitConfig) = enabled ? 1 : 0;
        try
        {
            WithEmitter(emitter =>
            {
                emitter.UsePromotedEvexEncodings = promoted;
                var source = sameRegister ? REG_RAX : REG_RCX;
                emitter.emitIns_BASE_R_R(ins, EA_8BYTE, REG_RAX, source);
                var id = Last(emitter);

                Assert.That(emitter.DoJitUseApxNDD(ins), Is.EqualTo(enabled && promoted));
                Assert.That(emitter.DoJitUseApxNDD(INS_push), Is.False);
                Assert.That(CurrentCount(emitter), Is.EqualTo(count));
                Assert.That(CurrentSize(emitter), Is.EqualTo(sameRegister ? 3 : 6));
                Assert.That(id.idIns(), Is.EqualTo(ins));
                Assert.That(id.idReg1(), Is.EqualTo(REG_RAX));
                Assert.That(id.idIsEvexNdContextSet(), Is.EqualTo(ndd));
                Assert.That(id.idInsFmt(), Is.EqualTo(ndd ? IF_RWR_RRD : IF_RRW));
                if (ndd)
                {
                    Assert.That(id.idReg2(), Is.EqualTo(source));
                }
                else if (count == 2)
                {
                    var descriptors = Buffer(emitter) ?? throw new AssertionException("Missing instruction buffer.");
                    Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                    Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RAX));
                    Assert.That(descriptors[0].idReg2(), Is.EqualTo(source));
                }
            });
        }
        finally
        {
            ApxNdd(ref JitConfig) = saved;
        }
    }

    [TestCase(REG_RSP, EA_1BYTE, 0UL, 0x4000000000UL, 4u)]
    [TestCase(REG_R8, EA_4BYTE, 0UL, 0x4100000000UL, 0u)]
    [TestCase(REG_R16, EA_8BYTE, 0UL, 0UL, 0u)]
    [TestCase(REG_R16, EA_8BYTE, 0xD50000000000UL, 0xD51000000000UL, 0u)]
    [TestCase(REG_R24, EA_8BYTE, 0UL, 0xD51100000000UL, 0u)]
    [TestCase(REG_R24, EA_8BYTE, 0x62F0000000000000UL, 0x62D8000000000000UL, 0u)]
    [TestCase(REG_RSP, EA_1BYTE, 0x62F0000000000000UL, 0x62F0000000000000UL, 4u)]
    [TestCase(REG_XMM8, EA_16BYTE, 0xC4E07800000000UL, 0xC4C07800000000UL, 0u)]
    [TestCase(REG_XMM16, EA_16BYTE, 0x62F0000000000000UL, 0x62B0000000000000UL, 0u)]
    [TestCase(REG_XMM27, EA_16BYTE, 0x62F0000000000000UL, 0x6290000000000000UL, 3u)]
    public static void RegisterOperandBitsPreserveExistingPrefixesAndHighRegisterEncodings(
        regNumber reg, emitAttr attr, ulong code, ulong expected, uint encoding)
    {
        WithEmitter(emitter =>
        {
            emitter.UseRex2Encodings = true;
            var id = EmitterInstructionAllocationTests.Allocate(emitter, attr);
            id.idIns(reg.IsFltReg ? INS_movaps : INS_neg);
            id.idInsFmt(IF_RWR_RRD);
            id.idReg1(reg);
            var encoded = code;
            var actual = emitter.insEncodeReg012(id, reg, attr, &encoded);

            Assert.That(actual, Is.EqualTo(encoding));
            Assert.That(encoded, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void RegisterOperandEncodingAcceptsNullForUnextendedRegisters()
    {
        WithEmitter(emitter =>
        {
            var id = EmitterInstructionAllocationTests.Allocate(emitter, EA_4BYTE);
            id.idIns(INS_neg);
            Assert.That(emitter.insEncodeReg012(id, REG_RDX, EA_4BYTE, null), Is.EqualTo(2u));
            Assert.That(emitter.insEncodeReg012(id, REG_RSP, EA_1BYTE, null), Is.EqualTo(4u));
            Assert.That(AbsRegNumber(REG_XMM27), Is.EqualTo((regNumber)27));
            Assert.That(AbsRegNumber(REG_K3), Is.EqualTo((regNumber)3));
            Assert.That(RegEncoding(REG_K3), Is.EqualTo(3u));
            Assert.That(emitter.insEncodeMRreg(id, REG_RDX, EA_4BYTE, insCodeMR(INS_neg)),
                Is.EqualTo(0xDAF6UL));
        });
    }

    [TestCase(0UL, 0x4200000000UL)]
    [TestCase(0xC4E07800000000UL, 0xC4A07800000000UL)]
    [TestCase(0x62F0000000000000UL, 0x62B0000000000000UL)]
    public static void XPrefixUsesInvertedBitsOnlyForVexAndEvex(ulong code, ulong expected)
    {
        WithEmitter(emitter =>
        {
            var id = EmitterInstructionAllocationTests.Allocate(emitter, EA_4BYTE);
            id.idIns(INS_neg);
            id.idInsFmt(IF_RRW);
            Assert.That(emitter.AddRexXPrefix(id, code), Is.EqualTo(expected));

            emitter.UseRex2Encodings = true;
            id.idReg1(REG_R16);
            Assert.That(emitter.AddRexXPrefix(id, 0), Is.EqualTo(0xD50200000000UL));
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void DisassemblyRejectsBeforeAllocationCopiesOrElision(int entrypoint)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                if (entrypoint == 0)
                {
                    emitter.emitIns_R(INS_neg, EA_8BYTE, REG_RAX);
                }
                else
                {
                    emitter.emitIns_BASE_R_R(INS_neg, EA_8BYTE, REG_RAX, entrypoint == 1 ? REG_RCX : REG_RAX);
                }
            });

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Used(emitter), Is.EqualTo(used));
            Assert.That(CurrentCount(emitter), Is.Zero);
            Assert.That(CurrentSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }
#endif

    private static void WithEmitter(Action<Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (_, codeGen, _) => action(codeGen.Emitter));
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableApxNDD")]
    private static extern ref int ApxNdd(ref JitConfigValues values);

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
