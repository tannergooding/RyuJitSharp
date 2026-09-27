// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FinallyOptimizationTests
{
    private static PhaseStatus Merge(Compiler compiler) => compiler.fgMergeFinallyChains();
    private static PhaseStatus Clone(Compiler compiler) => compiler.fgCloneFinally();

    [Test]
    public static void MergeCanonicalizesBranchesWithSameContinuationAndTransfersProfile()
    {
        WithCompiler(compiler =>
        {
            var firstTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var secondTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var firstCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var firstLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var secondCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var secondLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, firstTry, secondTry, firstCall, firstLeave,
                secondCall, secondLeave, handler, continuation);
            Jump(compiler, firstTry, firstCall);
            Jump(compiler, secondTry, secondCall);
            Jump(compiler, firstCall, handler);
            Jump(compiler, secondCall, handler);
            Jump(compiler, firstLeave, continuation);
            Jump(compiler, secondLeave, continuation);
            firstTry.TryIndex = 0;
            secondTry.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            firstTry.setBBProfileWeight(10);
            secondTry.setBBProfileWeight(30);
            firstCall.setBBProfileWeight(10);
            secondCall.setBBProfileWeight(30);
            Clause(compiler, firstTry, secondTry, handler, handler);

            Assert.That(Merge(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(firstTry.Target, Is.SameAs(firstCall));
            Assert.That(secondTry.Target, Is.SameAs(firstCall));
            Assert.That(firstCall.bbWeight, Is.EqualTo(40));
            Assert.That(secondCall.bbWeight, Is.Zero);
            Assert.That(firstCall.bbRefs, Is.EqualTo(2));
            Assert.That(secondCall.bbRefs, Is.Zero);
            Assert.That(firstLeave.Target, Is.SameAs(continuation));
            Assert.That(secondLeave.Target, Is.SameAs(continuation));
            Assert.That(compiler.compHndBBtab[0].ebdHandlerType, Is.EqualTo(EH_HANDLER_FINALLY));
        });
    }

    [Test]
    public static void DifferentContinuationsDoNotMerge()
    {
        WithCompiler(compiler =>
        {
            var firstTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var secondTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var firstCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var firstLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var secondCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var secondLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var firstContinuation = BasicBlock.New(compiler, BBJ_RETURN);
            var secondContinuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, firstTry, secondTry, firstCall, firstLeave,
                secondCall, secondLeave, handler, firstContinuation, secondContinuation);
            Jump(compiler, firstTry, firstCall);
            Jump(compiler, secondTry, secondCall);
            Jump(compiler, firstCall, handler);
            Jump(compiler, secondCall, handler);
            Jump(compiler, firstLeave, firstContinuation);
            Jump(compiler, secondLeave, secondContinuation);
            firstTry.TryIndex = 0;
            secondTry.TryIndex = 0;
            handler.HndIndex = 0;
            Clause(compiler, firstTry, secondTry, handler, handler);

            Assert.That(Merge(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(firstTry.Target, Is.SameAs(firstCall));
            Assert.That(secondTry.Target, Is.SameAs(secondCall));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CloneRetargetsCallFinallyAndConvertsOriginalHandlerToFault(bool secondContinuation)
    {
        WithCompiler(compiler =>
        {
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var call = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var leave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var otherCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var otherLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            var otherContinuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, tryBlock, call, leave, otherCall, otherLeave,
                handler, continuation, otherContinuation);
            Jump(compiler, tryBlock, call);
            Jump(compiler, call, handler);
            Jump(compiler, leave, continuation);
            Jump(compiler, otherCall, handler);
            Jump(compiler, otherLeave, secondContinuation ? otherContinuation : continuation);
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            handler.bbRefs++;
            handler.SetFlags(BBF_DONT_REMOVE);
            var firstReturn = compiler.fgAddRefPred(leave, handler);
            var otherReturn = compiler.fgAddRefPred(otherLeave, handler);
            firstReturn.Likelihood = 0.5;
            otherReturn.Likelihood = 0.5;
            handler.SetEhf(new BBJumpTable([firstReturn, otherReturn]));
            compiler.fgInsertStmtAtEnd(handler,
                compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null)));
            Clause(compiler, tryBlock, tryBlock, handler, handler);

            Assert.That(compiler.compHndBBtab[0].HasFinallyHandler, Is.True);
            Assert.That(call.isBBCallFinallyPair, Is.True);
            Assert.That(tryBlock.Target, Is.SameAs(call));
#if DEBUG
            Assert.That(Globals.JitConfig.JitEnableFinallyCloning, Is.EqualTo(1));
#endif
            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
            var cloned = call.Target;
            Assert.That(cloned, Is.Not.SameAs(handler));
            Assert.That(call.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(cloned.HasFlag(BBF_CLONED_FINALLY_BEGIN | BBF_CLONED_FINALLY_END), Is.True);
            Assert.That(cloned.HasFlag(BBF_DONT_REMOVE), Is.False);
            Assert.That(cloned.hasHndIndex, Is.False);
            Assert.That(cloned.CatchType, Is.EqualTo(bbCatchType.BBCT_NONE));
            Assert.That(cloned.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(cloned.Target, Is.SameAs(continuation));
            Assert.That(cloned.LastStmt, Is.Null);
            Assert.That(leave.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(secondContinuation, Is.EqualTo(compiler.compHndBBtab[0].HasFinallyHandler));
            Assert.That(handler.Kind, Is.EqualTo(secondContinuation ? BBJ_EHFINALLYRET : BBJ_EHFAULTRET));
            Assert.That(handler.CatchType,
                Is.EqualTo(secondContinuation ? bbCatchType.BBCT_FINALLY : bbCatchType.BBCT_FAULT));
            Assert.That(otherCall.Kind, Is.EqualTo(secondContinuation ? BBJ_CALLFINALLY : BBJ_ALWAYS));
            Assert.That(otherCall.Target, Is.SameAs(secondContinuation ? handler : cloned));
            Assert.That(otherLeave.HasFlag(BBF_REMOVED), Is.EqualTo(!secondContinuation));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CloneRejectsThrowOnlyOrOversizedFinally(bool oversized)
    {
        WithCompiler(compiler =>
        {
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var call = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var leave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, oversized ? BBJ_EHFINALLYRET : BBJ_THROW);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, tryBlock, call, leave, handler, continuation);
            Jump(compiler, tryBlock, call);
            Jump(compiler, call, handler);
            Jump(compiler, leave, continuation);
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            if (oversized)
            {
                for (var i = 0; i < 15; i++)
                {
                    compiler.fgInsertStmtAtEnd(handler,
                        compiler.fgNewStmtFromTree(compiler.gtNewIconNode(TYP_INT, i)));
                }
                compiler.fgInsertStmtAtEnd(handler,
                    compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null)));
            }
            Clause(compiler, tryBlock, tryBlock, handler, handler);

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(call.Target, Is.SameAs(handler));
            Assert.That(call.Kind, Is.EqualTo(BBJ_CALLFINALLY));
            Assert.That(compiler.compHndBBtab[0].ebdHandlerType, Is.EqualTo(EH_HANDLER_FINALLY));
        });
    }

    [Test]
    public static void CloneMapsInternalBranchesAndSplitsProfileWeights()
    {
        WithCompiler(compiler =>
        {
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var call = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var leave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var firstHandler = BasicBlock.New(compiler, BBJ_ALWAYS);
            var lastHandler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, tryBlock, call, leave, firstHandler, lastHandler, continuation);
            Jump(compiler, tryBlock, call);
            Jump(compiler, call, firstHandler);
            Jump(compiler, leave, continuation);
            Jump(compiler, firstHandler, lastHandler);
            var ehfEdge = compiler.fgAddRefPred(leave, lastHandler);
            ehfEdge.Likelihood = 1.0;
            lastHandler.SetEhf(new BBJumpTable([ehfEdge]));
            compiler.fgInsertStmtAtEnd(firstHandler,
                compiler.fgNewStmtFromTree(compiler.gtNewIconNode(TYP_INT, 17)));
            compiler.fgInsertStmtAtEnd(lastHandler,
                compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null)));
            tryBlock.TryIndex = 0;
            firstHandler.HndIndex = 0;
            lastHandler.HndIndex = 0;
            firstHandler.CatchType = bbCatchType.BBCT_FINALLY;
            firstHandler.bbRefs++;
            firstHandler.setBBProfileWeight(100);
            lastHandler.setBBProfileWeight(80);
            call.setBBProfileWeight(40);
            continuation.setBBProfileWeight(80);
            compiler.fgPgoHaveWeights = true;
            Clause(compiler, tryBlock, tryBlock, firstHandler, lastHandler);

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var firstClone = call.Target;
            var lastClone = firstClone.Target;
            Assert.That(firstClone, Is.Not.SameAs(firstHandler));
            Assert.That(lastClone, Is.Not.SameAs(lastHandler));
            Assert.That(firstClone.HasFlag(BBF_CLONED_FINALLY_BEGIN), Is.True);
            Assert.That(lastClone.HasFlag(BBF_CLONED_FINALLY_END), Is.True);
            Assert.That(firstClone.FirstStmt!.RootNode.AsIntCon().IconValue, Is.EqualTo((nint)17));
            Assert.That(firstClone.FirstStmt, Is.Not.SameAs(firstHandler.FirstStmt));
            Assert.That(lastClone.LastStmt, Is.Null);
            Assert.That(lastClone.Target, Is.SameAs(continuation));
            Assert.That(firstHandler.bbWeight, Is.EqualTo(60));
            Assert.That(lastHandler.bbWeight, Is.EqualTo(48));
            Assert.That(firstClone.bbWeight, Is.EqualTo(40));
            Assert.That(lastClone.bbWeight, Is.EqualTo(32));
            Assert.That(continuation.bbWeight, Is.EqualTo(32));
            Assert.That(compiler.compHndBBtab[0].ebdHandlerType, Is.EqualTo(EH_HANDLER_FAULT_WAS_FINALLY));
        });
    }

    [Test]
    public static void ProfiledClonePrefersHeavierTryExitOverLastLexicalExit()
    {
        WithCompiler(compiler =>
        {
            var firstTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var lastTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var firstCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var firstLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var lastCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var lastLeave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var firstContinuation = BasicBlock.New(compiler, BBJ_RETURN);
            var lastContinuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, firstTry, lastTry, firstCall, firstLeave, lastCall, lastLeave,
                handler, firstContinuation, lastContinuation);
            Jump(compiler, firstTry, firstCall);
            Jump(compiler, lastTry, lastCall);
            Jump(compiler, firstCall, handler);
            Jump(compiler, lastCall, handler);
            Jump(compiler, firstLeave, firstContinuation);
            Jump(compiler, lastLeave, lastContinuation);
            var firstReturn = compiler.fgAddRefPred(firstLeave, handler);
            var lastReturn = compiler.fgAddRefPred(lastLeave, handler);
            firstReturn.Likelihood = 0.5;
            lastReturn.Likelihood = 0.5;
            handler.SetEhf(new BBJumpTable([firstReturn, lastReturn]));
            compiler.fgInsertStmtAtEnd(handler,
                compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null)));
            firstTry.TryIndex = 0;
            lastTry.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            handler.bbRefs++;
            firstTry.setBBProfileWeight(50);
            lastTry.setBBProfileWeight(10);
            firstCall.setBBProfileWeight(50);
            lastCall.setBBProfileWeight(10);
            handler.setBBProfileWeight(100);
            compiler.fgPgoHaveWeights = true;
            Clause(compiler, firstTry, lastTry, handler, handler);

            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(firstCall.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(firstCall.Target.HasFlag(BBF_CLONED_FINALLY_BEGIN), Is.True);
            Assert.That(firstCall.Target.Target, Is.SameAs(firstContinuation));
            Assert.That(firstLeave.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(lastCall.Kind, Is.EqualTo(BBJ_CALLFINALLY));
            Assert.That(lastCall.Target, Is.SameAs(handler));
            Assert.That(lastLeave.HasFlag(BBF_REMOVED), Is.False);
            Assert.That(handler.bbWeight, Is.EqualTo(50));
            Assert.That(firstCall.Target.bbWeight, Is.EqualTo(50));
            Assert.That(compiler.compHndBBtab[0].ebdHandlerType, Is.EqualTo(EH_HANDLER_FINALLY));
            Assert.That(compiler.fgPgoConsistent, Is.False);
        });
    }

    [Test]
    public static void NoExceptionRegionsLeaveBothPrivateCoresUnchanged()
    {
        WithCompiler(compiler =>
        {
            Assert.That(Merge(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(Clone(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        var previous = Globals.JitConfig;
        object config = new JitConfigValues();
        var field = typeof(JitConfigValues).GetField("_jitEnableFinallyCloning",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing finally-cloning configuration.");
        field.SetValue(config, 1);
        Globals.JitConfig = (JitConfigValues)config;
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, action);
        }
        finally
        {
            Globals.JitConfig = previous;
        }
#else
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, action);
#endif
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        foreach (var block in blocks)
        {
            block.bbRefs = 0;
        }

        for (var index = 1; index < blocks.Length; index++)
        {
            blocks[index - 1].Next = blocks[index];
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
    }

    private static void Jump(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(source.Kind, compiler.fgAddRefPred(target, source));
    }

    private static void Clause(Compiler compiler, BasicBlock firstTry, BasicBlock lastTry,
        BasicBlock firstHandler, BasicBlock lastHandler)
    {
        firstTry.SetFlags(BBF_DONT_REMOVE);
        firstHandler.SetFlags(BBF_DONT_REMOVE);
        firstHandler.CatchType = bbCatchType.BBCT_FINALLY;
        compiler.compHndBBtab =
        [
            new EHblkDsc
            {
                ebdHandlerType = EH_HANDLER_FINALLY,
                ebdTryBeg = firstTry,
                ebdTryLast = lastTry,
                ebdHndBeg = firstHandler,
                ebdHndLast = lastHandler,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            },
        ];
        compiler.compHndBBtabCount = 1;
        compiler.compEHID = 1;
    }
}
