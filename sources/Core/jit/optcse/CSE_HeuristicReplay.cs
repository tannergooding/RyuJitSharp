// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public unsafe class CSE_HeuristicReplay : CSE_HeuristicCommon
{
    public CSE_HeuristicReplay(Compiler compiler) : base(compiler)
    {
    }

    public override string Name() => "Replay CSE Heuristic";

    public override void Announce()
        => JITDUMP($"JitReplayCSE is enabled with config {System.Runtime.InteropServices.Marshal.PtrToStringUTF8(
            (nint)JitConfig.JitReplayCSE)}\n");

    public override bool ConsiderTree(GenTree tree, bool isReturn)
        => CanConsiderTree(tree, isReturn);

    public override void ConsiderCandidates()
    {
        var count = m_compiler.CseCandidateCount;
        if (count == 0)
        {
            return;
        }

        ConfigIntArray choices = default;
        choices.EnsureInit(JitConfig.JitReplayCSE);
        for (var i = 0; i < choices.GetLength(); i++)
        {
            var index = unchecked(choices.GetData()[i] - 1);
            if ((index < 0) || (index >= count))
            {
                JITDUMP($"Invalid candidate number {unchecked(index + 1)}\n");
                continue;
            }

            _ = m_compiler.NextCseAttempt();
            var descriptor = m_compiler.CseCandidateTable[index]
                ?? throw new FatalJitException("Replay CSE candidate is missing its descriptor.");
            var candidate = new CSE_Candidate(this, descriptor);

            JITDUMP($"\nReplay attempting {FMT_CSE(candidate.CseIndex())}\n");
            JITDUMP("CSE Expression : \n");
            if (m_compiler.verbose)
            {
                m_compiler.gtDispTree(candidate.Expr());
            }
            JITDUMP("\n");

            if (!descriptor.IsViable())
            {
                JITDUMP($"Abandoned {FMT_CSE(candidate.CseIndex())} -- not viable\n");
                continue;
            }

            PerformCSE(candidate);
            madeChanges = true;
        }
    }
}
#endif
