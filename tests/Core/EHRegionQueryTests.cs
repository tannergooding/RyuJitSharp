// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using NUnit.Framework;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.EHblkDsc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EHRegionQueryTests
{
    [TestCase(NO_ENCLOSING_INDEX, NO_ENCLOSING_INDEX, 0u, 0u)]
    [TestCase(NO_ENCLOSING_INDEX, (ushort)3, 1u, 0u)]
    [TestCase(NO_ENCLOSING_INDEX, (ushort)2, 2u, 0u)]
    [TestCase((ushort)2, (ushort)3, 1u, 1u)]
    [TestCase((ushort)2, (ushort)1, 2u, 1u)]
    [TestCase((ushort)2, (ushort)0, 3u, 1u)]
    [TestCase((ushort)0, (ushort)1, 2u, 2u)]
    public static void HandlerNestingTracksTheInnermostFinally(
        ushort tryIndex, ushort handlerIndex, uint expectedNesting, uint expectedFinally)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_FINALLY,
                    ebdTryBeg = new BasicBlock(null, null),
                    ebdHndBeg = new BasicBlock(null, null),
                    ebdEnclosingTryIndex = 2,
                    ebdEnclosingHndIndex = 1,
                },
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_CATCH,
                    ebdTryBeg = new BasicBlock(null, null),
                    ebdHndBeg = new BasicBlock(null, null),
                    ebdEnclosingTryIndex = 2,
                    ebdEnclosingHndIndex = 3,
                },
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_FINALLY,
                    ebdTryBeg = new BasicBlock(null, null),
                    ebdHndBeg = new BasicBlock(null, null),
                    ebdEnclosingTryIndex = NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = 3,
                },
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_CATCH,
                    ebdTryBeg = new BasicBlock(null, null),
                    ebdHndBeg = new BasicBlock(null, null),
                    ebdEnclosingTryIndex = NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 4;
            var block = new BasicBlock(null, null);
            if (tryIndex != NO_ENCLOSING_INDEX)
            {
                block.TryIndex = tryIndex;
            }
            if (handlerIndex != NO_ENCLOSING_INDEX)
            {
                block.HndIndex = handlerIndex;
            }

            Assert.That(compiler.fgGetNestingLevel(block, out var finallyNesting), Is.EqualTo(expectedNesting));
            Assert.That(finallyNesting, Is.EqualTo(expectedFinally));
            Assert.That(compiler.fgGetNestingLevel(block), Is.EqualTo(expectedNesting));
        });
    }

    [Test]
    public static void EmptyTableHasNoNesting()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            compiler.compHndBBtab = [];
            compiler.compHndBBtabCount = 0;

            Assert.That(compiler.fgGetNestingLevel(new BasicBlock(null, null), out var finallyNesting), Is.Zero);
            Assert.That(finallyNesting, Is.Zero);
        });
    }

    [TestCase(10, 20, false, false, (ushort)2)]
    [TestCase(10, 20, true, false, (ushort)2)]
    [TestCase(9, 20, true, false, (ushort)1)]
    [TestCase(10, 21, true, false, (ushort)1)]
    [TestCase(10, 20, false, true, NO_ENCLOSING_INDEX)]
    public static void ImportEnclosingTryUsesILBoundsRatherThanBlockIdentity(
        int enclosingBegin, int enclosingEnd, bool sameBlocks, bool allMutualProtect, ushort expectedIndex)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var first = new BasicBlock(null, null);
            var second = sameBlocks ? first : new BasicBlock(null, null);
            var outer = new BasicBlock(null, null);
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    _ebdTryBegOffset = 10,
                    _ebdTryEndOffset = 20,
                    ebdTryBeg = first,
                    ebdTryLast = first,
                    ebdEnclosingTryIndex = 1,
                },
                new EHblkDsc
                {
                    _ebdTryBegOffset = enclosingBegin,
                    _ebdTryEndOffset = enclosingEnd,
                    ebdTryBeg = second,
                    ebdTryLast = second,
                    ebdEnclosingTryIndex = 2,
                },
                new EHblkDsc
                {
                    _ebdTryBegOffset = allMutualProtect ? 10 : 0,
                    _ebdTryEndOffset = allMutualProtect ? 20 : 30,
                    ebdTryBeg = outer,
                    ebdTryLast = outer,
                    ebdEnclosingTryIndex = NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 3;
            compiler.fgImportDone = false;

            Assert.That(compiler.ehTrueEnclosingTryIndexIL(0), Is.EqualTo(expectedIndex));
            Assert.That(compiler.ehTrueEnclosingTryIndexIL(2), Is.EqualTo(NO_ENCLOSING_INDEX));

            compiler.fgImportDone = true;
            Assert.That(compiler.ehTrueEnclosingTryIndex(0), Is.EqualTo(sameBlocks ? 2 : 1));
        });
    }
}
