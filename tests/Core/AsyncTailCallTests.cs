// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncTailCallTests
{
    private const int AsyncVersionTailAwait = 0x200;
    private const int AdaptedFromValueTask = 0x400;

    [TestCase(false, false, false, true)]
    [TestCase(false, true, false, true)]
    [TestCase(false, true, true, false)]
    [TestCase(true, false, false, true)]
    [TestCase(true, true, false, true)]
    public static void TailPatternsResolveOnlyCompleteReturns(bool asTask, bool generic, bool valueConstructor, bool expected)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.resolveToken = &ResolvePatternToken;
            vtable.Base.Base.isIntrinsic =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, bool>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, byte>)&IsIntrinsic;
            vtable.Base.Base.getMethodNameFromMetadata = &GetPatternName;
            vtable.Base.Base.getMethodSig = &GetPatternSignature;
            vtable.Base.Base.getArgClass = &GetPatternArgumentClass;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            PatternState state = new() {
                AsTask = asTask,
                Generic = generic,
                ValueConstructor = valueConstructor,
                GenericType = (CORINFO_CLASS_STRUCT_*)17,
            };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compScopeHnd = (CORINFO_MODULE_STRUCT_*)&state;
            compiler.info.compRetType = generic ? var_types.TYP_INT : var_types.TYP_VOID;
            byte[] bytes = asTask ? [0x0A, 0x12, 0, 0x28, 1, 0, 0, 0, 0x2A] : [0x73, 1, 0, 0, 0, 0x2A];

            fixed (byte* code = bytes)
            {
                for (var length = 0; length <= bytes.Length; length++)
                {
                    state.Resolutions = 0;
                    var flags = 4;
                    var result = compiler.impMatchAsyncVersionTailCall(code, code + length, ref flags, out var matched);
                    var complete = length == bytes.Length;
                    Assert.That(result, Is.EqualTo(complete && expected), $"Length {length}");
                    Assert.That(matched, Is.EqualTo(complete && expected ? bytes.Length : 0));
                    Assert.That(flags, Is.EqualTo(4 | (complete && expected && asTask ? AdaptedFromValueTask : 0)));
                    Assert.That(state.Resolutions, Is.EqualTo(complete ? 1 : 0));
                }
            }
        });
    }

    [Test]
    public static void RecognizedTailAwaitRemainsEligibleAfterItsRetWasConsumed()
    {
        WithCompiler(compiler => {
            var code = stackalloc byte[] { 0x2A };
            var flags = 0;
            Assert.That(compiler.impMatchAsyncVersionTailCall(code, code + 1, ref flags, out var matched), Is.True);
            Assert.That(matched, Is.EqualTo(1));
            compiler.opts.compTailCallOpt = true;
            compiler.compCurBB = new BasicBlock(null, null);
            compiler.compCurBB.SetKindAndTargetEdge(BBKinds.BBJ_RETURN, null);
            compiler.info.compCode = code;
            compiler.info.compILCodeSize = 1;

            Assert.That(compiler.impIsImplicitTailCallCandidate(OPCODE.CEE_CALL, code + 1, code + 1, 0, false), Is.False);
            Assert.That(compiler.impIsImplicitTailCallCandidate(OPCODE.CEE_CALL, code + 1, code + 1, AsyncVersionTailAwait, false), Is.True);
        });
    }

    [TestCase(true, false, false, true, true, true)]
    [TestCase(false, false, false, true, true, false)]
    [TestCase(true, true, false, true, true, true)]
    [TestCase(true, false, true, true, true, false)]
    [TestCase(true, false, false, false, true, false)]
    [TestCase(true, false, false, true, false, false)]
    public static void RootTailAwaitsRequireEEConsentAndCompatibleUnadaptedReturns(
        bool allowed, bool virtualCall, bool adapted, bool compatible, bool optimize, bool expected)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.canTailCall =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, CORINFO_METHOD_STRUCT_*, CORINFO_METHOD_STRUCT_*, bool, bool>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, CORINFO_METHOD_STRUCT_*, CORINFO_METHOD_STRUCT_*, byte, byte>)&CanTailCall;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            TailState state = new() { Allowed = allowed };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)&state;
            compiler.info.compRetType = compatible ? var_types.TYP_INT : var_types.TYP_VOID;
            compiler.opts.compDbgCode = !optimize;
            compiler.opts.SetMinOpts(false);
            var method = (CORINFO_METHOD_STRUCT_*)7;
            var call = compiler.gtNewCallNode(var_types.TYP_INT, gtCallTypes.CT_USER_FUNC, method);
            if (virtualCall)
            {
                call.Flags |= GenTreeFlags.GTF_CALL_VIRT_VTABLE;
            }

            compiler.impSetupAsyncCall(call, method, OPCODE.CEE_CALL,
                AsyncVersionTailAwait | (adapted ? AdaptedFromValueTask : 0), NamedIntrinsic.NI_Illegal, default, out var ownContexts);

            var queried = !adapted && compatible && optimize;
            Assert.That(state.Queries, Is.EqualTo(queried ? 1 : 0));
            if (queried)
            {
                Assert.That(state.ExactCallee, Is.EqualTo(virtualCall ? (nint)0 : (nint)method));
                Assert.That(state.ExplicitPrefix, Is.False);
            }
            Assert.That(ownContexts, Is.False);
            Assert.That(call.IsAsync, Is.True);
            Assert.That(call.GetAsyncInfo().IsTailAwait, Is.EqualTo(expected));
            Assert.That(call.GetAsyncInfo().IsValueTaskAsTask, Is.EqualTo(adapted));
        });
    }

    [TestCase(NamedIntrinsic.NI_Illegal, false)]
    [TestCase(NamedIntrinsic.NI_System_Runtime_CompilerServices_AsyncHelpers_AwaitAwaiter, true)]
    [TestCase(NamedIntrinsic.NI_System_Runtime_CompilerServices_AsyncHelpers_UnsafeAwaitAwaiter, true)]
    [TestCase(NamedIntrinsic.NI_System_Runtime_CompilerServices_AsyncHelpers_Suspend, true)]
    [TestCase(NamedIntrinsic.NI_System_Runtime_CompilerServices_AsyncHelpers_TransparentSuspend, true)]
    public static void AlwaysSuspendingHelpersRetainAsyncDebugLocations(NamedIntrinsic intrinsic, bool suspends)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)1);
            var location = new ILLocation(17, ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.STACK_EMPTY);
            var debugInfo = new DebugInfo(null, location);
            compiler.impSetupAsyncCall(call, call._callMethHnd, OPCODE.CEE_CALL, 0, intrinsic, debugInfo, out var ownContexts);

            Assert.That(ownContexts, Is.False);
            Assert.That(call.GetAsyncInfo().AlwaysSuspends, Is.EqualTo(suspends));
            Assert.That(call.GetAsyncInfo().CallAsyncDebugInfo.Location.Offset, Is.EqualTo(17));
            Assert.That(call.GetAsyncInfo().CallAsyncDebugInfo.Location.SourceTypes, Is.EqualTo(ICorDebugInfo.ASYNC | ICorDebugInfo.STACK_EMPTY));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void OnlyContextRestoreTryRegionsAreIgnored(bool hasTry, bool outerIsRestore, bool expected)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            if (hasTry)
            {
                block.TryIndex = 0;
            }
            else
            {
                block.HndIndex = 0;
            }
            compiler.compHndBBtab = [
                new EHblkDsc { ebdID = 10, ebdEnclosingTryIndex = 1 },
                new EHblkDsc { ebdID = 20, ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX },
            ];
            compiler.compHndBBtabCount = 2;
            HashSet<ushort> restoreIDs = [10];
            if (outerIsRestore)
            {
                _ = restoreIDs.Add(20);
            }
            SetField(typeof(Compiler), compiler, "_asyncContextRestoreEHIDs", restoreIDs);

            Assert.That(compiler.ehIsInsideNonAsyncContextRestoreRegion(block), Is.EqualTo(expected));
            var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            inlinee.opts.compFlags = Globals.CLFLG_INLINING;
            inlinee.impInlineInfo = new InlineInfo { InlineRoot = compiler };
            Assert.That(inlinee.ehIsAsyncContextRestore(10), Is.True);
            Assert.That(inlinee.ehIsAsyncContextRestore(20), Is.EqualTo(outerIsRestore));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InlinedAwaitsDistinguishOwnAndInheritedContexts(bool tailAwait)
    {
        WithCompiler(compiler => {
            object config = Globals.JitConfig;
            SetField(typeof(JitConfigValues), config, "_jitAsyncInlining", 1);
            Globals.JitConfig = (JitConfigValues)config;
            var root = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            JitFlags rootFlags = default;
            rootFlags.Set(JitFlags.JIT_FLAG_ASYNC);
            root.opts.jitFlags = &rootFlags;
            var callSite = new BasicBlock(null, null);
            var inliningCall = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)1);
            inliningCall.SetIsAsync(new AsyncCallInfo {
                IsTailAwait = true,
                ContinuationContextHandling = ContinuationContextHandling.ContinueOnCapturedContext,
            });
            compiler.lvaTable = new LclVarDsc[3];
            compiler.lvaCount = 3;
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            compiler.lvaTable[1].Type = compiler.lvaTable[2].Type = var_types.TYP_REF;
            _ = inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_INT, 0))
                .WithWellKnownArg(WellKnownArg.AsyncResumedUse));
            _ = inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclAddrNode(var_types.TYP_BYREF, 0, 0))
                .WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            _ = inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_REF, 1))
                .WithWellKnownArg(WellKnownArg.AsyncExecutionContext));
            _ = inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(var_types.TYP_REF, 2))
                .WithWellKnownArg(WellKnownArg.AsyncSynchronizationContext));
            compiler.opts.compFlags = Globals.CLFLG_INLINING;
            compiler.impInlineInfo = new InlineInfo { InlineRoot = root, InlinerCompiler = root, iciBlock = callSite, iciCall = inliningCall };
            compiler.compCurBB = new BasicBlock(null, null);
            SetField(typeof(Compiler), compiler, "_nextAwaitIsTail", tailAwait);
            var call = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_INDIRECT, null);

            compiler.impSetupAsyncCall(call, null, OPCODE.CEE_CALLVIRT, 0, NamedIntrinsic.NI_Illegal, default, out var ownContexts);
            compiler.impInsertAsyncArgsForLdvirtftnCall(call, ownContexts);

            Assert.That(ownContexts, Is.EqualTo(!tailAwait));
            Assert.That(call.GetAsyncInfo().IsTailAwait, Is.EqualTo(tailAwait));
            Assert.That(call.GetAsyncInfo().ContinuationContextHandling, Is.EqualTo(tailAwait
                ? ContinuationContextHandling.ContinueOnCapturedContext : ContinuationContextHandling.None));
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncContinuation), Is.Not.Null);
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse) is not null, Is.EqualTo(tailAwait));
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedDef) is not null, Is.EqualTo(tailAwait));
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncExecutionContext) is not null, Is.EqualTo(tailAwait));
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncSynchronizationContext) is not null, Is.EqualTo(tailAwait));
        });
    }

    private struct PatternState
    {
        public bool AsTask;
        public bool Generic;
        public bool ValueConstructor;
        public CORINFO_CLASS_STRUCT_* GenericType;
        public int Resolutions;
    }

    private struct TailState
    {
        public bool Allowed;
        public int Queries;
        public nint ExactCallee;
        public bool ExplicitPrefix;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ResolvePatternToken(ICorJitInfo* jitInfo, CORINFO_RESOLVED_TOKEN* token)
    {
        var state = (PatternState*)token->tokenScope;
        state->Resolutions++;
        token->hMethod = (CORINFO_METHOD_STRUCT_*)state;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsic(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
    {
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetPatternName(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method,
        byte** className, byte** namespaceName, byte** enclosingClassNames, nint maxEnclosingClassNames)
    {
        var state = (PatternState*)method;
        *className = state->Generic
            ? (byte*)Unsafe.AsPointer(in MemoryMarshal.GetReference("ValueTask`1"u8))
            : (byte*)Unsafe.AsPointer(in MemoryMarshal.GetReference("ValueTask"u8));
        *namespaceName = (byte*)Unsafe.AsPointer(in MemoryMarshal.GetReference("System.Threading.Tasks"u8));
        return state->AsTask
            ? (byte*)Unsafe.AsPointer(in MemoryMarshal.GetReference("AsTask"u8))
            : (byte*)Unsafe.AsPointer(in MemoryMarshal.GetReference(".ctor"u8));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetPatternSignature(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* sig, CORINFO_CLASS_STRUCT_* owner)
    {
        var state = (PatternState*)method;
        *sig = default;
        sig->numArgs = 1;
        sig->sigInst.classInstCount = state->Generic ? 1 : 0;
        sig->sigInst.classInst = &state->GenericType;
        sig->args = (CORINFO_ARG_LIST_STRUCT_*)state;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetPatternArgumentClass(ICorJitInfo* jitInfo, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* arg)
    {
        var state = (PatternState*)arg;
        return state->ValueConstructor ? state->GenericType : (CORINFO_CLASS_STRUCT_*)18;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte CanTailCall(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* caller, CORINFO_METHOD_STRUCT_* declaredCallee,
        CORINFO_METHOD_STRUCT_* exactCallee, byte explicitPrefix)
    {
        var state = (TailState*)caller;
        state->Queries++;
        state->ExactCallee = (nint)exactCallee;
        state->ExplicitPrefix = explicitPrefix != 0;
        return state->Allowed ? (byte)1 : (byte)0;
    }

    private static void SetField([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object instance, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}.");
        field.SetValue(instance, value);
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
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info = new Compiler.Info { compMethodInfo = &methodInfo };
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
