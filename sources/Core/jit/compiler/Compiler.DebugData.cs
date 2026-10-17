// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class DebugData
    {
        internal bool AsciiTrees;
        internal long CompCyclesAtEndOfInlining;
        internal long CompCycles;
        internal int InlineMethodXmlFilePosition;
        internal Dictionary<int, LocalDebugData>? LocalVariables;
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
