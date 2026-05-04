// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class Lowering : Phase
{
    public Lowering(Compiler compiler, IRegAlloc regAlloc)
        : base(compiler, PHASE_LOWERING)
    {
        // TODO: Port Lowering.ctor
    }

    public void FinalizeOutgoingArgSpace()
    {
        // TODO: Port Lowering.FinalizeOutgoingArgSpace
    }

    // TODO: Port Lowering.DoPhase
    protected override PhaseStatus DoPhase() => PhaseStatus.MODIFIED_NOTHING;
}
