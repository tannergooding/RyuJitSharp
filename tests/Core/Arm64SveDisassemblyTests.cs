// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.insSvePattern;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64SveDisassemblyTests
{
    [TestCase(IF_SVE_AA_3A, INS_sve_add, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z0.s, z1.s")]
    [TestCase(IF_SVE_AO_3A, INS_sve_asr, INS_OPTS_SCALABLE_H, "z0.h, p7/m, z0.h, z1.d")]
    [TestCase(IF_SVE_AR_4A, INS_sve_mla, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z1.s, z31.s")]
    [TestCase(IF_SVE_AH_3A, INS_sve_movprfx, INS_OPTS_SCALABLE_S, "z0.s, p7/z, z1.s")]
    [TestCase(IF_SVE_HO_3A, INS_sve_fcvtx, INS_OPTS_D_TO_S, "z0.s, p7/m, z1.d")]
    [TestCase(IF_SVE_GQ_3A, INS_sve_bfcvtnt, INS_OPTS_SCALABLE_H, "z0.h, p7/m, z1.s")]
    [TestCase(IF_SVE_EQ_3A, INS_sve_sadalp, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z1.h")]
    [TestCase(IF_SVE_GP_3A, INS_sve_fcadd, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z0.s, z1.s, #90")]
    [TestCase(IF_SVE_HM_2A, INS_sve_fmul, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z0.s, #0.5000")]
    public static void PredicatedFormatsPreserveDestructiveOperandsAndConversionWidths(
        Emitter.insFormat format, instruction ins, insOpts opt, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(format, ins, opt, REG_V0, REG_P7, REG_V1, REG_V31);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(IF_SVE_AU_3A, INS_sve_mov, INS_OPTS_SCALABLE_D, 0, "z0.d, z1.d")]
    [TestCase(IF_SVE_AU_3A, INS_sve_orr, INS_OPTS_SCALABLE_D, 0, "z0.d, z1.d, z31.d")]
    [TestCase(IF_SVE_AV_3A, INS_sve_eor3, INS_OPTS_SCALABLE_D, 0, "z0.d, z0.d, z1.d, z31.d")]
    [TestCase(IF_SVE_BZ_3A, INS_sve_tbl, INS_OPTS_SCALABLE_S, 0, "z0.s, { z1.s }, z31.s")]
    [TestCase(IF_SVE_BZ_3A, INS_sve_tbx, INS_OPTS_SCALABLE_S, 0, "z0.s, z1.s, z31.s")]
    [TestCase(IF_SVE_BZ_3A_A, INS_sve_tbl, INS_OPTS_SCALABLE_S, 0, "z0.s, { z1.s, z2.s }, z31.s")]
    [TestCase(IF_SVE_BH_3A, INS_sve_adr, INS_OPTS_SCALABLE_D, 0, "z0.d, [z1.d, z31.d]")]
    [TestCase(IF_SVE_BH_3A, INS_sve_adr, INS_OPTS_SCALABLE_D, 3, "z0.d, [z1.d, z31.d, lsl #3]")]
    [TestCase(IF_SVE_BH_3B, INS_sve_adr, INS_OPTS_SCALABLE_D, 0, "z0.d, [z1.d, z31.d, sxtw]")]
    [TestCase(IF_SVE_AX_1A, INS_sve_index, INS_OPTS_SCALABLE_S, 0x3F0, "z0.s, #-16, #15")]
    [TestCase(IF_SVE_FA_3A, INS_sve_cdot, INS_OPTS_SCALABLE_B, 14, "z0.s, z1.b, z31.b[3], #180")]
    [TestCase(IF_SVE_FA_3B, INS_sve_cdot, INS_OPTS_SCALABLE_H, 7, "z0.d, z1.h, z31.h[1], #270")]
    [TestCase(IF_SVE_FB_3A, INS_sve_cmla, INS_OPTS_SCALABLE_H, 9, "z0.h, z1.h, z31.h[2], #90")]
    [TestCase(IF_SVE_FR_2A, INS_sve_sshllb, INS_OPTS_SCALABLE_S, 3, "z0.s, z1.h, #3")]
    [TestCase(IF_SVE_GB_2A, INS_sve_shrnb, INS_OPTS_SCALABLE_H, 4, "z0.h, z1.s, #4")]
    [TestCase(IF_SVE_BS_1A, INS_sve_and, INS_OPTS_SCALABLE_D, 0x1000, "z0.d, z0.d, #1")]
    [TestCase(IF_SVE_BT_1A, INS_sve_dupm, INS_OPTS_SCALABLE_D, 0x1000, "z0.d, #1")]
    [TestCase(IF_SVE_BW_2A, INS_sve_dup, INS_OPTS_SCALABLE_S, 0, "z0.s, s1")]
    [TestCase(IF_SVE_BW_2A, INS_sve_dup, INS_OPTS_SCALABLE_S, 3, "z0.s, z1.s[3]")]
    public static void VectorFormatsPreserveAliasesListsPackedImmediatesAndIndexZero(
        Emitter.insFormat format, instruction ins, insOpts opt, int immediate, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(format, ins, opt, REG_V0, REG_V1, REG_V31,
                REG_V2, immediate: immediate);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(INS_sve_sel, "z0.s, p7, z1.s, z31.s")]
    [TestCase(INS_sve_mov, "z0.s, p7/m, z1.s")]
    public static void VectorSelectAliasOmitsItsImplicitFinalSource(instruction ins, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_CW_4A, ins, INS_OPTS_SCALABLE_S,
                REG_V0, REG_P7, REG_V1, REG_V31);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(INS_sve_and, "p0.b, p7/z, p1.b, p15.b")]
    [TestCase(INS_sve_sel, "p0.b, p7, p1.b, p15.b")]
    [TestCase(INS_sve_mov, "p0.b, p7/z, p1.b")]
    [TestCase(INS_sve_not, "p0.b, p7/z, p1.b")]
    public static void PredicateAliasesKeepTheNativePredicateKinds(instruction ins, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_CZ_4A, ins, INS_OPTS_SCALABLE_B,
                REG_P0, REG_P7, REG_P1, REG_P15);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(false, "z0.s, p7/z, z1.s")]
    [TestCase(true, "z0.s, p7/m, z1.s")]
    public static void ConstructivePrefixUsesTheRecordedMergeBit(bool merge, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_AH_3A, INS_sve_movprfx, INS_OPTS_SCALABLE_S,
                REG_V0, REG_P7, REG_V1, REG_V31);
            id.idPredicateReg2Merge(merge);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(SVE_PATTERN_ALL, "p0.s")]
    [TestCase(SVE_PATTERN_VL4, "p0.s, vl4")]
    public static void PredicateInitializeOmitsOnlyTheDefaultPattern(insSvePattern pattern, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_DE_1A, INS_sve_ptrue, INS_OPTS_SCALABLE_S,
                REG_P0, REG_P0, REG_P0, REG_P0);
            id.idSvePattern(pattern);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(1, "x0, vl4")]
    [TestCase(3, "x0, vl4, mul #3")]
    public static void ElementCountOmitsOnlyTheDefaultMultiplier(int multiplier, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_BL_1A, INS_sve_cntw, INS_OPTS_NONE,
                REG_R0, REG_R0, REG_R0, REG_R0, EA_8BYTE, multiplier);
            id.idSvePattern(SVE_PATTERN_VL4);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(IF_SVE_IJ_3A, INS_sve_ld1w, 0, "{ z31.s }, p7/z, [x2]")]
    [TestCase(IF_SVE_IJ_3A, INS_sve_ld1w, -2, "{ z31.s }, p7/z, [x2, #-2, mul vl]")]
    [TestCase(IF_SVE_IO_3A, INS_sve_ld1rqw, 32, "{ z31.s }, p7/z, [x2, #0x20]")]
    [TestCase(IF_SVE_IS_3A, INS_sve_ld2w, 2, "{ z31.s, z0.s }, p7/z, [x2, #0x02, mul vl]")]
    [TestCase(IF_SVE_JO_3A, INS_sve_st3w, 3, "{ z31.s, z0.s, z1.s }, p7, [x2, #0x03, mul vl]")]
    public static void MemoryFormatsKeepWrappedListsAndCapstoneHexOffsets(
        Emitter.insFormat format, instruction ins, int immediate, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(format, ins, INS_OPTS_SCALABLE_S,
                REG_V31, REG_P7, REG_R2, REG_R3, immediate: immediate);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(EA_4BYTE, "x0, p1.s, w0")]
    [TestCase(EA_8BYTE, "x0, p1.s")]
    public static void SignedPredicateCountPreservesTheThirtyTwoBitInputOperand(emitAttr size, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_DO_2A, INS_sve_sqincp, INS_OPTS_SCALABLE_S,
                REG_R0, REG_P1, REG_R0, REG_R0, size);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(false, "x0, pn8.s, vlx2")]
    [TestCase(true, "x0, pn8.s, vlx4")]
    public static void PredicateCountersKeepTheVectorLengthSpecifier(bool four, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_DL_2A, INS_sve_cntp, INS_OPTS_SCALABLE_S,
                REG_R0, REG_P8, REG_R0, REG_R0, EA_8BYTE);
            id.idVectorLength4x(four);
            AssertOutput(emitter, id, expected);
        });
    }

    [Test]
    public static void StackAdjustmentConvertsEncodedZeroRegistersToStackPointers()
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_BB_2A, INS_sve_addvl, INS_OPTS_NONE,
                REG_ZR, REG_ZR, REG_R0, REG_R0, EA_8BYTE, -16);
            AssertOutput(emitter, id, "sp, sp, #-16");
        });
    }

    [TestCase(false, "z0.s, #7")]
    [TestCase(true, "z0.s, #7, LSL #8")]
    public static void IntegerBroadcastPreservesTheRecordedShift(bool shift, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_EB_1A, INS_sve_dup, INS_OPTS_SCALABLE_S,
                REG_V0, REG_V0, REG_V0, REG_V0, immediate: 7);
            id.idHasShift(shift);
            AssertOutput(emitter, id, expected);
        });
    }

    [Test]
    public static void FfrInitializeHasNoOperands()
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_SVE_DQ_0A, INS_sve_setffr, INS_OPTS_NONE,
                REG_R0, REG_R0, REG_R0, REG_R0);
            AssertOutput(emitter, id, "");
        });
    }

    [Test]
    public static void UnexpectedFormatPreservesDiagnosticAndContinuingEeBehavior()
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_EN5A, INS_ldr, INS_OPTS_NONE,
                REG_R0, REG_R0, REG_R0, REG_R0);
            var (output, assertions) = Capture(() => Display(emitter, id));

            Assert.That(output, Is.EqualTo("unexpected format IF_EN5A"));
