// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public struct InternalRegs
{
    private regNumber _first;
    private regNumber _second;
    private int _count;

    public readonly bool IsEmpty => _count == 0;

    public readonly int Count => _count;

    public readonly regMask GetRegSetForType(var_types type)
    {
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RegisterMasksAreNotAvailableForTargetsWithoutAFixedRegisterSet
    );
        return default!;
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
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.TooManyInternalRegistersWereAddedToANode
    );
            return;
        }

        _count++;
    }

    public readonly regNumber GetAt(int index)
    {
        switch (index)
        {
            case 0 when _count > 0:
            {
                return _first;
            }

            case 1 when _count > 1:
            {
                return _second;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InternalRegisterIndexIsOutOfRange);
                return default!;
            }
        }
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
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InternalRegisterIndexIsOutOfRange);
                return;
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

        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.NoInternalRegisterIsAvailableToExtract);
        return default!;
    }
}

public partial struct NodeInternalRegisters
{
    private NodeInternalRegistersTable? _table;
    private WasmNodeInternalRegistersTable? _wasmTable;

    public NodeInternalRegisters()
    {
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            _table = [];
            _wasmTable = null;
        }
        else
        {
            _table = null;
            _wasmTable = [];
        }
    }

    public readonly void Add(GenTree tree, regMaskTP registers)
    {
        var table = GetFixedTable();
        assert(registers != RBM_NONE);
        _ = table.TryGetValue(tree, out var existing);
        table[tree] = existing | registers;
    }

    public readonly void Add(GenTree tree, regNumber register)
    {
        ref var registers = ref CollectionsMarshal.GetValueRefOrAddDefault(GetWasmTable(), tree, out _);
        registers.Add(register);
    }

    public readonly regNumber Extract(GenTree tree)
    {
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            return Extract(tree, ~RBM_NONE);
        }

        ref var registers = ref GetAllWasm(tree);
        return registers.Extract();
    }

    public readonly regNumber Extract(GenTree tree, regMaskTP mask)
    {
        var table = GetFixedTable();
        assert(table.ContainsKey(tree));
        var registers = table[tree];
        var available = registers & mask;
        assert(available != RBM_NONE);
        var lower = unchecked((ulong)available.Lower);
        regNumber result;
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            result = (regNumber)(lower != 0
                ? BitOperations.TrailingZeroCount(lower)
                : 64 + BitOperations.TrailingZeroCount((ulong)available.Upper));
        }
        else
        {
            result = (regNumber)BitOperations.TrailingZeroCount(lower);
        }

        table[tree] = registers ^ regMaskTP.CreateFromRegNum(result, result.SingleTypeMask);

        return result;
    }

    public readonly regNumber GetSingle(GenTree tree)
    {
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            return GetSingle(tree, ~RBM_NONE);
        }

        ref var registers = ref GetAllWasm(tree);
        if (registers.Count != 1)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.ExactlyOneInternalRegisterWasExpected);
            return default!;
        }

        return registers.GetAt(0);
    }

    public readonly regNumber GetSingle(GenTree tree, regMaskTP mask)
    {
        var table = GetFixedTable();
        assert(table.ContainsKey(tree));
        var registers = table[tree];
        var available = registers & mask;
        var lower = unchecked((ulong)available.Lower);
        regNumber result;
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            var upper = (ulong)available.Upper;
            assert((BitOperations.IsPow2(lower) && (upper == 0)) ||
                ((lower == 0) && BitOperations.IsPow2(upper)));
            result = (regNumber)(lower != 0
                ? BitOperations.TrailingZeroCount(lower)
                : 64 + BitOperations.TrailingZeroCount(upper));
        }
        else
        {
            assert(BitOperations.IsPow2(lower));
            result = (regNumber)BitOperations.TrailingZeroCount(lower);
        }

        if (Configuration.DEBUG)
        {
            table[tree] = registers & ~regMaskTP.CreateFromRegNum(result, result.SingleTypeMask);
        }

        return result;
    }

    /// <summary>Get all internal registers for the specified IR node.</summary>
    /// <param name="tree">IR node whose internal registers to query</param>
    /// <returns>Mask of registers.</returns>
    public readonly regMaskTP GetAll(GenTree tree)
    {
        var table = GetFixedTable();
        _ = table.TryGetValue(tree, out var regMask);
        return regMask;
    }

    /// <summary>Get all internal registers for the specified WebAssembly IR node.</summary>
    /// <param name="tree">IR node whose internal registers to query</param>
    /// <returns>Reference to the WebAssembly register slots.</returns>
    public readonly ref InternalRegs GetAllWasm(GenTree tree)
    {
        var table = GetWasmTable();
        if (!table.ContainsKey(tree))
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InternalRegistersAreMissingForTheSpecifiedNode
    );
        }

        return ref CollectionsMarshal.GetValueRefOrAddDefault(table, tree, out _);
    }

    private readonly NodeInternalRegistersTable GetFixedTable()
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RegisterMaskTrackingIsNotAvailableForThisTarget
    );
            return default!;
        }

        var table = _table;
        if (table is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.FixedRegisterTableIsNotInitialized);
            return default!;
        }

        return table;
    }

    private readonly WasmNodeInternalRegistersTable GetWasmTable()
    {
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.WebAssemblyRegisterTrackingIsNotAvailableForThisTarget
    );
            return default!;
        }

        var wasmTable = _wasmTable;
        if (wasmTable is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.WebAssemblyRegisterTableIsNotInitialized
    );
            return default!;
        }

        return wasmTable;
    }
}
