// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class InlineDecisionTests
{
    [TestCase(InlineDecision.UNDECIDED, true, false, false, false, false, "undecided")]
    [TestCase(InlineDecision.CANDIDATE, true, false, false, false, false, "candidate")]
    [TestCase(InlineDecision.SUCCESS, true, true, false, true, false, "success")]
    [TestCase(InlineDecision.FAILURE, false, true, true, false, false, "failed this call site")]
    [TestCase(InlineDecision.NEVER, false, true, true, false, true, "failed this callee")]
    public static void DecisionPropertiesMatchNativeMapping(
        InlineDecision decision,
        bool isCandidate,
        bool isDecided,
        bool isFailure,
        bool isSuccess,
        bool isNever,
        string description)
    {
        Assert.That(decision.IsCandidate, Is.EqualTo(isCandidate));
        Assert.That(decision.IsDecided, Is.EqualTo(isDecided));
        Assert.That(decision.IsFailure, Is.EqualTo(isFailure));
        Assert.That(decision.IsSuccess, Is.EqualTo(isSuccess));
        Assert.That(decision.IsNever, Is.EqualTo(isNever));
        Assert.That(decision.String, Is.EqualTo(description));
    }

    [TestCase(InlineDecision.SUCCESS, CorInfoInline.INLINE_PASS)]
    [TestCase(InlineDecision.FAILURE, CorInfoInline.INLINE_FAIL)]
    [TestCase(InlineDecision.NEVER, CorInfoInline.INLINE_NEVER)]
    public static void TerminalDecisionMapsToRuntimeResult(InlineDecision decision, CorInfoInline result)
    {
        Assert.That(decision.CorInfo, Is.EqualTo(result));
    }

#if !DEBUG
    [TestCase(InlineDecision.UNDECIDED)]
    [TestCase(InlineDecision.CANDIDATE)]
    [TestCase((InlineDecision)5)]
    [TestCase((InlineDecision)(-1))]
    public static void InvalidRuntimeResultMappingIsFatal(InlineDecision decision)
    {
        AssertUnreachable(() => _ = decision.CorInfo);
    }

    [TestCase((InlineDecision)5)]
    [TestCase((InlineDecision)(-1))]
    public static void UnknownDecisionIsUnreachable(InlineDecision decision)
    {
        AssertUnreachable(() => _ = decision.IsCandidate);
        AssertUnreachable(() => _ = decision.IsDecided);
        AssertUnreachable(() => _ = decision.IsFailure);
        AssertUnreachable(() => _ = decision.IsNever);
        AssertUnreachable(() => _ = decision.IsSuccess);
        AssertUnreachable(() => _ = decision.String);
    }

    private static void AssertUnreachable(TestDelegate action)
    {
        var exception = Assert.Throws<FatalJitException>(action);
        Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
    }
#endif
}
