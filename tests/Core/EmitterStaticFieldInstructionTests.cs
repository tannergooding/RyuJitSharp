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

internal static unsafe class EmitterStaticFieldInstructionTests
{
    [TestCase(EA_1BYTE, REG_RAX, 6u)]
    [TestCase(EA_1BYTE, REG_RSP, 7u)]
    [TestCase(EA_2BYTE, REG_RAX, 7u)]
    [TestCase(EA_4BYTE, REG_RAX, 6u)]
    [TestCase(EA_4BYTE, REG_R8, 7u)]
    [TestCase(EA_8BYTE, REG_RAX, 7u)]
    [TestCase(EA_8BYTE, REG_R16, 8u)]
    [TestCase(EA_GCREF, REG_RAX, 7u)]
    [TestCase(EA_BYREF, REG_RAX, 7u)]
    public static void ScalarLoadsPreserveOperandAndGcAttributesAndPrefixWidths(
        emitAttr attr, regNumber reg, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseRex2Encodings = true;
            var field = (CORINFO_FIELD_STRUCT_*)0x123456789ABCDEF0UL;
            emitter.emitIns_R_C(INS_mov, attr, reg, field, 0);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(INS_mov));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_MRD));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.EqualTo((nuint)field));
            Assert.That(id.idAddr().iiaIsJitDataOffset(), Is.False);
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SIZE(attr)));
            Assert.That(id.idGCref(), Is.EqualTo(EA_IS_GCREF(attr) ? GCInfo.GCtype.GCT_GCREF
                : EA_IS_BYREF(attr) ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public static void FieldOffsetsKeepFullSignedStorageAndFixedRipRelativeSizing(int offset)
    {
        WithEmitter((_, emitter) =>
        {
            var field = Compiler.eeFindJitDataOffs(64);
            emitter.emitIns_R_C(INS_mov, EA_4BYTE, REG_RAX, field, offset);
            var id = Last(emitter);

            Assert.That(id.idAddr().iiaIsJitDataOffset(), Is.True);
            Assert.That(id.idAddr().iiaGetJitDataOffset(), Is.EqualTo(64));
            Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.EqualTo((nuint)field));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)offset));
            Assert.That(id.idIsLargeDsp(), Is.EqualTo(offset != 0));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(offset == 0 ? 16 : 24));
            Assert.That(id.idCodeSize(), Is.EqualTo(6u));
            Assert.That(id.idIsDspReloc(), Is.True);
        });
    }

    [TestCase(-4, false, 6u)]
    [TestCase(-8, false, 7u)]
    [TestCase(-12, false, 8u)]
    [TestCase(-4, true, 6u)]
    [TestCase(-8, true, 7u)]
    [TestCase(-12, true, 8u)]
    public static void GlobalSegmentsPreserveExplicitRelocationsAndTheirDistinctPrefixCosts(
        int handle, bool relocation, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var field = unchecked((CORINFO_FIELD_STRUCT_*)(nint)handle);
            var attr = relocation ? EA_4BYTE | EA_DSP_RELOC_FLG : EA_4BYTE;
            emitter.emitIns_R_C(INS_mov, attr, REG_RAX, field, 32);
            var id = Last(emitter);

            Assert.That(jitStaticFldIsGlobAddr(field), Is.True);
            Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.EqualTo(unchecked((nuint)(nint)handle)));
            Assert.That(id.idAddr().iiaIsJitDataOffset(), Is.False);
            Assert.That(id.idIsDspReloc(), Is.EqualTo(relocation));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)32));
        });
    }

    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(0x100000001L)]
    public static void OnlyTheThreeGlobalSentinelsAvoidAutomaticRelocation(long bits)
    {
        WithEmitter((_, emitter) =>
        {
            var field = unchecked((CORINFO_FIELD_STRUCT_*)(nint)bits);
            Assert.That(jitStaticFldIsGlobAddr(field), Is.False);
            emitter.emitIns_R_C(INS_mov, EA_4BYTE, REG_RAX, field, 0);
            Assert.That(Last(emitter).idIsDspReloc(), Is.True);
            Assert.That((nuint)Last(emitter).idAddr().iiaFieldHnd, Is.EqualTo((nuint)field));
            Assert.That(Last(emitter).idAddr().iiaIsJitDataOffset(), Is.EqualTo(bits == 1));
        });
    }

    [TestCase(0, false)]
    [TestCase(32, false)]
    [TestCase(32, true)]
    public static void OffsetLoadsUseTheNativeAccumulatorSpecialDescriptor(int offset, bool gs)
    {
        WithEmitter((_, emitter) =>
        {
            var field = gs ? FLD_GLOBAL_GS : Compiler.eeFindJitDataOffs(64);
            emitter.emitIns_R_C(INS_mov, EA_OFFSET | EA_DSP_RELOC_FLG, REG_RAX, field, offset);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_MRD_OFF));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_1BYTE));
            Assert.That(id.idCodeSize(), Is.EqualTo(9u));
            Assert.That(id.idIsDspReloc(), Is.False);
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)offset));
            Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.EqualTo((nuint)field));
        });
    }

    [TestCase(false, REG_XMM0, 1, 7)]
    [TestCase(false, REG_XMM1, 2, 10)]
    [TestCase(true, REG_XMM1, 1, 8)]
    [TestCase(true, REG_XMM8, 1, 8)]
    [TestCase(true, REG_XMM16, 1, 10)]
    public static void SimdWrappersRetainLegacyCopiesAndTheEncodedSourceRegister(
        bool vex, regNumber source, int count, int size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = vex;
            emitter.UseVexEncodings = vex;
            var field = Compiler.eeFindJitDataOffs(128);
            emitter.emitIns_SIMD_R_R_C(INS_addps, EA_16BYTE, REG_XMM0, source, field, 4, INS_OPTS_NONE);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(INS_addps));
            Assert.That(id.idInsFmt(), Is.EqualTo(vex ? IF_RWR_RRD_MRD : IF_RWR_MRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(id.idAddr().iiaGetJitDataOffset(), Is.EqualTo(128));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)4));
            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            if (vex)
            {
                Assert.That(id.idReg2(), Is.EqualTo(source));
            }
            else if (count == 2)
            {
                var descriptors = Buffer(emitter) ?? throw new AssertionException("Missing instruction buffer.");
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movaps));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM0));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(source));
            }
        });
    }

    [Test]
    public static void SharedStaticFieldLoadsPreserveMulxDualWriteOperands()
    {
        WithEmitter((_, emitter) =>
        {
            var field = (CORINFO_FIELD_STRUCT_*)0x1000;
            emitter.emitIns_R_R_C(INS_mulx, EA_8BYTE, REG_R8, REG_R9, field, -4);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_RWR_MRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(id.idReg2(), Is.EqualTo(REG_R9));
            Assert.That(id.idIsReg1Write(), Is.True);
            Assert.That(id.idIsReg2Write(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(9u));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)(-4)));
        });
    }

    [Test]
    public static void EvexBroadcastMasksAndDefaultFlagsSurviveStaticFieldRecording()
    {
        WithEmitter((_, emitter) =>
        {
            var field = Compiler.eeFindJitDataOffs(64);
            emitter.emitIns_R_R_C(INS_addps, EA_64BYTE, REG_XMM0, REG_XMM1, field, 64,
                INS_OPTS_EVEX_eb | INS_OPTS_EVEX_em_k2 | INS_OPTS_EVEX_em_zero);
            var broadcast = Last(emitter);
            Assert.That(broadcast.idGetEvexbContext(), Is.EqualTo(1u));
            Assert.That(broadcast.idGetEvexAaaContext(), Is.EqualTo(2u));
            Assert.That(broadcast.idIsEvexZContextSet(), Is.True);
            Assert.That(broadcast.idCodeSize(), Is.EqualTo(10u));
            Assert.That(Displacement(emitter, broadcast), Is.EqualTo((nint)64));

            emitter.emitIns_R_C(INS_cvtdq2ps, EA_64BYTE, REG_XMM0, field, 0,
                INS_OPTS_EVEX_eb | INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero);
            Assert.That(Last(emitter).idGetEvexbContext(), Is.EqualTo(1u));
            Assert.That(Last(emitter).idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(Last(emitter).idIsEvexZContextSet(), Is.True);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(10u));

            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R_C(INS_ccmpe, EA_4BYTE, REG_RAX, field, 0,
                INS_OPTS_EVEX_dfv_cf | INS_OPTS_EVEX_dfv_of);
            Assert.That(Last(emitter).idGetEvexDFV(), Is.EqualTo(9u));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(10u));
        });
    }

    [TestCase(EA_4BYTE, REG_RAX, REG_RCX)]
    [TestCase(EA_4BYTE, REG_RAX, REG_R8)]
    [TestCase(EA_8BYTE, REG_RAX, REG_RCX)]
    public static void StaticFieldSizingCountsEmbeddedRexPrefixesExactlyOnce(
        emitAttr attr, regNumber first, regNumber second)
    {
        WithEmitter((_, emitter) =>
        {
            var id = EmitterInstructionAllocationTests.Allocate(emitter, attr);
            id.idIns(INS_imul_08);
            id.idInsFmt(IF_RWR_RRD_MRD);
            id.idReg1(first);
            id.idReg2(second);

            Assert.That(emitter.emitInsSizeCV(id, insCodeMI(INS_imul_08)), Is.EqualTo(7u));
        });
    }

#if DEBUG
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(2, false)]
    public static void DisassemblyRejectsBeforeRecordingOrTheLegacyDestinationCopy(int entrypoint, bool vex)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.UseEvexEncodings = vex;
            emitter.UseVexEncodings = vex;
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                var field = Compiler.eeFindJitDataOffs(64);
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitIns_R_C(INS_movss, EA_4BYTE, REG_XMM0, field, 0);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitIns_R_R_C(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, field, 0);
                        break;
                    }

                    default:
                    {
                        emitter.emitIns_SIMD_R_R_C(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, field, 0, INS_OPTS_NONE);
                        break;
                    }
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

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsDsp")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

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
