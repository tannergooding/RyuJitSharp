// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CodeGenEHTransferTargetTests
{
    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void RetlessFinallyCallStopsAtUnsupportedInstructionRecordingBoundary(
        bool hasNextBlock, bool hasEhBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(BBJ_CALLFINALLY, target);
            block.SetFlags(BBF_RETLESS_CALL);

            if (!hasNextBlock)
            {
                block.Next = null;
            }
            else if (hasEhBoundary)
            {
                var next = Label();
                next.TryIndex = 0;
                block.Next = next;
            }
            else
            {
                block.Next = Label();
            }

            compiler.compCurBB = block;

            AssertUnsupportedRecording(() => codeGen.genCallFinally(block));
        });
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void ReturningFinallyCallStopsAtUnsupportedInstructionRecordingBoundary(
        bool adjacent, bool differentRegions)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var continuation = Label();
            var block = Transfer(BBJ_CALLFINALLY, target);
            var finallyReturn = Transfer(BBJ_CALLFINALLYRET, continuation);
            block.Next = finallyReturn;
            finallyReturn.Next = adjacent ? continuation : Label();
            if (differentRegions)
            {
                continuation.SetFlags(BBF_COLD);
                compiler.fgFirstColdBlock = continuation;
            }

            compiler.compCurBB = block;

            AssertUnsupportedRecording(() => codeGen.genCallFinally(block));
        });
    }

    [Test]
    public static void CatchReturnStopsAtUnsupportedInstructionRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(BBJ_EHCATCHRET, target);
            compiler.compCurBB = block;

            AssertUnsupportedRecording(() => codeGen.genEHCatchRet(block));
        });
    }

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);

        return block;
    }

    private static BasicBlock Transfer(BBKinds kind, BasicBlock target)
    {
        var block = new BasicBlock(null, null);
        block.SetKindAndTargetEdge(kind, new FlowEdge(block, target, null));

        return block;
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
        Arm64HardwareIntrinsicCodegenTests.WithCodeGen(action);
    }

    private static void AssertUnsupportedRecording(Action action)
    {
        var failure = Assert.Throws<FatalJitException>(() => action()) ??
            throw new AssertionException("The unported ARM64 instruction recorder did not fail.");
        Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(failure.Message, Is.EqualTo("Instruction recording outside AMD64 is not ported."));
    }
}
#endif
