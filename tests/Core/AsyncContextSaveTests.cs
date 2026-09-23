// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncContextSaveTests
{
    private static int s_metadataQueries;
    private static int s_methodQueries;

    [Test]
    public static void ContinuationMembersShareRootStorageAndCompatibleAwaiterLayouts()
    {
        WithCompiler(compiler => {
            var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            inlinee.impInlineInfo = new InlineInfo { InlineRoot = compiler, InlinerCompiler = compiler };
            var exec = ContinuationMember.InlineFrameExecutionContext(1);
            Assert.That(inlinee.TryGetContinuationMemberIndex(exec, out _), Is.False);
            Assert.That(compiler.GetContinuationMemberCount(), Is.Zero);
            Assert.That(inlinee.GetContinuationMemberIndex(exec), Is.Zero);
            Assert.That(compiler.GetContinuationMemberIndex(exec), Is.Zero);
            Assert.That(compiler.GetContinuationMemberIndex(ContinuationMember.InlineFrameContinuationContext(1)), Is.EqualTo(1));
            Assert.That(compiler.GetContinuationMemberIndex(ContinuationMember.InlineFrameFlags(1)), Is.EqualTo(2));
            Assert.That(compiler.GetContinuationMemberIndex(ContinuationMember.InlineFrameExecutionContext(2)), Is.EqualTo(3));
            var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x100, true, 8, TYP_STRUCT, "Awaiter", "Awaiter");
            Assert.That(compiler.GetContinuationMemberIndex(ContinuationMember.CustomAwaiterOfLayout(layout)), Is.EqualTo(4));
            var compatible = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x200, true, 8, TYP_STRUCT, "OtherAwaiter", "OtherAwaiter");
            Assert.That(inlinee.GetContinuationMemberIndex(ContinuationMember.CustomAwaiterOfLayout(compatible)), Is.EqualTo(4));
            Assert.That(compiler.GetContinuationMemberIndex(ContinuationMember.CustomAwaiterOfLayout(new ClassLayout(16))), Is.EqualTo(5));
            Assert.That(inlinee.GetContinuationMemberCount(), Is.EqualTo(6));
            Assert.That(inlinee.TryGetContinuationMemberIndex(ContinuationMember.InlineFrameFlags(1), out var index), Is.True);
            Assert.That(index, Is.EqualTo(2));
            Assert.That(inlinee.TryGetContinuationMemberIndex(ContinuationMember.InlineFrameFlags(2), out _), Is.False);
            Assert.That(inlinee.GetContinuationMemberCount(), Is.EqualTo(6));
            Assert.That(inlinee.GetContinuationMember(0).GetStorageType(out var execLayout), Is.EqualTo(TYP_REF));
            Assert.That(execLayout, Is.Null);
            Assert.That(inlinee.GetContinuationMember(2).GetStorageType(out var flagsLayout), Is.EqualTo(TYP_INT));
            Assert.That(flagsLayout, Is.Null);
            Assert.That(inlinee.GetContinuationMember(3).InlineDepth, Is.EqualTo(2));
            Assert.That(inlinee.GetContinuationMember(4).GetStorageType(out var awaiterLayout), Is.EqualTo(TYP_STRUCT));
            Assert.That(awaiterLayout, Is.SameAs(layout));
            Assert.That(inlinee.GetContinuationMember(4).CustomAwaiterLayout, Is.SameAs(layout));
        });
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_INT)]
    public static void ContinuationMemberLoadsKeepSymbolicOffsetAndObjectHeader(var_types type)
    {
        WithCompiler(compiler => {
            compiler.lvaAsyncContinuationArg = 0;
            compiler.lvaTable[0].Type = TYP_REF;
            var member = type == TYP_REF ? ContinuationMember.InlineFrameExecutionContext(1) : ContinuationMember.InlineFrameFlags(1);
            var indir = compiler.gtNewContinuationMemberIndir(member, type);
            Assert.That(indir.Type, Is.EqualTo(type));
            Assert.That(indir.Flags & GenTreeFlags.GTF_IND_NONFAULTING, Is.EqualTo(GenTreeFlags.GTF_IND_NONFAULTING));
            Assert.That(indir.Flags & GenTreeFlags.GTF_EXCEPT, Is.EqualTo(GenTreeFlags.GTF_EMPTY));
            var address = indir.AsIndir().Op1.AsOp();
            Assert.That(address.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(address.Op1.AsLclVarCommon().LclNum, Is.Zero);
            var offset = address.Op2.AsOp();
            Assert.That(offset.Op1.Oper, Is.EqualTo(GT_CONTINUATION_MEMBER_OFFSET));
            Assert.That(offset.Op1.AsVal().Val1, Is.EqualTo((nint)0));
            var copy = compiler.gtCloneExpr(offset.Op1);
            Assert.That(copy, Is.Not.SameAs(offset.Op1));
            Assert.That(copy.Oper, Is.EqualTo(GT_CONTINUATION_MEMBER_OFFSET));
            Assert.That(copy.AsVal().Val1, Is.EqualTo((nint)0));
            var edgeCount = 0;
            foreach (ref var edge in copy.UseEdges)
            {
                edgeCount++;
            }
            Assert.That(edgeCount, Is.Zero);
            Assert.That(offset.Op2.IsIntegralConst(SIZEOF__CORINFO_Object), Is.True);
            Assert.That(compiler.GetContinuationMember(0).Type, Is.EqualTo(member.Type));
        });
    }

#if DEBUG
    [TestCase(ContinuationMemberType.CustomAwaiterOfLayout, "CustomAwaiter<Awaiter>")]
    [TestCase(ContinuationMemberType.InlineFrameExecutionContext, "ExecutionContext for inline depth 2")]
    [TestCase(ContinuationMemberType.InlineFrameContinuationContext, "Continuation context for inline depth 2")]
    [TestCase(ContinuationMemberType.InlineFrameFlags, "Continuation flags for inline depth 2")]
    public static void ContinuationMemberLeafDumpMatchesNative(ContinuationMemberType type, string expected)
    {
        WithCompiler(compiler => {
            var member = type switch {
                ContinuationMemberType.CustomAwaiterOfLayout => ContinuationMember.CustomAwaiterOfLayout(
                    new ClassLayout((CORINFO_CLASS_STRUCT_*)0x100, true, 8, TYP_STRUCT, "Awaiter", "Awaiter")),
                ContinuationMemberType.InlineFrameExecutionContext => ContinuationMember.InlineFrameExecutionContext(2),
                ContinuationMemberType.InlineFrameContinuationContext => ContinuationMember.InlineFrameContinuationContext(2),
                _ => ContinuationMember.InlineFrameFlags(2),
            };
            var index = compiler.GetContinuationMemberIndex(member);
            var node = new GenTreeVal(GT_CONTINUATION_MEMBER_OFFSET, TYP_I_IMPL, index);
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previousWriter = Globals.s_jitstdout;
            try
            {
                Globals.s_jitstdout = writer;
                IndentStack indent = default;
                compiler.gtDispLeaf(node, ref indent);
                writer.Flush();
            }
            finally
            {
                Globals.s_jitstdout = previousWriter;
            }
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo($" index=0 {expected}"));
        });
    }
