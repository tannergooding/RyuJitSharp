// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class InlineDecisionExtensions
{
    extension(InlineDecision decision)
    {
        public CorInfoInline CorInfo => decision switch {
            InlineDecision.SUCCESS => INLINE_PASS,
            InlineDecision.FAILURE => INLINE_FAIL,
            InlineDecision.NEVER => INLINE_NEVER,
            _ => UnexpectedDecision<CorInfoInline>(),
        };

        /// <summary>check if this decision describes a viable candidate</summary>
        public bool IsCandidate => !decision.IsFailure;

        public bool IsDecided => decision switch {
            InlineDecision.SUCCESS or InlineDecision.FAILURE or InlineDecision.NEVER => true,
            InlineDecision.UNDECIDED or InlineDecision.CANDIDATE => false,
            _ => UnexpectedDecision<bool>(),
        };

        /// <summary>check if this decision describes a failing inline</summary>
        public bool IsFailure => decision switch {
            InlineDecision.FAILURE or InlineDecision.NEVER => true,
            InlineDecision.SUCCESS or InlineDecision.UNDECIDED or InlineDecision.CANDIDATE => false,
            _ => UnexpectedDecision<bool>(),
        };

        /// <summary>check if this decision describes a never inline</summary>
        public bool IsNever => decision switch {
            InlineDecision.NEVER => true,
            InlineDecision.SUCCESS or InlineDecision.FAILURE or InlineDecision.UNDECIDED or InlineDecision.CANDIDATE => false,
            _ => UnexpectedDecision<bool>(),
        };

        /// <summary>check if this decision describes a successful inline</summary>
        public bool IsSuccess => decision switch {
            InlineDecision.SUCCESS => true,
            InlineDecision.FAILURE or InlineDecision.NEVER or InlineDecision.UNDECIDED or InlineDecision.CANDIDATE => false,
            _ => UnexpectedDecision<bool>(),
        };

        /// <summary>get a string representing this decision</summary>
        public string String => decision switch {
            InlineDecision.SUCCESS => "success",
            InlineDecision.FAILURE => "failed this call site",
            InlineDecision.NEVER => "failed this callee",
            InlineDecision.CANDIDATE => "candidate",
            InlineDecision.UNDECIDED => "undecided",
            _ => UnexpectedDecision<string>(),
        };
    }

    private static T UnexpectedDecision<T>()
    {
        assert(false, "Unexpected InlineDecision");
        throw new FatalJitException(CORJIT_INTERNALERROR, "Unexpected InlineDecision.");
    }
}
