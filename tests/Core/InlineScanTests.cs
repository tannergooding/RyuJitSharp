// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;

namespace RyuJitSharp.UnitTests;

internal static class InlineScanTests
{
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

    private sealed class RecordingPolicy(Compiler compiler) : DefaultPolicy(compiler, isPrejitRoot: false)
    {
        public List<InlineObservation> Observations { get; } = [];

        public override void NoteBool(InlineObservation observation, bool value)
        {
            Assert.That(value, Is.True);
            Observations.Add(observation);
        }
    }
}
