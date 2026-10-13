// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class UnknownSizeFrameConfigurationTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InitializationUsesTheRuntimeTargetAndFeature(bool arm64, bool simd)
    {
        WithCompiler(compiler =>
        {
            compiler.compUsesUnknownSizeFrame = true;
            compiler.compLocallocUsed = true;
            compiler.unkSizeFrame.nVector = 3;
            compiler.unkSizeFrame.nMask = 9;
            compiler.unkSizeFrame.FinalizeLayout();

            WithConfiguration(arm64, simd, () =>
            {
                if (arm64 && simd)
                {
                    compiler.lvaInitUnknownSizeFrame();
                }
                else
                {
                    var failure = Assert.Throws<FatalJitException>(() => compiler.lvaInitUnknownSizeFrame());
                    Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
                }
            });

            var supported = arm64 && simd;
            Assert.That(compiler.compUsesUnknownSizeFrame, Is.EqualTo(!supported));
            Assert.That(compiler.compLocallocUsed, Is.True);
            Assert.That(compiler.unkSizeFrame.nVector, Is.EqualTo(supported ? 0u : 3u));
            Assert.That(compiler.unkSizeFrame.nMask, Is.EqualTo(supported ? 0u : 9u));
#if DEBUG
            Assert.That(compiler.unkSizeFrame.isFinalized, Is.EqualTo(!supported));
#endif
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void UnsupportedAllocationAndEmissionTerminateBeforeChangingState(bool arm64, bool simd)
    {
        WithCompiler(compiler =>
        {
            compiler.lvaCount = 1;
            compiler.lvaTable = [new() { Type = TYP_INT, StackOffset = 19 }];
            compiler.unkSizeFrame.nVector = 3;
            compiler.unkSizeFrame.nMask = 9;
            var temp = new TempDsc(-1, 4, TYP_INT) { tdTempOffs = 23 };
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;

            WithConfiguration(arm64, simd, () =>
            {
                var localFailure = Assert.Throws<FatalJitException>(() => compiler.lvaAllocUnknownSizeLocal(0));
                var tempFailure = Assert.Throws<FatalJitException>(() => compiler.lvaAllocateUnknownSizeTemp(temp));
                var frameFailure = Assert.Throws<FatalJitException>(() => codeGen.genUnknownSizeFrame());
                var zeroFailure = Assert.Throws<FatalJitException>(() => codeGen.genZeroInitializeUnknownSizeFrame());
                Assert.That(localFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
                Assert.That(tempFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
                Assert.That(frameFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(zeroFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            });

            Assert.That(compiler.compUsesUnknownSizeFrame, Is.False);
            Assert.That(compiler.compLocallocUsed, Is.False);
            Assert.That(compiler.unkSizeFrame.nVector, Is.EqualTo(3u));
            Assert.That(compiler.unkSizeFrame.nMask, Is.EqualTo(9u));
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(19));
            Assert.That(temp.tdTempOffs, Is.EqualTo(23));
        });
    }

    [TestCase(0u, 0u)]
    [TestCase(1u, 1u)]
    [TestCase(8u, 1u)]
    [TestCase(9u, 2u)]
    public static void SharedBookkeepingPreservesMaskPaddingAndNegativeVectorOffsets(uint maskCount, uint maskVectors)
    {
        Compiler.UnknownSizeFrame frame = default;
        for (var index = 0u; index < maskCount; index++)
        {
            Assert.That(frame.AllocMask(), Is.EqualTo(index));
        }

        Assert.That(frame.AllocVector(), Is.Zero);
        Assert.That(frame.AllocVector(), Is.EqualTo(1u));
        frame.FinalizeLayout();

        Assert.That(frame.MaskBlockSizeInVectors(), Is.EqualTo(maskVectors));
        Assert.That(frame.VectorBlockSize(), Is.EqualTo(2u));
        Assert.That(frame.FrameSizeInVectors(), Is.EqualTo(maskVectors + 2u));
        Assert.That(frame.GetOffset(0), Is.EqualTo(-unchecked((int)maskVectors) - 1));
        Assert.That(frame.GetOffset(1), Is.EqualTo(-unchecked((int)maskVectors) - 2));

        if (maskCount > 0)
        {
            Assert.That(frame.GetOffset(maskCount - 1, isMask: true), Is.EqualTo(-unchecked((int)maskCount)));
        }
    }

    [TestCase(16u)]
    [TestCase(32u)]
    [TestCase(256u)]
    public static void Arm64LocalAndTempAllocationShareTheNativeVectorIndices(uint vectorLength)
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            Assert.Ignore("Scalable local and temporary allocation requires SIMD-enabled ARM64.");
        }

        WithCompiler(compiler =>
        {
            compiler.lvaCount = 2;
            compiler.lvaTable = [new() { Type = TYP_SIMD_ARM64 }, new() { Type = TYP_SIMD_ARM64 }];
            var temp = new TempDsc(-1, 0, TYP_SIMD_ARM64);
            compiler.lvaInitUnknownSizeFrame();

            compiler.lvaAllocUnknownSizeLocal(0);
            compiler.lvaAllocUnknownSizeLocal(1);
            compiler.lvaAllocateUnknownSizeTemp(temp);
            compiler.unkSizeFrame.FinalizeLayout();

            Assert.That(compiler.compUsesUnknownSizeFrame, Is.True);
            Assert.That(compiler.compLocallocUsed, Is.True);
            Assert.That(compiler.lvaTable[0].UnknownSizeFrameIndex, Is.Zero);
            Assert.That(compiler.lvaTable[1].UnknownSizeFrameIndex, Is.EqualTo(1));
            Assert.That(temp.tdTempOffs, Is.EqualTo(2));
            Assert.That(compiler.unkSizeFrame.nMask, Is.Zero);
            Assert.That(compiler.unkSizeFrame.FrameSizeInVectors(), Is.EqualTo(3u));
            Assert.That(compiler.unkSizeFrame.GetAddressingOffset(in compiler.lvaTable[0]), Is.EqualTo(-1));
            Assert.That(compiler.unkSizeFrame.GetAddressingOffset(in compiler.lvaTable[1]), Is.EqualTo(-2));
            Assert.That(compiler.unkSizeFrame.GetAddressingOffset(temp), Is.EqualTo(-3));
            Assert.That(compiler.unkSizeFrame.GetExactOffset(in compiler.lvaTable[1], vectorLength),
                Is.EqualTo(-2 * unchecked((int)vectorLength)));
        });
    }

    private static void WithConfiguration(bool arm64, bool simd, Action action)
    {
        const string targetSwitch = "RyuJitSharp.TARGET_ARM64";
        const string featureSwitch = "RyuJitSharp.FEATURE_SIMD";
        Assert.That(AppContext.TryGetSwitch(targetSwitch, out var previousTarget), Is.True);
        Assert.That(AppContext.TryGetSwitch(featureSwitch, out var previousFeature), Is.True);
        AppContext.SetSwitch(targetSwitch, arm64);
        AppContext.SetSwitch(featureSwitch, simd);

        try
        {
            action();
        }
        finally
        {
            AppContext.SetSwitch(targetSwitch, previousTarget);
            AppContext.SetSwitch(featureSwitch, previousFeature);
        }
    }

    private static unsafe void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.info.compFullName = nameof(UnknownSizeFrameConfigurationTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