#endif

    [Test]
    public static void FrameTransitionAwaitHasOnlyItsContinuationPseudoArgument()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 5)));
            compiler.fgSetupAsyncFrameTransitionCall(call, default);
            Assert.That(call.IsAsync, Is.True);
            Assert.That(call.GetAsyncInfo().ContinuationContextHandling, Is.EqualTo(ContinuationContextHandling.None));
            var kinds = new List<WellKnownArg>();
            foreach (var arg in call.Args.Args)
            {
                kinds.Add(arg.WellKnownArg);
            }
            WellKnownArg[] expected = Target.TgtArgOrder == Target.ARG_ORDER_R2L
                ? [WellKnownArg.AsyncContinuation, WellKnownArg.None] : [WellKnownArg.None, WellKnownArg.AsyncContinuation];
            Assert.That(kinds, Is.EqualTo(expected));
            var continuation = call.Args.FindWellKnownArg(WellKnownArg.AsyncContinuation)
                ?? throw new InvalidOperationException("Missing continuation.");
            Assert.That(continuation.Node.IsIntegralConst(0), Is.True);
        });
    }

    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    public static void InlinedFrameRestoresCallerContextsOnlyOnResumption(bool faultHandler, int depth)
    {
        WithCompiler(compiler => {
            var previousConfig = Globals.JitConfig;
            object config = previousConfig;
            var configField = typeof(JitConfigValues).GetField("_jitAsyncInlining", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing async inlining setting.");
            configField.SetValue(config, 1);
            Globals.JitConfig = (JitConfigValues)config;
            try
            {
                compiler.lvaTable = [
                    new LclVarDsc { Type = TYP_I_IMPL },
                    new LclVarDsc { Type = TYP_REF },
                    new LclVarDsc { Type = TYP_I_IMPL },
                ];
                compiler.lvaCount = 3;
                compiler.lvaAsyncContinuationArg = 1;
                var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                inlinee.lvaResumedIndicator = 2;
                inlinee.compAsyncBodyMaySuspend = true;
                inlinee.asyncContextRestoreEHID = faultHandler ? (ushort)7 : ushort.MaxValue;
                compiler.InlineeCompiler = inlinee;
                var join = NewBlock(compiler, BBJ_RETURN, 0);
                join.setBBProfileWeight(10);
                compiler.fgFirstBB = join;
                compiler.fgLastBB = join;
                compiler.fgReturnCount = 1;
                var originalReturn = AddReturn(compiler, join, TYP_VOID, 0);
                BasicBlock? handler = null;
                if (faultHandler)
                {
                    handler = NewBlock(compiler, BBJ_EHFAULTRET, 10);
                    join.Next = handler;
                    compiler.fgLastBB = handler;
                    join.TryIndex = 0;
                    handler.HndIndex = 0;
                    compiler.compHndBBtab = [new EHblkDsc {
                        ebdID = 7, ebdTryBeg = join, ebdTryLast = join, ebdHndBeg = handler, ebdHndLast = handler,
                        ebdHandlerType = EHHandlerType.EH_HANDLER_FAULT,
                        ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    }];
                    compiler.compHndBBtabCount = 1;
                }
                var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclVarAddrNode(TYP_BYREF, 0)).WithWellKnownArg(WellKnownArg.AsyncResumedDef));
                for (var i = 0; i < depth; i++)
                {
                    _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_I_IMPL, 0)).WithWellKnownArg(WellKnownArg.AsyncResumedUse));
                }
                var info = new InlineInfo { iciCall = call, iciStmt = compiler.gtNewStmt(call) };

                compiler.fgInlineAppendAsyncFrameStatements(info, join);

                Assert.That(join.Kind, Is.EqualTo(BBJ_COND));
                var restore = join.FalseTarget;
                var rest = join.TrueTarget;
                Assert.That(join.TrueEdge.Likelihood, Is.EqualTo(1.0));
                Assert.That(join.FalseEdge.Likelihood, Is.EqualTo(0.0));
                Assert.That(restore.bbWeight, Is.Zero);
                Assert.That(rest.bbWeight, Is.EqualTo(10));
                Assert.That(restore.Target, Is.SameAs(rest));
                Assert.That(rest.FirstStmt, Is.SameAs(originalReturn));
                Assert.That(rest.bbRefs, Is.EqualTo(2));
                var restoreStatements = Statements(restore);
                Assert.That(restoreStatements.Count, Is.EqualTo(2));
                var restoreCall = restoreStatements[0].RootNode.AsCall();
                Assert.That((nuint)restoreCall._callMethHnd, Is.EqualTo((nuint)4));
                Assert.That(restoreCall.IsAsync, Is.True);
                Assert.That(restoreCall.GetAsyncInfo().ContinuationContextHandling, Is.EqualTo(ContinuationContextHandling.None));
                var memberIndex = 0;
                foreach (var arg in restoreCall.Args.Args)
                {
                    if (arg.IsUserArg)
                    {
                        var offset = arg.Node.AsIndir().Op1.AsOp().Op2.AsOp().Op1.AsVal();
                        Assert.That(offset.Val1, Is.EqualTo((nint)memberIndex++));
                    }
                    else
                    {
                        Assert.That(arg.WellKnownArg, Is.EqualTo(WellKnownArg.AsyncContinuation));
                    }
                }
                Assert.That(memberIndex, Is.EqualTo(3));
                var callerResumed = restoreStatements[1].RootNode.AsLclVar();
                Assert.That(callerResumed.LclNum, Is.Zero);
                Assert.That(callerResumed.Data.IsIntegralConst(1), Is.True);
                Assert.That(compiler.GetContinuationMemberCount(), Is.EqualTo(3));
                for (var i = 0; i < 3; i++)
                {
                    Assert.That(compiler.GetContinuationMember(i).InlineDepth, Is.EqualTo(depth));
                }
                if (handler is not null)
                {
                    var propagate = Statements(handler)[0].RootNode.AsLclVar();
                    Assert.That(propagate.LclNum, Is.Zero);
                    var merged = propagate.Data.AsOp();
                    Assert.That(merged.Oper, Is.EqualTo(GT_OR));
                    Assert.That(merged.Op1.AsLclVarCommon().LclNum, Is.Zero);
                    Assert.That(merged.Op2.AsLclVarCommon().LclNum, Is.EqualTo(2));
                    Assert.That(restore.TryIndex, Is.Zero);
                    Assert.That(rest.TryIndex, Is.Zero);
                }
            }
            finally
            {
                Globals.JitConfig = previousConfig;
            }
        });
    }

    [TestCase("disabled")]
    [TestCase("inherited")]
    [TestCase("synchronous")]
    [TestCase("no-caller")]
    public static void FrameTransitionExclusionsLeaveTheGraphUntouched(string reason)
    {
        WithCompiler(compiler => {
            var previousConfig = Globals.JitConfig;
            object config = previousConfig;
            var configField = typeof(JitConfigValues).GetField("_jitAsyncInlining", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing async inlining setting.");
            configField.SetValue(config, reason == "disabled" ? 0 : 1);
            Globals.JitConfig = (JitConfigValues)config;
            try
            {
                var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                inlinee.lvaResumedIndicator = reason == "inherited" ? BAD_VAR_NUM : 0;
                inlinee.compAsyncBodyMaySuspend = reason != "synchronous";
                compiler.InlineeCompiler = inlinee;
                var join = NewBlock(compiler, BBJ_RETURN, 0);
                compiler.fgFirstBB = join;
                compiler.fgLastBB = join;
                var originalReturn = AddReturn(compiler, join, TYP_VOID, 0);
                var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
                if (reason != "no-caller")
                {
                    _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclVarAddrNode(TYP_BYREF, 0)).WithWellKnownArg(WellKnownArg.AsyncResumedDef));
                    _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_I_IMPL, 0)).WithWellKnownArg(WellKnownArg.AsyncResumedUse));
                }
                var info = new InlineInfo { iciCall = call, iciStmt = compiler.gtNewStmt(call) };
                compiler.fgInlineAppendAsyncFrameStatements(info, join);
                Assert.That(join.Kind, Is.EqualTo(BBJ_RETURN));
                Assert.That(join.FirstStmt, Is.SameAs(originalReturn));
                Assert.That(join.Next, Is.Null);
                Assert.That(compiler.GetContinuationMemberCount(), Is.Zero);
                Assert.That(s_metadataQueries, Is.Zero);
            }
            finally
            {
                Globals.JitConfig = previousConfig;
            }
        });
    }

    [TestCase(TYP_VOID, false, false, false)]
    [TestCase(TYP_INT, false, false, false)]
    [TestCase(TYP_BYREF, false, false, false)]
    [TestCase(TYP_VOID, false, true, false)]
    [TestCase(TYP_INT, false, true, true)]
    [TestCase(TYP_INT, true, false, false)]
    [TestCase(TYP_VOID, true, true, true)]
    public static void SavesContextsAndMergesReturns(var_types returnType, bool osr, bool initMem, bool loop)
    {
        WithCompiler(compiler => {
            compiler.info.compRetType = returnType == TYP_BYREF ? TYP_STRUCT : returnType;
            compiler.info.compRetBuffArg = returnType == TYP_BYREF ? 0 : BAD_VAR_NUM;
            compiler.info.compInitMem = initMem;
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            var entry = NewBlock(compiler, BBJ_COND, 0);
            var left = NewBlock(compiler, BBJ_RETURN, 10);
            var right = NewBlock(compiler, BBJ_RETURN, 20);
            entry.Next = left;
            left.Next = right;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = right;
            compiler.compCurBB = right;
            compiler.fgReturnCount = 2;
            entry.SetCond(compiler.fgAddRefPred(left, entry), compiler.fgAddRefPred(right, entry));
            entry.TrueEdge.Likelihood = 0.3;
            entry.FalseEdge.Likelihood = 0.7;
            left.setBBProfileWeight(3);
            right.setBBProfileWeight(7);
            if (loop)
            {
                entry.SetFlags(BBF_BACKWARD_JUMP);
            }
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)3);
            call.SetIsAsync(default);
            compiler.fgInsertStmtAtEnd(entry, compiler.gtNewStmt(call));
            compiler.fgInsertStmtAtEnd(entry, compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_EQ, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 0)))));
            var leftReturn = AddReturn(compiler, left, returnType, 11);
            var rightReturn = AddReturn(compiler, right, returnType, 22);

            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var descriptor = compiler.compHndBBtab[0];
            var fault = descriptor.ebdHndBeg;
            var merged = compiler.fgLastBB ?? throw new InvalidOperationException("Missing merged return.");
            Assert.Multiple(() => {
                Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
                Assert.That(descriptor.HasFaultHandler, Is.True);
                Assert.That(descriptor.ebdID, Is.EqualTo(7));
                Assert.That(compiler.compEHID, Is.EqualTo(8));
                Assert.That(compiler.ehIsAsyncContextRestore(descriptor.ebdID), Is.True);
                Assert.That(compiler.ehIsInsideNonAsyncContextRestoreRegion(left), Is.False);
                Assert.That(descriptor.ebdTryBeg, Is.SameAs(entry.Next));
                Assert.That(descriptor.ebdTryLast, Is.SameAs(right));
                Assert.That(descriptor.ebdHndLast, Is.SameAs(fault));
                Assert.That(descriptor.ebdTryBegOffs, Is.EqualTo(0));
                Assert.That(descriptor.ebdTryEndOffs, Is.EqualTo(30));
                Assert.That(descriptor.ebdEnclosingTryIndex, Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
                Assert.That(entry.hasTryIndex, Is.False);
                Assert.That(left.TryIndex, Is.Zero);
                Assert.That(right.TryIndex, Is.Zero);
                Assert.That(fault.Kind, Is.EqualTo(BBJ_EHFAULTRET));
                Assert.That(fault.bbRefs, Is.EqualTo(1));
                Assert.That(fault.bbWeight, Is.Zero);
                Assert.That(fault.HndIndex, Is.Zero);
                Assert.That(fault.hasTryIndex, Is.False);
                Assert.That(merged.Kind, Is.EqualTo(BBJ_RETURN));
                Assert.That(merged.hasTryIndex, Is.False);
                Assert.That(merged.hasHndIndex, Is.False);
                Assert.That(merged.bbRefs, Is.EqualTo(2));
                Assert.That(merged.bbWeight, Is.EqualTo(10));
                Assert.That(left.Target, Is.SameAs(merged));
                Assert.That(right.Target, Is.SameAs(merged));
                Assert.That(compiler.fgReturnCount, Is.EqualTo(1));
                Assert.That(compiler.compAsyncBodyMaySuspend, Is.True);
                Assert.That(leftReturn.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(rightReturn.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(s_metadataQueries, Is.EqualTo(1));
                Assert.That(s_methodQueries, Is.EqualTo(osr ? 1 : 2));
            });

            var initialize = !osr && (loop || !initMem);
            var entryStatements = Statements(entry);
            Assert.That(entryStatements.Count, Is.EqualTo(osr ? 0 : initialize ? 2 : 1));
            if (!osr)
            {
                var capture = entryStatements[^1].RootNode.AsCall();
                Assert.That((nint)capture._callMethHnd, Is.EqualTo((nint)1));
                Assert.That(ArgumentLocals(capture), Is.EqualTo([
                    compiler.lvaAsyncThreadObjectVar, compiler.lvaAsyncExecutionContextVar, compiler.lvaAsyncSynchronizationContextVar
                ]));
                if (initialize)
                {
                    var store = entryStatements[0].RootNode.AsLclVar();
                    Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    Assert.That(store.LclNum, Is.EqualTo(compiler.lvaResumedIndicator));
                    Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
                }
            }

            foreach (var local in new[] {
                compiler.lvaResumedIndicator, compiler.lvaAsyncThreadObjectVar,
                compiler.lvaAsyncExecutionContextVar, compiler.lvaAsyncSynchronizationContextVar
            })
            {
                Assert.That(compiler.lvaTable[local].lvOnlyUsedOnSynchronousPath, Is.True);
                Assert.That(compiler.lvaTable[local].lvIsOSRLocal, Is.EqualTo(osr));
                Assert.That(compiler.lvaTable[local].Type, Is.EqualTo(local == compiler.lvaResumedIndicator ? TYP_I_IMPL : TYP_REF));
                Assert.That(compiler.lvaTable[local].lvHasLdAddrOp, Is.EqualTo(!osr || local == compiler.lvaResumedIndicator));
            }
            AssertRestore(compiler, Statements(fault)[0].RootNode.AsCall());
            AssertRestore(compiler, Statements(merged)[0].RootNode.AsCall());
            var args = new List<WellKnownArg>();
            foreach (var arg in call.Args.Args)
            {
                args.Add(arg.WellKnownArg);
            }
            Assert.That(args, Is.EqualTo([
                WellKnownArg.AsyncResumedDef, WellKnownArg.AsyncResumedUse,
                WellKnownArg.AsyncExecutionContext, WellKnownArg.AsyncSynchronizationContext
            ]));
            var finalReturn = Statements(merged)[^1].RootNode.AsUnOp();
            Assert.That(finalReturn.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(finalReturn.Type, Is.EqualTo(returnType));
            if (returnType != TYP_VOID)
            {
                var local = finalReturn.Op1.AsLclVar().LclNum;
                Assert.That(Statements(left)[^1].RootNode.AsLclVar().LclNum, Is.EqualTo(local));
                Assert.That(Statements(right)[^1].RootNode.AsLclVar().LclNum, Is.EqualTo(local));
                Assert.That(compiler.lvaTable[local].Type, Is.EqualTo(returnType));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DoesNotCreateAReturnForNonReturningBodies(bool osr)
    {
        WithCompiler(compiler => {
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            var entry = NewBlock(compiler, BBJ_ALWAYS, 0);
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(entry, entry));
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;

            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgReturnCount, Is.Zero);
            Assert.That(compiler.fgLastBB?.Kind, Is.EqualTo(BBJ_EHFAULTRET));
            Assert.That(compiler.compAsyncBodyMaySuspend, Is.False);
            Assert.That(compiler.lvaCount, Is.EqualTo(5));
            Assert.That(s_methodQueries, Is.EqualTo(osr ? 0 : 1));
        });
    }

    [Test]
    public static void LeavesMethodsWithoutSaveContextsUnchanged()
    {
        WithCompiler(compiler => {
            compiler.info.compMethodInfo->options = 0;
            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(compiler.compEHID, Is.EqualTo(7));
            Assert.That(s_metadataQueries, Is.Zero);
            Assert.That(s_methodQueries, Is.Zero);
        });
    }

    [Test]
    public static void EnclosesExistingClausesWithoutReplacingTheirIDsOrInnermostIndices()
    {
        WithCompiler(compiler => {
            var entry = NewBlock(compiler, BBJ_ALWAYS, 0);
            var body = NewBlock(compiler, BBJ_THROW, 10);
            var handler = NewBlock(compiler, BBJ_THROW, 20);
            entry.Next = body;
            body.Next = handler;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(body, entry));
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = handler;
            body.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [new EHblkDsc {
                ebdID = 3, ebdTryBeg = body, ebdTryLast = body, ebdHndBeg = handler, ebdHndLast = handler,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
            }];
            compiler.compHndBBtabCount = 1;

            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[0].ebdID, Is.EqualTo(3));
            Assert.That(compiler.compHndBBtab[0].ebdEnclosingTryIndex, Is.EqualTo(1));
            Assert.That(body.TryIndex, Is.Zero);
            Assert.That(handler.TryIndex, Is.EqualTo(1));
            Assert.That(handler.HndIndex, Is.Zero);
            Assert.That(compiler.ehIsAsyncContextRestore(3), Is.False);
            Assert.That(compiler.ehIsInsideNonAsyncContextRestoreRegion(body), Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BeginningInsertionPreservesPhiAndCatchPrefixes(bool hasBody, bool insertPhi)
    {
        WithCompiler(compiler => {
            var block = NewBlock(compiler, BBJ_RETURN, 0);
            var phi = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_BYREF)));
            var catchStore = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, new GenTree(GT_CATCH_ARG, TYP_REF)));
            compiler.fgInsertStmtAtBeg(block, phi);
            compiler.fgInsertStmtAtEnd(block, catchStore);
            var body = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
            if (hasBody)
            {
                compiler.fgInsertStmtAtEnd(block, body);
            }
            var inserted = compiler.gtNewStmt(insertPhi
                ? compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_BYREF))
                : compiler.gtNewIconNode(TYP_INT, 2));

            compiler.fgInsertStmtAtBeg(block, inserted);
            var expected = insertPhi ? new List<Statement> { inserted, phi, catchStore } : [phi, catchStore, inserted];
            if (hasBody)
            {
                expected.Add(body);
            }
            Assert.That(Statements(block), Is.EqualTo(expected));
            Assert.That(block.FirstStmt?.PrevStmt, Is.SameAs(expected[^1]));
            Assert.That(expected[^1].NextStmt, Is.Null);
            for (var i = 1; i < expected.Count; i++)
            {
                Assert.That(expected[i].PrevStmt, Is.SameAs(expected[i - 1]));
            }
        });
    }

    private static Statement AddReturn(Compiler compiler, BasicBlock block, var_types type, int value)
    {
        GenTree? operand = type == TYP_VOID ? null : type == TYP_BYREF
            ? compiler.gtNewLclVarNode(TYP_BYREF, 0) : compiler.gtNewIconNode(type, value);
        var stmt = compiler.gtNewStmt(new GenTreeUnOp(GT_RETURN, type, operand));
        compiler.fgInsertStmtAtEnd(block, stmt);
        return stmt;
    }

    private static void AssertRestore(Compiler compiler, GenTreeCall call)
    {
        Assert.That((nint)call._callMethHnd, Is.EqualTo((nint)2));
        Assert.That(ArgumentLocals(call), Is.EqualTo([
            compiler.lvaResumedIndicator, compiler.lvaAsyncThreadObjectVar,
            compiler.lvaAsyncExecutionContextVar, compiler.lvaAsyncSynchronizationContextVar
        ]));
        var resumed = call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException("Missing resumed argument.");
        Assert.That(resumed.Node.Type, Is.EqualTo(TYP_INT));
    }

    private static List<int> ArgumentLocals(GenTreeCall call)
    {
        var result = new List<int>();
        foreach (var arg in call.Args.Args)
        {
            result.Add(arg.Node.AsLclVarCommon().LclNum);
        }
        return result;
    }

    private static List<Statement> Statements(BasicBlock block)
    {
        var result = new List<Statement>();
        foreach (var stmt in block.Statements)
        {
            result.Add(stmt);
        }
        return result;
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind, int offset)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbCodeOffs = offset;
        block.bbCodeOffsEnd = offset + 10;
        return block;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetAsyncInfo(ICorJitInfo* jitInfo, CORINFO_ASYNC_INFO* result)
    {
        s_metadataQueries++;
        *result = default;
        result->captureContextsMethHnd = (CORINFO_METHOD_STRUCT_*)1;
        result->restoreContextsMethHnd = (CORINFO_METHOD_STRUCT_*)2;
        result->restoreInlinedFrameContextsMethHnd = (CORINFO_METHOD_STRUCT_*)4;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetMethodFlags(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
    {
        s_methodQueries++;
        return CorInfoFlag.CORINFO_FLG_STATIC;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        CORINFO_METHOD_INFO methodInfo = default;
        methodInfo.options = CorInfoOptions.CORINFO_ASYNC_SAVE_CONTEXTS;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getAsyncInfo = &GetAsyncInfo;
        vtable.Base.Base.getMethodAttribs = &GetMethodFlags;
        var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMethodInfo = &methodInfo, compRetBuffArg = BAD_VAR_NUM, compRetType = TYP_VOID };
        compiler.lvaTable = [new LclVarDsc { Type = TYP_BYREF }];
        compiler.lvaCount = 1;
        compiler.compHndBBtab = [];
        compiler.compEHID = 7;
        compiler.fgPredsComputed = true;
        compiler.compInlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        s_metadataQueries = 0;
        s_methodQueries = 0;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
