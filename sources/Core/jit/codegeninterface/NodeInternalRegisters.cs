// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct NodeInternalRegisters
{
    private NodeInternalRegistersTable _table;

    public NodeInternalRegisters()
    {
        _table = [];
    }

#if HAS_FIXED_REGISTER_SET
    // void Add(GenTree* tree, regMaskTP reg);
    // regNumber Extract(GenTree* tree, regMaskTP mask = static_cast<regMaskTP>(-1));
    // regNumber GetSingle(GenTree* tree, regMaskTP mask = static_cast<regMaskTP>(-1));

    /// <summary>Get all internal registers for the specified IR node.</summary>
    /// <param name="tree">IR node whose internal registers to query</param>
    /// <returns>Mask of registers.</returns>
    public readonly regMaskTP GetAll(GenTree tree)
    {
        _ = _table.TryGetValue(tree, out var regMask);
        return regMask;
    }

    // unsigned Count(GenTree* tree, regMaskTP mask = static_cast<regMaskTP>(-1));
#else  // !HAS_FIXED_REGISTER_SET
    // void Add(GenTree* tree, regNumber reg);
    // InternalRegs* GetAll(GenTree* tree);
    // NodeInternalRegistersTable::KeyValueIteration Iterate();
#endif
}
