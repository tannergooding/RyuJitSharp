// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class EmitterRegisterInstructionTests
{
    [TestCase(INS_mov, EA_8BYTE, REG_RAX, 0xFFFFFFFFL, EA_4BYTE, 5u, IF_RWR_CNS)]
    [TestCase(INS_mov, EA_8BYTE, REG_R8, 0xFFFFFFFFL, EA_4BYTE, 6u, IF_RWR_CNS)]
    [TestCase(INS_mov, EA_8BYTE, REG_RAX, 0x100000000L, EA_8BYTE, 10u, IF_RWR_CNS)]
    [TestCase(INS_mov, EA_8BYTE, REG_RAX, -1L, EA_8BYTE, 10u, IF_RWR_CNS)]
    [TestCase(INS_add, EA_8BYTE, REG_RAX, 127L, EA_8BYTE, 4u, IF_RRW_CNS)]
    [TestCase(INS_add, EA_8BYTE, REG_RCX, -128L, EA_8BYTE, 4u, IF_RRW_CNS)]
    [TestCase(INS_add, EA_8BYTE, REG_RAX, 128L, EA_8BYTE, 6u, IF_RRW_CNS)]
    [TestCase(INS_add, EA_8BYTE, REG_RCX, -129L, EA_8BYTE, 7u, IF_RRW_CNS)]
    [TestCase(INS_add, EA_1BYTE, REG_RAX, 5L, EA_1BYTE, 2u, IF_RRW_CNS)]
    [TestCase(INS_add, EA_1BYTE, REG_RSI, 5L, EA_1BYTE, 4u, IF_RRW_CNS)]
    [TestCase(INS_add, EA_2BYTE, REG_RAX, 128L, EA_2BYTE, 4u, IF_RRW_CNS)]
    [TestCase(INS_test, EA_8BYTE, REG_RAX, 1L, EA_8BYTE, 6u, IF_RRD_CNS)]
    [TestCase(INS_test, EA_8BYTE, REG_RCX, 1L, EA_8BYTE, 7u, IF_RRD_CNS)]
    [TestCase(INS_imul_08, EA_4BYTE, REG_RAX, 7L, EA_4BYTE, 4u, IF_RRD_CNS)]
    [TestCase(INS_pslld, EA_16BYTE, REG_XMM0, 4L, EA_16BYTE, 5u, IF_RWR_CNS)]
    [TestCase(INS_pslld, EA_16BYTE, REG_XMM8, 4L, EA_16BYTE, 6u, IF_RWR_CNS)]
    [TestCase(INS_pslld, EA_16BYTE, REG_XMM16, 4L, EA_16BYTE, 7u, IF_RWR_CNS)]
    public static void ImmediatesPreserveWidthSignExtensionAndPrefixSizing(
        instruction ins, emitAttr attr, regNumber reg, long value, emitAttr expectedAttr,
        uint expectedSize, Emitter.insFormat expectedFormat)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_I(ins, attr, reg, (nint)value);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idOpSize(), Is.EqualTo(expectedAttr));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)value));
            Assert.That(id.idCodeSize(), Is.EqualTo(expectedSize));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)expectedSize));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [TestCase(INS_shl_N, -1L, 127L)]
    [TestCase(INS_ror_N, 128L, 0L)]
    [TestCase(INS_sar_N, 255L, 127L)]
    public static void ShiftCountsAreRecordedWithTheNativeSevenBitMask(instruction ins, long value, long expected)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_I(ins, EA_8BYTE, REG_RCX, (nint)value);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RRW_SHF));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)expected));
            Assert.That(id.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [TestCase(INS_mov, EA_8BYTE, REG_RAX, 10u)]
    [TestCase(INS_add, EA_4BYTE, REG_RAX, 5u)]
    [TestCase(INS_add, EA_4BYTE, REG_RCX, 6u)]
    public static void RelocatableImmediatesDoNotUseNarrowEncodings(
        instruction ins, emitAttr attr, regNumber reg, uint size)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = true;
            // Force a large descriptor as required by the native relocation sanity contract.
            emitter.emitIns_R_I(ins, attr | EA_CNS_RELOC_FLG, reg, 0x123456);
            var id = Last(emitter);

            Assert.That(id.idIsCnsReloc(), Is.True);
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)0x123456));
        });
    }

    [TestCase(false, false, 8)]
    [TestCase(true, false, 8)]
    [TestCase(false, true, 8)]
    [TestCase(true, true, 16)]
    public static void NativeAotSectionRelocationsRequireAddressStorage(bool nativeAot, bool sectionRelative, int logicalSize)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.eeInfo.targetAbi = nativeAot ? CORINFO_NATIVEAOT_ABI : CORINFO_CORECLR_ABI;
            var attr = EA_4BYTE | (sectionRelative ? EA_CNS_SEC_RELOC : EA_UNKNOWN);
            emitter.emitIns_R_I(INS_mov, attr, REG_RAX, 7);
            var id = Last(emitter);

            Assert.That(id.NativeLogicalSize, Is.EqualTo(logicalSize));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)7));
            if (nativeAot && sectionRelative)
            {
                Assert.That(id.idAddr().iiaSecRel, Is.True);
            }
        });
    }

    [TestCase(INS_add, EA_8BYTE, REG_RAX, REG_RCX, 3u, IF_RRW_RRD)]
    [TestCase(INS_xor, EA_8BYTE, REG_RAX, REG_RAX, 2u, IF_RRW_RRD)]
    [TestCase(INS_xor, EA_8BYTE, REG_R8, REG_R8, 3u, IF_RRW_RRD)]
    [TestCase(INS_xchg, EA_8BYTE, REG_RAX, REG_RCX, 3u, IF_RRW_RRW)]
    [TestCase(INS_add, EA_1BYTE, REG_RSI, REG_RDI, 3u, IF_RRW_RRD)]
    [TestCase(INS_add, EA_2BYTE, REG_RAX, REG_RCX, 3u, IF_RRW_RRD)]
    [TestCase(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, 4u, IF_RWR_RRD)]
    [TestCase(INS_addps, EA_16BYTE, REG_XMM8, REG_XMM1, 4u, IF_RWR_RRD)]
    [TestCase(INS_addps, EA_16BYTE, REG_XMM1, REG_XMM8, 5u, IF_RWR_RRD)]
    [TestCase(INS_addps, EA_16BYTE, REG_XMM16, REG_XMM1, 6u, IF_RWR_RRD)]
    [TestCase(INS_addps, EA_64BYTE, REG_XMM0, REG_XMM1, 6u, IF_RWR_RRD)]
    public static void RegisterPairsRetainNativeFormatsAndSizing(
        instruction ins, emitAttr attr, regNumber dst, regNumber src, uint size, Emitter.insFormat format)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(ins, attr, dst, src);
            var id = Last(emitter);

            Assert.That(id.idReg1(), Is.EqualTo(dst));
            Assert.That(id.idReg2(), Is.EqualTo(src));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idIsSmallDsc(), Is.True);
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [Test]
    public static void LegacySseAndMaskMovesUseTheirNativeSizingPaths()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = false;
            emitter.UseVexEncodings = false;
            emitter.emitIns_R_R(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(3u));

            emitter.UseVexEncodings = true;
            emitter.UseEvexEncodings = true;
            Assert.That(emitter.emitIns_Mov(INS_kmovq_msk, EA_8BYTE, REG_K1, REG_K2, canSkip: false), Is.True);
            // Native emitInsSizeRR adds the provisional VEX prefix before sizing K instructions.
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(8u));
        });
    }

    [Test]
    public static void ApxRegisterAndImmediateOptionsRetainTheirOverlappingDescriptorContexts()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R_R(INS_add, EA_8BYTE, REG_R16, REG_R31);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(4u));
            emitter.emitIns_R_I(INS_add, EA_8BYTE, REG_R16, 7);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(5u));

            emitter.emitIns_R_R(INS_add, EA_8BYTE, REG_R16, REG_R31, INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            var ndd = Last(emitter);
            Assert.That(ndd.idInsFmt(), Is.EqualTo(IF_RWR_RRD));
            Assert.That(ndd.idIsEvexNdContextSet(), Is.True);
            Assert.That(ndd.idIsEvexNfContextSet(), Is.True);
            Assert.That(ndd.idCodeSize(), Is.EqualTo(6u));

            emitter.emitIns_R_I(INS_add, EA_8BYTE, REG_RAX, 7, INS_OPTS_EVEX_nf);
            Assert.That(Last(emitter).idIsEvexNfContextSet(), Is.True);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(8u));

            emitter.emitIns_R_R(INS_ccmpe, EA_4BYTE, REG_RAX, REG_RCX,
                INS_OPTS_EVEX_dfv_cf | INS_OPTS_EVEX_dfv_of);
            Assert.That(Last(emitter).idGetEvexDFV(), Is.EqualTo(9u));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(6u));

            emitter.emitIns_R_I(INS_cteste, EA_4BYTE, REG_RAX, 7, INS_OPTS_EVEX_dfv_zf);
            Assert.That(Last(emitter).idGetEvexDFV(), Is.EqualTo(2u));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(10u));

            emitter.emitIns_R_R(INS_push2, EA_8BYTE, REG_RAX, REG_RCX, INS_OPTS_APX_ppx);
            Assert.That(Last(emitter).idIsApxPpxContextSet(), Is.True);
        });
    }

    [Test]
    public static void EmbeddedRoundingMaskAndZeroingAreRecordedBeforeSizing()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_sqrtps, EA_64BYTE, REG_XMM0, REG_XMM1,
                INS_OPTS_EVEX_er_ru | INS_OPTS_EVEX_em_k7 | INS_OPTS_EVEX_em_zero);
            var id = Last(emitter);

            Assert.That(id.idGetEvexbContext(), Is.EqualTo(2u));
            Assert.That(id.idGetEvexAaaContext(), Is.EqualTo(7u));
            Assert.That(id.idIsEvexZContextSet(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(6u));
        });
    }

    [TestCase(INS_OPTS_NONE, 0u)]
    [TestCase(INS_OPTS_EVEX_eb, 1u)]
    [TestCase(INS_OPTS_EVEX_cd, 0u)]
    public static void MemoryBroadcastOptionsSelectOnlyTheNativeBroadcastBit(insOpts options, uint context)
    {
        WithEmitter((_, emitter) =>
        {
            var id = EmitterInstructionAllocationTests.Allocate(emitter, EA_16BYTE);
            id.idIns(INS_addps);
            id.idInsFmt(IF_RWR_SRD);
            id.idReg1(REG_XMM0);

            emitter.SetEvexBroadcastIfNeeded(id, options);

            Assert.That(id.idGetEvexbContext(), Is.EqualTo(context));
        });
    }

    [TestCase(false, 3)]
    [TestCase(true, 1)]
    public static void IdenticalAndReversedMovesAreElidedOnlyUnderOptimization(bool optimized, int count)
    {
        WithEmitter((compiler, emitter) =>
        {
            _ = emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RAX, REG_RCX, canSkip: false);
            _ = emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RAX, REG_RCX, canSkip: false);
            _ = emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RCX, REG_RAX, canSkip: false);
            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
        }, minopts: !optimized);
    }

    [Test]
    public static void MoveSideEffectsGcBirthsAndApxNddDeferralRetainTheNativeContract()
    {
        WithEmitter((compiler, emitter) =>
        {
            Assert.That(emitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_RAX, REG_RAX, canSkip: true), Is.False);
            Assert.That(CurrentCount(emitter), Is.Zero);
            Assert.That(emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RAX, REG_RCX,
                canSkip: false, useApxNdd: true), Is.True);
            Assert.That(CurrentCount(emitter), Is.Zero);

            _ = emitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_RAX, REG_RCX, canSkip: false);
            _ = emitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_RCX, REG_RAX, canSkip: false);
            Assert.That(CurrentCount(emitter), Is.EqualTo(2));

            _ = emitter.emitIns_Mov(INS_mov, EA_GCREF, REG_RAX, REG_RAX, canSkip: false);
            _ = emitter.emitIns_Mov(INS_mov, EA_GCREF, REG_RAX, REG_RAX, canSkip: false);
            Assert.That(CurrentCount(emitter), Is.EqualTo(4));
            Assert.That(Last(emitter).idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
        }, minopts: false);
    }

    [TestCase(INS_movsxd, EA_4BYTE, EA_8BYTE, 2u)]
    [TestCase(INS_movsx, EA_2BYTE, EA_4BYTE, 1u)]
    public static void AccumulatorSignExtensionsUseCwde(
        instruction ins, emitAttr sourceSize, emitAttr resultSize, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            Assert.That(emitter.emitIns_Mov(ins, sourceSize, REG_RAX, REG_RAX, canSkip: false), Is.False);
            Assert.That(Last(emitter).idIns(), Is.EqualTo(INS_cwde));
            Assert.That(Last(emitter).idOpSize(), Is.EqualTo(resultSize));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [TestCase(31, true)]
    [TestCase(32, false)]
    public static void UpperBitPeepholesRespectTheNativeInstructionLimit(int intervening, bool redundant)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, 42);
            for (var index = 0; index < intervening; index++)
            {
                emitter.emitIns_Nop(1);
            }

            var before = CurrentCount(emitter);
            Assert.That(emitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_RCX, REG_RCX, canSkip: false),
                Is.EqualTo(!redundant));
            Assert.That(CurrentCount(emitter), Is.EqualTo(before + (redundant ? 0 : 1)));
        }, minopts: false);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void PeepholesTraverseExtendedGroupsButNotGcInterruptBoundaries(bool changeGcState, bool zero)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, 42);
            ForceNewGroup(emitter) = true;
            emitter.emitIns_Nop(1);
            Assert.That(emitter.emitCurIG?.igFlags & InsGroupFlags.Extend, Is.EqualTo(InsGroupFlags.Extend));
            if (changeGcState)
            {
                emitter.emitCurIG!.igFlags ^= InsGroupFlags.NoGCInterrupt;
            }

            Assert.That(emitter.AreUpperBitsZero(REG_RCX, EA_4BYTE), Is.EqualTo(zero));
        });
    }

    [Test]
    public static void PeepholesStopAtImplicitWritesAndRetainSignExtensionKnowledge()
    {
        WithEmitter((compiler, emitter) =>
        {
            _ = emitter.emitIns_Mov(INS_movsx, EA_1BYTE, REG_RCX, REG_RDX, canSkip: false);
            emitter.emitIns_Nop(1);
            Assert.That(emitter.AreUpperBitsSignExtended(REG_RCX, EA_2BYTE), Is.True);
            Assert.That(emitter.AreUpperBitsZero(REG_RCX, EA_4BYTE), Is.False);

            emitter.emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, 42);
            emitter.emitIns(INS_r_movsb);
            Assert.That(emitter.AreUpperBitsZero(REG_RCX, EA_4BYTE), Is.True);
            emitter.emitIns_R_I(INS_mov, EA_4BYTE, REG_R8, 42);
            emitter.emitIns_R_I(INS_imul_08, EA_8BYTE, REG_RCX, 42);
            Assert.That(Emitter.emitIsInstrWritingToReg(Last(emitter), REG_R8), Is.True);
            Assert.That(Emitter.emitIsInstrWritingToReg(Last(emitter), REG_RCX), Is.False);
            Assert.That(emitter.AreUpperBitsZero(REG_R8, EA_4BYTE), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void ImmediateDebugInformationPreservesHandleBitsAndTreeFlags()
    {
        WithEmitter((_, emitter) =>
        {
            var handle = unchecked((nuint)0xFEDCBA9876543210UL);
            emitter.emitIns_R_I(INS_mov, EA_8BYTE, REG_RAX, -1, targetHandle: handle, gtFlags: GTF_ICON_HDL_MASK);
            var info = Last(emitter).idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");

            Assert.That(info.idMemCookie, Is.EqualTo(unchecked((nint)handle)));
            Assert.That(info.idFlags, Is.EqualTo(GTF_ICON_HDL_MASK));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void DisassemblyRecordsRegisterInstructionsAndPreservesMoveElision(int entrypoint)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitIns_R_I(INS_mov, EA_8BYTE, REG_RAX, 1);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitIns_R_R(INS_add, EA_8BYTE, REG_RAX, REG_RCX);
                        break;
                    }

                    default:
                    {
                        _ = emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RAX, REG_RAX, canSkip: true);
                        break;
                    }
                }
            });

            Assert.That(CurrentCount(emitter), Is.EqualTo(entrypoint == 2 ? 0 : 1));
            Assert.That(Used(emitter) > used, Is.EqualTo(entrypoint != 2));
            Assert.That(CurrentSize(emitter) > 0, Is.EqualTo(entrypoint != 2));
            Assert.That(diagnostic.Contains(entrypoint == 0 ? "mov" : "add", StringComparison.Ordinal),
                Is.EqualTo(entrypoint != 2));
        });
    }
#else
    [Test]
    public static void ReleaseRetainsNativeMovFallthroughInTheNonMovEntrypoint()
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.emitIns_R_R(INS_mov, EA_8BYTE, REG_RAX, REG_RCX);
            Assert.That(CurrentCount(emitter), Is.EqualTo(2));
            Assert.That(CurrentSize(emitter), Is.EqualTo(6));
        });
    }
#endif

    private static void WithEmitter(Action<Compiler, Emitter> action, bool minopts = true)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            action(compiler, codeGen.Emitter);
        }, minopts);
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint Constant(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);
}
