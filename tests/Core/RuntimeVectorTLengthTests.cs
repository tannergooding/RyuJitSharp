// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 && FEATURE_HW_INTRINSICS
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class RuntimeVectorTLengthTests
{
    [TestCase(InstructionSet_VectorT128, 16u)]
    [TestCase(InstructionSet_VectorT256, 32u)]
    [TestCase(InstructionSet_VectorT512, 64u)]
    public static void UsesReportedCompileTimeWidth(CORINFO_InstructionSet isa, uint expected)
    {
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
#endif
