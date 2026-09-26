// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class EHRegionInsertionTests
{
    [TestCase(true)]
    [TestCase(false)]
    public static void OutermostRegionPreservesTheIncomingKind(bool inTry)
    {
        var initialValue = inTry;
        var descriptor = new EHblkDsc
        {
            ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        };

        Assert.That(descriptor.ebdGetEnclosingRegionIndex(ref inTry), Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
        Assert.That(inTry, Is.EqualTo(initialValue));
    }

    [TestCase(1, -1, 1, true)]
    [TestCase(-1, 1, 1, false)]
    [TestCase(0, 1, 0, true)]
    [TestCase(1, 0, 0, false)]
    public static void EnclosingRegionSelectsTheInnermostKind(
        int tryIndex, int handlerIndex, int expectedIndex, bool expectedTry)
    {
        var descriptor = new EHblkDsc
        {
            ebdEnclosingTryIndex = tryIndex < 0 ? EHblkDsc.NO_ENCLOSING_INDEX : (ushort)tryIndex,
            ebdEnclosingHndIndex = handlerIndex < 0 ? EHblkDsc.NO_ENCLOSING_INDEX : (ushort)handlerIndex,
        };
        var inTry = !expectedTry;

        Assert.That(descriptor.ebdGetEnclosingRegionIndex(ref inTry), Is.EqualTo(expectedIndex));
        Assert.That(inTry, Is.EqualTo(expectedTry));
    }

    [Test]
    public static void InsertionAfterLastNestedHandlerStaysAdjacentToProtectedTry()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var root = BasicBlock.New(compiler, BBJ_RETURN);
            var outerTry = BasicBlock.New(compiler, BBJ_RETURN);
            var innerTry = BasicBlock.New(compiler, BBJ_RETURN);
            var innerHandler = BasicBlock.New(compiler, BBJ_RETURN);
            var outerHandler = BasicBlock.New(compiler, BBJ_RETURN);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);

            root.Next = outerTry;
            outerTry.Next = innerTry;
            innerTry.Next = innerHandler;
            innerHandler.Next = outerHandler;
            outerHandler.Next = continuation;
            compiler.fgFirstBB = root;
            compiler.fgLastBB = continuation;

            outerTry.TryIndex = 1;
            innerTry.TryIndex = 0;
            innerHandler.TryIndex = 1;
            innerHandler.HndIndex = 0;
            outerHandler.HndIndex = 1;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = innerTry,
                    ebdTryLast = innerTry,
                    ebdHndBeg = innerHandler,
                    ebdHndLast = innerHandler,
                    ebdEnclosingTryIndex = 1,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdTryBeg = outerTry,
                    ebdTryLast = innerHandler,
                    ebdHndBeg = outerHandler,
                    ebdHndLast = outerHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 2;

            Assert.That(compiler.fgCheckEHCanInsertAfterBlock(innerHandler, 0, true), Is.True);
            Assert.That(compiler.fgFindInsertPoint(0, true, root, null, innerTry, null, false),
                Is.SameAs(innerHandler));
        });
    }
}
