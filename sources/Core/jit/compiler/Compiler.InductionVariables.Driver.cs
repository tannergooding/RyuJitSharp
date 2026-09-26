// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private static ConfigMethodRange s_jitEnableInductionVariableOptsRange;
#endif

    public unsafe PhaseStatus optInductionVariables()
    {
        JITDUMP("*************** In optInductionVariables()\n");
#if DEBUG
        s_jitEnableInductionVariableOptsRange.EnsureInit(JitConfig.JitEnableInductionVariableOptsRange);
        if (!s_jitEnableInductionVariableOptsRange.Contains(info.compMethodHash()))
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif
        if (!fgMightHaveNaturalLoops)
        {
            JITDUMP("  Skipping since this method has no natural loops\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
        if (JitConfig.JitEnableInductionVariableOpts == 0)
        {
            JITDUMP("  Skipping since it is disabled due to JitEnableInductionVariableOpts\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var changed = false;
        optReachableBitVecTraits = null;
        _dfsTree ??= fgComputeDfs();
        _domTree ??= FlowGraphDominatorTree.Build(_dfsTree);
        _loops ??= FlowGraphNaturalLoops.Find(_dfsTree);
        var loopInfo = new PerLoopInfo(_loops);
        var scevContext = new ScalarEvolutionContext(this);
        JITDUMP("Optimizing induction variables:\n");

        foreach (var loop in _loops.InReversePostOrder())
        {
            JITDUMP("Processing ");
#if DEBUG
            if (verbose)
            {
                FlowGraphNaturalLoop.Dump(loop);
            }
#endif
            scevContext.ResetForLoop(loop);
            if (loop.GetPreheader() is null)
            {
                JITDUMP("  No preheader; skipping\n");
                continue;
            }

            var strengthReduction = new StrengthReductionContext(this, scevContext, loop, loopInfo);
            if (strengthReduction.TryStrengthReduce())
            {
                Metrics.LoopsStrengthReduced++;
                changed = true;
            }
            if (optMakeLoopDownwardsCounted(scevContext, loop, loopInfo))
            {
                Metrics.LoopsMadeDownwardsCounted++;
                changed = true;
            }
#if TARGET_XARCH && TARGET_64BIT
            if (optWidenIVs(scevContext, loop, loopInfo))
            {
                Metrics.LoopsIVWidened++;
                changed = true;
            }
#endif
            if (optRemoveUnusedIVs(loop, loopInfo))
            {
                changed = true;
            }
        }

        fgInvalidateDfsTree();
        return changed ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
