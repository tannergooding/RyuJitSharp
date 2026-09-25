// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private readonly uint[] _maxSpill = new uint[(int)TYP_COUNT];
    private readonly uint[] _currentSpill = new uint[(int)TYP_COUNT];

#if TARGET_X86
    private bool _needDoubleTmpForFPCall;
    private bool _needFloatTmpForFPCall;
#endif

    private void initMaxSpill()
    {
#if TARGET_X86
        _needDoubleTmpForFPCall = false;
        _needFloatTmpForFPCall = false;
#endif
        Array.Clear(_maxSpill);
        Array.Clear(_currentSpill);
    }

    private void recordMaxSpill()
    {
        JITDUMP("Recording the maximum number of concurrent spills:\n");
#if TARGET_X86
        var returnType = RegSet.tmpNormalizeType(_compiler.info.compRetType);
        if (_needDoubleTmpForFPCall || (returnType is TYP_DOUBLE))
        {
            JITDUMP("Adding a spill temp for moving a double call/return value between xmm reg and x87 stack.\n");
            _maxSpill[(int)TYP_DOUBLE] = unchecked(_maxSpill[(int)TYP_DOUBLE] + 1);
        }
        if (_needFloatTmpForFPCall || (returnType is TYP_FLOAT))
        {
            JITDUMP("Adding a spill temp for moving a float call/return value between xmm reg and x87 stack.\n");
            _maxSpill[(int)TYP_FLOAT] = unchecked(_maxSpill[(int)TYP_FLOAT] + 1);
        }
#endif
        assert(_compiler.codeGen is not null);
        ref var registers = ref _compiler.codeGen.RegSet;
        registers.tmpBeginPreAllocateTemps();
        for (var index = 0; index < (int)TYP_COUNT; index++)
        {
            var type = (var_types)index;
            if (type != RegSet.tmpNormalizeType(type))
            {
                assert(_maxSpill[index] == 0);
            }
            if (_maxSpill[index] != 0)
            {
                JITDUMP($"  {type.Name}: {unchecked((int)_maxSpill[index])}\n");
                registers.tmpPreAllocateTemps(type, _maxSpill[index]);
            }
        }
        JITDUMP("\n");
    }

    private void updateMaxSpill(RefPosition reference)
    {
        var refType = reference.refType;
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        if (refType is RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore)
        {
            var interval = reference.getInterval();
            if (!interval.isUpperVector)
            {
                assert(interval.firstRefPosition is not null);
                assert(interval.firstRefPosition.spillAfter);
            }
            else
            {
                var localInterval = interval.relatedInterval;
                assert(localInterval is not null);
                assert(localInterval.isSpilled || (!reference.spillAfter && !reference.reload));
            }
            return;
        }
#endif
        if (reference.spillAfter || reference.reload ||
            (reference.RegOptional() && (reference.assignedReg() is REG_NA)))
        {
            var interval = reference.getInterval();
            if (!interval.isLocalVar)
            {
                var tree = reference.treeNode;
                if (tree is null)
                {
                    assert(RefTypeIsUse(refType));
                    assert(interval.firstRefPosition is not null);
                    tree = interval.firstRefPosition.treeNode;
                }
                assert(tree is not null);

                // Count the same normalized stack-home types that RegSet preallocates.
                // Multireg results may have different types at each register index.
                var type = !tree.IsMultiRegNode
                    ? getDefType(tree)
                    : getRegisterTypeByIndex(tree, checked((int)reference.getMultiRegIdx()));
                type = RegSet.tmpNormalizeType(type);
                var index = (int)type;

                if (reference.spillAfter && !reference.reload)
                {
                    _currentSpill[index] = unchecked(_currentSpill[index] + 1);
                    if (_currentSpill[index] > _maxSpill[index])
                    {
                        _maxSpill[index] = _currentSpill[index];
                    }
                }
                else if (reference.reload)
                {
                    assert(_currentSpill[index] > 0);
                    _currentSpill[index] = unchecked(_currentSpill[index] - 1);
                }
                else if (reference.RegOptional() && (reference.assignedReg() is REG_NA))
                {
                    // An optional use can consume the spill directly from its stack home.
                    assert(RefTypeIsUse(refType));
                    assert(_currentSpill[index] > 0);
                    _currentSpill[index] = unchecked(_currentSpill[index] - 1);
                }
                JITDUMP($"  Max spill for {type.Name} is {unchecked((int)_maxSpill[index])}\n");
            }
        }
    }
}
