// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class insGroup
{
    private sealed class DebugData
    {
        private static ConditionalWeakTable<insGroup, DebugData>? s_instances;

        internal BasicBlock? LastGeneratedBlock;
        internal List<BasicBlock>? Blocks;
        internal nuint DataSize;

        internal static DebugData Get(insGroup group)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(group, static _ => new DebugData());
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

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("Instruction-group debug data is only available in Debug builds.");
        }

        return DebugData.Get(this);
    }
}
