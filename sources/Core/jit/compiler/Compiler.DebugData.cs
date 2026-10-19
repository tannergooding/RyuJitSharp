// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RyuJitSharp;

[System.Flags]
internal enum LocalDebugFlags : ushort
{
    None = 0,
    TrackedWithoutIndex = 1 << 0,
    ClassInfoUpdated = 1 << 1,
    IsHoist = 1 << 2,
    IsMultiDefCse = 1 << 3,
    KeepType = 1 << 4,
    NoLclFldStress = 1 << 5,
    DefinedViaAddress = 1 << 6,
    UnusedStruct = 1 << 7,
    UndoneStructPromotion = 1 << 8,
}

[System.Flags]
internal enum LocalDebugDataCopyFlags : byte
{
    None = 0,
    DoNotEnregisterReason = 1 << 0,
    AddressExposedReason = 1 << 1,
    KeepType = 1 << 2,
    DefinedViaAddress = 1 << 3,
    SingleDefDisqualifyReason = 1 << 4,
}

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
        internal LocalDebugData[]? LocalVariables;
        internal InlineArrayCompilerStressCount<byte> CompActiveStressModes;
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
        internal LocalDebugFlags Flags;
        internal DoNotEnregisterReason DoNotEnregisterReason;
        internal AddressExposedReason AddressExposedReason;
        internal byte EncodedSingleDefDisqualifyReason;

        internal readonly bool IsEmpty =>
            (Flags is LocalDebugFlags.None) &&
            (DoNotEnregisterReason is DoNotEnregisterReason.None) &&
            (AddressExposedReason is AddressExposedReason.NONE) &&
            (EncodedSingleDefDisqualifyReason == 0);
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
        return Configuration.DEBUG && GetLocalDebugFlag(localNumber, LocalDebugFlags.TrackedWithoutIndex);
    }

    [Conditional("DEBUG")]
    internal void SetLocalVarTrackedWithoutIndex(int localNumber, bool value)
    {
        SetLocalDebugFlag(localNumber, LocalDebugFlags.TrackedWithoutIndex, value);
    }

    internal bool GetLocalDebugFlag(int localNumber, LocalDebugFlags flag)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Local debug flags are only available in Debug builds.");
        }

        return (GetLocalDebugData(localNumber).Flags & flag) != 0;
    }

    internal bool GetLocalDebugFlag(in LclVarDsc local, LocalDebugFlags flag)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Local debug flags are only available in Debug builds.");
        }

        return GetLocalDebugFlag(lvaGetLclNum(in local), flag);
    }

    internal bool GetLocalClassInfoUpdated(int localNumber)
        => Configuration.DEBUG && GetLocalDebugFlag(localNumber, LocalDebugFlags.ClassInfoUpdated);

    internal bool GetLocalClassInfoUpdated(in LclVarDsc local)
        => Configuration.DEBUG && GetLocalDebugFlag(in local, LocalDebugFlags.ClassInfoUpdated);

    internal void SetLocalDebugFlag(int localNumber, LocalDebugFlags flag, bool value)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Local debug flags are only available in Debug builds.");
        }

        var localData = GetLocalDebugData(localNumber);
        localData.Flags = (localData.Flags & ~flag) | (value ? flag : LocalDebugFlags.None);
        SetLocalDebugData(localNumber, localData);
    }

    internal void SetLocalDebugFlag(in LclVarDsc local, LocalDebugFlags flag, bool value)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Local debug flags are only available in Debug builds.");
        }

        SetLocalDebugFlag(lvaGetLclNum(in local), flag, value);
    }

    internal DoNotEnregisterReason GetLocalDoNotEnregisterReason(int localNumber)
        => Configuration.DEBUG ? GetLocalDebugData(localNumber).DoNotEnregisterReason : DoNotEnregisterReason.None;

    internal DoNotEnregisterReason GetLocalDoNotEnregisterReason(in LclVarDsc local)
        => Configuration.DEBUG ? GetLocalDoNotEnregisterReason(lvaGetLclNum(in local)) : DoNotEnregisterReason.None;

    internal void SetLocalDoNotEnregisterReason(int localNumber, DoNotEnregisterReason reason)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("The do-not-enregister reason is only available in Debug builds.");
        }

        var localData = GetLocalDebugData(localNumber);
        localData.DoNotEnregisterReason = reason;
        SetLocalDebugData(localNumber, localData);
    }

    internal AddressExposedReason GetLocalAddressExposedReason(int localNumber)
        => Configuration.DEBUG ? GetLocalDebugData(localNumber).AddressExposedReason : AddressExposedReason.NONE;

    internal AddressExposedReason GetLocalAddressExposedReason(in LclVarDsc local)
        => Configuration.DEBUG ? GetLocalAddressExposedReason(lvaGetLclNum(in local)) : AddressExposedReason.NONE;

    internal void SetLocalAddressExposedReason(int localNumber, AddressExposedReason reason)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Address-exposed reason is only available in Debug builds.");
        }

        var localData = GetLocalDebugData(localNumber);
        localData.AddressExposedReason = reason;
        SetLocalDebugData(localNumber, localData);
    }

    internal void SetLocalAddressExposed(int localNumber, bool value, AddressExposedReason reason)
    {
        lvaGetDesc(localNumber).SetAddressExposed(value);
        if (Configuration.DEBUG)
        {
            SetLocalAddressExposedReason(localNumber, reason);
        }
    }

    internal void SetLocalAddressExposed(in LclVarDsc local, bool value, AddressExposedReason reason)
        => SetLocalAddressExposed(lvaGetLclNum(in local), value, reason);

    internal byte GetLocalSingleDefDisqualifyReason(int localNumber)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("The single-definition disqualification reason is only available in Debug builds.");
        }

        return (byte)(GetLocalDebugData(localNumber).EncodedSingleDefDisqualifyReason ^ 'H');
    }

    internal byte GetLocalSingleDefDisqualifyReason(in LclVarDsc local)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("The single-definition disqualification reason is only available in Debug builds.");
        }

        return GetLocalSingleDefDisqualifyReason(lvaGetLclNum(in local));
    }

    internal void SetLocalSingleDefDisqualifyReason(int localNumber, byte reason)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("The single-definition disqualification reason is only available in Debug builds.");
        }

        var localData = GetLocalDebugData(localNumber);
        localData.EncodedSingleDefDisqualifyReason = (byte)(reason ^ 'H');
        SetLocalDebugData(localNumber, localData);
    }

    internal void SetLocalSingleDefDisqualifyReason(in LclVarDsc local, byte reason)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("The single-definition disqualification reason is only available in Debug builds.");
        }

        SetLocalSingleDefDisqualifyReason(lvaGetLclNum(in local), reason);
    }

    internal void CopyLocalDebugData(int sourceLocalNumber, int destinationLocalNumber, LocalDebugDataCopyFlags fields)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("Compiler debug data is only available in Debug builds.");
        }

        var source = GetLocalDebugData(sourceLocalNumber);
        var destination = GetLocalDebugData(destinationLocalNumber);

        if ((fields & LocalDebugDataCopyFlags.DoNotEnregisterReason) != 0)
        {
            destination.DoNotEnregisterReason = source.DoNotEnregisterReason;
        }

        if ((fields & LocalDebugDataCopyFlags.AddressExposedReason) != 0)
        {
            destination.AddressExposedReason = source.AddressExposedReason;
        }

        if ((fields & LocalDebugDataCopyFlags.KeepType) != 0)
        {
            destination.Flags = (destination.Flags & ~LocalDebugFlags.KeepType) |
                (source.Flags & LocalDebugFlags.KeepType);
        }

        if ((fields & LocalDebugDataCopyFlags.DefinedViaAddress) != 0)
        {
            destination.Flags = (destination.Flags & ~LocalDebugFlags.DefinedViaAddress) |
                (source.Flags & LocalDebugFlags.DefinedViaAddress);
        }

        if ((fields & LocalDebugDataCopyFlags.SingleDefDisqualifyReason) != 0)
        {
            destination.EncodedSingleDefDisqualifyReason = source.EncodedSingleDefDisqualifyReason;
        }

        SetLocalDebugData(destinationLocalNumber, destination);
    }

    internal void CopyLocalDebugData(in LclVarDsc source, in LclVarDsc destination, LocalDebugDataCopyFlags fields)
        => CopyLocalDebugData(lvaGetLclNum(in source), lvaGetLclNum(in destination), fields);

    [Conditional("DEBUG")]
    internal void ResetLocalDebugData(int localNumber)
        => SetLocalDebugData(localNumber, default);

    private LocalDebugData GetLocalDebugData(int localNumber)
    {
        var owner = GetLocalDebugDataOwner();
        var localVariables = owner.GetExistingDebugData()?.LocalVariables;
        return localVariables is not null && (uint)localNumber < (uint)localVariables.Length
            ? localVariables[localNumber]
            : default;
    }

    private void SetLocalDebugData(int localNumber, LocalDebugData localData)
    {
        var owner = GetLocalDebugDataOwner();
        var data = owner.GetExistingDebugData();
        var localVariables = data?.LocalVariables;

        if (localVariables is not null && (uint)localNumber < (uint)localVariables.Length)
        {
            localVariables[localNumber] = localData;
            return;
        }

        if (localData.IsEmpty)
        {
            return;
        }

        System.ArgumentOutOfRangeException.ThrowIfNegative(localNumber);

        data ??= owner.GetDebugData();

        if (localVariables is null)
        {
            localVariables = new LocalDebugData[System.Math.Max(4, localNumber + 1)];
        }
        else
        {
            var newLength = System.Math.Max(localVariables.Length * 2, localNumber + 1);
            System.Array.Resize(ref localVariables, newLength);
        }

        data.LocalVariables = localVariables;
        localVariables[localNumber] = localData;
    }

    private Compiler GetLocalDebugDataOwner()
    {
        var owner = this;
        while (owner.compIsForInlining)
        {
            owner = owner.impInlineInfo.InlinerCompiler;
        }

        return owner;
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
