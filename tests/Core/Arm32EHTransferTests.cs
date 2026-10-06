// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32EHTransferTests
{
    [TestCase(false, false, 2)]
    [TestCase(true, false, 1)]
    [TestCase(true, true, 2)]
    public static void RetlessFinallyCallsKeepTheReturnAddressInsideTheirEHRegion(
        bool hasNextBlock, bool differentRegion, int expectedInstructionCount)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(BBJ_CALLFINALLY, target);
            block.SetFlags(BBF_RETLESS_CALL);
            if (hasNextBlock)
            {
                var nextBlock = Label();
                if (differentRegion)
                {
                    nextBlock.TryIndex = 0;
                }
                block.Next = nextBlock;
            }
            compiler.compCurBB = block;

            codeGen.genCallFinally(block);

            var descriptors = Descriptors(codeGen.Emitter);
            instruction[] expectedInstructions = expectedInstructionCount == 1
                ? [INS_bl]
                : [INS_bl, INS_BREAKPOINT];
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo(expectedInstructions));
            Assert.That(((Emitter.instrDescJmp)descriptors[0]).idjTarget, Is.SameAs(target));
        });
    }

    [TestCase(true, false, INS_nop)]
    [TestCase(false, false, INS_b)]
    [TestCase(true, true, INS_b)]
    public static void ReturningFinallyCallsSelectTheNativeContinuationForm(
        bool adjacent, bool crossesColdBoundary, instruction continuationInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var handler = Label();
            var continuation = Label();
            var block = Transfer(BBJ_CALLFINALLY, handler);
            var finallyReturn = Transfer(BBJ_CALLFINALLYRET, continuation);
            block.Next = finallyReturn;
            finallyReturn.Next = adjacent ? continuation : Label();
            if (crossesColdBoundary)
            {
                continuation.SetFlags(BBF_COLD);
                compiler.fgFirstColdBlock = continuation;
            }
            compiler.compCurBB = block;

            codeGen.genCallFinally(block);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_bl, continuationInstruction]));
            Assert.That(((Emitter.instrDescJmp)descriptors[0]).idjTarget, Is.SameAs(handler));
            if (continuationInstruction is INS_b)
            {
                Assert.That(((Emitter.instrDescJmp)descriptors[1]).idjTarget, Is.SameAs(continuation));
            }
            Assert.That(codeGen.Emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.NoGCInterrupt));
        });
    }

    [Test]
    public static void CatchReturnsLoadTheirRelocatableAddressIntoTheIntegerReturnRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(BBJ_EHCATCHRET, target);
            compiler.compCurBB = block;

            codeGen.genEHCatchRet(block);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_movw, INS_movt]));
            foreach (var descriptor in descriptors)
            {
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_INTRET));
                Assert.That(((Emitter.instrDescJmp)descriptor).idjTarget, Is.SameAs(target));
            }
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

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
