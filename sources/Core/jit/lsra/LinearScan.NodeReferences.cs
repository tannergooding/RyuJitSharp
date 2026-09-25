// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if DEBUG
    private int computeOperandDstCount(GenTree operand)
    {
        if (operand.IsContained)
        {
            var destinationCount = 0;
            foreach (var source in operand.Operands)
            {
                destinationCount += computeOperandDstCount(source);
            }
            return destinationCount;
        }

        if (operand.IsUnusedValue)
        {
            return 0;
        }
        if (operand.IsValue)
        {
            return operand.GetRegisterDstCount(_compiler);
        }

        assert(operand.Oper.IsStore || operand.Oper.IsPutArgStk || operand.Type is TYP_VOID);
        return 0;
    }

    private int computeAvailableSrcCount(GenTree node)
    {
        var sourceCount = 0;
        foreach (var operand in node.Operands)
        {
            sourceCount += computeOperandDstCount(operand);
        }
        return sourceCount;
    }

    private int countPendingDefinitions()
    {
        var count = 0;
        for (var definition = _definitionList.First; definition is not null; definition = definition.next)
        {
            count++;
        }
        return count;
    }
#endif

    private void buildRefPositionsForNode(GenTree tree, LsraLocation currentLocation)
    {
#if TARGET_AMD64
#if DEBUG
        if (VERBOSE)
        {
            dumpDefList();
            _compiler.gtDispTree(tree, topOnly: true);
        }
#endif

        if (tree.IsContained)
        {
            if (tree.Oper.IsLocal && ((tree.Flags & GTF_VAR_DEATH) != 0))
            {
                ref var local = ref _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
                if (local.lvLRACandidate)
                {
                    assert(local.lvTracked);
                    var varIndex = local._varIndex;
                    VarSetOps.RemoveElemD(_compiler, _currentLiveVariables, checked((int)varIndex));
                    updatePreferencesOfDyingLocal(getIntervalForLocalVar(varIndex));
                }
            }
            JITDUMP("Contained\n");
            return;
        }

#if DEBUG
        var refPositionMark = refPositions.Count;
        var oldDefinitionCount = countPendingDefinitions();
        _currentBuildNode = tree;
#endif

        var consumed = buildNode(tree);

#if DEBUG
        var produced = countPendingDefinitions() - oldDefinitionCount;
        _ = produced;
        assert((consumed == 0) || (computeAvailableSrcCount(tree) == consumed));

        // lsra.h packs the AMD64 register limits and selection heuristics into the stress mask.
        const int registerLimitMask = 0x6003;
        const int selectionHeuristicsMask = 0x1c;
        if ((_lsraStressMask & (registerLimitMask | selectionHeuristicsMask)) != 0)
        {
            uint minimumRegisterCount = 0;
            for (var index = refPositionMark; index < refPositions.Count; index++)
            {
                var reference = refPositions[index];
                if (!reference.isIntervalRef())
                {
                    continue;
                }

                if ((reference.refType is RefType.RefTypeUse) ||
                    ((reference.refType is RefType.RefTypeDef) && !reference.getInterval().isInternal))
                {
                    minimumRegisterCount++;
                }
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                else if (reference.refType is RefType.RefTypeUpperVectorSave)
                {
                    minimumRegisterCount++;
                }
#endif
                if (reference.getInterval().isSpecialPutArg)
                {
                    minimumRegisterCount++;
                }
            }

            for (var index = refPositionMark; index < refPositions.Count; index++)
            {
                var reference = refPositions[index];
                var minimumForReference = minimumRegisterCount;
                if (RefTypeIsUse(reference.refType) && reference.delayRegFree)
                {
                    minimumForReference += checked((uint)countRegisterMaskBits(getKillSetForNode(tree)));
                }
                else if ((reference.refType is RefType.RefTypeDef) &&
                    reference.getInterval().isSpecialPutArg)
                {
                    minimumForReference++;
                }

                reference.minRegCandidateCount = minimumForReference;
                if (reference.IsActualRef() && doReverseCallerCallee())
                {
                    var interval = reference.getInterval();
                    var previousCandidates = reference.registerAssignment;
                    var calleeSaved = calleeSaveRegs(interval.registerType);
                    reference.registerAssignment = getConstrainedRegMask(
                        reference, interval.registerType, previousCandidates, calleeSaved, minimumForReference);

                    if ((reference.registerAssignment != previousCandidates) &&
                        (reference.refType is RefType.RefTypeUse) && !interval.isLocalVar)
                    {
                        checkConflictingDefUse(reference);
                    }
                }
            }
            _consecutiveRegistersLocation = MinLocation;
        }
#endif
        JITDUMP("\n");
#else
        throw new FatalJitException("LSRA node reference building is not implemented outside AMD64.");
#endif
    }
}
