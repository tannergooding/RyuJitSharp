// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if TARGET_ARM64
    private readonly Dictionary<RefPosition, RefPosition?> _nextConsecutiveRefPositions = [];

    private RefPosition? getNextConsecutiveRefPosition(RefPosition reference)
    {
        if (!_nextConsecutiveRefPositions.TryGetValue(reference, out var next))
        {
            throw new FatalJitException("Consecutive register reference is missing its successor.");
        }
        return next;
    }

    private int buildConsecutiveRegistersForUse(GenTree operand, GenTree? rmwOperand)
    {
        var sourceCount = 0;
        Interval? rmwInterval = null;
        var rmwIsLastUse = false;
        if ((rmwOperand is not null) && isCandidateLocalRef(rmwOperand))
        {
            rmwInterval = getIntervalForLocalVarNode(rmwOperand.AsLclVarCommon());
            rmwIsLastUse = rmwOperand.AsLclVar().IsLastUse(0);
        }

        if (operand.Oper is GT_FIELD_LIST)
        {
            assert(_compiler.info.compNeedsConsecutiveRegisters);
            RefPosition? first = null;
            RefPosition? previous = null;
            foreach (var field in operand.AsFieldList().Uses)
            {
                var before = refPositions.Count;
                var current = buildUse(field.Node);
                current.needsConsecutive = true;
                current.regCount = 0;

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                if (refPositions.Count != before + 1)
                {
                    var restore = refPositions[before];
                    assert(restore.refType is RefType.RefTypeUpperVectorRestore);
                    restore.needsConsecutive = true;
                    restore.regCount = 0;
                    if (previous is not null)
                    {
                        _nextConsecutiveRefPositions[previous] = restore;
                    }
                    _nextConsecutiveRefPositions[restore] = current;
                    if ((rmwOperand is not null) &&
                        ((restore.getInterval() != rmwInterval) || (!rmwIsLastUse && !restore.lastUse)))
                    {
                        setDelayFree(restore);
                    }
                }
                else
#endif
                if (previous is not null)
                {
                    _nextConsecutiveRefPositions[previous] = current;
                }

                first ??= current;
                _nextConsecutiveRefPositions[current] = null;
                previous = current;
                sourceCount++;

                if ((rmwOperand is not null) &&
                    ((current.getInterval() != rmwInterval) || (!rmwIsLastUse && !current.lastUse)))
                {
                    setDelayFree(current);
                }
            }

            assert(first is not null);
            first.regCount = checked((byte)sourceCount);
#if DEBUG
            for (var current = first; current is not null; current = getNextConsecutiveRefPosition(current))
            {
                current.minRegCandidateCount = (uint)sourceCount;
            }
#endif
        }
        else
        {
            var before = refPositions.Count;
            sourceCount = buildOperandUses(operand);
            if (rmwOperand is not null)
            {
                for (var index = before; index < refPositions.Count; index++)
                {
                    var reference = refPositions[index];
                    if ((reference.getInterval() != rmwInterval) || (!rmwIsLastUse && !reference.lastUse))
                    {
                        setDelayFree(reference);
                    }
                }
            }
        }
        return sourceCount;
    }

    private void buildConsecutiveRegistersForDef(GenTree tree, int registerCount)
    {
        assert(registerCount > 1);
        assert(_compiler.info.compNeedsConsecutiveRegisters);

        RefPosition? previous = null;
        for (var index = 0; index < registerCount; index++)
        {
            var current = buildDef(tree, SRBM_NONE, index);
            current.needsConsecutive = true;
            current.regCount = index == 0 ? checked((byte)registerCount) : (byte)0;
#if DEBUG
            current.minRegCandidateCount = (uint)registerCount;
#endif
            if (previous is not null)
            {
                _nextConsecutiveRefPositions[previous] = current;
            }
            _nextConsecutiveRefPositions[current] = null;
            previous = current;
        }
    }
#endif
}
