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
}
