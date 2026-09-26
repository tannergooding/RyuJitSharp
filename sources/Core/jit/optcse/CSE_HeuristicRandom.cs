// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public class CSE_HeuristicRandom : CSE_HeuristicCommon
{
    private readonly CLRRandom _cseRng;

    public CSE_HeuristicRandom(Compiler compiler) : base(compiler)
    {
        _cseRng = new CLRRandom(compiler.info.compMethodHash() ^ JitConfig.JitRandomCSE);
    }

    public override string Name() => "Random CSE Heuristic";

    public override void Announce()
        => JITDUMP($"JitRandomCSE is enabled with salt {JitConfig.JitRandomCSE}\n");

    public override bool ConsiderTree(GenTree tree, bool isReturn)
        => CanConsiderTree(tree, isReturn);

    public override void ConsiderCandidates()
    {
        var count = m_compiler.CseCandidateCount;
        if (count == 0)
        {
            return;
        }

        sortTab = new CSEdsc?[count];
        for (var i = 0; i < count; i++)
        {
            var j = _cseRng.Next(i + 1);
            if (i != j)
            {
                sortTab[i] = sortTab[j];
            }
            sortTab[j] = m_compiler.CseCandidateTable[i];
        }

        var selected = _cseRng.Next(count) + 1;
        for (var index = 0; index < selected; index++)
        {
            _ = m_compiler.NextCseAttempt();
            var descriptor = sortTab[index]
                ?? throw new FatalJitException("Random CSE permutation is missing a descriptor.");
            var candidate = new CSE_Candidate(this, descriptor);

            JITDUMP($"\nRandomly attempting {FMT_CSE(candidate.CseIndex())}\n");
            JITDUMP("CSE Expression : \n");
            if (m_compiler.verbose)
            {
                m_compiler.gtDispTree(candidate.Expr());
            }
            JITDUMP("\n");

            if (m_compiler.optConfigDisableCSE2())
            {
                continue;
            }

            if (descriptor.defExcSetPromise == ValueNumStore.NoVN)
            {
                JITDUMP($"Abandoned {FMT_CSE(candidate.CseIndex())} because we had defs with different Exc sets\n");
                continue;
            }

            candidate.InitializeCounts();
            if (candidate.UseCount() == 0)
            {
                JITDUMP($"Skipped {FMT_CSE(candidate.CseIndex())} because use count is 0\n");
                continue;
            }

            if ((descriptor.csdDefCount == 0) || (descriptor.csdUseCount == 0))
            {
                continue;
            }

            PerformCSE(candidate);
            madeChanges = true;
        }
    }
}
#endif
