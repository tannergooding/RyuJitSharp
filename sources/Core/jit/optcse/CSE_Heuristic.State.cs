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

    private protected Compiler.DebugData.CSEHeuristicDebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CSEHeuristicDiagnosticsAreOnlyAvailableInDebugBuilds);
            return default!;
        }

        return Compiler.DebugData.Get(this);
    }

    protected List<uint> GetSequence()
    {
        var debugData = GetDebugData();
        return debugData.Sequence ??= [];
    }

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
            GetSequence().Add(0);
        }
    }

    public virtual string Name() => "Common CSE Heuristic";

    protected static void EnsureDebugDiagnosticsAvailable()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CSEHeuristicDiagnosticsAreOnlyAvailableInDebugBuilds);
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
        var sequence = GetSequence();
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