#if DEBUG
            Assert.That(assertions, Is.EqualTo<string[]>(["!\"unexpectedFormat\""]));
#else
            Assert.That(assertions, Is.Empty);
#endif
        });
    }

    internal static void WithEmitter(Action<Emitter> action)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) => action(codeGen.Emitter));
    }

    internal static (string Output, string[] Assertions) Capture(Action action)
    {
#if DEBUG
        return Arm64SveInstructionSanityTests.Capture(action);
#else
        var previousConfig = JitConfig;
        var previousOutput = s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        JitConfig = new JitConfigValues();
        s_jitstdout = writer;
        try
        {
            action();
            writer.Flush();

            return (Encoding.UTF8.GetString(stream.ToArray()), []);
        }
        finally
        {
            JitConfig = previousConfig;
            s_jitstdout = previousOutput;
        }
#endif
    }

    private static void AssertOutput(Emitter emitter, Emitter.instrDesc id, string expected)
    {
        var (output, assertions) = Capture(() => Display(emitter, id));
        Assert.That(output, Is.EqualTo(expected));
        Assert.That(assertions, Is.Empty);
    }

    internal abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static instrDesc Descriptor(Emitter.insFormat format, instruction ins, insOpts opt,
            regNumber first, regNumber second, regNumber third, regNumber fourth,
            emitAttr size = EA_SCALABLE, int immediate = 0)
        {
            instrDesc id;
            if (instrDesc.fitsInSmallCns(immediate))
            {
                id = new instrDescBasic();
                id.idSmallCns(immediate);
            }
            else
            {
                var constant = new instrDescCns { idcCnsVal = immediate };
                constant.idSetIsLargeCns();
                id = constant;
            }

            id.idIns(ins);
            id.idInsFmt(format);
            id.idInsOpt(opt);
            id.idOpSize(size);
            id.idReg1(first);
            id.idReg2(second);
            id.idReg3(third);
            id.idReg4(fourth);

            return id;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispInsSveHelp")]
    private static extern void Display(Emitter emitter, Emitter.instrDesc id);
}
#endif
