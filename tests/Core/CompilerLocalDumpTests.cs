// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if DEBUG
[NonParallelizable]
internal static class CompilerLocalDumpTests
{
    [TestCase(0, "struct ( 0)")]
    [TestCase(16, "struct (16)")]
    public static void StructStackHomeSizeUsesSpacePaddedNativeWidth(int size, string expected)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.lvaTable[1] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = new ClassLayout(size),
                lvOnFrame = true,
                RegNum = REG_STK,
            };
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;

            var output = CodeGenLifeTransitionTests.Capture(
                () => compiler.lvaDumpEntry(1, Compiler.FINAL_FRAME_LAYOUT, 0));

            Assert.That(output, Does.Contain(expected));
        });
    }
}
#endif
