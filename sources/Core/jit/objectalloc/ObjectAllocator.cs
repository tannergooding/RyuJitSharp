// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ObjectAllocator : Phase
{
    public ObjectAllocator(Compiler compiler)
        : base(compiler, PHASE_ALLOCATE_OBJECTS)
    {
        // TODO: Port ObjectAllocator.ctor
    }

    public void EnableObjectStackAllocation()
    {
        // TODO: Port ObjectAllocator.EnableObjectStackAllocation
    }

    // TODO: Port ObjectAllocator.DoPhase
    protected override PhaseStatus DoPhase() => PhaseStatus.MODIFIED_NOTHING;
}
