// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineReportingTests
{
    private static readonly List<string> s_callbacks = [];
    private static string? s_reason;
    private static CorInfoInline s_result;
    private static nint s_caller;
    private static nint s_callee;
    private static CorInfoMethodRuntimeFlags s_attributes;

    [TestCase(InlineObservation.CALLSITE_IS_VIRTUAL, false)]
    [TestCase(InlineObservation.CALLEE_IS_NOINLINE, false)]
    [TestCase(InlineObservation.CALLEE_HAS_EH, true)]
    [TestCase(InlineObservation.CALLEE_DOES_NOT_RETURN, false)]
    public static void ReportsDecisionsOnceAndPropagatesOnlyEligibleNever(InlineObservation observation, bool marksNoInline)
    {
        WithCompiler((compiler, call) => {
            using var result = new InlineResult(compiler, call, null, "reporting");
            if (observation is InlineObservation.CALLEE_DOES_NOT_RETURN)
            {
                result.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
                result.NoteBool(observation, true);
                result.NoteInt(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, 1);
            }
            else
            {
                result.NoteFatal(observation);
            }
            result.Report();
            result.Dispose();

            string[] expected = marksNoInline ? ["begin", "attributes", "report"] : ["begin", "report"];
            Assert.That(s_callbacks, Is.EqualTo(expected));
            Assert.That(s_reason, Is.EqualTo(result.ReasonString));
            Assert.That(s_result, Is.EqualTo(result.Result));
            Assert.That(s_caller, Is.EqualTo((nint)0x100));
            Assert.That(s_callee, Is.EqualTo((nint)0x200));
            Assert.That(s_attributes, Is.EqualTo(marksNoInline ? CorInfoMethodRuntimeFlags.CORINFO_FLG_BAD_INLINEE : 0));
#if DEBUG
            Assert.That(call._inlineObservation, Is.EqualTo(observation));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SuppressionStillRecordsFirstDebugFailure(bool priorObservation)
    {
        WithCompiler((compiler, call) => {
#if DEBUG
            if (priorObservation)
            {
                call._inlineObservation = InlineObservation.CALLSITE_TOO_MANY_LOCALS;
            }
#endif
            using var result = new InlineResult(compiler, call, null, "suppressed", doNotReport: true);
            result.NoteFatal(InlineObservation.CALLEE_HAS_EH);
            result.Report();

            Assert.That(s_callbacks, Is.Empty);
#if DEBUG
            Assert.That(call._inlineObservation, Is.EqualTo(priorObservation
                ? InlineObservation.CALLSITE_TOO_MANY_LOCALS : InlineObservation.CALLEE_HAS_EH));
#endif
        });
    }

    [TestCase(CorInfoInline.INLINE_PASS)]
    [TestCase(CorInfoInline.INLINE_PREJIT_SUCCESS)]
    [TestCase(CorInfoInline.INLINE_CHECK_CAN_INLINE_SUCCESS)]
    public static void ReportsSpecialSuccessWithoutPolicyDecision(CorInfoInline success)
    {
        WithCompiler((compiler, call) => {
            using var result = new InlineResult(compiler, call, null, "special");
            result.Result = success;
            result.Report();

            Assert.That(result.IsDecided, Is.False);
            string[] expected = success == CorInfoInline.INLINE_PASS ? ["begin"] : ["begin", "report"];
            Assert.That(s_callbacks, Is.EqualTo(expected));
            if (success != CorInfoInline.INLINE_PASS)
            {
                Assert.That(s_result, Is.EqualTo(success));
                Assert.That(s_reason, Is.EqualTo(result.ReasonString));
            }
        });
    }

    [Test]
    public static void ReportsVmFailureWithoutPolicyDecision()
    {
        WithCompiler((compiler, call) => {
            using var result = new InlineResult(compiler, call, null, "vm rejection");
            result.SetVMFailure();
            result.Report();
            Assert.That(s_result, Is.EqualTo(CorInfoInline.INLINE_CHECK_CAN_INLINE_VMFAIL));
            Assert.That(s_reason, Is.EqualTo("VM Reported !CanInline"));
            string[] expected = ["begin", "report"];
            Assert.That(s_callbacks, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ScopeExitReportsDuringExceptionUnwinding()
    {
        WithCompiler((compiler, call) => {
            _ = Assert.Throws<InvalidOperationException>(() => {
                using var result = new InlineResult(compiler, call, null, "unwind");
                result.NoteFatal(InlineObservation.CALLSITE_IS_VIRTUAL);
                throw new InvalidOperationException("unwind");
            });
            string[] expected = ["begin", "report"];
            Assert.That(s_callbacks, Is.EqualTo(expected));
        });
    }

    private static void WithCompiler(Action<Compiler, GenTreeCall> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.beginInlining = &Begin;
        vtable.Base.Base.reportInliningDecision = &Report;
        vtable.Base.Base.setMethodAttribs = &SetAttributes;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x100;
        compiler._inlineStrategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitTls.Compiler = compiler;
        JitConfig = new JitConfigValues();
        s_callbacks.Clear();
        s_reason = null;
        s_result = default;
        s_caller = s_callee = 0;
        s_attributes = 0;
        try
        {
            var call = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x200);
            action(compiler, call);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Begin(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* caller, CORINFO_METHOD_STRUCT_* callee)
    {
        s_callbacks.Add("begin");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Report(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* caller, CORINFO_METHOD_STRUCT_* callee, CorInfoInline result, byte* reason)
    {
        s_callbacks.Add("report");
        s_caller = (nint)caller;
        s_callee = (nint)callee;
        s_result = result;
        s_reason = Marshal.PtrToStringUTF8((nint)reason);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetAttributes(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, CorInfoMethodRuntimeFlags attributes)
    {
        s_callbacks.Add("attributes");
        s_attributes = attributes;
    }
}
