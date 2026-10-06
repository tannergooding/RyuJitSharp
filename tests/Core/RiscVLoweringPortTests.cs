// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RiscVLoweringPortTests
{
#if FEATURE_SIMD
    [Test]
    public static void UnportedSimdLoweringDependenciesTerminateAfterTheNyiDiagnosticReturns()
    {
        WithAltJit(() =>
        {
            var lowering = CreateUninitializedLowering();
            AssertSkipped(() => LowerSIMD(lowering, null!));
            AssertSkipped(() => ContainCheckSIMD(lowering, null!));
        });
    }

    [Test]
    public static void SimdIndirectionContainmentContinuesAfterTheNyiDiagnosticReturns()
    {
        WithAltJitLowering((compiler, lowering) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 4096);
            var indirection = new GenTreeIndir(GT_IND, TYP_SIMD16, address);

            Assert.DoesNotThrow(() => ContainCheckIndir(lowering, indirection));
            Assert.That(address.IsContained, Is.True);
        });
    }
#endif

#if FEATURE_HW_INTRINSICS
    [Test]
    public static void UnportedHardwareIntrinsicLoweringDependenciesTerminateAfterTheNyiDiagnosticReturns()
    {
        WithAltJit(() =>
        {
            var lowering = CreateUninitializedLowering();
            AssertSkipped(() => LowerHWIntrinsic(lowering, null!));
            AssertSkipped(() => IsValidConstForMovImm(lowering, null!));
            AssertSkipped(() => LowerHWIntrinsicCmpOp(lowering, null!, GT_EQ));
            AssertSkipped(() => LowerHWIntrinsicCreate(lowering, null!));
            AssertSkipped(() => LowerHWIntrinsicDot(lowering, null!));
            AssertSkipped(() => ContainCheckHWIntrinsic(lowering, null!));
        });
    }
#endif

    private static void AssertSkipped(Action action)
    {
        Assert.That(Assert.Throws<FatalJitException>(() => action())?.Result, Is.EqualTo(CORJIT_SKIPPED));
    }

    private static Lowering CreateUninitializedLowering()
        => (Lowering)RuntimeHelpers.GetUninitializedObject(typeof(Lowering));

    private static void WithAltJitLowering(Action<Compiler, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);

        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void WithAltJit(Action action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            action();
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if FEATURE_SIMD
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerSIMD")]
    private static extern void LowerSIMD(Lowering lowering, GenTreeSIMD node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckSIMD")]
    private static extern void ContainCheckSIMD(Lowering lowering, GenTreeSIMD node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckIndir")]
    private static extern void ContainCheckIndir(Lowering lowering, GenTreeIndir node);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

#if FEATURE_HW_INTRINSICS
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsic")]
    private static extern GenTree? LowerHWIntrinsic(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "IsValidConstForMovImm")]
    private static extern bool IsValidConstForMovImm(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCmpOp")]
    private static extern GenTree? LowerHWIntrinsicCmpOp(
        Lowering lowering,
        GenTreeHWIntrinsic node,
        genTreeOps cmpOp);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCreate")]
    private static extern GenTree? LowerHWIntrinsicCreate(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicDot")]
    private static extern GenTree? LowerHWIntrinsicDot(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckHWIntrinsic")]
    private static extern void ContainCheckHWIntrinsic(Lowering lowering, GenTreeHWIntrinsic node);
#endif
}
#endif
