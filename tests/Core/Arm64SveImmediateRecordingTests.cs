// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSvePattern;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64SveImmediateRecordingTests
{
    [Test]
    public static void SetFirstFaultRegisterOverridesAttributeAndImmediate()
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_I(INS_sve_setffr, EA_4BYTE, 123));

            Assert.That(id.idIns(), Is.EqualTo(INS_sve_setffr));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_SVE_DQ_0A));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(Emitter.emitGetInsSC(id), Is.Zero);
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_sve_aesmc, REG_V0, INS_OPTS_NONE, INS_sve_aesmc, IF_SVE_GL_1A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_aesimc, REG_V31, INS_OPTS_NONE, INS_sve_aesimc, IF_SVE_GL_1A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_rdffr, REG_P0, INS_OPTS_NONE, INS_sve_rdffr, IF_SVE_DH_1A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_pfalse, REG_P15, INS_OPTS_NONE, INS_sve_pfalse, IF_SVE_DJ_1A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_wrffr, REG_P15, INS_OPTS_NONE, INS_sve_wrffr, IF_SVE_DR_1A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_ptrue, REG_P8, INS_OPTS_SCALABLE_D, INS_sve_ptrue, IF_SVE_DZ_1A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_fmov, REG_V31, INS_OPTS_SCALABLE_S, INS_sve_mov, IF_SVE_EB_1B, INS_OPTS_SCALABLE_S)]
    public static void RegisterFormsPreserveForcedOptionsAndPreferredAliases(
        instruction ins, regNumber reg, insOpts opt, instruction expectedIns,
        Emitter.insFormat format, insOpts expectedOpt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R(ins, EA_SCALABLE, reg, opt));

            Assert.That(id.idIns(), Is.EqualTo(expectedIns));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idInsOpt(), Is.EqualTo(expectedOpt));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_sve_rdvl, EA_8BYTE, REG_R0, INS_OPTS_NONE, -32, -32, false, INS_sve_rdvl, IF_SVE_BC_1A)]
    [TestCase(INS_sve_rdvl, EA_8BYTE, REG_R30, INS_OPTS_NONE, 31, 31, false, INS_sve_rdvl, IF_SVE_BC_1A)]
    [TestCase(INS_sve_smax, EA_SCALABLE, REG_V0, INS_OPTS_SCALABLE_B, -128, -128, false, INS_sve_smax, IF_SVE_ED_1A)]
    [TestCase(INS_sve_umin, EA_SCALABLE, REG_V0, INS_OPTS_SCALABLE_B, 255, 255, false, INS_sve_umin, IF_SVE_ED_1A)]
    [TestCase(INS_sve_mul, EA_SCALABLE, REG_V0, INS_OPTS_SCALABLE_S, 127, 127, false, INS_sve_mul, IF_SVE_EE_1A)]
    [TestCase(INS_sve_mov, EA_SCALABLE, REG_V0, INS_OPTS_SCALABLE_B, -128, -128, false, INS_sve_mov, IF_SVE_EB_1A)]
    [TestCase(INS_sve_mov, EA_SCALABLE, REG_V0, INS_OPTS_SCALABLE_H, -32768, -128, true, INS_sve_mov, IF_SVE_EB_1A)]
    [TestCase(INS_sve_dup, EA_SCALABLE, REG_V31, INS_OPTS_SCALABLE_D, 32512, 127, true, INS_sve_mov, IF_SVE_EB_1A)]
    [TestCase(INS_sve_add, EA_SCALABLE, REG_V0, INS_OPTS_SCALABLE_B, 255, 255, false, INS_sve_add, IF_SVE_EC_1A)]
    [TestCase(INS_sve_subr, EA_SCALABLE, REG_V31, INS_OPTS_SCALABLE_H, 65280, 255, true, INS_sve_subr, IF_SVE_EC_1A)]
    public static void ImmediateFormsPreserveBoundsArithmeticShiftAndDescriptorChoice(
        instruction ins, emitAttr attr, regNumber reg, insOpts opt, int immediate,
        int encoded, bool shifted, instruction expectedIns, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_I(ins, attr, reg, immediate, opt));

            Assert.That(id.idIns(), Is.EqualTo(expectedIns));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idInsOpt(), Is.EqualTo(opt));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)encoded));
            Assert.That(id.idHasShift(), Is.EqualTo(shifted));
            Assert.That(id.idIsSmallDsc(), Is.EqualTo(!shifted && Emitter.instrDesc.fitsInSmallCns(encoded)));
        });
    }

    [TestCase(-16, 15, 0x3F0)]
    [TestCase(15, -16, 0xC0F)]
    [TestCase(-16, -16, 0xC30)]
    public static void TwoSignedImmediatesPreserveSignAndMagnitudeEncoding(int imm1, int imm2, int encoded)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_I_I(
                INS_sve_index, EA_SCALABLE, REG_V0, imm1, imm2, INS_OPTS_SCALABLE_S));

            Assert.That(id.idIns(), Is.EqualTo(INS_sve_index));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_SVE_AX_1A));
            Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_S));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)encoded));
        });
    }

    [TestCase(INS_sve_fadd, REG_P0, 0.5, INS_OPTS_SCALABLE_H, INS_sve_fadd, IF_SVE_HM_2A, 0)]
    [TestCase(INS_sve_fsubr, REG_P7, 1.0, INS_OPTS_SCALABLE_D, INS_sve_fsubr, IF_SVE_HM_2A, 1)]
    [TestCase(INS_sve_fmaxnm, REG_P0, 0.0, INS_OPTS_SCALABLE_S, INS_sve_fmaxnm, IF_SVE_HM_2A, 0)]
    [TestCase(INS_sve_fmul, REG_P7, 2.0, INS_OPTS_SCALABLE_H, INS_sve_fmul, IF_SVE_HM_2A, 1)]
    [TestCase(INS_sve_fcpy, REG_P15, -10.0, INS_OPTS_SCALABLE_D, INS_sve_fmov, IF_SVE_BU_2A, 0xA4)]
    [TestCase(INS_sve_fmov, REG_P0, 2.0, INS_OPTS_SCALABLE_H, INS_sve_fmov, IF_SVE_BU_2A, 0)]
    public static void RegisterFloatConstantFormsPreserveImmediateEncodingsAndAliases(
        instruction ins, regNumber predicate, double immediate, insOpts opt, instruction expectedIns,
        Emitter.insFormat format, int encoded)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_F(
                ins, EA_SCALABLE, REG_V0, predicate, immediate, opt));

            Assert.That(id.idIns(), Is.EqualTo(expectedIns));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idInsOpt(), Is.EqualTo(opt));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(id.idReg2(), Is.EqualTo(predicate));
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)encoded));
        });
    }

    [TestCase(INS_sve_and, 255L, INS_SCALABLE_OPTS_NONE, INS_sve_and, IF_SVE_BS_1A, 4103)]
    [TestCase(INS_sve_bic, -256L, INS_SCALABLE_OPTS_NONE, INS_sve_and, IF_SVE_BS_1A, 4103)]
    [TestCase(INS_sve_eon, -256L, INS_SCALABLE_OPTS_NONE, INS_sve_eor, IF_SVE_BS_1A, 4103)]
    [TestCase(INS_sve_orn, -256L, INS_SCALABLE_OPTS_NONE, INS_sve_orr, IF_SVE_BS_1A, 4103)]
    [TestCase(INS_sve_mov, 255L, INS_SCALABLE_OPTS_IMM_BITMASK, INS_sve_dupm, IF_SVE_BT_1A, 4103)]
    [TestCase(INS_sve_dupm, 0x00ff00ff00ff00ffL, INS_SCALABLE_OPTS_NONE, INS_sve_dupm, IF_SVE_BT_1A, 39)]
    [TestCase(INS_sve_dupm, 0x0000000000ffff00L, INS_SCALABLE_OPTS_NONE, INS_sve_mov, IF_SVE_BT_1A, 7695)]
    [TestCase(INS_sve_dupm, -256L, INS_SCALABLE_OPTS_NONE, INS_sve_dupm, IF_SVE_BT_1A, 7735)]
    [TestCase(INS_sve_bic, long.MinValue, INS_SCALABLE_OPTS_NONE, INS_sve_and, IF_SVE_BS_1A, 4158)]
    public static void BitmaskFormsPreserveComplementEncodingAndDisassemblyPreference(
        instruction ins, long immediate, insScalableOpts sopt, instruction expectedIns,
        Emitter.insFormat format, int encoded)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_I(
                ins, EA_SCALABLE, REG_V0, (nint)immediate, INS_OPTS_SCALABLE_D, sopt));

            Assert.That(id.idIns(), Is.EqualTo(expectedIns));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_D));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)encoded));
            Assert.That(id.idHasShift(), Is.False);
        });
    }

    [TestCase(INS_sve_fmov, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_fdup, INS_OPTS_SCALABLE_D)]
    public static void FloatingFormsPreserveEveryImmediateEncodingAndPreferredAlias(instruction ins, insOpts opt)
    {
        for (var encoded = 0; encoded <= 255; encoded++)
        {
            var exponent = ((encoded >> 4) & 7) ^ 4;
            var value = Math.ScaleB(16 + (encoded & 15), exponent - 7);
            if ((encoded & 128) != 0)
            {
                value = -value;
            }

            WithEmitter(emitter =>
            {
                var id = Record(emitter, () => emitter.emitIns_R_F(ins, EA_SCALABLE, REG_V0, value, opt));

                Assert.That(id.idIns(), Is.EqualTo(INS_sve_fmov));
                Assert.That(id.idInsFmt(), Is.EqualTo(IF_SVE_EA_1A));
                Assert.That(id.idInsOpt(), Is.EqualTo(opt));
                Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
                Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)encoded));
            });
        }
    }

    [TestCase(INS_sve_ptrue, REG_P0, INS_OPTS_SCALABLE_B, SVE_PATTERN_ALL)]
    [TestCase(INS_sve_ptrues, REG_P15, INS_OPTS_SCALABLE_D, SVE_PATTERN_VL1)]
    public static void PredicatePatternsUseNormalDescriptorsAndRetainPattern(
        instruction ins, regNumber reg, insOpts opt, insSvePattern pattern)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_PATTERN(ins, EA_SCALABLE, reg, opt, pattern));

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_SVE_DE_1A));
            Assert.That(id.idInsOpt(), Is.EqualTo(opt));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idSvePattern(), Is.EqualTo(pattern));
            Assert.That(id.idIsSmallDsc(), Is.False);
        });
    }

    [TestCase(0x81, 8u, 0x8181818181818181UL, 0xFFFFFFFFFFFFFF81UL)]
    [TestCase(0x81, 16u, 0x0081008100810081UL, 0xFF81FF81FF81FF81UL)]
    [TestCase(0x12FF, 16u, 0x12FF12FF12FF12FFUL, 0xFFFFFFFFFFFF12FFUL)]
    [TestCase(0x12FF, 32u, 0x000012FF000012FFUL, 0xFFFF12FFFFFF12FFUL)]
    [TestCase(0x81, 64u, 0x81UL, 0xFFFFFFFFFFFFFF81UL)]
    [TestCase(0x12FF, 64u, 0x12FFUL, 0xFFFFFFFFFFFF12FFUL)]
    public static void BitmaskHelpersRetainLiteralManagedShiftBehavior(
        int immediate, uint width, ulong zeroes, ulong ones)
    {
        Assert.That(unchecked((ulong)BitMaskZeroes(null, immediate, width)), Is.EqualTo(zeroes));
        Assert.That(unchecked((ulong)BitMaskOnes(null, immediate, width)), Is.EqualTo(ones));
    }

    [TestCase(INS_OPTS_SCALABLE_B, EA_1BYTE)]
    [TestCase(INS_OPTS_SCALABLE_H, EA_2BYTE)]
    [TestCase(INS_OPTS_SCALABLE_S, EA_4BYTE)]
    [TestCase(INS_OPTS_SCALABLE_S_UXTW, EA_4BYTE)]
    [TestCase(INS_OPTS_SCALABLE_S_SXTW, EA_4BYTE)]
    [TestCase(INS_OPTS_SCALABLE_D, EA_8BYTE)]
    [TestCase(INS_OPTS_SCALABLE_D_UXTW, EA_8BYTE)]
    [TestCase(INS_OPTS_SCALABLE_D_SXTW, EA_8BYTE)]
    [TestCase(INS_OPTS_SCALABLE_Q, EA_16BYTE)]
    public static void ScalableElementMappingPreservesExtensionAndQuadwordOptions(insOpts opt, emitAttr attr)
    {
        Assert.That(Emitter.optGetSveElemsize(opt), Is.EqualTo(attr));
    }

    private static Emitter.instrDesc Record(Emitter emitter, Action record)
    {
#if DEBUG
        var (output, assertions) = Arm64SveInstructionSanityTests.Capture(record);
        Assert.That(assertions, Is.Empty);
        Assert.That(output, Is.Empty);
#else
        record();
#endif
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
        var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
        Assert.That(id.idCodeSize(), Is.EqualTo(4u));

        return id;
    }

    private static void WithEmitter(Action<Emitter> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            var emitter = new Emitter(codeGen);
            CodeGenEmitter(codeGen) = emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(emitter);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cgEmitter")]
    private static extern ref Emitter CodeGenEmitter(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "getBitMaskZeroes")]
    private static extern nint BitMaskZeroes(Emitter? emitter, nint immediate, uint width);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "getBitMaskOnes")]
    private static extern nint BitMaskOnes(Emitter? emitter, nint immediate, uint width);
}
#endif
