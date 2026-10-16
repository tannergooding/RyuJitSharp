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
    protected readonly List<uint>? m_sequence = Configuration.DEBUG ? [] : null;

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
        if (Configuration.DEBUG)
        {
            (m_sequence ?? throw new System.InvalidOperationException(
                "CSE heuristic metrics are only available in Debug builds.")).Add(0);
        }
    }

    public virtual string Name() => "Common CSE Heuristic";

    protected static void EnsureDebugDiagnosticsAvailable()
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("CSE heuristic diagnostics are only available in Debug builds.");
        }
    }

    public virtual void Announce()
    {
        EnsureDebugDiagnosticsAvailable();
        Globals.JITDUMP($"{Name()}\n");
    }

    public virtual void DumpMetrics()
    {
        EnsureDebugDiagnosticsAvailable();
        var sequence = m_sequence
            ?? throw new System.InvalidOperationException("CSE heuristic metrics have not been initialized.");
        Globals.jitprintf($" {Name()} seq ");
        for (var index = 0; index < sequence.Count; index++)
        {
            Globals.jitprintf($"{(index == 0 ? "" : ",")}{sequence[index]}");
        }
    }
}

public sealed partial class CSE_Heuristic
{
    public override string Name() => "Standard CSE Heuristic";
}
