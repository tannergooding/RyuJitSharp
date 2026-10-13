// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public partial struct NodeInternalRegisters
{
    public readonly uint Count(GenTree tree)
    {
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            return Count(tree, ~RBM_NONE);
        }

        return GetWasmTable().TryGetValue(tree, out var registers) ? (uint)registers.Count : 0;
    }

    public readonly uint Count(GenTree tree, regMaskTP mask)
    {
        var table = GetFixedTable();
        if (!table.TryGetValue(tree, out var registers))
        {
            return 0;
        }

        var available = registers & mask;
        var count = BitOperations.PopCount(unchecked((ulong)available.Lower));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            count += BitOperations.PopCount((ulong)available.Upper);
        }

        return (uint)count;
    }
}
