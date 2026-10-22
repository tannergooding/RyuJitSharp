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
            global::System.Collections.Generic.List<uint> __jitExceptionReceiver520;
            var __jitExceptionCoalesceLeft521 = m_sequence;
            if (__jitExceptionCoalesceLeft521 != null)
            {
                __jitExceptionReceiver520 = __jitExceptionCoalesceLeft521;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CSEHeuristicMetricsAreOnlyAvailableInDebugBuilds);
                __jitExceptionReceiver520 = default;
            }
            __jitExceptionReceiver520.Add(0);
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
        global::System.Collections.Generic.List<uint> __jitExceptionResult522;
        var __jitExceptionCoalesceLeft523 = m_sequence;
        if (__jitExceptionCoalesceLeft523 != null)
        {
            __jitExceptionResult522 = __jitExceptionCoalesceLeft523;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CSEHeuristicMetricsHaveNotBeenInitialized);
            __jitExceptionResult522 = default;
        }
        var sequence = __jitExceptionResult522;
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
