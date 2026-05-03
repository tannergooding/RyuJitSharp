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
            _ => INLINE_FAIL,
        };

        /// <summary>check if this decision describes a viable candidate</summary>
        public bool IsCandidate => !decision.IsFailure;

        public bool IsDecided => decision is InlineDecision.SUCCESS or InlineDecision.FAILURE or InlineDecision.NEVER;

        /// <summary>check if this decision describes a failing inline</summary>
        public bool IsFailure => decision is InlineDecision.FAILURE or InlineDecision.NEVER;

        /// <summary>check if this decision describes a never inline</summary>
        public bool IsNever => decision is InlineDecision.NEVER;

        /// <summary>check if this decision describes a successful inline</summary>
        public bool IsSuccess => decision is InlineDecision.SUCCESS;
    }
}
