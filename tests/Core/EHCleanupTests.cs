// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Reflection;
#endif
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EHCleanupTests
{
    private static PhaseStatus RemoveEmptyFinally(Compiler compiler)
        => compiler.fgRemoveEmptyFinally();

    private static PhaseStatus RemoveEmptyTry(Compiler compiler)
        => compiler.fgRemoveEmptyTry();

    private static PhaseStatus RemoveEmptyTryCatchOrFault(Compiler compiler)
        => compiler.fgRemoveEmptyTryCatchOrTryFault();

    [Test]
    public static void EmptyFinallyRedirectsCallFinallyAndRemovesHandler()
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var callFinally = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var leave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, tryBlock, callFinally, leave, handler, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, tryBlock);
            Jump(compiler, tryBlock, callFinally);
            Jump(compiler, callFinally, handler);
            Jump(compiler, leave, continuation);
            var ehfEdge = compiler.fgAddRefPred(leave, handler);
            ehfEdge.Likelihood = 1.0;
            handler.SetEhf(new BBJumpTable([ehfEdge]));
            compiler.fgInsertStmtAtEnd(handler,
                compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null)));
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            handler.bbRefs++;
            tryBlock.SetFlags(BBF_DONT_REMOVE);
            handler.SetFlags(BBF_DONT_REMOVE);
            callFinally.setBBProfileWeight(25);
            continuation.setBBProfileWeight(100);
            Clause(compiler, EH_HANDLER_FINALLY, tryBlock, tryBlock, handler, handler);

            Assert.That(RemoveEmptyFinally(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(callFinally.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(callFinally.Target, Is.SameAs(continuation));
            Assert.That(continuation.bbWeight, Is.EqualTo(125));
            Assert.That(callFinally.Next, Is.SameAs(continuation));
            Assert.That(leave.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(handler.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(tryBlock.hasTryIndex, Is.False);
            Assert.That(tryBlock.HasFlag(BBF_DONT_REMOVE), Is.False);
            Assert.That(continuation.GetUniquePred(compiler), Is.SameAs(callFinally));
        });
    }

    [Test]
    public static void EmptyTryPromotesFinallyAndConnectsItsReturn()
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var callFinally = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var leave = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, tryBlock, callFinally, leave, handler, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, tryBlock);
            Jump(compiler, tryBlock, callFinally);
            Jump(compiler, callFinally, handler);
            Jump(compiler, leave, continuation);
            var ehfEdge = compiler.fgAddRefPred(leave, handler);
            ehfEdge.Likelihood = 1.0;
            handler.SetEhf(new BBJumpTable([ehfEdge]));
            compiler.fgInsertStmtAtEnd(handler,
                compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null)));
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            handler.bbRefs++;
            tryBlock.SetFlags(BBF_DONT_REMOVE);
            handler.SetFlags(BBF_DONT_REMOVE);
            handler.setBBProfileWeight(30);
            continuation.setBBProfileWeight(100);
            Clause(compiler, EH_HANDLER_FINALLY, tryBlock, tryBlock, handler, handler);

            Assert.That(RemoveEmptyTry(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(tryBlock.hasTryIndex, Is.False);
            Assert.That(callFinally.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(callFinally.Target, Is.SameAs(handler));
            Assert.That(handler.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(handler.Target, Is.SameAs(continuation));
            Assert.That(handler.CatchType, Is.EqualTo(bbCatchType.BBCT_NONE));
            Assert.That(handler.LastStmt, Is.Null);
            Assert.That(handler.bbRefs, Is.EqualTo(1));
            Assert.That(handler.hasHndIndex, Is.False);
            Assert.That(handler.HasFlag(BBF_DONT_REMOVE), Is.False);
            Assert.That(continuation.bbWeight, Is.EqualTo(130));
            Assert.That(leave.HasFlag(BBF_REMOVED), Is.True);
        });
    }

    [TestCase(EH_HANDLER_FAULT)]
    [TestCase(EH_HANDLER_FAULT_WAS_FINALLY)]
    public static void EmptyFaultRemovesHandlerWithoutCallFinally(EHHandlerType kind)
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var handler = BasicBlock.New(compiler, BBJ_EHFAULTRET);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, tryBlock, handler, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, tryBlock);
            Jump(compiler, tryBlock, continuation);
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.bbRefs = 1;
            handler.SetFlags(BBF_DONT_REMOVE);
            tryBlock.SetFlags(BBF_DONT_REMOVE);
            Clause(compiler, kind, tryBlock, tryBlock, handler, handler);

            Assert.That(RemoveEmptyFinally(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(tryBlock.hasTryIndex, Is.False);
            Assert.That(tryBlock.Next, Is.SameAs(continuation));
            Assert.That(handler.HasFlag(BBF_REMOVED), Is.True);
        });
    }

    [TestCase(EH_HANDLER_CATCH)]
    [TestCase(EH_HANDLER_FAULT)]
    public static void NonThrowingTryRemovesHandlerAndReparentsTry(EHHandlerType kind)
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var handler = BasicBlock.New(compiler, BBJ_THROW);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, tryBlock, handler, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, tryBlock);
            Jump(compiler, tryBlock, continuation);
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.bbRefs = 1;
            tryBlock.SetFlags(BBF_DONT_REMOVE);
            handler.SetFlags(BBF_DONT_REMOVE);
            Clause(compiler, kind, tryBlock, tryBlock, handler, handler);

            Assert.That(RemoveEmptyTryCatchOrFault(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(tryBlock.hasTryIndex, Is.False);
            Assert.That(tryBlock.HasFlag(BBF_DONT_REMOVE), Is.False);
            Assert.That(handler.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(tryBlock.Next, Is.SameAs(continuation));
            Assert.That(continuation.GetUniquePred(compiler), Is.SameAs(tryBlock));
        });
    }

    [Test]
    public static void FilterAndHandlerBackwardEdgesAreRemovedInTwoPasses()
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var filterStart = BasicBlock.New(compiler, BBJ_ALWAYS);
            var filterEnd = BasicBlock.New(compiler, BBJ_COND);
            var handlerStart = BasicBlock.New(compiler, BBJ_ALWAYS);
            var handlerEnd = BasicBlock.New(compiler, BBJ_THROW);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, tryBlock, filterStart, filterEnd, handlerStart, handlerEnd, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, tryBlock);
            Jump(compiler, tryBlock, continuation);
            Jump(compiler, filterStart, filterEnd);
            filterEnd.SetCond(compiler.fgAddRefPred(filterStart, filterEnd),
                compiler.fgAddRefPred(handlerStart, filterEnd));
            Jump(compiler, handlerStart, handlerEnd);
            tryBlock.TryIndex = 0;
            foreach (var block in new[] { filterStart, filterEnd, handlerStart, handlerEnd })
            {
                block.HndIndex = 0;
                block.SetFlags(BBF_DONT_REMOVE);
            }

            filterStart.bbRefs++;
            handlerStart.bbRefs++;
            Clause(compiler, EH_HANDLER_FILTER, tryBlock, tryBlock, handlerStart, handlerEnd,
                filterStart);

            Assert.That(RemoveEmptyTryCatchOrFault(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(tryBlock.Next, Is.SameAs(continuation));
            Assert.That(filterStart.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(filterEnd.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(handlerStart.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(handlerEnd.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(continuation.GetUniquePred(compiler), Is.SameAs(tryBlock));
        });
    }

    [Test]
    public static void ThrowingStatementKeepsCatchRegion()
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tryBlock = BasicBlock.New(compiler, BBJ_ALWAYS);
            var handler = BasicBlock.New(compiler, BBJ_THROW);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, tryBlock, handler, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, tryBlock);
            Jump(compiler, tryBlock, continuation);
            var throwing = compiler.gtNewIconNode(TYP_INT, 5);
            throwing.Flags |= GTF_EXCEPT;
            compiler.fgInsertStmtAtEnd(tryBlock, compiler.fgNewStmtFromTree(throwing));
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            handler.bbRefs = 1;
            Clause(compiler, EH_HANDLER_CATCH, tryBlock, tryBlock, handler, handler);

            Assert.That(RemoveEmptyTryCatchOrFault(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
            Assert.That(handler.HasFlag(BBF_REMOVED), Is.False);
            Assert.That(tryBlock.TryIndex, Is.Zero);
        });
    }

    [Test]
    public static void RemovingFirstClauseCompactsLaterEHIndicesWithoutRemovingThrowingClause()
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var firstTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var firstHandler = BasicBlock.New(compiler, BBJ_THROW);
            var secondTry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var secondHandler = BasicBlock.New(compiler, BBJ_THROW);
            var continuation = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, entry, firstTry, firstHandler, secondTry, secondHandler, continuation);
            entry.bbRefs = 1;
            Jump(compiler, entry, firstTry);
            Jump(compiler, firstTry, secondTry);
            Jump(compiler, secondTry, continuation);
            firstTry.TryIndex = 0;
            firstHandler.HndIndex = 0;
            firstHandler.bbRefs = 1;
            secondTry.TryIndex = 1;
            secondHandler.HndIndex = 1;
            secondHandler.bbRefs = 1;
            var throwing = compiler.gtNewIconNode(TYP_INT, 1);
            throwing.Flags |= GTF_CALL;
            compiler.fgInsertStmtAtEnd(secondTry, compiler.fgNewStmtFromTree(throwing));
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_CATCH,
                    ebdTryBeg = firstTry,
                    ebdTryLast = firstTry,
                    ebdHndBeg = firstHandler,
                    ebdHndLast = firstHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_CATCH,
                    ebdTryBeg = secondTry,
                    ebdTryLast = secondTry,
                    ebdHndBeg = secondHandler,
                    ebdHndLast = secondHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 2;

            Assert.That(RemoveEmptyTryCatchOrFault(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(secondTry));
            Assert.That(firstHandler.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(firstTry.hasTryIndex, Is.False);
            Assert.That(secondTry.TryIndex, Is.Zero);
            Assert.That(secondHandler.HndIndex, Is.Zero);
            Assert.That(secondHandler.HasFlag(BBF_REMOVED), Is.False);
            Assert.That(firstTry.Next, Is.SameAs(secondTry));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        var previous = Globals.JitConfig;
        object config = new JitConfigValues();
        foreach (var fieldName in new[] { "_jitEnableRemoveEmptyTry", "_jitEnableRemoveEmptyTryCatchOrTryFault" })
        {
            var field = typeof(JitConfigValues).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertionException($"Missing configuration {fieldName}.");
            field.SetValue(config, 1);
        }

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

    private static void Clause(Compiler compiler, EHHandlerType kind,
        BasicBlock firstTry, BasicBlock lastTry, BasicBlock firstHandler, BasicBlock lastHandler,
        BasicBlock? filter = null)
    {
        compiler.compHndBBtab =
        [
            new EHblkDsc
            {
                ebdHandlerType = kind,
                ebdTryBeg = firstTry,
                ebdTryLast = lastTry,
                ebdHndBeg = firstHandler,
                ebdHndLast = lastHandler,
                ebdFilter = filter,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            },
        ];
        compiler.compHndBBtabCount = 1;
    }
}
