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
#if DEBUG
        if (VERBOSE)
        {
            dumpDefList();
            _compiler.gtDispTree(tree, topOnly: true);
        }
#endif

        if (tree.IsContained)
        {
#if TARGET_XARCH
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
#else
            assert(!isCandidateLocalRef(tree));
#endif
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

#if TARGET_AMD64
        const int registerLimitMask = 0x6003;
#else
        const int registerLimitMask = 0x3;
#endif
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
#if TARGET_ARM64
                else if (reference.needsConsecutive)
                {
                    assert(reference.refType is RefType.RefTypeUpperVectorRestore);
                    minimumRegisterCount++;
                }
#endif
#endif
#if TARGET_ARM64
                if (reference.needsConsecutive)
                {
                    _consecutiveRegistersLocation = reference.nodeLocation;
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
#if TARGET_ARM64
                    if (!reference.isLiveAtConsecutiveRegistersLoc(_consecutiveRegistersLocation))
#endif
                    {
                        reference.registerAssignment = getConstrainedRegMask(
                            reference, interval.registerType, previousCandidates, calleeSaved, minimumForReference);
                    }

                    if ((reference.registerAssignment != previousCandidates) &&
                        (reference.refType is RefType.RefTypeUse) && !interval.isLocalVar)
                    {
#if TARGET_ARM64
                        var firstReference = interval.firstRefPosition;
                        assert(firstReference is not null);
                        assert(firstReference.treeNode is not null);
                        if (!firstReference.isLiveAtConsecutiveRegistersLoc(_consecutiveRegistersLocation))
#endif
                        {
                            checkConflictingDefUse(reference);
                        }
                    }
                }
            }
            _consecutiveRegistersLocation = MinLocation;
        }
#endif
        JITDUMP("\n");
    }
}
