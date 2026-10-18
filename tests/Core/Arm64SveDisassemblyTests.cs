// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [SetUp]
    public static void RequireArm64Target()
    {
        Assume.That(Configuration.TARGET_ARM64, "This fixture requires the ARM64 target.");
    }

    [TestCase(IF_ARM64_SVE_AA_3A, INS_sve_add, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z0.s, z1.s")]
    [TestCase(IF_ARM64_SVE_AO_3A, INS_sve_asr, INS_OPTS_SCALABLE_H, "z0.h, p7/m, z0.h, z1.d")]
    [TestCase(IF_ARM64_SVE_AR_4A, INS_sve_mla, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z1.s, z31.s")]
    [TestCase(IF_ARM64_SVE_AH_3A, INS_sve_movprfx, INS_OPTS_SCALABLE_S, "z0.s, p7/z, z1.s")]
    [TestCase(IF_ARM64_SVE_HO_3A, INS_sve_fcvtx, INS_OPTS_D_TO_S, "z0.s, p7/m, z1.d")]
    [TestCase(IF_ARM64_SVE_GQ_3A, INS_sve_bfcvtnt, INS_OPTS_SCALABLE_H, "z0.h, p7/m, z1.s")]
    [TestCase(IF_ARM64_SVE_EQ_3A, INS_sve_sadalp, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z1.h")]
    [TestCase(IF_ARM64_SVE_GP_3A, INS_sve_fcadd, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z0.s, z1.s, #90")]
    [TestCase(IF_ARM64_SVE_HM_2A, INS_sve_fmul, INS_OPTS_SCALABLE_S, "z0.s, p7/m, z0.s, #0.5000")]
    public static void PredicatedFormatsPreserveDestructiveOperandsAndConversionWidths(
        Emitter.insFormat format, instruction ins, insOpts opt, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(format, ins, opt, REG_ARM64_V0, REG_ARM64_P7, REG_ARM64_V1, REG_ARM64_V31);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(IF_ARM64_SVE_AU_3A, INS_sve_mov, INS_OPTS_SCALABLE_D, 0, "z0.d, z1.d")]
    [TestCase(IF_ARM64_SVE_AU_3A, INS_sve_orr, INS_OPTS_SCALABLE_D, 0, "z0.d, z1.d, z31.d")]
    [TestCase(IF_ARM64_SVE_AV_3A, INS_sve_eor3, INS_OPTS_SCALABLE_D, 0, "z0.d, z0.d, z1.d, z31.d")]
    [TestCase(IF_ARM64_SVE_BZ_3A, INS_sve_tbl, INS_OPTS_SCALABLE_S, 0, "z0.s, { z1.s }, z31.s")]
    [TestCase(IF_ARM64_SVE_BZ_3A, INS_sve_tbx, INS_OPTS_SCALABLE_S, 0, "z0.s, z1.s, z31.s")]
    [TestCase(IF_ARM64_SVE_BZ_3A_A, INS_sve_tbl, INS_OPTS_SCALABLE_S, 0, "z0.s, { z1.s, z2.s }, z31.s")]
    [TestCase(IF_ARM64_SVE_BH_3A, INS_sve_adr, INS_OPTS_SCALABLE_D, 0, "z0.d, [z1.d, z31.d]")]
    [TestCase(IF_ARM64_SVE_BH_3A, INS_sve_adr, INS_OPTS_SCALABLE_D, 3, "z0.d, [z1.d, z31.d, lsl #3]")]
    [TestCase(IF_ARM64_SVE_BH_3B, INS_sve_adr, INS_OPTS_SCALABLE_D, 0, "z0.d, [z1.d, z31.d, sxtw]")]
    [TestCase(IF_ARM64_SVE_AX_1A, INS_sve_index, INS_OPTS_SCALABLE_S, 0x3F0, "z0.s, #-16, #15")]
    [TestCase(IF_ARM64_SVE_FA_3A, INS_sve_cdot, INS_OPTS_SCALABLE_B, 14, "z0.s, z1.b, z31.b[3], #180")]
    [TestCase(IF_ARM64_SVE_FA_3B, INS_sve_cdot, INS_OPTS_SCALABLE_H, 7, "z0.d, z1.h, z31.h[1], #270")]
    [TestCase(IF_ARM64_SVE_FB_3A, INS_sve_cmla, INS_OPTS_SCALABLE_H, 9, "z0.h, z1.h, z31.h[2], #90")]
    [TestCase(IF_ARM64_SVE_FR_2A, INS_sve_sshllb, INS_OPTS_SCALABLE_S, 3, "z0.s, z1.h, #3")]
    [TestCase(IF_ARM64_SVE_GB_2A, INS_sve_shrnb, INS_OPTS_SCALABLE_H, 4, "z0.h, z1.s, #4")]
    [TestCase(IF_ARM64_SVE_BS_1A, INS_sve_and, INS_OPTS_SCALABLE_D, 0x1000, "z0.d, z0.d, #1")]
    [TestCase(IF_ARM64_SVE_BT_1A, INS_sve_dupm, INS_OPTS_SCALABLE_D, 0x1000, "z0.d, #1")]
    [TestCase(IF_ARM64_SVE_BW_2A, INS_sve_dup, INS_OPTS_SCALABLE_S, 0, "z0.s, s1")]
    [TestCase(IF_ARM64_SVE_BW_2A, INS_sve_dup, INS_OPTS_SCALABLE_S, 3, "z0.s, z1.s[3]")]
    public static void VectorFormatsPreserveAliasesListsPackedImmediatesAndIndexZero(
        Emitter.insFormat format, instruction ins, insOpts opt, int immediate, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(format, ins, opt, REG_ARM64_V0, REG_ARM64_V1, REG_ARM64_V31,
                REG_ARM64_V2, immediate: immediate);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(INS_sve_sel, "z0.s, p7, z1.s, z31.s")]
    [TestCase(INS_sve_mov, "z0.s, p7/m, z1.s")]
    public static void VectorSelectAliasOmitsItsImplicitFinalSource(instruction ins, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_CW_4A, ins, INS_OPTS_SCALABLE_S,
                REG_ARM64_V0, REG_ARM64_P7, REG_ARM64_V1, REG_ARM64_V31);
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
            var id = View.Descriptor(IF_ARM64_SVE_CZ_4A, ins, INS_OPTS_SCALABLE_B,
                REG_ARM64_P0, REG_ARM64_P7, REG_ARM64_P1, REG_ARM64_P15);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(false, "z0.s, p7/z, z1.s")]
    [TestCase(true, "z0.s, p7/m, z1.s")]
    public static void ConstructivePrefixUsesTheRecordedMergeBit(bool merge, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_AH_3A, INS_sve_movprfx, INS_OPTS_SCALABLE_S,
                REG_ARM64_V0, REG_ARM64_P7, REG_ARM64_V1, REG_ARM64_V31);
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
            var id = View.Descriptor(IF_ARM64_SVE_DE_1A, INS_sve_ptrue, INS_OPTS_SCALABLE_S,
                REG_ARM64_P0, REG_ARM64_P0, REG_ARM64_P0, REG_ARM64_P0);
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
            var id = View.Descriptor(IF_ARM64_SVE_BL_1A, INS_sve_cntw, INS_OPTS_NONE,
                REG_ARM64_R0, REG_ARM64_R0, REG_ARM64_R0, REG_ARM64_R0, EA_8BYTE, multiplier);
            id.idSvePattern(SVE_PATTERN_VL4);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(IF_ARM64_SVE_IJ_3A, INS_sve_ld1w, 0, "{ z31.s }, p7/z, [x2]")]
    [TestCase(IF_ARM64_SVE_IJ_3A, INS_sve_ld1w, -2, "{ z31.s }, p7/z, [x2, #-2, mul vl]")]
    [TestCase(IF_ARM64_SVE_IO_3A, INS_sve_ld1rqw, 32, "{ z31.s }, p7/z, [x2, #0x20]")]
    [TestCase(IF_ARM64_SVE_IS_3A, INS_sve_ld2w, 2, "{ z31.s, z0.s }, p7/z, [x2, #0x02, mul vl]")]
    [TestCase(IF_ARM64_SVE_JO_3A, INS_sve_st3w, 3, "{ z31.s, z0.s, z1.s }, p7, [x2, #0x03, mul vl]")]
    public static void MemoryFormatsKeepWrappedListsAndCapstoneHexOffsets(
        Emitter.insFormat format, instruction ins, int immediate, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(format, ins, INS_OPTS_SCALABLE_S,
                REG_ARM64_V31, REG_ARM64_P7, REG_ARM64_R2, REG_ARM64_R3, immediate: immediate);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(EA_4BYTE, "x0, p1.s, w0")]
    [TestCase(EA_8BYTE, "x0, p1.s")]
    public static void SignedPredicateCountPreservesTheThirtyTwoBitInputOperand(emitAttr size, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_DO_2A, INS_sve_sqincp, INS_OPTS_SCALABLE_S,
                REG_ARM64_R0, REG_ARM64_P1, REG_ARM64_R0, REG_ARM64_R0, size);
            AssertOutput(emitter, id, expected);
        });
    }

    [TestCase(false, "x0, pn8.s, vlx2")]
    [TestCase(true, "x0, pn8.s, vlx4")]
    public static void PredicateCountersKeepTheVectorLengthSpecifier(bool four, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_DL_2A, INS_sve_cntp, INS_OPTS_SCALABLE_S,
                REG_ARM64_R0, REG_ARM64_P8, REG_ARM64_R0, REG_ARM64_R0, EA_8BYTE);
            id.idVectorLength4x(four);
            AssertOutput(emitter, id, expected);
        });
    }

    [Test]
    public static void StackAdjustmentConvertsEncodedZeroRegistersToStackPointers()
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_BB_2A, INS_sve_addvl, INS_OPTS_NONE,
                REG_ARM64_ZR, REG_ARM64_ZR, REG_ARM64_R0, REG_ARM64_R0, EA_8BYTE, -16);
            AssertOutput(emitter, id, "sp, sp, #-16");
        });
    }

    [TestCase(false, "z0.s, #7")]
    [TestCase(true, "z0.s, #7, LSL #8")]
    public static void IntegerBroadcastPreservesTheRecordedShift(bool shift, string expected)
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_EB_1A, INS_sve_dup, INS_OPTS_SCALABLE_S,
                REG_ARM64_V0, REG_ARM64_V0, REG_ARM64_V0, REG_ARM64_V0, immediate: 7);
            id.idHasShift(shift);
            AssertOutput(emitter, id, expected);
        });
    }

    [Test]
    public static void FfrInitializeHasNoOperands()
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_SVE_DQ_0A, INS_sve_setffr, INS_OPTS_NONE,
                REG_ARM64_R0, REG_ARM64_R0, REG_ARM64_R0, REG_ARM64_R0);
            AssertOutput(emitter, id, "");
        });
    }

    [Test]
    public static void UnexpectedFormatPreservesDiagnosticAndContinuingEeBehavior()
    {
        WithEmitter(emitter =>
        {
            var id = View.Descriptor(IF_ARM64_EN5A, INS_ldr, INS_OPTS_NONE,
                REG_ARM64_R0, REG_ARM64_R0, REG_ARM64_R0, REG_ARM64_R0);
            var (output, assertions) = Capture(() => Display(emitter, id));

            Assert.That(output, Is.EqualTo("unexpected format IF_EN5A"));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(assertions, Is.EqualTo<string[]>(["!\"unexpectedFormat\""]));
            }
            else
            {
                Assert.That(assertions, Is.Empty);
            }
        });
    }

    internal static void WithEmitter(Action<Emitter> action)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) => action(codeGen.Emitter));
    }

    internal static unsafe (string Output, string[] Assertions) Capture(Action action)
    {
        var isDebug = TestConfiguration.IsDebug;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        ICorJitInfo jitInfo = default;
        ICorJitInfo* jitInfoPointer = null;
        if (isDebug)
        {
            vtable.doAssert = &RecordAssertion;
            jitInfo.lpVtbl = &vtable;
            jitInfoPointer = &jitInfo;
        }

        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        var previousOutput = s_jitstdout;
        var previousAssertions = s_assertions;
        using var tls = TestConfiguration.CreateJitTls(jitInfoPointer);
        JitTls.Compiler = previousCompiler;
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        JitConfig = new JitConfigValues();
        s_jitstdout = writer;
        s_assertions = isDebug ? [] : null;
        try
        {
            action();
            writer.Flush();

            return (Encoding.UTF8.GetString(stream.ToArray()), s_assertions?.ToArray() ?? []);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
            s_jitstdout = previousOutput;
            s_assertions = previousAssertions;
        }
    }

    private static List<string>? s_assertions;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        (s_assertions ?? throw new AssertionException("Unexpected SVE diagnostic assertion callback."))
            .Add(Marshal.PtrToStringUTF8((nint)expression)
                ?? throw new AssertionException("Missing native assertion text."));

        return 0;
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
