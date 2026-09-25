// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class IntrinsicEncodingTests
{
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BroadcastUsesTheEmitterEncodingState(bool evex, bool contained)
    {
        WithCompiler((compiler, codeGen) => {
            EnableIsa(compiler, InstructionSet_AVX512);
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = evex;
            var broadcast = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX2_BroadcastScalarToVector128, TYP_FLOAT, 16,
                new GenTreeLclVar(TYP_BYREF, 0)) { IsContained = contained };

            Assert.That(codeGen.IsEmbeddedBroadcastEnabled(INS_addps, broadcast), Is.EqualTo(evex && contained));
            Assert.That(codeGen.IsEmbeddedBroadcastEnabled(INS_pextrb, broadcast), Is.False);
        });
    }

    [Test]
    public static void PromotedEvexDoesNotRequireVectorEvex()
    {
        WithCompiler((_, codeGen) => {
            var emitter = codeGen.Emitter;
            emitter.UsePromotedEvexEncodings = true;

            Assert.That(emitter.UseEvexEncodings, Is.False);
            Assert.That(emitter.IsEvexEncodableInstruction(INS_add), Is.True);
            Assert.That(emitter.IsEvexEncodableInstruction(INS_kmovq_gpr), Is.True);
            Assert.That(emitter.IsEvexEncodableInstruction(INS_addps), Is.False);
        });
    }

    [TestCase(INS_aesenc, InstructionSet_AES_V512)]
    [TestCase(INS_vpdpbusd, InstructionSet_AVX512v3)]
    [TestCase(INS_vpdpwsud, InstructionSet_AVXVNNIINT_V512)]
    public static void EvexAvailabilityRetainsInstructionSpecificIsaRequirements(
        instruction instruction, CORINFO_InstructionSet requiredIsa)
    {
        WithCompiler((compiler, codeGen) => {
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = true;
            emitter.UseEvexEncodings = true;
            compiler.opts.compSupportsISAReported.AddInstructionSet(requiredIsa);

            Assert.That(emitter.IsEvexEncodableInstruction(instruction), Is.False);
            EnableIsa(compiler, requiredIsa);
            Assert.That(emitter.IsEvexEncodableInstruction(instruction), Is.True);
        });
    }

    private static void EnableIsa(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.Emitter.emitBegCG(compiler, null);
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
