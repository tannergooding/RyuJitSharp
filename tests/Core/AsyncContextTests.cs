// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncContextTests
{
    private static int s_metadataQueries;

    [Test]
    public static void AsyncMetadataIsCachedPerCompilerAndReturnedByReference()
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getAsyncInfo = &GetAsyncInfo;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            s_metadataQueries = 0;

            ref var first = ref compiler.eeGetAsyncInfo();
            Assert.That(s_metadataQueries, Is.EqualTo(1));
            Assert.That((nint)first.captureContextsMethHnd, Is.EqualTo((nint)1));
            first.restoreContextsMethHnd = (CORINFO_METHOD_STRUCT_*)123;
            ref var second = ref compiler.eeGetAsyncInfo();
            Assert.That(Unsafe.AreSame(ref first, ref second), Is.True);
            Assert.That((nint)second.restoreContextsMethHnd, Is.EqualTo((nint)123));
            Assert.That(s_metadataQueries, Is.EqualTo(1));
            Assert.That(compiler.asyncInfoInitialized, Is.True);

            var other = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            other.info = new Compiler.Info { compCompHnd = &jitInfo };
            Assert.That((nint)other.eeGetAsyncInfo().captureContextsMethHnd, Is.EqualTo((nint)2));
            Assert.That(s_metadataQueries, Is.EqualTo(2));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RootCallsReceiveOwnContextsEvenWhenNested(bool nested)
    {
        WithCompiler(compiler => {
            var call = NewAsyncCall(compiler);
            var userArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(var_types.TYP_INT, 42)));
            GenTree root = call;

            if (nested)
            {
                var outer = compiler.gtNewCallNode(var_types.TYP_INT, gtCallTypes.CT_USER_FUNC, null);
                _ = outer.Args.PushBack(NewCallArg.CreateForPrimitive(call));
                root = outer;
            }

            var block = new BasicBlock(null, null);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(root));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewIconNode(var_types.TYP_INT, 7)));

            compiler.AddContextArgsToAsyncCalls(block);

            var args = GetArgs(call);
            Assert.That(compiler.compAsyncBodyMaySuspend, Is.True);
            Assert.That(compiler.lvaTable[0].lvHasLdAddrOp, Is.True);
            AssertOwnContexts(args);
            Assert.That(args.Count, Is.EqualTo(5));
            Assert.That(args[4], Is.SameAs(userArg));
            Assert.That(call.GetAsyncInfo().InlineFrameContextHandling, Is.Null);
#if DEBUG
            Assert.That(args[0].Node.TreeId, Is.EqualTo(args[1].Node.TreeId + 1));
