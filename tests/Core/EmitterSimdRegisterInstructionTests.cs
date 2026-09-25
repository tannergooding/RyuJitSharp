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

internal static class EmitterSimdRegisterInstructionTests
{
    [TestCase(INS_addps, REG_XMM1, REG_XMM8, INS_OPTS_NONE, REG_XMM8, REG_XMM1, 4u)]
    [TestCase(INS_addps, REG_XMM9, REG_XMM8, INS_OPTS_NONE, REG_XMM9, REG_XMM8, 5u)]
    [TestCase(INS_addps, REG_XMM1, REG_XMM16, INS_OPTS_NONE, REG_XMM16, REG_XMM1, 6u)]
    [TestCase(INS_divps, REG_XMM1, REG_XMM8, INS_OPTS_NONE, REG_XMM1, REG_XMM8, 5u)]
    [TestCase(INS_addps, REG_XMM1, REG_XMM8, INS_OPTS_EVEX_em_k1, REG_XMM1, REG_XMM8, 6u)]
    public static void SimdWrappersSwapOnlyEligibleCommutativeSources(
        instruction ins, regNumber first, regNumber second, insOpts options,
        regNumber expectedFirst, regNumber expectedSecond, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_SIMD_R_R_R(ins, EA_16BYTE, REG_XMM0, first, second, options);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_RRD_RRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(id.idReg2(), Is.EqualTo(expectedFirst));
            Assert.That(id.idReg3(), Is.EqualTo(expectedSecond));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(16));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
        });
    }

    [TestCase(INS_addps, REG_XMM0, 1, 3)]
    [TestCase(INS_addps, REG_XMM1, 2, 6)]
    [TestCase(INS_movaps, REG_XMM1, 2, 6)]
    public static void LegacyWrappersCopyTheFirstOperandAndUseTheMoveEntrypointWhenRequired(
        instruction ins, regNumber first, int count, int size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = false;
            emitter.UseVexEncodings = false;
            emitter.emitIns_SIMD_R_R_R(ins, EA_16BYTE, REG_XMM0, first, REG_XMM2, INS_OPTS_NONE);

            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            var id = Last(emitter);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_RRD));
            if (count == 2)
            {
                var copy = Instructions(emitter)[0];
                Assert.That(copy.idIns(), Is.EqualTo(INS_movaps));
                Assert.That(copy.idReg1(), Is.EqualTo(REG_XMM0));
                Assert.That(copy.idReg2(), Is.EqualTo(first));
            }
        });
    }

    [TestCase(false, REG_XMM0, 1, 4)]
    [TestCase(false, REG_XMM1, 2, 7)]
    [TestCase(true, REG_XMM1, 1, 5)]
    public static void ImmediateWrappersRetainLegacyCopiesAndVexThreeOperandForms(
        bool vex, regNumber first, int count, int size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = vex;
            emitter.UseVexEncodings = vex;
            emitter.emitIns_SIMD_R_R_R_I(INS_shufps, EA_16BYTE, REG_XMM0, first, REG_XMM2, 7, INS_OPTS_NONE);
            var id = Last(emitter);

            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)7));
            Assert.That(id.idInsFmt(), Is.EqualTo(vex ? IF_RWR_RRD_RRD_CNS : IF_RWR_RRD_CNS));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(id.idReg2(), Is.EqualTo(vex ? first : REG_XMM2));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(vex ? 16 : 8));
            if (vex)
            {
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM2));
            }
        });
    }

    [TestCase(7, REG_XMM2, 5u, 16)]
    [TestCase(16, REG_XMM2, 5u, 24)]
    [TestCase(-128, REG_XMM8, 6u, 24)]
    [TestCase(127, REG_XMM16, 7u, 24)]
    public static void ThreeRegisterImmediatesKeepSignedValuesAndFullRegisterStorage(
        int value, regNumber second, uint size, int logicalSize)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R_R_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, second, value);
            var id = Last(emitter);

            Assert.That(Constant(emitter, id), Is.EqualTo((nint)value));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(logicalSize));
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idReg3(), Is.EqualTo(second));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
        });
    }

    [TestCase(INS_pslld, 5u)]
    [TestCase(INS_pshufd, 5u)]
    public static void TwoRegisterSimdImmediatesSelectTheirMiOrRmOpcodes(instruction ins, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R_I(ins, EA_16BYTE, REG_XMM0, REG_XMM1, 7);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_RRD_CNS));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idIsSmallDsc(), Is.True);
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)7));
        });
    }

    [TestCase(INS_add, EA_8BYTE, -128, false, IF_RWR_RRD_CNS, 7u)]
    [TestCase(INS_add, EA_8BYTE, 128, false, IF_RWR_RRD_CNS, 10u)]
    [TestCase(INS_add, EA_2BYTE, 128, false, IF_RWR_RRD_CNS, 8u)]
    [TestCase(INS_add, EA_4BYTE, 16, true, IF_RWR_RRD_CNS, 10u)]
    [TestCase(INS_add, EA_4BYTE, 0x123456, true, IF_RWR_RRD_CNS, 10u)]
    [TestCase(INS_shl_N, EA_8BYTE, 3, false, IF_RWR_RRD_SHF, 7u)]
    public static void TwoRegisterIntegerImmediatesPreserveApxFormatsAndRelocationWidths(
        instruction ins, emitAttr attr, int value, bool relocatable, Emitter.insFormat format, uint size)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = relocatable;
            emitter.UsePromotedEvexEncodings = true;
            if (relocatable)
            {
                attr |= EA_CNS_RELOC_FLG;
            }

            emitter.emitIns_R_R_I(ins, attr, REG_RAX, REG_RCX, value, INS_OPTS_EVEX_nd);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idIsEvexNdContextSet(), Is.True);
            Assert.That(id.idIsCnsReloc(), Is.EqualTo(relocatable));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)value));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            if (relocatable)
            {
                Assert.That(id.idIsLargeCns(), Is.True);
            }
        });
    }

    [Test]
    public static void ApxThreeRegisterOperationsPreserveNewDestinationAndNoFlags()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R_R_R(INS_add, EA_8BYTE, REG_R16, REG_R17, REG_R31,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_RRD_RRD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_R16));
            Assert.That(id.idReg2(), Is.EqualTo(REG_R17));
            Assert.That(id.idReg3(), Is.EqualTo(REG_R31));
            Assert.That(id.idIsEvexNdContextSet(), Is.True);
            Assert.That(id.idIsEvexNfContextSet(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(6u));
        });
    }

    [Test]
    public static void MulxHasTwoWrittenDestinationsAndMaskOperationsKeepTheirOwnOperandClass()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R_R(INS_mulx, EA_8BYTE, REG_R8, REG_R9, REG_R10);
            var multiply = Last(emitter);
            Assert.That(multiply.idInsFmt(), Is.EqualTo(IF_RWR_RWR_RRD));
            Assert.That(multiply.idIsReg1Write(), Is.True);
            Assert.That(multiply.idIsReg2Write(), Is.True);
            Assert.That(multiply.idIsReg3Read(), Is.True);
            Assert.That(multiply.idCodeSize(), Is.EqualTo(5u));

            emitter.emitIns_R_R_R(INS_kandw, EA_2BYTE, REG_K1, REG_K2, REG_K3);
            var mask = Last(emitter);
            Assert.That(mask.idInsFmt(), Is.EqualTo(IF_RWR_RRD_RRD));
            Assert.That(mask.idReg3(), Is.EqualTo(REG_K3));
            Assert.That(mask.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void EmbeddedOptionsAreAppliedBeforeSizingTheSelectedDescriptor()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R_R(INS_addps, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2,
                INS_OPTS_EVEX_er_ru | INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero);
            var rounded = Last(emitter);
            Assert.That(rounded.idGetEvexbContext(), Is.EqualTo(2u));
            Assert.That(rounded.idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(rounded.idIsEvexZContextSet(), Is.True);
            Assert.That(rounded.idCodeSize(), Is.EqualTo(6u));

            emitter.emitIns_R_R_R_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 7,
                INS_OPTS_EVEX_em_k7 | INS_OPTS_EVEX_em_zero);
            Assert.That(Last(emitter).idGetEvexAaaContext(), Is.EqualTo(7u));
            Assert.That(Last(emitter).idIsEvexZContextSet(), Is.True);
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(7u));

            emitter.emitIns_R_R_I(INS_pshufd, EA_16BYTE, REG_XMM0, REG_XMM1, 7, INS_OPTS_EVEX_em_k2);
            Assert.That(Last(emitter).idGetEvexAaaContext(), Is.EqualTo(2u));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(7u));
        });
    }

    [Test]
    public static void RegisterSizingIncludesTheFourthRegisterAndEmbeddedOpcodePrefixes()
    {
        WithEmitter((_, emitter) =>
        {
            var id = EmitterInstructionAllocationTests.Allocate(emitter, EA_4BYTE);
            id.idIns(INS_add);
            id.idInsFmt(IF_RWR_RRD_RRD_RRD);
            id.idReg1(REG_RAX);
            id.idReg2(REG_RCX);
            id.idReg3(REG_RDX);
            id.idReg4(REG_R8);
            Assert.That(emitter.emitInsSizeRR(id, insCodeRM(INS_add)), Is.EqualTo(3u));

            id.idIns(INS_imul_08);
            id.idReg4(REG_RAX);
            Assert.That(emitter.emitInsSizeRR(id, insCodeMI(INS_imul_08)), Is.EqualTo(3u));
        });
    }

#if DEBUG
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public static void DisassemblyRejectsBeforeCopiesElisionOrAllocation(int entrypoint, bool vex)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.UseEvexEncodings = vex;
            emitter.UseVexEncodings = vex;
            compiler.opts.dspCode = true;
            var used = Used(emitter);
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitIns_SIMD_R_R_R(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2, INS_OPTS_NONE);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitIns_SIMD_R_R_R_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 7, INS_OPTS_NONE);
                        break;
                    }

                    case 2:
                    {
                        emitter.emitIns_R_R_R(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
                        break;
                    }

                    case 3:
                    {
                        emitter.emitIns_R_R_R_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 7);
                        break;
                    }

                    default:
                    {
                        emitter.emitIns_R_R_I(INS_pshufd, EA_16BYTE, REG_XMM0, REG_XMM1, 7);
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

    private static List<Emitter.instrDesc> Instructions(Emitter emitter)
    {
        return Buffer(emitter) ?? throw new AssertionException("Missing recording buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint Constant(Emitter emitter, Emitter.instrDesc descriptor);

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
