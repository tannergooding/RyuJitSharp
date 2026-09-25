// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public partial struct NodeInternalRegisters
{
    private NodeInternalRegistersTable _table;

    public NodeInternalRegisters()
    {
        _table = [];
    }

#if HAS_FIXED_REGISTER_SET
    public readonly void Add(GenTree tree, regMaskTP registers)
    {
        assert(registers != RBM_NONE);
        _ = _table.TryGetValue(tree, out var existing);
        _table[tree] = existing | registers;
    }

    public readonly regNumber Extract(GenTree tree) => Extract(tree, ~RBM_NONE);

    public readonly regNumber Extract(GenTree tree, regMaskTP mask)
    {
        assert(_table.ContainsKey(tree));
        var registers = _table[tree];
        var available = registers & mask;
        assert(available != RBM_NONE);
        var lower = (ulong)available.Lower;
#if HAS_MORE_THAN_64_REGISTERS
        var result = (regNumber)(lower != 0
            ? BitOperations.TrailingZeroCount(lower)
            : 64 + BitOperations.TrailingZeroCount((ulong)available.Upper));
#else
        var result = (regNumber)BitOperations.TrailingZeroCount(lower);
#endif
        _table[tree] = registers ^ regMaskTP.CreateFromRegNum(result, result.SingleTypeMask);

        return result;
    }

    public readonly regNumber GetSingle(GenTree tree) => GetSingle(tree, ~RBM_NONE);

    public readonly regNumber GetSingle(GenTree tree, regMaskTP mask)
    {
        assert(_table.ContainsKey(tree));
        var registers = _table[tree];
        var available = registers & mask;
        var lower = (ulong)available.Lower;
#if HAS_MORE_THAN_64_REGISTERS
        var upper = (ulong)available.Upper;
        assert((BitOperations.IsPow2(lower) && (upper == 0)) ||
            ((lower == 0) && BitOperations.IsPow2(upper)));
        var result = (regNumber)(lower != 0
            ? BitOperations.TrailingZeroCount(lower)
            : 64 + BitOperations.TrailingZeroCount(upper));
#else
        assert(BitOperations.IsPow2(lower));
        var result = (regNumber)BitOperations.TrailingZeroCount(lower);
#endif
#if DEBUG
        _table[tree] = registers & ~regMaskTP.CreateFromRegNum(result, result.SingleTypeMask);
#endif

        return result;
    }

    /// <summary>Get all internal registers for the specified IR node.</summary>
    /// <param name="tree">IR node whose internal registers to query</param>
    /// <returns>Mask of registers.</returns>
    public readonly regMaskTP GetAll(GenTree tree)
    {
        _ = _table.TryGetValue(tree, out var regMask);
        return regMask;
    }

#else  // !HAS_FIXED_REGISTER_SET
    // void Add(GenTree* tree, regNumber reg);
    // InternalRegs* GetAll(GenTree* tree);
    // NodeInternalRegistersTable::KeyValueIteration Iterate();
#endif
}
