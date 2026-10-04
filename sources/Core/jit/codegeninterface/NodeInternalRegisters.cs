// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

#if !HAS_FIXED_REGISTER_SET
public struct InternalRegs
{
    public const int MAX_REG_COUNT = 2;

    private regNumber _first;
    private regNumber _second;
    private int _count;

    public readonly bool IsEmpty => _count == 0;

    public readonly int Count => _count;

    public readonly regMask GetRegSetForType(var_types type)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Register masks are not available for targets without a fixed register set.");
    }

    public void Add(regNumber reg)
    {
        assert(reg != REG_NA);

        if (_count == 0)
        {
            _first = reg;
        }
        else if (_count == 1)
        {
            _second = reg;
        }
        else
        {
            throw new FatalJitException(CORJIT_INTERNALERROR, "Too many internal registers were added to a node.");
        }

        _count++;
    }

    public readonly regNumber GetAt(int index)
    {
        return index switch
        {
            0 when _count > 0 => _first,
            1 when _count > 1 => _second,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, "The internal register index is out of range."),
        };
    }

    public void SetAt(int index, regNumber reg)
    {
        assert(reg != REG_NA);

        switch (index)
        {
            case 0 when _count > 0:
            {
                _first = reg;
                break;
            }
            case 1 when _count > 1:
            {
                _second = reg;
                break;
            }
            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR, "The internal register index is out of range.");
            }
        }
    }

    public regNumber Extract()
    {
        if (_count == 2)
        {
            var result = _second;
            _second = REG_NA;
            _count = 1;
            return result;
        }

        if (_count == 1)
        {
            var result = _first;
            _first = REG_NA;
            _count = 0;
            return result;
        }

        throw new FatalJitException(CORJIT_INTERNALERROR, "No internal register is available to extract.");
    }
}
#endif

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
        var lower = unchecked((ulong)available.Lower);
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
        var lower = unchecked((ulong)available.Lower);
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
    public readonly void Add(GenTree tree, regMaskTP registers)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Internal register tracking is not ported for targets without a fixed register set.");
    }

    public readonly void Add(GenTree tree, regNumber register)
    {
        ref var registers = ref CollectionsMarshal.GetValueRefOrAddDefault(_table, tree, out _);
        registers.Add(register);
    }

    public readonly regNumber Extract(GenTree tree)
    {
        ref var registers = ref GetAll(tree);
        return registers.Extract();
    }

    public readonly regNumber GetSingle(GenTree tree)
    {
        ref var registers = ref GetAll(tree);
        if (registers.Count != 1)
        {
            throw new FatalJitException(CORJIT_INTERNALERROR, "Exactly one internal register was expected.");
        }

        return registers.GetAt(0);
    }

    public readonly regNumber GetSingle(GenTree tree, regMaskTP mask)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Internal register queries are not ported for targets without a fixed register set.");
    }

    public readonly ref InternalRegs GetAll(GenTree tree)
    {
        if (!_table.ContainsKey(tree))
        {
            throw new FatalJitException(CORJIT_INTERNALERROR, "Internal registers are missing for the specified node.");
        }

        return ref CollectionsMarshal.GetValueRefOrAddDefault(_table, tree, out _);
    }

    // NodeInternalRegistersTable::KeyValueIteration Iterate();
#endif
}
