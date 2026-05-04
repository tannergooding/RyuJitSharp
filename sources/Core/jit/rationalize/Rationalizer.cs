// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class Rationalizer : Phase
{
    public Rationalizer(Compiler compiler)
        : base(compiler, PHASE_RATIONALIZE)
    {
        // TODO: Port Rationalize.ctor
    }

    // TODO: Port Rationalize.DoPhase
    protected override PhaseStatus DoPhase() => PhaseStatus.MODIFIED_NOTHING;
}
