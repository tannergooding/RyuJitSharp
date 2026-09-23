// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineCandidateTests
{
    private static readonly List<nuint> s_reportedCallees = [];

    [TestCase(true, false, false, false, false, 1, true)]
    [TestCase(true, false, false, false, true, 1, true)]
    [TestCase(true, false, false, false, false, 3, true)]
    [TestCase(false, false, false, false, false, 1, false)]
    [TestCase(true, true, false, false, false, 1, false)]
    [TestCase(true, false, true, false, false, 1, false)]
    [TestCase(true, false, false, true, false, 1, false)]
    [TestCase(true, false, false, true, false, 3, false)]
    public static void RejectedGdvCandidatesPreserveNativeRetentionAndCallee(
        bool classBased, bool tailCall, bool nextCallReturnAddress, bool requireInlining,
        bool unboxed, int candidateCount, bool expectedRetention)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        compiler.opts.compFlags = Globals.CLFLG_INLINING;
        compiler.opts.compDbgCode = true;
        compiler.info.compHasNextCallRetAddr = nextCallReturnAddress;
        compiler.compCurBB = new BasicBlock(null, null);
        compiler._inlineStrategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        var context = new InlineContext(compiler._inlineStrategy);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.beginInlining = &BeginInlining;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        JitTls.Compiler = compiler;
        s_reportedCallees.Clear();

        try
        {
            object config = new JitConfigValues();
            var field = typeof(JitConfigValues).GetField("_jitGuardedDevirtualizationRequireInlining",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing guarded devirtualization config field.");
            field.SetValue(config, requireInlining ? 1 : 0);
            Globals.JitConfig = (JitConfigValues)config;

            var call = new GenTreeCall(var_types.TYP_VOID) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)100,
            };
            if (tailCall)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_IMPLICIT_TAILCALL;
            }

            var expectedCallees = new nuint[candidateCount];
            for (var i = 0; i < candidateCount; i++)
            {
                var candidate = new InlineCandidateInfo {
                    guardedClassHandle = classBased ? (CORINFO_CLASS_STRUCT_*)1 : null,
                    guardedMethodHandle = (CORINFO_METHOD_STRUCT_*)(200 + i),
                };
                if (unboxed)
                {
                    candidate.guardedMethodUnboxedResolvedToken.hMethod = (CORINFO_METHOD_STRUCT_*)(300 + i);
                }
                call.AddGdvCandidateInfo(compiler, candidate);
                expectedCallees[i] = (nuint)((unboxed ? 300 : 200) + i);
            }

            compiler.impMarkInlineCandidate(call, null, default, context);

            Assert.Multiple(() => {
                Assert.That(call.IsGuardedDevirtualizationCandidate, Is.EqualTo(expectedRetention));
                Assert.That(call.IsInlineCandidate, Is.False);
                Assert.That(call.InlineCandidatesCount, Is.EqualTo(expectedRetention ? candidateCount : 0));
                Assert.That(s_reportedCallees, Is.EqualTo(expectedCallees));
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void BeginInlining(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* caller, CORINFO_METHOD_STRUCT_* callee)
    {
        s_reportedCallees.Add((nuint)callee);
    }
}
