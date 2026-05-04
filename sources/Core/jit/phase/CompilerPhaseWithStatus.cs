// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

/// <summary>A phase that accepts a lambda for the actions done by the phase.</summary>
public sealed class CompilerPhaseWithStatus : Phase
{
    private Func<PhaseStatus> _action;

    public CompilerPhaseWithStatus(Compiler compiler, Phases phase, Func<PhaseStatus> action)
        : base(compiler, phase)
    {
        _action = action;
    }

    protected override PhaseStatus DoPhase() => _action();
}
