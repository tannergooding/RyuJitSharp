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
        private static ConditionalWeakTable<LinearScan, AllocationDebugData>? s_instances;

        internal static int EmptyColumnWidth;
        internal static ConfigMethodRange StressRegsRange;

        internal uint MaxNodeLocation;
        internal uint ConsecutiveRegistersLocation;
        internal RefPosition? ActiveRefPosition;
        internal regMaskTP Registers;
        internal bool DumpFormatInitialized;
        internal regMaskTP LastDumpRegisters;
        internal RefPosition? LastDumpRefPosition;
        internal int DumpLastUsedRegNumIndex;
        internal int DumpRegColumnWidth;
        internal int DumpNodeLocationWidth;
        internal int DumpRefPositionWidth;
        internal int DumpShortRefPositionWidth;
        internal int DumpTableIndent;
        internal int DumpBbNumWidth;
        internal int DumpPredBbNumWidth;
        internal int DumpRowsSinceTitle;
        internal string DumpColumnSeparator = "";
        internal string DumpLine = "";
        internal string DumpMiddleBox = "";
        internal string DumpRightBox = "";

        internal static AllocationDebugData Get(LinearScan linearScan)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(linearScan, static _ => new AllocationDebugData());
        }
    }

    private uint _maxNodeLocation
    {
        get
        {
            return GetAllocationDebugData().MaxNodeLocation;
        }

        set
        {
            GetAllocationDebugData().MaxNodeLocation = value;
        }
    }

    private RefPosition? _activeRefPosition
    {
        get
        {
            return GetAllocationDebugData().ActiveRefPosition;
        }

        set
        {
            GetAllocationDebugData().ActiveRefPosition = value;
        }
    }

    private uint _consecutiveRegistersLocation
    {
        get
        {
            return GetAllocationDebugData().ConsecutiveRegistersLocation;
        }

        set
        {
            GetAllocationDebugData().ConsecutiveRegistersLocation = value;
        }
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

    private bool _allocationDumpFormatInitialized
    {
        get
        {
            return GetAllocationDebugData().DumpFormatInitialized;
        }

        set
        {
            GetAllocationDebugData().DumpFormatInitialized = value;
        }
    }

    private regMaskTP _lastAllocationDumpRegisters
    {
        get
        {
            return GetAllocationDebugData().LastDumpRegisters;
        }

        set
        {
            GetAllocationDebugData().LastDumpRegisters = value;
        }
    }

    private RefPosition? _lastAllocationDumpRefPosition
    {
        get
        {
            return GetAllocationDebugData().LastDumpRefPosition;
        }

        set
        {
            GetAllocationDebugData().LastDumpRefPosition = value;
        }
    }

    private int _allocationDumpLastUsedRegNumIndex
    {
        get
        {
            return GetAllocationDebugData().DumpLastUsedRegNumIndex;
        }

        set
        {
            GetAllocationDebugData().DumpLastUsedRegNumIndex = value;
        }
    }

    private int _allocationDumpRegColumnWidth
    {
        get
        {
            return GetAllocationDebugData().DumpRegColumnWidth;
        }

        set
        {
            GetAllocationDebugData().DumpRegColumnWidth = value;
        }
    }

    private int _allocationDumpNodeLocationWidth
    {
        get
        {
            return GetAllocationDebugData().DumpNodeLocationWidth;
        }

        set
        {
            GetAllocationDebugData().DumpNodeLocationWidth = value;
        }
    }

    private int _allocationDumpRefPositionWidth
    {
        get
        {
            return GetAllocationDebugData().DumpRefPositionWidth;
        }

        set
        {
            GetAllocationDebugData().DumpRefPositionWidth = value;
        }
    }

    private int _allocationDumpShortRefPositionWidth
    {
        get
        {
            return GetAllocationDebugData().DumpShortRefPositionWidth;
        }

        set
        {
            GetAllocationDebugData().DumpShortRefPositionWidth = value;
        }
    }

    private int _allocationDumpTableIndent
    {
        get
        {
            return GetAllocationDebugData().DumpTableIndent;
        }

        set
        {
            GetAllocationDebugData().DumpTableIndent = value;
        }
    }

    private int _allocationDumpBbNumWidth
    {
        get
        {
            return GetAllocationDebugData().DumpBbNumWidth;
        }

        set
        {
            GetAllocationDebugData().DumpBbNumWidth = value;
        }
    }

    private int _allocationDumpPredBbNumWidth
    {
        get
        {
            return GetAllocationDebugData().DumpPredBbNumWidth;
        }

        set
        {
            GetAllocationDebugData().DumpPredBbNumWidth = value;
        }
    }

    private int _allocationDumpRowsSinceTitle
    {
        get
        {
            return GetAllocationDebugData().DumpRowsSinceTitle;
        }

        set
        {
            GetAllocationDebugData().DumpRowsSinceTitle = value;
        }
    }

    private string _allocationDumpColumnSeparator
    {
        get
        {
            return GetAllocationDebugData().DumpColumnSeparator;
        }

        set
        {
            GetAllocationDebugData().DumpColumnSeparator = value;
        }
    }

    private string _allocationDumpLine
    {
        get
        {
            return GetAllocationDebugData().DumpLine;
        }

        set
        {
            GetAllocationDebugData().DumpLine = value;
        }
    }

    private string _allocationDumpMiddleBox
    {
        get
        {
            return GetAllocationDebugData().DumpMiddleBox;
        }

        set
        {
            GetAllocationDebugData().DumpMiddleBox = value;
        }
    }

    private string _allocationDumpRightBox
    {
        get
        {
            return GetAllocationDebugData().DumpRightBox;
        }

        set
        {
            GetAllocationDebugData().DumpRightBox = value;
        }
    }

    private static int s_allocationDumpEmptyColumnWidth
    {
        get
        {
            return AllocationDebugData.EmptyColumnWidth;
        }

        set
        {
            AllocationDebugData.EmptyColumnWidth = value;
        }
    }

    private AllocationDebugData GetAllocationDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("Linear-scan allocation diagnostic state is only available in Debug builds.");
        }

        return AllocationDebugData.Get(this);
    }
}
