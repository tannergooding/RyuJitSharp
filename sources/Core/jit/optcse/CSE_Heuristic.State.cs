// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public abstract partial class CSE_HeuristicCommon
{
    protected uint m_addCSEcount;
    protected CSEdsc?[]? sortTab;
    protected nuint sortSiz;
    protected bool madeChanges;
    protected readonly int cntCalleeTrashInt;
    protected readonly int cntCalleeTrashFlt;
    protected readonly int cntCalleeTrashMsk;
#if DEBUG
    protected readonly List<uint> m_sequence = [];
#endif

    public Compiler.codeOptimize CodeOptKind() => codeOptKind;
    public bool MadeChanges() => madeChanges;

    public virtual void SortCandidates()
    {
    }

    public virtual bool PromotionCheck(CSE_Candidate candidate) => false;

    public virtual void AdjustHeuristic(CSE_Candidate candidate)
    {
    }

    public virtual void Cleanup()
    {
#if DEBUG
        m_sequence.Add(0);
#endif
    }

    public virtual string Name() => "Common CSE Heuristic";

#if DEBUG
    public virtual void Announce() => Globals.JITDUMP($"{Name()}\n");

    public virtual void DumpMetrics()
    {
        Globals.jitprintf($" {Name()} seq ");
        for (var index = 0; index < m_sequence.Count; index++)
        {
            Globals.jitprintf($"{(index == 0 ? "" : ",")}{m_sequence[index]}");
        }
    }
#endif
}

public sealed partial class CSE_Heuristic
{
    public override string Name() => "Standard CSE Heuristic";
}
