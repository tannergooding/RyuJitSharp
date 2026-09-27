// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgopt.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe PhaseStatus fgResolveGDVs()
    {
        if (!opts.OptimizationEnabled)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (!MethodHasGuardedDevirtualization)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (!hasUpdatedTypeLocals)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var madeChanges = false;
        foreach (var block in Blocks)
        {
            if (block.Kind is not BBJ_COND)
            {
                continue;
            }

            var guard = new ObjectAllocator.GuardInfo();
            if (ObjectAllocator.IsGuard(block, guard) is GenTree relop)
            {
                assert(block == guard.Block);
                ref var local = ref lvaGetDesc(guard.Local);
                if (local.lvClassIsExact && local.lvSingleDef && (local.lvClassHnd == guard.Type))
                {
                    JITDUMP($"GDV in {FMT_BB(block.bbNum)} can be resolved; type is now known exactly\n");
                    var isCondTrue = relop.Oper is GT_EQ;
                    var retainedEdge = isCondTrue ? block.TrueEdge : block.FalseEdge;
                    var removedEdge = isCondTrue ? block.FalseEdge : block.TrueEdge;
                    JITDUMP($"The conditional jump becomes an unconditional jump to {FMT_BB(retainedEdge.DestinationBlock.bbNum)}\n");

                    fgRemoveRefPred(removedEdge);
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, retainedEdge);
                    fgRepairProfileCondToUncond(block, retainedEdge, removedEdge);

                    // The method-table read can still throw; leave the relop for later cleanup.
                    var statement = guard.Stmt;
                    assert(statement is not null);
                    statement.RootNode = relop;
                    madeChanges = true;
                }
            }
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
