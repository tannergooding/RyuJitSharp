// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public abstract partial class CSE_HeuristicCommon
{
    public virtual void ConsiderCandidates()
    {
        assert(sortTab is not null);

        for (var index = 0; index < m_compiler.CseCandidateCount; index++)
        {
            var attempt = m_compiler.NextCseAttempt();
            var descriptor = sortTab[index];
            assert(descriptor is not null);

            var candidate = new CSE_Candidate(this, descriptor);
            if (!descriptor.IsViable())
            {
                continue;
            }

            candidate.InitializeCounts();
#if DEBUG
            if (m_compiler.verbose)
            {
                var key = descriptor.csdIsSharedConst
                    ? $"K_{Compiler.DecodeSharedCseConstant(descriptor.csdHashKey):x}"
                    : $"${descriptor.csdHashKey,-3:x}, ${descriptor.defExcSetPromise,-3:x}";
                Globals.jitprintf($"\nConsidering {FMT_CSE(candidate.CseIndex())} {{{key}}} " +
                    $"[def={formatFloat(candidate.DefCount(), "F6")}, " +
                    $"use={formatFloat(candidate.UseCount(), "F6")}, " +
                    $"cost={candidate.Cost(),3}{(descriptor.csdLiveAcrossCall ? ", call" : "      ")}]\n");
                Globals.jitprintf("CSE Expression : \n");
                m_compiler.gtDispTree(candidate.Expr());
                Globals.jitprintf("\n");
            }
#endif
            var promote = PromotionCheck(candidate);

#if DEBUG
            var hash = JitConfig.JitCSEHash;
            if ((hash == 0) || (m_compiler.info.compMethodHash() == hash))
            {
                // The native mask only represents the first 32 attempts.
                if (attempt >= 32)
                {
                    promote = false;
                    JITDUMP($"{FMT_CSE(candidate.CseIndex())} attempt {attempt} disabled, out of mask range\n");
                }
                else
                {
                    var mask = unchecked((uint)JitConfig.JitCSEMask);
                    promote = ((1u << attempt) & mask) != 0;
                    JITDUMP($"{FMT_CSE(candidate.CseIndex())} attempt {attempt} mask 0x{mask:x8}: " +
                        $"{(promote ? "allowed" : "disabled")}\n");
                }
            }

            if (m_compiler.verbose)
            {
                Globals.jitprintf(promote ? "\nPromoting CSE:\n" : "Did Not promote this CSE\n");
            }
#endif

            if (promote)
            {
                PerformCSE(candidate);
                madeChanges = true;
            }
        }
    }
}
