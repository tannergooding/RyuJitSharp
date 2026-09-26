// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, forwardsub.cpp.

using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    private PhaseStatus fgForwardSub()
    {
        if (!opts.OptimizationEnabled)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }
#if DEBUG
        if (JitConfig.JitNoForwardSub > 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif
        if (!fgDidEarlyLiveness)
        {
            JITDUMP("Liveness information not available, skipping forward sub\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var changed = false;
        foreach (var block in Blocks)
        {
            JITDUMP($"\n\n===> {FMT_BB(block.bbNum)}\n");
            changed |= fgForwardSubBlock(block);
        }
        return changed ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool fgForwardSubBlock(BasicBlock block)
    {
        var stmt = block.FirstStmt;
        var lastStmt = block.LastStmt;
        var changed = false;

        while (stmt != lastStmt)
        {
            var current = stmt ?? throw new FatalJitException("A forward-substitution block has a broken statement list.");
            var previous = current.PrevStmt;
            var next = current.NextStmt
                ?? throw new FatalJitException("A non-final statement must have a successor.");
            var substituted = fgForwardSubStatement(current);
            if (substituted)
            {
                fgRemoveStmt(block, current);
                changed = true;
            }

            stmt = substituted && (previous is not null) && (previous != lastStmt) &&
                (previous.RootNode.Oper is GT_STORE_LCL_VAR) ? previous : next;
        }

        return changed;
    }
}
