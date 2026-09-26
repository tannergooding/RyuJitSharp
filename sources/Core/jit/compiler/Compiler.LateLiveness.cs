// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgLateLiveness()
    {
        if (!backendRequiresLocalVarLifetimes())
        {
            fgInvalidateDfsTree();
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert(_dfsTree is not null);
        lvaComputeRefCounts(isRecompute: true, setSlotNumbers: false);
        assert(opts.OptimizationEnabled);

        fgPostLowerLiveness();
        var modified = fgUpdateFlowGraph(doTailDuplication: false, isPhase: false);
        if (modified)
        {
            _ = fgDfsBlocksAndRemove();
            JITDUMP("had to run another liveness pass:\n");
            fgPostLowerLiveness();
        }

        lvaComputeRefCounts(isRecompute: true, setSlotNumbers: false);
        fgInvalidateDfsTree();
        return PhaseStatus.MODIFIED_EVERYTHING;
    }
}
