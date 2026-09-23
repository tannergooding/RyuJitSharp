// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineInvocationTests
{
    private static bool s_executeCallback;
    private static int s_traps;

    [TestCase("locals", InlineObservation.CALLSITE_TOO_MANY_LOCALS)]
    [TestCase("virtual", InlineObservation.CALLSITE_IS_VIRTUAL)]
    [TestCase("tail", InlineObservation.CALLSITE_IMPLICIT_REC_TAIL_CALL)]
    [TestCase("continuation", InlineObservation.CALLER_ASYNC_USED_CONTINUATION)]
    public static void RejectsUnsupportedSitesBeforeInvokingTheEE(string reason, InlineObservation expected)
    {
        WithCompiler((compiler, call, result) => {
            switch (reason)
            {
                case "locals":
                    compiler.lvaCount = 512;
                    break;
                case "virtual":
                    call.Flags |= GTF_CALL_VIRT_VTABLE;
                    break;
                case "tail":
                    call._callMethHnd = compiler.info.compMethodHnd;
                    call._callMoreFlags |= GTF_CALL_M_IMPLICIT_TAILCALL;
                    break;
                case "continuation":
                    call.SetIsAsync(new AsyncCallInfo());
                    compiler.info.compUsesAsyncContinuation = true;
                    break;
            }

            compiler.fgMorphCallInlineHelper(call, result, out var context);

            Assert.That(result.Observation, Is.EqualTo(expected));
            Assert.That(result.IsFailure, Is.True);
            Assert.That(context, Is.Null);
            Assert.That(s_traps, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FailedInvocationPreservesItsReasonAndRollsBackTemps(bool nullReceiver)
    {
        WithCompiler((compiler, call, result) => {
            s_executeCallback = nullReceiver;
            if (nullReceiver)
            {
                var candidate = call.SingleInlineCandidateInfo ?? throw new InvalidOperationException();
                candidate.methInfo.args.callConv = CorInfoCallConv.CORINFO_CALLCONV_HASTHIS;
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewNull()).WithWellKnownArg(WellKnownArg.ThisPointer));
            }

            compiler.fgMorphCallInlineHelper(call, result, out var context);

            Assert.Multiple(() => {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Observation, Is.EqualTo(nullReceiver
                    ? InlineObservation.CALLSITE_ARG_HAS_NULL_THIS : InlineObservation.CALLSITE_COMPILATION_ERROR));
                Assert.That(context, Is.Null);
                Assert.That(s_traps, Is.EqualTo(1));
                Assert.That(compiler.lvaCount, Is.EqualTo(1));
                Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(TYP_INT));
                Assert.That(compiler.fgBBNumMax, Is.EqualTo(7));
            });
            if (!nullReceiver)
            {
                for (var i = 1; i < 4; i++)
                {
                    Assert.That(compiler.lvaTable[i].Type, Is.EqualTo(TYP_UNDEF));
                    Assert.That(compiler.lvaTable[i].lvHasLdAddrOp, Is.False);
                }
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FailedInlineRestoresCallThroughReturnPlaceholder(bool returnsValue, bool guardedOnly)
    {
        WithCompiler((compiler, call, result) => {
            var candidate = call.SingleInlineCandidateInfo ?? throw new InvalidOperationException();
            var statement = compiler.compCurBB?.FirstStmt ?? throw new InvalidOperationException();
            call.Type = call._returnType = returnsValue ? TYP_INT : TYP_VOID;
            call.IsNoReturn = true;
            var placeholder = new GenTreeRetExpr(TYP_INT, call);
            candidate.retExpr = placeholder;
            if (guardedOnly)
            {
                call.IsGuardedDevirtualizationCandidate = true;
                call.Flags &= ~GTF_CALL_INLINE_CANDIDATE;
            }
            else
            {
                call.Flags |= GTF_CALL_VIRT_VTABLE;
            }

            compiler.fgMorphCallInline(call, result);

            Assert.Multiple(() => {
                Assert.That(call.IsInlineCandidate, Is.False);
                Assert.That(compiler.optNoReturnCallCount, Is.EqualTo(1));
                Assert.That(s_traps, Is.Zero);
                Assert.That(statement.RootNode.Oper, Is.EqualTo(returnsValue ? GT_NOP : GT_CALL));
                Assert.That(placeholder.SubstExpr, Is.SameAs(returnsValue ? call : null));
                Assert.That(placeholder.SubstBB, Is.SameAs(returnsValue ? compiler.compCurBB : null));
#if DEBUG
                Assert.That(call.WasInlineCandidate, Is.EqualTo(!guardedOnly));
                var failedContext = candidate.inlinersContext?.Child;
                Assert.That(failedContext is not null, Is.EqualTo(!guardedOnly));
                if (failedContext is not null)
                {
                    Assert.That(failedContext.IsSuccess, Is.False);
                    Assert.That(failedContext.Observation, Is.EqualTo(InlineObservation.CALLSITE_IS_VIRTUAL));
                }
#endif
            });
        });
    }

    private static void WithCompiler(Action<Compiler, GenTreeCall, InlineResult> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        object config = previousConfig;
        SetField(typeof(JitConfigValues), config, "_jitMaxLocalsToTrack", 512);
#if DEBUG
        SetField(typeof(JitConfigValues), config, "_jitInlineLimit", -1);
#endif
        JitConfig = (JitConfigValues)config;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        SetField(typeof(InlineStrategy), strategy, "_maxInlineDepth", 3);
        compiler._inlineStrategy = strategy;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaCount = 1;
        compiler.fgBBNumMax = 7;
        compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x100;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compFlags = CLFLG_INLINING;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.haveSameMethodDefinition =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, CORINFO_METHOD_STRUCT_*, byte>)&HaveSameDefinition;
        vtable.Base.Base.runWithErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&RunWithErrorTrap;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        JitTls.Compiler = compiler;
        s_executeCallback = false;
        s_traps = 0;
        try
        {
            var parent = new InlineContext(strategy) { _callee = (CORINFO_METHOD_STRUCT_*)0x100 };
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call._callMethHnd = (CORINFO_METHOD_STRUCT_*)0x200;
            call._inlineContext = parent;
            call.SingleInlineCandidateInfo = new InlineCandidateInfo { inlinersContext = parent };
            compiler.compCurBB = new BasicBlock(null, null) { bbNum = 1 };
            var statement = compiler.gtNewStmt(call);
            var morphStatement = typeof(Compiler).GetField("fgMorphStmt", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing current morph statement.");
            morphStatement.SetValue(compiler, statement);
            compiler.fgInsertStmtAtEnd(compiler.compCurBB, statement);
            var result = new InlineResult(compiler, call, null, "inline invocation", doNotReport: true);
            result.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
            result.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 1);
            action(compiler, call, result);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }

    private static void SetField([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object instance, string name, int value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing {name}.");
        field.SetValue(instance, value);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte HaveSameDefinition(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* first, CORINFO_METHOD_STRUCT_* second)
        => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithErrorTrap(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        s_traps++;
        if (s_executeCallback)
        {
            callback(state);
            return 1;
        }

        if (JitTls.Compiler is Compiler compiler)
        {
            compiler.fgBBNumMax = 99;
            compiler.lvaCount = 4;
            for (var i = 1; i < 4; i++)
            {
                compiler.lvaTable[i].Type = TYP_REF;
                compiler.lvaTable[i].lvHasLdAddrOp = true;
            }
        }

        return 0;
    }
}
