// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public partial struct NodeInternalRegisters
{
#if HAS_FIXED_REGISTER_SET
    public readonly uint Count(GenTree tree) => Count(tree, ~RBM_NONE);

    public readonly uint Count(GenTree tree, regMaskTP mask)
    {
        if (!_table.TryGetValue(tree, out var registers))
        {
            return 0;
        }

        var available = registers & mask;
        var count = BitOperations.PopCount((ulong)available.Lower);
#if HAS_MORE_THAN_64_REGISTERS
        count += BitOperations.PopCount((ulong)available.Upper);
#endif

        return (uint)count;
    }
#endif
}
