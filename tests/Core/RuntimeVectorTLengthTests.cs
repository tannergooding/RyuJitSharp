// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class RuntimeVectorTLengthTests
{
    [TestCase(InstructionSet_VectorT128, 16u)]
    [TestCase(InstructionSet_VectorT256, 32u)]
    [TestCase(InstructionSet_VectorT512, 64u)]
    public static void UsesReportedCompileTimeWidth(ManagedInstructionSet isa, uint expected)
    {
        Assume.That(Configuration.TARGET_AMD64 && Configuration.FEATURE_HW_INTRINSICS,
            "requires AMD64 hardware intrinsic support");

        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT128);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT256);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT512);
            compiler.opts.compSupportsISA.AddInstructionSet(isa);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);

            Assert.That(compiler.getRuntimeVectorTByteLength(), Is.EqualTo(expected));
        });
    }
}
