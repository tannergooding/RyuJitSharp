// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.InlineObservation;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class InlineScanTests
{
    private static IEnumerable<TestCaseData> ConstantScanCases()
    {
        yield return ScanCase("literal-branch", [0x17, 0x2D, 0, 0x2A], 0, CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("argument-branch", [0x02, 0x2D, 0, 0x2A], 0, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("constant-argument-branch", [0x02, 0x2D, 0, 0x2A], 1, CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("argument-and-literal", [0x02, 0x17, 0x5F, 0x2D, 0, 0x2A], 0,
            CALLEE_BINARY_EXRP_WITH_CNS, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("literal-and-argument", [0x17, 0x02, 0x5F, 0x2D, 0, 0x2A], 0,
            CALLEE_BINARY_EXRP_WITH_CNS, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("literal-add-literal", [0x17, 0x18, 0x58, 0x2D, 0, 0x2A], 0,
            CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("constant-argument-add-literal", [0x02, 0x17, 0x58, 0x2D, 0, 0x2A], 1,
            CALLSITE_FOLDABLE_EXPR, CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("literal-add-constant-argument", [0x17, 0x02, 0x58, 0x2D, 0, 0x2A], 1,
            CALLSITE_FOLDABLE_EXPR, CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("argument-div-literal", [0x02, 0x17, 0x5B, 0x2D, 0, 0x2A], 0,
            CALLEE_BINARY_EXRP_WITH_CNS, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("argument-div-constant-argument", [0x02, 0x03, 0x5B, 0x2D, 0, 0x2A], 2,
            CALLEE_BINARY_EXRP_WITH_CNS, CALLSITE_DIV_BY_CNS, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("constant-arguments-div", [0x02, 0x03, 0x5B, 0x2D, 0, 0x2A], 3,
            CALLSITE_FOLDABLE_EXPR, CALLSITE_DIV_BY_CNS, CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("argument-ceq-literal", [0x02, 0x17, 0xFE, 0x01, 0x2D, 0, 0x2A], 0,
            CALLEE_BINARY_EXRP_WITH_CNS, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("literal-ceq-literal", [0x17, 0x18, 0xFE, 0x01, 0x2D, 0, 0x2A], 0,
            CALLSITE_FOLDABLE_BRANCH);
        yield return ScanCase("argument-beq-literal", [0x02, 0x17, 0x2E, 0, 0x2A], 0,
            CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("literal-beq-argument", [0x17, 0x02, 0x2E, 0, 0x2A], 0,
            CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("constant-arguments-beq", [0x02, 0x03, 0x2E, 0, 0x2A], 3,
            CALLSITE_FOLDABLE_BRANCH, CALLSITE_CONSTANT_ARG_FEEDS_TEST, CALLEE_ARG_FEEDS_CONSTANT_TEST);
        yield return ScanCase("arguments-beq", [0x02, 0x03, 0x2E, 0, 0x2A], 0, CALLEE_ARG_FEEDS_TEST);
        yield return ScanCase("length-beq-argument", [0x03, 0x8E, 0x02, 0x2E, 0, 0x2A], 0,
            CALLEE_ARG_FEEDS_RANGE_CHECK);
        yield return ScanCase("argument-beq-length", [0x02, 0x03, 0x8E, 0x2E, 0, 0x2A], 0,
            CALLEE_ARG_FEEDS_RANGE_CHECK);
    }

    private static TestCaseData ScanCase(string name, byte[] il, int invariantArgs, params InlineObservation[] expected)
    {
        return new TestCaseData(il, invariantArgs, expected).SetName($"PreciseConstantScan_{name}");
    }

    [TestCaseSource(nameof(ConstantScanCases))]
    public static unsafe void PreciseConstantScanPreservesNativeObservations(
        byte[] il, int invariantArgs, InlineObservation[] expected)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var policy = new RecordingPolicy(compiler);
        var result = (InlineResult)RuntimeHelpers.GetUninitializedObject(typeof(InlineResult));
        var policyField = typeof(InlineResult).GetField("_policy", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("InlineResult policy field was not found.");
        policyField.SetValue(result, policy);
        compiler.compInlineResult = result;
        compiler.compInlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
        compiler.compInlineContext._ilSize = il.Length;
        compiler.impInlineInfo = new InlineInfo {
            argCnt = 2,
            iciBlock = new BasicBlock(null, null),
        };
        compiler.impInlineInfo.inlArgInfo[0].argIsInvariant = (invariantArgs & 1) != 0;
        compiler.impInlineInfo.inlArgInfo[1].argIsInvariant = (invariantArgs & 2) != 0;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            fixed (byte* code = il)
            {
                compiler.fgFindJumpTargets(code, il.Length, new BitArray(il.Length), makeInlineObservations: true);
            }

            var actual = policy.Observations.Where(observation => observation is
                CALLSITE_FOLDABLE_BRANCH or CALLSITE_CONSTANT_ARG_FEEDS_TEST or
                CALLSITE_FOLDABLE_EXPR or CALLSITE_DIV_BY_CNS or CALLEE_BINARY_EXRP_WITH_CNS or
                CALLEE_ARG_FEEDS_CONSTANT_TEST or CALLEE_ARG_FEEDS_RANGE_CHECK or CALLEE_ARG_FEEDS_TEST);
            Assert.That(actual, Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(NI_IsSupported_True, true, InlineObservation.CALLSITE_FOLDABLE_INTRINSIC)]
    [TestCase(NI_System_Runtime_CompilerServices_RuntimeHelpers_IsRuntimeAsync, true, InlineObservation.CALLSITE_FOLDABLE_INTRINSIC)]
    [TestCase(NI_SRCS_UNSAFE_SizeOf, true, InlineObservation.CALLSITE_FOLDABLE_INTRINSIC)]
    [TestCase(NI_SRCS_UNSAFE_Add, true, InlineObservation.CALLSITE_FOLDABLE_INTRINSIC)]
    [TestCase(NI_SRCS_UNSAFE_IsNullRef, true, InlineObservation.CALLSITE_FOLDABLE_INTRINSIC)]
    [TestCase(NI_Throw_PlatformNotSupportedException, false, InlineObservation.CALLEE_THROW_BLOCK)]
    public static void IntrinsicObservationsPreserveNativeStackAndClassification(
        NamedIntrinsic intrinsic, bool returnsConstant, InlineObservation expectedObservation)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var policy = new RecordingPolicy(compiler);
        var result = (InlineResult)RuntimeHelpers.GetUninitializedObject(typeof(InlineResult));
        var policyField = typeof(InlineResult).GetField("_policy", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("InlineResult policy field was not found.");
        policyField.SetValue(result, policy);
        compiler.compInlineResult = result;

        var stack = new FgStack();
        stack.PushConstant();
        stack.PushConstant();

        var observe = typeof(Compiler).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => method.Name.Contains("g__ObserveNamedIntrinsicPrecise|", StringComparison.Ordinal));
        object[] arguments = [compiler, intrinsic, stack];
        _ = observe.Invoke(null, arguments);
        stack = (FgStack)arguments[2];

        InlineObservation[] expected = [expectedObservation];
        Assert.Multiple(() => {
            Assert.That(FgStack.IsConstant(stack.Top()), Is.EqualTo(returnsConstant));
            Assert.That(policy.Observations, Is.EqualTo(expected));
        });
    }

    private sealed class RecordingPolicy : DefaultPolicy
    {
        public RecordingPolicy(Compiler compiler) : base(compiler, isPrejitRoot: false)
        {
            _decision = InlineDecision.CANDIDATE;
            _observation = CALLEE_BELOW_ALWAYS_INLINE_SIZE;
        }

        public override bool RequiresPreciseScan => true;

        public List<InlineObservation> Observations { get; } = [];

        public override void NoteBool(InlineObservation observation, bool value)
        {
            if (value)
            {
                Observations.Add(observation);
            }
        }

        public override void NoteInt(InlineObservation observation, int value) { }

        public override void NoteDouble(InlineObservation observation, double value) { }
    }
}
