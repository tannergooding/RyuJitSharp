// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class insGroup
{
    public insGroup? igSelf
    {
        get
        {
            return GetDebugData().Self;
        }

        set
        {
            GetDebugData().Self = value;
        }
    }

    public BasicBlock? lastGeneratedBlock
    {
        get
        {
            return GetDebugData().LastGeneratedBlock;
        }

        set
        {
            GetDebugData().LastGeneratedBlock = value;
        }
    }

    public List<BasicBlock> igBlocks
    {
        get
        {
            var data = GetDebugData();
            return data.Blocks ??= [];
        }
    }

    public nuint igDataSize
    {
        get
        {
            return GetDebugData().DataSize;
        }

        set
        {
            GetDebugData().DataSize = value;
        }
    }

    private Compiler.DebugData.InstructionGroupDebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupDebugDataIsOnlyAvailableInDebugBuilds);
            return null!;
        }

        return Compiler.DebugData.Get(this);
    }
}
