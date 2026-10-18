// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class DebugData
    {
        internal bool AsciiTrees;
        internal GenTree? AssertionPropCurrentTree;
        internal long CompCyclesAtEndOfInlining;
        internal long CompCycles;
        internal int InlineMethodXmlFilePosition;
        internal int ExpensiveDebugCheckLevel;
        internal Dictionary<int, LocalDebugData>? LocalVariables;
        internal GenTree? LastConsumedNode;
        internal bool TreesBeforeAfterMorph;
        internal int BlockTraversalLabel = 1;
        internal bool ImplicitByRefLclFldsStale;
        internal bool CompDebugBreak;
        internal bool JitFallbackCompile;
        internal bool PgoDeferredInconsistency;
        internal int MorphNum;
        internal bool SafeBasicBlockCreation = true;
        internal bool SafeFlowEdgeCreation = true;
        internal bool StackCheckOnRet;
        internal bool StackCheckOnCall;
        internal bool CodeGenDone;
        internal NodeToTestDataMap? NodeTestData;
        internal int LoopHoistCSEClass = FIRST_LOOP_HOIST_CSE_CLASS;
        internal long NumStatementLinksTraversed;
        internal nint SizeEstimate;
        internal nint CycleEstimate;
        internal bool PoisoningAnyImplicitByrefs;
        internal int ReturnSpCheck = BAD_VAR_NUM;
        internal int CallSpCheck = BAD_VAR_NUM;
    }

    private struct LocalDebugData
    {
        internal bool TrackedWithoutIndex;

        internal readonly bool IsEmpty => !TrackedWithoutIndex;
    }

    private static class DebugDataStorage
    {
        internal static readonly ConditionalWeakTable<Compiler, DebugData> Table = [];
    }

    /// <summary>If true, dump trees using only ASCII characters.</summary>
    public bool asciiTrees
    {
        get
        {
            return GetDebugData().AsciiTrees;
        }

        set
        {
            GetDebugData().AsciiTrees = value;
        }
    }

    /// <summary>If true, print trees before and after morphing.</summary>
    public bool treesBeforeAfterMorph
    {
        get
        {
            return Configuration.DEBUG && (GetExistingDebugData()?.TreesBeforeAfterMorph ?? false);
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().TreesBeforeAfterMorph = value;
            }
        }
    }

    /// <summary>This counts morphed trees to label their diagnostic output.</summary>
    public int morphNum
    {
        get
        {
            return Configuration.DEBUG ? (GetExistingDebugData()?.MorphNum ?? 0) : 0;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().MorphNum = value;
            }
        }
    }

    public int expensiveDebugCheckLevel
    {
        get
        {
            return GetExistingDebugData()?.ExpensiveDebugCheckLevel ?? 0;
        }

        set
        {
            GetDebugData().ExpensiveDebugCheckLevel = value;
        }
    }

    /// <summary>Indicates that code generation has completed for the method.</summary>
    public bool compCodeGenDone
    {
        get
        {
            return GetExistingDebugData()?.CodeGenDone ?? false;
        }

        set
        {
            GetDebugData().CodeGenDone = value;
        }
    }

    public bool compDebugBreak
    {
        get
        {
            return Configuration.DEBUG && (GetExistingDebugData()?.CompDebugBreak ?? false);
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                throw new System.InvalidOperationException("Compiler debug-break state is only available in Debug builds.");
            }

            if (value)
            {
                GetDebugData().CompDebugBreak = true;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.CompDebugBreak = false;
            }
        }
    }

    public bool fgPgoDeferredInconsistency
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                throw new System.InvalidOperationException("Deferred profile inconsistency state is only available in Debug builds.");
            }

            return GetExistingDebugData()?.PgoDeferredInconsistency ?? false;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                throw new System.InvalidOperationException("Deferred profile inconsistency state is only available in Debug builds.");
            }

            if (value)
            {
                GetDebugData().PgoDeferredInconsistency = true;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.PgoDeferredInconsistency = false;
            }
        }
    }

    /// <summary>Are we doing a fallback compile?</summary>
    public bool jitFallbackCompile
    {
        get
        {
            return Configuration.DEBUG && (GetExistingDebugData()?.JitFallbackCompile ?? false);
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().JitFallbackCompile = value;
            }
        }
    }

    private NodeToTestDataMap? _nodeTestData
    {
        get
        {
            return GetExistingDebugData()?.NodeTestData;
        }

        set
        {
            GetDebugData().NodeTestData = value;
        }
    }

    private int _loopHoistCSEClass
    {
        get
        {
            return GetExistingDebugData()?.LoopHoistCSEClass ?? FIRST_LOOP_HOIST_CSE_CLASS;
        }

        set
        {
            GetDebugData().LoopHoistCSEClass = value;
        }
    }

    /// <summary>Number of statement links traversed while performing debug checks.</summary>
    public ref long compNumStatementLinksTraversed => ref GetDebugData().NumStatementLinksTraversed;

    /// <summary>The estimated size of the method as per `gtSetEvalOrder`.</summary>
    public nint compSizeEstimate
    {
        get
        {
            return GetExistingDebugData()?.SizeEstimate ?? 0;
        }

        set
        {
            GetDebugData().SizeEstimate = value;
        }
    }

    /// <summary>The estimated cycle count of the method as per `gtSetEvalOrder`.</summary>
    public nint compCycleEstimate
    {
        get
        {
            return GetExistingDebugData()?.CycleEstimate ?? 0;
        }

        set
        {
            GetDebugData().CycleEstimate = value;
        }
    }

    /// <summary>Importer inserted IR before returns to poison implicit byrefs.</summary>
    public bool compPoisoningAnyImplicitByrefs
    {
        get
        {
            return GetExistingDebugData()?.PoisoningAnyImplicitByrefs ?? false;
        }

        set
        {
            GetDebugData().PoisoningAnyImplicitByrefs = value;
        }
    }

    private int NextBlockTraversalLabel => Interlocked.Increment(ref GetDebugData().BlockTraversalLabel);

    public bool fgImplicitByRefLclFldsStale
    {
        get
        {
            return GetExistingDebugData()?.ImplicitByRefLclFldsStale ?? false;
        }

        set
        {
            if (value)
            {
                GetDebugData().ImplicitByRefLclFldsStale = true;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.ImplicitByRefLclFldsStale = false;
            }
        }
    }

    internal bool GetLocalVarTrackedWithoutIndex(int localNumber)
    {
        var data = GetDebugData();
        return (data.LocalVariables is not null) &&
               data.LocalVariables.TryGetValue(localNumber, out var localData) &&
               localData.TrackedWithoutIndex;
    }

    [Conditional("DEBUG")]
    internal void SetLocalVarTrackedWithoutIndex(int localNumber, bool value)
    {
        var data = GetDebugData();
        var localData = default(LocalDebugData);

        if (data.LocalVariables is not null)
        {
            _ = data.LocalVariables.TryGetValue(localNumber, out localData);
        }

        localData.TrackedWithoutIndex = value;
        SetLocalDebugData(data, localNumber, localData);
    }

    internal GenTree? LastConsumedNode => GetExistingDebugData()?.LastConsumedNode;

    [Conditional("DEBUG")]
    internal void SetOptAssertionPropCurrentTree(GenTree tree)
    {
        GetDebugData().AssertionPropCurrentTree = tree;
    }

    [Conditional("DEBUG")]
    internal void SetLastConsumedNode(GenTree? node)
    {
        if (node is null)
        {
            if (GetExistingDebugData() is { } data)
            {
                data.LastConsumedNode = null;
            }
        }
        else
        {
            GetDebugData().LastConsumedNode = node;
        }
    }

    /// <summary>When false, assert when creating a new basic block.</summary>
    public bool fgSafeBasicBlockCreation
    {
        get
        {
            return !Configuration.DEBUG || (GetExistingDebugData()?.SafeBasicBlockCreation ?? true);
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                throw new System.InvalidOperationException("Basic-block creation state is only available in Debug builds.");
            }

            if (value)
            {
                if (GetExistingDebugData() is { } data)
                {
                    data.SafeBasicBlockCreation = true;
                }
            }
            else
            {
                GetDebugData().SafeBasicBlockCreation = false;
            }
        }
    }

    /// <summary>When false, assert when creating a new flow edge.</summary>
    public bool fgSafeFlowEdgeCreation
    {
        get
        {
            return !Configuration.DEBUG || (GetExistingDebugData()?.SafeFlowEdgeCreation ?? true);
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                throw new System.InvalidOperationException("Flow-edge creation state is only available in Debug builds.");
            }

            if (value)
            {
                if (GetExistingDebugData() is { } data)
                {
                    data.SafeFlowEdgeCreation = true;
                }
            }
            else
            {
                GetDebugData().SafeFlowEdgeCreation = false;
            }
        }
    }

    private static void SetLocalDebugData(DebugData data, int localNumber, LocalDebugData localData)
    {
        if (localData.IsEmpty)
        {
            _ = data.LocalVariables?.Remove(localNumber);
            return;
        }

        data.LocalVariables ??= [];
        data.LocalVariables[localNumber] = localData;
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Compiler debug data is only available in Debug builds.");
        }

        return DebugDataStorage.Table.GetValue(this, static _ => new DebugData());
    }

    private DebugData? GetExistingDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Compiler debug data is only available in Debug builds.");
        }

        return DebugDataStorage.Table.TryGetValue(this, out var data) ? data : null;
    }

    internal bool compStackCheckOnRet
    {
        get
        {
            return GetExistingDebugData()?.StackCheckOnRet ?? false;
        }

        set
        {
            if (value)
            {
                GetDebugData().StackCheckOnRet = true;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.StackCheckOnRet = false;
            }
        }
    }

    internal bool compStackCheckOnCall
    {
        get
        {
            return GetExistingDebugData()?.StackCheckOnCall ?? false;
        }

        set
        {
            if (value)
            {
                GetDebugData().StackCheckOnCall = true;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.StackCheckOnCall = false;
            }
        }
    }

    internal int lvaReturnSpCheck
    {
        get
        {
            return GetExistingDebugData()?.ReturnSpCheck ?? BAD_VAR_NUM;
        }

        set
        {
            if (value != BAD_VAR_NUM)
            {
                GetDebugData().ReturnSpCheck = value;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.ReturnSpCheck = BAD_VAR_NUM;
            }
        }
    }

    internal int lvaCallSpCheck
    {
        get
        {
            return GetExistingDebugData()?.CallSpCheck ?? BAD_VAR_NUM;
        }

        set
        {
            if (value != BAD_VAR_NUM)
            {
                GetDebugData().CallSpCheck = value;
            }
            else if (GetExistingDebugData() is { } data)
            {
                data.CallSpCheck = BAD_VAR_NUM;
            }
        }
    }

    internal long InlineDiagnosticCompCycles
    {
        get
        {
            return GetDebugData().CompCycles;
        }

        set
        {
            GetDebugData().CompCycles = value;
        }
    }

    internal long InlineDiagnosticCompCyclesAtEndOfInlining
    {
        get
        {
            return GetDebugData().CompCyclesAtEndOfInlining;
        }

        set
        {
            GetDebugData().CompCyclesAtEndOfInlining = value;
        }
    }

    internal int InlineMethodXmlFilePosition
    {
        get
        {
            return GetDebugData().InlineMethodXmlFilePosition;
        }

        set
        {
            GetDebugData().InlineMethodXmlFilePosition = value;
        }
    }

    // The upstream counter is already in microseconds, but inline diagnostics scale it again.
    internal uint InlineDiagnosticJitTimeMicroseconds
    {
        get
        {
            var compCycles = InlineDiagnosticCompCycles;
            if (compCycles <= 0)
            {
                return 0;
            }

            var microseconds = ((double)compCycles / Stopwatch.Frequency) * 1_000_000;
            return unchecked((uint)microseconds);
        }
    }
}
