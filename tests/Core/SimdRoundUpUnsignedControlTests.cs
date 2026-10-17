// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SimdRoundUpUnsignedControlTests
{
    [TestCase(32, int.MinValue, 32)]
    [TestCase(32, -1, 32)]
    [TestCase(32, 16, 16)]
    [TestCase(32, 17, 32)]
    [TestCase(32, 32, 32)]
    [TestCase(32, 33, 32)]
    [TestCase(64, int.MinValue, 64)]
    [TestCase(64, -1, 64)]
    [TestCase(64, 16, 16)]
    [TestCase(64, 17, 32)]
    [TestCase(64, 32, 32)]
    [TestCase(64, 33, 64)]
    public static void PreferredWidthUsesUnsignedSize(int preferredWidth, int size, int expected)
    {
        Assume.That(Configuration.FEATURE_SIMD && Configuration.TARGET_AMD64,
            "requires AMD64 SIMD support");

        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX);

            if (preferredWidth == 64)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            }

            compiler.opts.preferredVectorByteLength = preferredWidth;

            Assert.That(compiler.roundUpSimdSize(size), Is.EqualTo(expected));
        });
    }
}
