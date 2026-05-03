// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>describes the various states the jit goes through when evaluating an inline candidate</summary>
/// <remarks>It is distinct from CorInfoInline because it must capture internal states that don't get reported back to the runtime.</remarks>
public enum InlineDecision
{
    UNDECIDED,
    CANDIDATE,
    SUCCESS,
    FAILURE,
    NEVER
}
