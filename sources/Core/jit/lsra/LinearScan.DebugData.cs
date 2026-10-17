// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private sealed class AllocationDebugData
    {
        internal regMaskTP Registers;
    }

    private static class AllocationDebugDataStorage
    {
        internal static readonly ConditionalWeakTable<LinearScan, AllocationDebugData> Table = [];
    }

    private regMaskTP _allocationDumpRegisters
    {
        get
        {
            return GetAllocationDebugData().Registers;
        }

        set
        {
            GetAllocationDebugData().Registers = value;
        }
    }

    private AllocationDebugData GetAllocationDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("Linear-scan allocation diagnostic state is only available in Debug builds.");
        }

        return AllocationDebugDataStorage.Table.GetValue(this, static _ => new AllocationDebugData());
    }
}
