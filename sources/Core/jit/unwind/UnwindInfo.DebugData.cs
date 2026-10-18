// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
    private sealed class DebugData
    {
        internal bool AddingNOP;
    }

    private static class DebugDataStorage
    {
        internal static readonly ConditionalWeakTable<UnwindInfo, DebugData> Table = [];
    }

    public bool uwiAddingNOP
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                throw new InvalidOperationException("Unwind NOP diagnostics are only available in Debug builds.");
            }

            return GetExistingDebugData()?.AddingNOP ?? false;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                throw new InvalidOperationException("Unwind NOP diagnostics are only available in Debug builds.");
            }

            if (value)
            {
                GetDebugData().AddingNOP = true;
            }
            else if (GetExistingDebugData() is { } debugData)
            {
                debugData.AddingNOP = false;
            }
        }
    }

    private DebugData GetDebugData()
    {
        return DebugDataStorage.Table.GetValue(this, static _ => new DebugData());
    }

    private DebugData? GetExistingDebugData()
    {
        return DebugDataStorage.Table.TryGetValue(this, out var debugData) ? debugData : null;
    }
}
