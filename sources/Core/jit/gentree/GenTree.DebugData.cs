// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    private int _treeId
    {
        get
        {
            return GetDebugData()?.TreeId ?? 0;
        }

        set
        {
            if (GetDebugData() is { } debugData)
            {
                debugData.TreeId = value;
            }
        }
    }

    internal int _seqNum
    {
        get
        {
            return GetDebugData()?.SequenceNumber ?? 0;
        }

        set
        {
            if (GetDebugData() is { } debugData)
            {
                debugData.SequenceNumber = value;
            }
        }
    }

    private int _useNum
    {
        get
        {
            return GetDebugData()?.UseNumber ?? 0;
        }

        set
        {
            if (GetDebugData() is { } debugData)
            {
                debugData.UseNumber = value;
            }
        }
    }

    internal bool _costsInitialized
    {
        get
        {
            return GetDebugData()?.CostsInitialized ?? false;
        }

        set
        {
            if (GetDebugData() is { } debugData)
            {
                debugData.CostsInitialized = value;
            }
        }
    }

    private genRegTag _regTag
    {
        get
        {
            return GetDebugData()?.RegTag ?? default;
        }

        set
        {
            if (GetDebugData() is { } debugData)
            {
                debugData.RegTag = value;
            }
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

    private Compiler.DebugData.GenTreeDebugData? GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.GenTreeDebugDataIsOnlyAvailableInDebugBuilds);
            return null;
        }

        return Compiler.DebugData.Get(this);
    }

    private void CopyDebugDataFrom(GenTree source, bool copyDerivedDebugData = false)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        var sourceData = Compiler.DebugData.Get(source);
        var targetData = Compiler.DebugData.Get(this);
        targetData.OperSave = sourceData.OperSave;
        targetData.Flags = sourceData.Flags;
        targetData.TreeId = sourceData.TreeId;
        targetData.SequenceNumber = sourceData.SequenceNumber;
        targetData.UseNumber = sourceData.UseNumber;
        targetData.CostsInitialized = sourceData.CostsInitialized;
        targetData.RegTag = sourceData.RegTag;
        targetData.CallDebugFlags = sourceData.CallDebugFlags;
        targetData.InlineObservation = sourceData.InlineObservation;
        targetData.RawILOffset = sourceData.RawILOffset;

        if (copyDerivedDebugData)
        {
            targetData.TargetHandle = sourceData.TargetHandle;
            targetData.LocalILOffset = sourceData.LocalILOffset;
            targetData.StmtLastILOffset = sourceData.StmtLastILOffset;
        }
    }

    internal nint GetDebugTargetHandle()
    {
        return GetDebugData()?.TargetHandle ?? 0;
    }

    internal void SetDebugTargetHandle(nint value)
    {
        if (GetDebugData() is { } debugData)
        {
            debugData.TargetHandle = value;
        }
    }

    internal IL_OFFSET GetDebugLocalILOffset()
    {
        return GetDebugData()?.LocalILOffset ?? BAD_IL_OFFSET;
    }

    internal void SetDebugLocalILOffset(IL_OFFSET value)
    {
        if (GetDebugData() is { } debugData)
        {
            debugData.LocalILOffset = value;
        }
    }
}
