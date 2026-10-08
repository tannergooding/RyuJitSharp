// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64SveInstructionSanityTests
{
    [TestCase(IF_SVE_AA_3A, INS_sve_add, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_V31, REG_V0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_AC_3A, INS_sve_sdiv, INS_OPTS_SCALABLE_D, REG_V0, REG_P0, REG_V1, REG_V0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_AR_4A, INS_sve_mla, INS_OPTS_SCALABLE_S, REG_V0, REG_P0, REG_V1, REG_V31, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_BA_3A, INS_sve_index, INS_OPTS_SCALABLE_S, REG_V0, REG_ZR, REG_R30, REG_V0, EA_8BYTE, 0)]
    [TestCase(IF_SVE_BH_3B, INS_sve_adr, INS_OPTS_SCALABLE_D_UXTW, REG_V0, REG_V1, REG_V2, REG_V0, EA_SCALABLE, 3)]
    [TestCase(IF_SVE_BL_1A, INS_sve_cntb, INS_OPTS_NONE, REG_R0, REG_R0, REG_R0, REG_R0, EA_8BYTE, 16)]
    [TestCase(IF_SVE_CX_4A, INS_sve_cmpeq, INS_OPTS_SCALABLE_H, REG_P15, REG_P7, REG_V1, REG_V31, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_CY_3A, INS_sve_cmpeq, INS_OPTS_SCALABLE_B, REG_P0, REG_P0, REG_V0, REG_V0, EA_SCALABLE, -16)]
    [TestCase(IF_SVE_CY_3B, INS_sve_cmpeq, INS_OPTS_SCALABLE_B, REG_P0, REG_P0, REG_V0, REG_V0, EA_SCALABLE, 127)]
    [TestCase(IF_SVE_FD_3A, INS_sve_mul, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, REG_V0, EA_SCALABLE, 7)]
    [TestCase(IF_SVE_FD_3C, INS_sve_mul, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, REG_V0, EA_SCALABLE, 1)]
    [TestCase(IF_SVE_CZ_4A, INS_sve_mov, INS_OPTS_SCALABLE_B, REG_P0, REG_P15, REG_P7, REG_R0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_CW_4A, INS_sve_mov, INS_OPTS_SCALABLE_D, REG_V0, REG_P15, REG_V31, REG_R0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_GQ_3A, INS_sve_fcvtnt, INS_OPTS_S_TO_H, REG_V0, REG_P7, REG_V31, REG_V0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_GQ_3A, INS_sve_bfcvtnt, INS_OPTS_NONE, REG_V0, REG_P0, REG_V1, REG_V0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_AQ_3A, INS_sve_sxtb, INS_OPTS_SCALABLE_H, REG_V0, REG_P0, REG_V1, REG_V0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_FZ_2A, INS_sve_mov, INS_OPTS_NONE, REG_V31, REG_V30, REG_V0, REG_V0, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_AW_2A, INS_sve_xar, INS_OPTS_SCALABLE_D, REG_V0, REG_V1, REG_V0, REG_V0, EA_SCALABLE, 64)]
    [TestCase(IF_SVE_DU_3A, INS_sve_cmpeq, INS_OPTS_SCALABLE_D, REG_P0, REG_R0, REG_R30, REG_R0, EA_8BYTE, 0)]
    [TestCase(IF_SVE_DW_2B, INS_sve_pext, INS_OPTS_SCALABLE_S, REG_P0, REG_P15, REG_R0, REG_R0, EA_SCALABLE, 1)]
    [TestCase(IF_SVE_EJ_3A, INS_sve_cdot, INS_OPTS_SCALABLE_S, REG_V0, REG_V1, REG_V2, REG_V0, EA_SCALABLE, 3)]
    [TestCase(IF_SVE_IH_3A, INS_sve_ld3b, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_R0, REG_R0, EA_SCALABLE, -24)]
    [TestCase(IF_SVE_IO_3A, INS_sve_ld1rob, INS_OPTS_SCALABLE_B, REG_V0, REG_P0, REG_R0, REG_R0, EA_SCALABLE, 224)]
    [TestCase(IF_SVE_HW_4A, INS_sve_ld1b, INS_OPTS_SCALABLE_S_SXTW, REG_V0, REG_P7, REG_R0, REG_V31, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_IW_4A, INS_sve_ld1q, INS_OPTS_SCALABLE_Q, REG_V31, REG_P0, REG_V0, REG_ZR, EA_SCALABLE, 0)]
    [TestCase(IF_SVE_HM_2A, INS_sve_fadd, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_V0, REG_V0, EA_SCALABLE, 1)]
    [TestCase(IF_SVE_ID_2A, INS_sve_ldr, INS_OPTS_NONE, REG_P15, REG_ZR, REG_R0, REG_R0, EA_SCALABLE, -256)]
    [TestCase(IF_SVE_BF_2A, INS_sve_lsr, INS_OPTS_SCALABLE_B, REG_V0, REG_V1, REG_V0, REG_V0, EA_SCALABLE, 8)]
    [TestCase(IF_SVE_BW_2A, INS_sve_dup, INS_OPTS_SCALABLE_Q, REG_V0, REG_V31, REG_V0, REG_V0, EA_SCALABLE, 3)]
    public static void RepresentativeFormatsRetainNativeOptionRegisterAndImmediateChecks(
        Emitter.insFormat format, instruction ins, insOpts opt, regNumber first, regNumber second,
        regNumber third, regNumber fourth, emitAttr size, int immediate)
    {
        var id = SanityEmitter.Descriptor(format, ins, opt, first, second, third, fourth, size, immediate);
        var (output, assertions) = Capture(() => Check(EmitterInstance(), id));

        Assert.That(assertions, Is.Empty);
        Assert.That(output, Is.Empty);
    }

    [Test]
    public static void PredicatedChecksRetainAssertionOrderWhenTheEeContinues()
    {
        var id = SanityEmitter.Descriptor(IF_SVE_AA_3A, INS_sve_add, INS_OPTS_NONE,
            REG_R0, REG_P8, REG_R1, REG_R0, EA_4BYTE, 0);
        var (output, assertions) = Capture(() => Check(EmitterInstance(), id));

        Assert.That(assertions, Is.EqualTo<string[]>([
            "insOptsScalableStandard(id.idInsOpt())",
            "isVectorRegister(id.idReg1())",
            "isLowPredicateRegister(id.idReg2())",
            "isVectorRegister(id.idReg3())",
            "isScalableVectorSize(id.idOpSize())"
        ]));
        Assert.That(output, Is.Empty);
    }

    [TestCase(IF_SVE_BI_2A, INS_OPTS_NONE, REG_V1)]
    [TestCase(IF_SVE_AH_3A, INS_OPTS_SCALABLE_B, REG_P7)]
    public static void MovprfxPairSanityAcceptsMatchingUnpredicatedAndPredicatedPairs(
        Emitter.insFormat prefixFormat, insOpts prefixOptions, regNumber prefixSecondRegister)
    {
        var previous = SanityEmitter.Descriptor(prefixFormat, INS_sve_movprfx, prefixOptions,
            REG_V0, prefixSecondRegister, REG_V0, REG_V0, EA_SCALABLE, 0);
        var current = SanityEmitter.Descriptor(IF_SVE_AA_3A, INS_sve_add, INS_OPTS_SCALABLE_B,
            REG_V0, REG_P7, REG_V2, REG_V3, EA_SCALABLE, 0);
        var (_, assertions) = Capture(() => CheckPair(EmitterInstance(), previous, current));

        Assert.That(assertions, Is.Empty);
    }

    [TestCase(IF_SVE_BN_1A, INS_sve_incd)]
    [TestCase(IF_SVE_AA_3A, INS_sve_add)]
    public static void MovprfxPairSanityRetainsPredicationAndPredicateChecks(
        Emitter.insFormat format, instruction ins)
    {
        var previous = SanityEmitter.Descriptor(IF_SVE_AH_3A, INS_sve_movprfx, INS_OPTS_SCALABLE_B,
            REG_V0, REG_P7, REG_V0, REG_V0, EA_SCALABLE, 0);
        var secondPredicate = format is IF_SVE_AA_3A ? REG_P6 : REG_P7;
        var current = SanityEmitter.Descriptor(format, ins, INS_OPTS_SCALABLE_B,
            REG_V0, secondPredicate, REG_V2, REG_V3, EA_SCALABLE, 0);
        var (_, assertions) = Capture(() => CheckPair(EmitterInstance(), previous, current));
        string[] expectedAssertions = format is IF_SVE_BN_1A
            ? ["!movprfxIsPredicated", "!movprfxIsPredicated"]
            : ["previousId.idReg2() == id.idReg2()"];
        Assert.That(assertions, Is.EqualTo<string[]>(expectedAssertions));
    }

    [TestCase(IF_SVE_DU_3A, INS_OPTS_SCALABLE_B, REG_P0, REG_R0, REG_R1, EA_4BYTE, 0, "id.idOpSize() == EA_8BYTE")]
    [TestCase(IF_SVE_DW_2B, INS_OPTS_SCALABLE_D, REG_P0, REG_P8, REG_R0, EA_SCALABLE, 2, "isValidUimm(emitGetInsSC(id), 1)")]
    [TestCase(IF_SVE_EJ_3A, INS_OPTS_SCALABLE_B, REG_V0, REG_V1, REG_V2, EA_SCALABLE, 0, "insOptsScalableWords(id.idInsOpt())")]
    public static void FallthroughChecksKeepTheirAdditionalNativePredicate(
        Emitter.insFormat format, insOpts opt, regNumber first, regNumber second, regNumber third,
        emitAttr size, int immediate, string assertion)
    {
        var id = SanityEmitter.Descriptor(format, INS_sve_add, opt,
            first, second, third, REG_R0, size, immediate);
        var (_, assertions) = Capture(() => Check(EmitterInstance(), id));

        Assert.That(assertions, Is.EqualTo<string[]>([assertion]));
    }

    [TestCase(IF_SVE_GA_2A, INS_OPTS_SCALABLE_H)]
    [TestCase(IF_SVE_FZ_2A, INS_OPTS_NONE)]
    [TestCase(IF_SVE_HG_2A, INS_OPTS_NONE)]
    public static void PairedVectorFormatsRejectOddSourceRegisters(Emitter.insFormat format, insOpts opt)
    {
        var id = SanityEmitter.Descriptor(format, INS_sve_mov, opt,
            REG_V0, REG_V31, REG_V0, REG_V0, EA_SCALABLE, 0);
        var (_, assertions) = Capture(() => Check(EmitterInstance(), id));

        Assert.That(assertions, Is.EqualTo<string[]>(["isEvenRegister(id.idReg2())"]));
    }

    [TestCase(INS_sve_ld2b, -16, 16, 2)]
    [TestCase(INS_sve_ld3b, -24, 24, 3)]
    [TestCase(INS_sve_ld4b, -32, 32, 4)]
    [TestCase(INS_sve_ld1rqb, -128, 128, 16)]
    [TestCase(INS_sve_ld1rob, -256, 256, 32)]
    public static void StructureImmediatesRetainSignedQuotientAndDivisibilityBounds(
        instruction ins, int minimum, int excludedMaximum, int multiple)
    {
        foreach (var immediate in new[] { minimum, excludedMaximum - multiple, minimum + 1, excludedMaximum })
        {
            var id = SanityEmitter.Descriptor(IF_SVE_IH_3A, ins, INS_OPTS_SCALABLE_B,
                REG_V0, REG_P0, REG_R0, REG_R0, EA_SCALABLE, immediate);
            var (_, assertions) = Capture(() => Check(EmitterInstance(), id));
            var valid = (immediate == minimum) || (immediate == excludedMaximum - multiple);

            Assert.That(assertions.Length, Is.EqualTo(valid ? 0 : 1));
        }
    }

    [TestCase(INS_OPTS_SCALABLE_B, 64)]
    [TestCase(INS_OPTS_SCALABLE_H, 32)]
    [TestCase(INS_OPTS_SCALABLE_S, 16)]
    [TestCase(INS_OPTS_SCALABLE_D, 8)]
    [TestCase(INS_OPTS_SCALABLE_Q, 4)]
    public static void BroadcastIndicesRetainEveryElementSizeBoundary(insOpts opt, int excludedMaximum)
    {
        foreach (var immediate in new[] { -1, 0, excludedMaximum - 1, excludedMaximum })
        {
            var id = SanityEmitter.Descriptor(IF_SVE_BW_2A, INS_sve_dup, opt,
                REG_V0, REG_V31, REG_R0, REG_R0, EA_SCALABLE, immediate);
            var (_, assertions) = Capture(() => Check(EmitterInstance(), id));
            var valid = (immediate >= 0) && (immediate < excludedMaximum);

            Assert.That(assertions, Is.EqualTo<string[]>(valid ? [] : [
                "isValidBroadcastImm(imm, optGetSveElemsize(id.idInsOpt()))"
            ]));
        }
    }

    [TestCase(IF_SVE_HM_2A, 0, true)]
    [TestCase(IF_SVE_HM_2A, 1, true)]
    [TestCase(IF_SVE_HM_2A, 2, false)]
    [TestCase(IF_SVE_HM_2A, -1, false)]
    [TestCase(IF_SVE_FV_2A, 0, true)]
    [TestCase(IF_SVE_FV_2A, 1, true)]
    [TestCase(IF_SVE_FV_2A, 2, false)]
    [TestCase(IF_SVE_FV_2A, -1, false)]
    [TestCase(IF_SVE_EK_3A, 0, true)]
    [TestCase(IF_SVE_EK_3A, 3, true)]
    [TestCase(IF_SVE_EK_3A, 4, false)]
    [TestCase(IF_SVE_EK_3A, -1, false)]
    public static void EncodedFloatAndRotationFieldsRetainTheirExactBounds(
        Emitter.insFormat format, int immediate, bool valid)
    {
        var id = SanityEmitter.Descriptor(format, INS_sve_add, INS_OPTS_SCALABLE_H,
            REG_V0, format == IF_SVE_HM_2A ? REG_P0 : REG_V1, REG_V2, REG_V0, EA_SCALABLE, immediate);
        var (_, assertions) = Capture(() => Check(EmitterInstance(), id));
        var assertion = format switch
        {
            IF_SVE_HM_2A => "emitIsValidEncodedSmallFloatImm(unchecked((nuint)imm))",
            IF_SVE_FV_2A => "emitIsValidEncodedRotationImm90_or_270(emitGetInsSC(id))",
            _ => "emitIsValidEncodedRotationImm0_to_270(emitGetInsSC(id))",
        };

        Assert.That(assertions, Is.EqualTo<string[]>(valid ? [] : [assertion]));
    }

    [TestCase(0x000, 0, 0)]
    [TestCase(0x3CF, 15, 15)]
    [TestCase(0xC30, -16, -16)]
    [TestCase(0x3E1, -1, 15)]
    [TestCase(0x84F, 15, -1)]
    [TestCase(0x820, 0, 0)]
    public static void TwoImmediateDecoderRetainsSeparateMagnitudeAndSignFields(
        int encoded, int expectedFirst, int expectedSecond)
    {
        nint first = 0;
        nint second = 0;
        Decode(null, encoded, &first, &second);
        Assert.That(first, Is.EqualTo((nint)expectedFirst));
        Assert.That(second, Is.EqualTo((nint)expectedSecond));

        var id = SanityEmitter.Descriptor(IF_SVE_AX_1A, INS_sve_index, INS_OPTS_SCALABLE_S,
            REG_V0, REG_R0, REG_R0, REG_R0, EA_SCALABLE, encoded);
        Assert.That(Capture(() => Check(EmitterInstance(), id)).Assertions, Is.Empty);
    }

    [Test]
    public static void InvalidTwoImmediateEncodingRetainsOutputsAndFinalAssertionOrder()
    {
        var (_, assertions) = Capture(() =>
        {
            nint first = 0;
            nint second = 0;
            Decode(null, 0x7DF, &first, &second);
            Assert.That(first, Is.EqualTo((nint)31));
            Assert.That(second, Is.EqualTo((nint)31));
        });

        Assert.That(assertions, Is.EqualTo<string[]>([
            "isValidSimm(*imm1, 5)",
            "isValidSimm(*imm2, 5)"
        ]));
    }

    [TestCase(0L, false)]
    [TestCase(1L, true)]
    [TestCase(16L, true)]
    [TestCase(17L, false)]
    // Native subtraction overflows here; this checks managed unchecked forwarding, not parity.
    [TestCase(long.MinValue, false)]
    public static void FromOneImmediatePredicatePreservesUncheckedForwarding(long value, bool valid)
    {
        Assert.That(FromOne(null, (nint)value, 4), Is.EqualTo(valid));
    }

    [Test]
    public static void UnexpectedFormatReportsNativeTextAndTheEeMayContinue()
    {
        var id = SanityEmitter.Descriptor(IF_EN5A, INS_ldr, INS_OPTS_NONE,
            REG_R0, REG_R0, REG_R0, REG_R0, EA_8BYTE, 0);
        var result = Capture(() => Check(EmitterInstance(), id));

        AssertUnexpectedFormat(result);
    }

    internal static void AssertUnexpectedFormat((string Output, string[] Assertions) result)
    {
#if HOST_WINDOWS
        const string newline = "\r\n";
#else
        const string newline = "\n";
#endif
        Assert.That(result.Output, Is.EqualTo($"unexpected format IF_EN5A{newline}"));
        Assert.That(result.Assertions, Is.EqualTo<string[]>(["!\"Unexpected format\""]));
    }

    internal static (string Output, string[] Assertions) Capture(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        var previousCompiler = JitTls.Compiler;
        using var tls = new JitTls(&ee);
        JitTls.Compiler = previousCompiler;
        var previousConfig = JitConfig;
        var previousOutput = s_jitstdout;
        var previousAssertions = s_assertions.ToArray();
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        JitConfig = new JitConfigValues();
        s_jitstdout = writer;
        s_assertions.Clear();
        try
        {
            action();
            writer.Flush();

            return (Encoding.UTF8.GetString(stream.ToArray()), s_assertions.ToArray());
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
            s_jitstdout = previousOutput;
            s_assertions.Clear();
            s_assertions.AddRange(previousAssertions);
        }
    }

    private static readonly List<string> s_assertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression)
            ?? throw new AssertionException("Missing native assertion text."));

        return 0;
    }

    private static Emitter EmitterInstance()
    {
        return (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
    }

    private abstract class SanityEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Descriptor(Emitter.insFormat format, instruction ins, insOpts opt,
            regNumber first, regNumber second, regNumber third, regNumber fourth, emitAttr size, nint immediate)
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsSveSanityCheck")]
    private static extern void Check(Emitter emitter, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsPairSanityCheck")]
    private static extern void CheckPair(Emitter emitter, Emitter.instrDesc? previousId, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insSveDecodeTwoSimm5")]
    private static extern void Decode(Emitter? emitter, nint immediate, nint* first, nint* second);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidUimmFrom1")]
    private static extern bool FromOne(Emitter? emitter, nint immediate, int bits);
}
#endif