#endif
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void OwnInlineFramesCopyTheEnclosingChainInOrder(int enclosingFrames)
    {
        WithCompiler(compiler => {
            EnableAsyncInlining(true);
            var inliningCall = NewAsyncCall(compiler);
            List<ContinuationContextHandling> outerHandling = [];

            for (var i = 1; i < enclosingFrames; i++)
            {
                outerHandling.Add(ContinuationContextHandling.ContinueOnThreadPool);
            }

            inliningCall.GetAsyncInfo().ContinuationContextHandling = ContinuationContextHandling.ContinueOnCapturedContext;
            inliningCall.GetAsyncInfo().InlineFrameContextHandling = outerHandling;
            _ = inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(var_types.TYP_INT, 1)));
            _ = inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclAddrNode(var_types.TYP_BYREF, 3, 0))
                .WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            var originals = new List<CallArg>();

            for (var frame = 0; frame < enclosingFrames; frame++)
            {
                var local = (frame + 1) * 3;
                originals.Add(inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_INT, local))
                    .WithWellKnownArg(WellKnownArg.AsyncResumedUse)));
                originals.Add(inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_REF, local + 1))
                    .WithWellKnownArg(WellKnownArg.AsyncExecutionContext)));
                originals.Add(inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_REF, local + 2))
                    .WithWellKnownArg(WellKnownArg.AsyncSynchronizationContext)));
            }

            SetInlineContext(compiler, inliningCall);
            var call = NewAsyncCall(compiler);
            var userArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(var_types.TYP_INT, 9)));
            var block = new BasicBlock(null, null);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));

            compiler.AddContextArgsToAsyncCalls(block);

            var args = GetArgs(call);
            AssertOwnContexts(args);
            Assert.That(args.Count, Is.EqualTo(5 + originals.Count));
            Assert.That(args[^1], Is.SameAs(userArg));

            for (var i = 0; i < originals.Count; i++)
            {
                Assert.That(args[i + 4].WellKnownArg, Is.EqualTo(originals[i].WellKnownArg));
                Assert.That(args[i + 4].Node, Is.Not.SameAs(originals[i].Node));
                Assert.That(args[i + 4].Node.AsLclVar().LclNum, Is.EqualTo(originals[i].Node.AsLclVar().LclNum));
            }

            var handling = call.GetAsyncInfo().InlineFrameContextHandling;

            if (enclosingFrames == 0)
            {
                Assert.That(handling, Is.Null);
            }
            else
            {
                var actualHandling = handling ?? throw new InvalidOperationException("Missing enclosing frame handling.");
                Assert.That(actualHandling, Is.Not.SameAs(outerHandling));
                Assert.That(actualHandling.Count, Is.EqualTo(enclosingFrames));
                Assert.That(actualHandling[0], Is.EqualTo(ContinuationContextHandling.ContinueOnCapturedContext));
                Assert.That(actualHandling.GetRange(1, actualHandling.Count - 1), Is.EqualTo(outerHandling));
            }

            Assert.That(outerHandling.Count, Is.EqualTo(Math.Max(0, enclosingFrames - 1)));
            Assert.That(compiler.compAsyncBodyMaySuspend, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InlinedCallsPreserveExistingOrDisabledContexts(bool existing, bool enabled)
    {
        WithCompiler(compiler => {
            EnableAsyncInlining(enabled);
            SetInlineContext(compiler, NewAsyncCall(compiler));
            var call = NewAsyncCall(compiler);
            CallArg? context = null;

            if (existing)
            {
                context = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_INT, 3))
                    .WithWellKnownArg(WellKnownArg.AsyncResumedUse));
            }

            var block = new BasicBlock(null, null);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));

            compiler.AddContextArgsToAsyncCalls(block);

            var args = GetArgs(call);
            Assert.That(args.Count, Is.EqualTo(existing ? 1 : 0));
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse), Is.SameAs(context));
            Assert.That(compiler.compAsyncBodyMaySuspend, Is.True);
            Assert.That(compiler.lvaTable[0].lvHasLdAddrOp, Is.False);
        });
    }

    [Test]
    public static void SynchronousBodiesDoNotAcquireAsyncContextState()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var block = new BasicBlock(null, null);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
            compiler.AddContextArgsToAsyncCalls(block);
            Assert.That(compiler.compAsyncBodyMaySuspend, Is.False);
            Assert.That(GetArgs(call), Is.Empty);
            Assert.That(compiler.lvaTable[0].lvHasLdAddrOp, Is.False);
        });
    }

    private static void AssertOwnContexts(List<CallArg> args)
    {
        WellKnownArg[] kinds = [WellKnownArg.AsyncResumedDef, WellKnownArg.AsyncResumedUse,
            WellKnownArg.AsyncExecutionContext, WellKnownArg.AsyncSynchronizationContext];
        int[] locals = [0, 0, 1, 2];

        for (var i = 0; i < kinds.Length; i++)
        {
            Assert.That(args[i].WellKnownArg, Is.EqualTo(kinds[i]));
            Assert.That(args[i].Node.AsLclVarCommon().LclNum, Is.EqualTo(locals[i]));
        }

        Assert.That(args[0].Node.Type, Is.EqualTo(var_types.TYP_BYREF));
        Assert.That(args[1].Node.Type, Is.EqualTo(var_types.TYP_INT));
    }

    private static GenTreeCall NewAsyncCall(Compiler compiler)
    {
        var call = compiler.gtNewCallNode(var_types.TYP_INT, gtCallTypes.CT_USER_FUNC, null);
        call.SetIsAsync(default);

        return call;
    }

    private static List<CallArg> GetArgs(GenTreeCall call)
    {
        var result = new List<CallArg>();

        foreach (var arg in call.Args.Args)
        {
            result.Add(arg);
        }

        return result;
    }

    private static void SetInlineContext(Compiler compiler, GenTreeCall inliningCall)
    {
        compiler.opts.compFlags = Globals.CLFLG_INLINING;
        compiler.impInlineInfo = new InlineInfo { InlineRoot = compiler, InlinerCompiler = compiler, iciCall = inliningCall };
    }

    private static void EnableAsyncInlining(bool enabled)
    {
        var config = Globals.JitConfig;
        AsyncInlining(ref config) = enabled ? 1 : 0;
        Globals.JitConfig = config;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitAsyncInlining")]
    private static extern ref int AsyncInlining(ref JitConfigValues config);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetAsyncInfo(ICorJitInfo* jitInfo, CORINFO_ASYNC_INFO* result)
    {
        GC.Collect();
        *result = default;
        result->captureContextsMethHnd = (CORINFO_METHOD_STRUCT_*)(++s_metadataQueries);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[9];
        compiler.lvaCount = 9;
        compiler.lvaResumedIndicator = 0;
        compiler.lvaAsyncExecutionContextVar = 1;
        compiler.lvaAsyncSynchronizationContextVar = 2;

        for (var i = 0; i < compiler.lvaCount; i++)
        {
            compiler.lvaTable[i].Type = i % 3 == 0 ? Globals.TYP_I_IMPL : var_types.TYP_REF;
        }

        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            Globals.JitConfig = previousConfig;
        }
    }
}
