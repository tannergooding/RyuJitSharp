// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private void optValnumCSE_Heuristic(CSE_HeuristicCommon heuristic)
    {
        if (Configuration.DEBUG && verbose)
        {
            jitprintf("\n************ Trees at start of optValnumCSE_Heuristic()\n");
            fgDumpTrees(fgFirstBB, null);
            jitprintf("\n");
        }

        if (Configuration.DEBUG)
        {
            heuristic.Announce();
        }
        heuristic.Initialize();
        heuristic.SortCandidates();
        heuristic.ConsiderCandidates();
        heuristic.Cleanup();
    }

    public unsafe CSE_HeuristicCommon optGetCSEheuristic()
    {
        if (optCSEheuristic is not null)
        {
            return optCSEheuristic;
        }

        if (Configuration.DEBUG && (JitConfig.JitRLHook > 0))
        {
            optCSEheuristic = new CSE_HeuristicRLHook(this);
        }

        if (Configuration.DEBUG && (optCSEheuristic is null) && (JitConfig.JitRLCSE is not null))
        {
            optCSEheuristic = new CSE_HeuristicRL(this);
        }

        if (Configuration.DEBUG && (optCSEheuristic is null))
        {
            var useRandomHeuristic = JitConfig.JitRandomCSE > 0;
            if (!useRandomHeuristic && compStressCompile(STRESS_MAKE_CSE, MAX_STRESS_WEIGHT))
            {
                useRandomHeuristic = true;
            }

            if (useRandomHeuristic)
            {
                optCSEheuristic = new CSE_HeuristicRandom(this);
            }
        }

        if (Configuration.DEBUG && (optCSEheuristic is null) && (JitConfig.JitReplayCSE is not null))
        {
            optCSEheuristic = new CSE_HeuristicReplay(this);
        }
        if ((optCSEheuristic is null) && (JitConfig.JitRLCSEGreedy > 0))
        {
            optCSEheuristic = new CSE_HeuristicParameterized(this);
        }

        optCSEheuristic ??= new CSE_Heuristic(this);
        if (Configuration.DEBUG)
        {
            optCSEheuristic.Announce();
        }
        return optCSEheuristic;
    }

    public PhaseStatus optOptimizeValnumCSEs()
    {
        if (Configuration.DEBUG && optConfigDisableCSE())
        {
            JITDUMP("Disabled by JitNoCSE\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
        var heuristic = optGetCSEheuristic();
        if (Configuration.DEBUG)
        {
            heuristic.Announce();
        }
        optValnumCSE_phase = true;
        optCSEweight = -1.0f;

        optValnumCSE_Init();
        if (optValnumCSE_Locate(heuristic))
        {
            optValnumCSE_InitDataFlow();
            optValnumCSE_DataFlow();
            optValnumCSE_Availability();
            optValnumCSE_Heuristic(heuristic);
        }

        optValnumCSE_phase = false;
        return heuristic.MadeChanges() ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    public bool optIsCSEcandidate(GenTree tree, bool isReturn = false)
        => optGetCSEheuristic().ConsiderTree(tree, isReturn);

    private bool optConfigDisableCSE()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CSEDiagnosticConfigurationIsOnlyAvailableInDebugBuilds);
        }

        var jitNoCSE = unchecked((uint)JitConfig.JitNoCSE);
        if (jitNoCSE > 0)
        {
            var methodCount = unchecked((uint)jitTotalMethodCompiled);
            if ((jitNoCSE & 0x0F000000) == 0x0F000000)
            {
                var methodCountMask = methodCount & 0xFFF;
                var bitsZero = (jitNoCSE >> 12) & 0xFFF;
                var bitsOne = jitNoCSE & 0xFFF;
                if (((methodCountMask & bitsOne) == bitsOne) && ((~methodCountMask & bitsZero) == bitsZero))
                {
                    JITDUMP(" Disabled by JitNoCSE methodCountMask\n");
                    return true;
                }
            }
            else if (jitNoCSE <= unchecked(methodCount + 1))
            {
                JITDUMP(" Disabled by JitNoCSE > methodCount\n");
                return true;
            }
        }
        return false;
    }

    public void optOptimizeCSEs()
    {
        if (optCSEstart != BAD_VAR_NUM)
        {
            optCleanupCSEs();
        }

        optCSECandidateCount = 0;
        optCSEstart = lvaCount;
        if (Configuration.DEBUG)
        {
            optEnsureClearCSEInfo();
        }
        _ = optOptimizeValnumCSEs();
    }

    private void optCleanupCSEs()
    {
        foreach (var block in Blocks)
        {
            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                for (var tree = stmt.RootNode; tree is not null; tree = tree.Prev)
                {
                    tree._cseNum = NO_CSE;
                }
            }
        }
    }

    private void optEnsureClearCSEInfo()
    {
        foreach (var block in Blocks)
        {
            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                for (var tree = stmt.RootNode; tree is not null; tree = tree.Prev)
                {
                    assert(tree._cseNum == NO_CSE);
                }
            }
        }
    }
}
