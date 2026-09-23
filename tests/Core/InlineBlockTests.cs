// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class InlineBlockTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void SingleReturnSplicesStatementsWithoutAddingBlocks(bool empty, bool suffix)
    {
        WithCompiler((compiler, inlinee, info) => {
            var top = info.iciBlock ?? throw new InvalidOperationException();
            var call = info.iciStmt ?? throw new InvalidOperationException();
            var body = NewBlock(inlinee, BBJ_RETURN);
            body.SetFlags(BBF_GC_SAFE_POINT);
            var first = inlinee.gtNewStmt(inlinee.gtNewIconNode(TYP_INT, 1));
            var last = inlinee.gtNewStmt(inlinee.gtNewIconNode(TYP_INT, 2));

            if (!empty)
            {
                inlinee.fgInsertStmtAtEnd(body, first);
                inlinee.fgInsertStmtAtEnd(body, last);
            }

            var after = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 3));

            if (suffix)
            {
                compiler.fgInsertStmtAtEnd(top, after);
            }

            compiler.fgInsertInlineeBlocks(info);

            Assert.Multiple(() => {
                Assert.That(call.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(top.FirstStmt, Is.SameAs(call));
                Assert.That(call.NextStmt, Is.SameAs(empty ? (suffix ? after : null) : first));
                Assert.That(call.PrevStmt, Is.SameAs(suffix ? after : (empty ? call : last)));
                Assert.That(top.Next, Is.Null);
                Assert.That(compiler.fgLastBB, Is.SameAs(top));
                Assert.That(compiler.fgBBcount, Is.EqualTo(1));
                Assert.That(top.HasFlag(BBF_GC_SAFE_POINT), Is.True);
                Assert.That(info.inlineContext.Ordinal, Is.EqualTo(1));
                Assert.That(compiler._inlineStrategy?.InlineCount, Is.EqualTo(1));
            });

            if (!empty)
            {
                Assert.That(first.PrevStmt, Is.SameAs(call));
                Assert.That(first.NextStmt, Is.SameAs(last));
                Assert.That(last.PrevStmt, Is.SameAs(first));
                Assert.That(last.NextStmt, Is.SameAs(suffix ? after : null));
            }

            if (suffix)
            {
                Assert.That(after.PrevStmt, Is.SameAs(empty ? call : last));
                Assert.That(after.NextStmt, Is.Null);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BlockInsertionRedirectsEdgesAndPreservesCallerSuffix(bool noReturn)
    {
        WithCompiler((compiler, inlinee, info) => {
            var top = info.iciBlock ?? throw new InvalidOperationException();
            var call = info.iciStmt ?? throw new InvalidOperationException();
            var suffix = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 3));
            compiler.fgInsertStmtAtEnd(top, suffix);
            top.SetFlags(BBF_DONT_REMOVE | BBF_BACKWARD_JUMP);
            top.bbWeight = 10;
            var first = NewBlock(inlinee, noReturn ? BBJ_THROW : BBJ_ALWAYS);
            var last = first;

            if (!noReturn)
            {
                last = NewBlock(inlinee, BBJ_RETURN);
                first.SetKindAndTargetEdge(BBJ_ALWAYS, inlinee.fgAddRefPred(last, first));
            }

            inlinee.fgReturnCount = noReturn ? 0 : 1;

            compiler.fgInsertInlineeBlocks(info);
            var bottom = compiler.fgLastBB ?? throw new InvalidOperationException();

            Assert.Multiple(() => {
                Assert.That(top.Kind, Is.EqualTo(BBJ_ALWAYS));
                Assert.That(top.Target, Is.SameAs(first));
                Assert.That(top.Next, Is.SameAs(first));
                Assert.That(first.Prev, Is.SameAs(top));
                Assert.That(first.bbRefs, Is.EqualTo(1));
                Assert.That(first.bbPreds, Is.SameAs(top.TargetEdge));
                Assert.That(first.bbNum, Is.EqualTo(3));
                Assert.That(first.HasFlag(BBF_BACKWARD_JUMP), Is.True);
                Assert.That(first.bbCodeOffs, Is.EqualTo(5));
                Assert.That(first.bbCodeOffsEnd, Is.EqualTo(6));
                Assert.That(last.Next, Is.SameAs(bottom));
                Assert.That(bottom.Prev, Is.SameAs(last));
                Assert.That(bottom.Kind, Is.EqualTo(BBJ_RETURN));
                Assert.That(bottom.HasFlag(BBF_DONT_REMOVE), Is.False);
                Assert.That(bottom.FirstStmt, Is.SameAs(suffix));
                Assert.That(suffix.PrevStmt, Is.SameAs(suffix));
                Assert.That(call.NextStmt, Is.Null);
                Assert.That(call.PrevStmt, Is.SameAs(call));
                Assert.That(call.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(bottom.bbRefs, Is.EqualTo(noReturn ? 0 : 1));
                Assert.That(compiler.fgBBcount, Is.EqualTo(noReturn ? 3 : 4));
                Assert.That(compiler.fgBBNumMax, Is.EqualTo(noReturn ? 3 : 4));
                Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!noReturn));
                Assert.That(compiler.Metrics.ProfileInconsistentNoReturnInlinee, Is.EqualTo(noReturn ? 1 : 0));
            });

            if (!noReturn)
            {
                Assert.That(last.Kind, Is.EqualTo(BBJ_ALWAYS));
                Assert.That(last.Target, Is.SameAs(bottom));
                Assert.That(bottom.bbPreds, Is.SameAs(last.TargetEdge));
                Assert.That(last.bbRefs, Is.EqualTo(1));
            }
        });
    }

    [Test]
    public static void MergesRootFlagsCountersAndProfileConsistency()
    {
        WithCompiler((compiler, inlinee, info) => {
            _ = NewBlock(inlinee, BBJ_RETURN);
            compiler.compLongUsed = true;
            inlinee.compFloatingPointUsed = true;
            inlinee.compLocallocUsed = true;
            inlinee.compLocallocOptimized = true;
            inlinee.compQmarkUsed = true;
            inlinee.compHasBackwardJump = true;
            inlinee.compMaskConvertUsed = true;
            inlinee.lvaGenericsContextInUse = true;
            inlinee.fgHasSwitch = true;
            compiler.opts.compProcedureSplitting = true;
            compiler.info.compUnmanagedCallCountWithGCTransition = 2;
            inlinee.info.compUnmanagedCallCountWithGCTransition = 3;
            compiler.optNoReturnCallCount = 4;
            inlinee.optNoReturnCallCount = 5;
            inlinee.fgPgoFailReason = "no data";
            inlinee.fgPgoConsistent = false;
#if DEBUG
            inlinee.Metrics.ImporterBranchFold = 7;
#endif

            compiler.fgInsertInlineeBlocks(info);

            Assert.Multiple(() => {
                Assert.That(compiler.compLongUsed, Is.True);
                Assert.That(compiler.compFloatingPointUsed, Is.True);
                Assert.That(compiler.compLocallocUsed, Is.True);
                Assert.That(compiler.compLocallocOptimized, Is.True);
                Assert.That(compiler.compQmarkUsed, Is.True);
                Assert.That(compiler.compHasBackwardJump, Is.True);
                Assert.That(compiler.compMaskConvertUsed, Is.True);
                Assert.That(compiler.lvaGenericsContextInUse, Is.True);
                Assert.That(compiler.fgHasSwitch, Is.True);
                Assert.That(compiler.opts.compProcedureSplitting, Is.False);
                Assert.That(compiler.info.compUnmanagedCallCountWithGCTransition, Is.EqualTo(5));
                Assert.That(compiler.optNoReturnCallCount, Is.EqualTo(9));
                Assert.That(compiler.fgPgoInlineeNoPgoSingleBlock, Is.EqualTo(1));
                Assert.That(compiler.fgPgoConsistent, Is.False);
                Assert.That(compiler.Metrics.ProfileInconsistentInlinee, Is.EqualTo(1));
#if DEBUG
                Assert.That(compiler.Metrics.ImporterBranchFold, Is.EqualTo(7));
#endif
            });
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public static void InsertsEHClausesAtTheCallsiteRegion(int region, bool spareCapacity)
    {
        WithCompiler((compiler, inlinee, info) => {
            var top = info.iciBlock ?? throw new InvalidOperationException();
            var rootTry = region == 1 ? top : NewBlock(compiler, BBJ_RETURN);
            var rootHandler = region == 2 ? top : NewBlock(compiler, BBJ_EHFAULTRET);
            rootTry.TryIndex = 0;
            rootHandler.HndIndex = 0;
            compiler.compHndBBtab = new EHblkDsc[spareCapacity ? 4 : 1];
            compiler.compHndBBtabCount = 1;
            compiler.compHndBBtab[0] = new EHblkDsc {
                ebdID = 11,
                ebdTryBeg = rootTry,
                ebdTryLast = rootTry,
                ebdHndBeg = rootHandler,
                ebdHndLast = rootHandler,
                ebdHandlerType = EHHandlerType.EH_HANDLER_FAULT,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            };
            var body = NewBlock(inlinee, BBJ_ALWAYS);
            var handler = NewBlock(inlinee, BBJ_EHFAULTRET);
            var exit = NewBlock(inlinee, BBJ_RETURN);
            body.SetKindAndTargetEdge(BBJ_ALWAYS, inlinee.fgAddRefPred(exit, body));
            body.TryIndex = 0;
            handler.HndIndex = 0;
            inlinee.compHndBBtab = [new EHblkDsc {
                ebdID = 12, ebdTryBeg = body, ebdTryLast = body,
                ebdHndBeg = handler, ebdHndLast = handler,
                ebdHandlerType = EHHandlerType.EH_HANDLER_FAULT,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            inlinee.compHndBBtabCount = 1;

            compiler.fgInsertInlineeBlocks(info);

            var index = region == 0 ? 1 : 0;
            var rootIndex = 1 - index;
            var clause = compiler.compHndBBtab[index];
            Assert.Multiple(() => {
                Assert.That(compiler.compHndBBtabCount, Is.EqualTo(2));
                Assert.That(clause.ebdID, Is.EqualTo(12));
                Assert.That(clause.ebdTryBeg, Is.SameAs(body));
                Assert.That(clause.ebdHndBeg, Is.SameAs(handler));
                Assert.That(clause.ebdEnclosingTryIndex, Is.EqualTo(region == 1 ? rootIndex : EHblkDsc.NO_ENCLOSING_INDEX));
                Assert.That(clause.ebdEnclosingHndIndex, Is.EqualTo(region == 2 ? rootIndex : EHblkDsc.NO_ENCLOSING_INDEX));
                Assert.That(compiler.compHndBBtab[rootIndex].ebdID, Is.EqualTo(11));
                Assert.That(rootTry.TryIndex, Is.EqualTo(rootIndex));
                Assert.That(rootHandler.HndIndex, Is.EqualTo(rootIndex));
                Assert.That(body.TryIndex, Is.EqualTo(index));
                Assert.That(handler.HndIndex, Is.EqualTo(index));
                Assert.That(exit.bbTryIndex, Is.EqualTo(region == 1 ? rootIndex + 1 : 0));
                Assert.That(exit.bbHndIndex, Is.EqualTo(region == 2 ? rootIndex + 1 : 0));
                Assert.That(inlinee.compHndBBtab[0].ebdEnclosingTryIndex, Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
                Assert.That(inlinee.compHndBBtab[0].ebdEnclosingHndIndex, Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
            });
        });
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = compiler.fgFirstBB is null ? 1 : 0;

        if (compiler.fgLastBB is BasicBlock last)
        {
            last.Next = block;
        }
        else
        {
            compiler.fgFirstBB = block;
        }

        compiler.fgLastBB = block;

        return block;
    }

    private static void WithCompiler(Action<Compiler, Compiler, InlineInfo> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.InlineeCompiler = inlinee;
        compiler.compHndBBtab = [];
        inlinee.compHndBBtab = [];
        compiler.fgPredsComputed = inlinee.fgPredsComputed = true;
        compiler.fgPgoConsistent = inlinee.fgPgoConsistent = true;
        compiler.lvaResumedIndicator = inlinee.lvaResumedIndicator = BAD_VAR_NUM;
        inlinee.fgReturnCount = 1;
        JitFlags flags = default;
        compiler.opts.jitFlags = inlinee.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        inlinee.opts.SetMinOpts(false);
        CORINFO_METHOD_INFO methodInfo = default;
        inlinee.info.compMethodInfo = &methodInfo;
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        compiler._inlineStrategy = strategy;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = inlinee.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = inlinee.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;

        try
        {
            var root = new InlineContext(strategy) { _ilSize = 6 };
#if DEBUG
            root._ilInstsSet = new BitArray(6, true);
#endif
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var stmt = compiler.gtNewStmt(call, new DebugInfo(root, new ILLocation(5, 0)));
            var block = NewBlock(compiler, BBJ_RETURN);
            compiler.fgInsertStmtAtEnd(block, stmt);
            var result = new InlineResult(compiler, call, null, "block insertion", doNotReport: true);
            result.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
            result.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 1);
            result.NoteSuccess();
            var info = new InlineInfo {
                InlinerCompiler = compiler,
                InlineRoot = compiler,
                inlineCandidateInfo = new InlineCandidateInfo(),
                inlineContext = new InlineContext(strategy) { _parent = root },
                inlineResult = result,
                iciBlock = block,
                iciStmt = stmt,
                iciCall = call,
            };
            inlinee.impInlineInfo = info;
            action(compiler, inlinee, info);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
