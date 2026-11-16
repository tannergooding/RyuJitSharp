// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareIntrinsicOracleRefreshTests
{
    [Test]
    public static void SimdSubtractionPreservesOrderForThrowingNegatedOperands()
    {
        Assume.That(Configuration.FEATURE_HW_INTRINSICS &&
            (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64),
            "This test requires xarch or ARM64 hardware-intrinsic support.");

        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            compiler.fgGlobalMorph = true;

            var firstCall = compiler.gtNewCallNode(TYP_SIMD16, gtCallTypes.CT_USER_FUNC, null);
            var secondCall = compiler.gtNewCallNode(TYP_SIMD16, gtCallTypes.CT_USER_FUNC, null);
            var firstNegation = compiler.gtNewSimdUnOpNode(GT_NEG, TYP_SIMD16, firstCall, TYP_INT, 16);
            var secondNegation = compiler.gtNewSimdUnOpNode(GT_NEG, TYP_SIMD16, secondCall, TYP_INT, 16);
            var subtraction = compiler.gtNewSimdBinOpNode(
                GT_SUB, TYP_SIMD16, firstNegation, secondNegation, TYP_INT, 16).AsHWIntrinsic();

            Assert.That(compiler.gtCanSwapOrder(firstNegation, secondNegation), Is.False);

            var result = compiler.fgOptimizeHWIntrinsic(subtraction).AsHWIntrinsic();

            Assert.That(result.GetOperForHWIntrinsicId(out _), Is.EqualTo(GT_ADD));
            Assert.That(result.GetOp(1), Is.SameAs(firstNegation));
            Assert.That(result.GetOp(2), Is.SameAs(secondCall));
        });
    }

    [TestCase(NI_IsSupported)]
    [TestCase(NI_IsHardwareAccelerated)]
    public static void WasmNativeAotIntrinsicQueryUsesCompileTimeIsaSupport(NamedIntrinsic intrinsic)
    {
        Assume.That(Configuration.TARGET_WASM && Configuration.FEATURE_HW_INTRINSICS,
            "This test requires WebAssembly hardware-intrinsic support.");

        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            EnableHardwareIntrinsics(ref JitConfig) = 1;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_WasmBase);

            var result = ResolveHardwareIntrinsicId(
                compiler, intrinsic, InstructionSet_WasmBase, isXplatIntrinsic: true);

            Assert.That(result, Is.EqualTo(NI_IsSupported_True));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveHardwareIntrinsicId")]
    private static extern NamedIntrinsic ResolveHardwareIntrinsicId(
        Compiler compiler, NamedIntrinsic intrinsic, ManagedInstructionSet isa, bool isXplatIntrinsic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableHWIntrinsic")]
    private static extern ref int EnableHardwareIntrinsics(ref JitConfigValues jitConfig);
}
