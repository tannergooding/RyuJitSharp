// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LocalVariableTempTests
{
    [TestCase(0, 2)]
    [TestCase(1, 2)]
    [TestCase(2, 2)]
    [TestCase(3, 2)]
    [TestCase(0, 8)]
    [TestCase(1, 8)]
    [TestCase(2, 8)]
    [TestCase(3, 8)]
    public static void BatchAllocationInitializesEveryTemporaryAndPreservesExistingLocals(int count, int capacity)
    {
        LinearScanMinimalCandidatesTests.WithCompiler(compiler => {
            compiler.lvaCount = 2;
            compiler.lvaTable = new LclVarDsc[capacity];
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[0].lvOnFrame = true;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaTable[1].lvIsParam = true;
            for (var index = 2; index < capacity; index++)
            {
                compiler.lvaTable[index].Type = TYP_LONG;
                compiler.lvaTable[index].lvIsTemp = true;
            }

            var first = compiler.lvaGrabTemps(count, nameof(LocalVariableTempTests));

            Assert.That(first, Is.EqualTo(2));
            Assert.That(compiler.lvaCount, Is.EqualTo(2 + count));
            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(TYP_REF));
            Assert.That(compiler.lvaTable[0].lvOnFrame, Is.True);
            Assert.That(compiler.lvaTable[1].Type, Is.EqualTo(TYP_INT));
            Assert.That(compiler.lvaTable[1].lvIsParam, Is.True);
            for (var index = first; index < compiler.lvaCount; index++)
            {
                Assert.That(compiler.lvaTable[index].lvOnFrame, Is.True, $"V{index:D2} needs a stack home.");
                Assert.That(compiler.lvaTable[index].Type, Is.EqualTo(TYP_UNDEF));
                Assert.That(compiler.lvaTable[index].lvIsTemp, Is.False);
            }
        });
    }
}
