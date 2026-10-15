// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class GenTree
{
    private sealed class DebugData
    {
        internal genTreeOps OperSave;
        internal GenTreeDebugFlags Flags;
        internal int TreeId;
        internal int SequenceNumber;
        internal int UseNumber;
        internal bool CostsInitialized;
        internal genRegTag RegTag;
        internal nint TargetHandle;
        internal IL_OFFSET LocalILOffset = BAD_IL_OFFSET;
    }

    private static class DebugDataStorage
    {
        internal static readonly ConditionalWeakTable<GenTree, DebugData> Table = [];
    }

    private int _treeId
    {
        get
        {
            return GetDebugData().TreeId;
        }

        set
        {
            GetDebugData().TreeId = value;
        }
    }

    internal int _seqNum
    {
        get
        {
            return GetDebugData().SequenceNumber;
        }

        set
        {
            GetDebugData().SequenceNumber = value;
        }
    }

    private int _useNum
    {
        get
        {
            return GetDebugData().UseNumber;
        }

        set
        {
            GetDebugData().UseNumber = value;
        }
    }

    internal bool _costsInitialized
    {
        get
        {
            return GetDebugData().CostsInitialized;
        }

        set
        {
            GetDebugData().CostsInitialized = value;
        }
    }

    private genRegTag _regTag
    {
        get
        {
            return GetDebugData().RegTag;
        }

        set
        {
            GetDebugData().RegTag = value;
        }
    }

    public genRegTag RegTag
    {
        get
        {
            var regTag = _regTag;
            assert(regTag is GT_REGTAG_NONE or GT_REGTAG_REG);
            return regTag;
        }
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("GenTree debug data is only available in Debug builds.");
        }

        return DebugDataStorage.Table.GetValue(this, static _ => new DebugData());
    }

    private void CopyDebugDataFrom(GenTree source, bool copyDerivedDebugData = false)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        var table = DebugDataStorage.Table;

        if (!table.TryGetValue(source, out var sourceData))
        {
            throw new InvalidOperationException("The source GenTree has no Debug data.");
        }

        var targetData = table.GetValue(this, static _ => new DebugData());
        targetData.OperSave = sourceData.OperSave;
        targetData.Flags = sourceData.Flags;
        targetData.TreeId = sourceData.TreeId;
        targetData.SequenceNumber = sourceData.SequenceNumber;
        targetData.UseNumber = sourceData.UseNumber;
        targetData.CostsInitialized = sourceData.CostsInitialized;
        targetData.RegTag = sourceData.RegTag;

        if (copyDerivedDebugData)
        {
            targetData.TargetHandle = sourceData.TargetHandle;
            targetData.LocalILOffset = sourceData.LocalILOffset;
        }
    }

    internal nint GetDebugTargetHandle()
    {
        return GetDebugData().TargetHandle;
    }

    internal void SetDebugTargetHandle(nint value)
    {
        GetDebugData().TargetHandle = value;
    }

    internal IL_OFFSET GetDebugLocalILOffset()
    {
        return GetDebugData().LocalILOffset;
    }

    internal void SetDebugLocalILOffset(IL_OFFSET value)
    {
        GetDebugData().LocalILOffset = value;
    }
}
