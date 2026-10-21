// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    internal enum ResolveType
    {
        ResolveSplit,
        ResolveJoin,
        ResolveCritical,
        ResolveSharedCritical,
    }

    private static string GetResolveTypeName(ResolveType resolveType)
    {
        switch (resolveType)
        {
            case ResolveType.ResolveSplit:
            {
                return "Split";
            }

            case ResolveType.ResolveJoin:
            {
                return "Join";
            }

            case ResolveType.ResolveCritical:
            {
                return "Critical";
            }

            case ResolveType.ResolveSharedCritical:
            {
                return "SharedCritical";
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage287
                );
                return default!;
            }
        }
    }

    private void requireLocalEdgeResolution()
    {
        if (!_enregisterLocalVars || !_blockSequencingDone ||
            (_inVarToRegMaps is null) || (_outVarToRegMaps is null) ||
            (_splitOrSpilledVars is null))
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage288
            );
            return;
        }
    }

    private void resolveEdges()
    {
        requireLocalEdgeResolution();
        JITDUMP("RESOLVING EDGES\n");
        VarSetOps.IntersectionD(_compiler, _resolutionCandidateVars, _splitOrSpilledVars!);
        if (VarSetOps.IsEmpty(_compiler, _resolutionCandidateVars))
        {
            return;
        }

        // Outgoing critical edges are resolved first, so common moves can be
        // shared before any edge is split and maps for inserted blocks are aliased.
        if (_hasCriticalEdges)
        {
            foreach (var block in _compiler.Blocks)
            {
                if ((uint)block.bbNum <= _bbNumMaxBeforeResolution &&
                    _blockInfo![block.bbNum].hasCriticalOutEdge)
                {
                    handleOutgoingCriticalEdges(block);
                }
            }
        }

        foreach (var block in _compiler.Blocks)
        {
            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                continue;
            }
            var successorCount = block.NumSucc;
            var predecessor = block.GetUniquePred(_compiler);
            var incoming = VarSetOps.Intersection(_compiler, block.bbLiveIn, _resolutionCandidateVars);
            if (!VarSetOps.IsEmpty(_compiler, incoming) && predecessor is not null)
            {
                // A fall-through redirected while splitting a critical edge may
                // introduce more than one consecutive empty resolution block.
                while ((uint)predecessor.bbNum > _bbNumMaxBeforeResolution)
                {
                    var uniquePredecessor = predecessor.GetUniquePred(_compiler);
                    if (uniquePredecessor is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage289
                        );
                        return;
                    }

                    predecessor = uniquePredecessor;
                }
                resolveEdge(predecessor, block, ResolveType.ResolveSplit, incoming, RBM_NONE);
            }

            if (successorCount == 1)
            {
                foreach (var successor in block.Succs)
                {
                    if (successor.GetUniquePred(_compiler) is null)
                    {
                        var outgoing = VarSetOps.Intersection(_compiler, successor.bbLiveIn, _resolutionCandidateVars);
                        if (!VarSetOps.IsEmpty(_compiler, outgoing))
                        {
                            resolveEdge(block, successor, ResolveType.ResolveJoin, outgoing, RBM_NONE);
                        }
                    }
                    break;
                }
            }
        }

        if ((uint)_compiler.fgBBNumMax > _bbNumMaxBeforeResolution)
        {
            var splitMap = getSplitBBNumToTargetBBNumMap();
            foreach (var block in _compiler.Blocks)
            {
                if ((uint)block.bbNum <= _bbNumMaxBeforeResolution)
                {
                    continue;
                }

                var successor = block;
                do
                {
                    var uniqueSuccessor = successor.UniqueSucc;
                    if (uniqueSuccessor is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage290
                        );
                        return;
                    }

                    successor = uniqueSuccessor;
                } while (((uint)successor.bbNum > _bbNumMaxBeforeResolution) && successor.isEmpty());

                var predecessor = block;
                do
                {
                    var uniquePredecessor = predecessor.GetUniquePred(_compiler);
                    if (uniquePredecessor is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage291
                        );
                        return;
                    }

                    predecessor = uniquePredecessor;
                } while (((uint)predecessor.bbNum > _bbNumMaxBeforeResolution) && predecessor.isEmpty());

                var successorNumber = checked((uint)successor.bbNum);
                var predecessorNumber = checked((uint)predecessor.bbNum);
                if (block.isEmpty())
                {
                    if (predecessorNumber > _bbNumMaxBeforeResolution)
                    {
                        assert(successorNumber <= _bbNumMaxBeforeResolution);
                        predecessorNumber = 0;
                    }
                    else
                    {
                        successorNumber = 0;
                    }
                }
                else
                {
                    assert(successorNumber <= _bbNumMaxBeforeResolution &&
                        predecessorNumber <= _bbNumMaxBeforeResolution);
                }
                splitMap.Add(checked((uint)block.bbNum), new SplitEdgeInfo {
                    fromBBNum = predecessorNumber,
                    toBBNum = successorNumber,
                });
                VarSetOps.Assign(_compiler, ref block.bbLiveIn, successor.bbLiveIn);
                VarSetOps.Assign(_compiler, ref block.bbLiveOut, successor.bbLiveIn);
            }
        }

        if (Configuration.DEBUG)
        {
            var foundMismatch = false;
            foreach (var block in _compiler.Blocks)
            {
                if (block.isEmpty() && (uint)block.bbNum > _bbNumMaxBeforeResolution)
                {
                    continue;
                }
                var incomingMap = getInVarToRegMap(checked((uint)block.bbNum));
                if (incomingMap is null)
                {
                    FatalJitException.ThrowFatalJitException("Resolution validation requires an incoming map.");
                    return;
                }
                foreach (var predBlock in block.PredBlocks)
                {
                    var outgoingMap = getOutVarToRegMap(checked((uint)predBlock.bbNum));
                    if (outgoingMap is null)
                    {
                        FatalJitException.ThrowFatalJitException("Resolution validation requires an outgoing map.");
                        return;
                    }
                    _ = VarSetOps.VisitBits(_compiler, block.bbLiveIn, index => {
                        var from = getVarReg(outgoingMap, checked((uint)index));
                        var to = getVarReg(incomingMap, checked((uint)index));
                        if (from != to)
                        {
                            var interval = getIntervalForLocalVar(checked((uint)index));
                            if (!interval.isWriteThru || to != REG_STK)
                            {
                                if (!foundMismatch)
                                {
                                    foundMismatch = true;
                                    JITDUMP("Found mismatched var locations after resolution!\n");
                                }
                                JITDUMP($" V{interval.varNum:D2}: {FMT_BB(predBlock.bbNum)} to {FMT_BB(block.bbNum)}: {from.Name} to {to.Name}\n");
                            }
                        }
                        return true;
                    });
                }
            }
            assert(!foundMismatch);
        }
        JITDUMP("\n");
    }

    private void handleOutgoingCriticalEdges(BasicBlock block)
    {
        var outgoing = VarSetOps.Intersection(_compiler, block.bbLiveOut, _resolutionCandidateVars);
        if (VarSetOps.IsEmpty(_compiler, outgoing))
        {
            return;
        }
        var same = VarSetOps.MakeEmpty(_compiler);
        var different = VarSetOps.MakeEmpty(_compiler);
        var outgoingMap = getOutVarToRegMap(checked((uint)block.bbNum));
        if (outgoingMap is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage292
            );
            return;
        }
        assert(block.NumSucc > 1);

        var liveOutRegs = RBM_NONE;
        _ = VarSetOps.VisitBits(_compiler, block.bbLiveOut, index => {
            var from = getVarReg(outgoingMap, checked((uint)index));
            if (from != REG_STK)
            {
                liveOutRegs |= blockLiveRegMask(from, getIntervalForLocalVar(checked((uint)index)).registerType);
            }
            return true;
        });
        var consumed = RBM_NONE;
        uint? firstTerminatorLocal = null;
        uint? secondTerminatorLocal = null;
        if (block.Kind is BBJ_SWITCH)
        {
            var terminator = block.LastNode;
            if (terminator is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage293
                );
                return;
            }
            assert(terminator.Oper is GT_SWITCH_TABLE);
            var codeGen = _compiler.codeGen;
            if (codeGen is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage294
                );
                return;
            }
            consumed = new regMaskTP(codeGen.InternalRegisters.GetAll(terminator).GetRegSetForType(TYP_INT));
            var first = terminator.AsOp().Op1;
            var second = terminator.AsOp().Op2;
            noway_assert(first is not null && second is not null);
            assert(first.RegNum != REG_NA && second.RegNum != REG_NA);
            assert(varTypeIsIntegralOrI(first.Type) && varTypeIsIntegralOrI(second.Type));
            consumed |= regMaskTP.CreateFromRegNum(first.RegNum, genSingleTypeRegMask(first.RegNum));
            consumed |= regMaskTP.CreateFromRegNum(second.RegNum, genSingleTypeRegMask(second.RegNum));
            MarkTerminatorOperand(first, ref firstTerminatorLocal);
            MarkTerminatorOperand(second, ref secondTerminatorLocal);
        }
        else if (block.Kind is BBJ_COND)
        {
            var terminator = block.LastNode;
            if (terminator is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage295
                );
                return;
            }
            if (terminator.Oper is GT_JTRUE or GT_JCMP or GT_JTEST)
            {
                var first = terminator.Oper is GT_JTRUE
                    ? terminator.AsUnOp().Op1 : terminator.AsOp().Op1;
                assert(terminator.Oper is not GT_JTRUE || !first.IsContained);
                if (!first.IsContained)
                {
                    consumed |= regMaskTP.CreateFromRegNum(first.RegNum, genSingleTypeRegMask(first.RegNum));
                    MarkTerminatorOperand(first, ref firstTerminatorLocal);
                }
                if (terminator.Oper is GT_JCMP or GT_JTEST &&
                    terminator.AsOp().Op2 is GenTree second && !second.IsContained)
                {
                    consumed |= regMaskTP.CreateFromRegNum(second.RegNum, genSingleTypeRegMask(second.RegNum));
                    MarkTerminatorOperand(second, ref secondTerminatorLocal);
                }
            }
        }

        void MarkTerminatorOperand(GenTree operand, ref uint? localNumber)
        {
            if (operand.Oper is GT_COPY)
            {
                var source = operand.AsCopyOrReload().Op1;
                consumed |= regMaskTP.CreateFromRegNum(source.RegNum, genSingleTypeRegMask(source.RegNum));
            }
            else if (operand.Oper.IsLocal)
            {
                localNumber = _compiler.lvaTable[operand.AsLclVarCommon().LclNum]._varIndex;
            }
        }

        var sharedMap = _sharedCriticalVarToRegMap;
        if (sharedMap is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage296
            );
            return;
        }
        var sameWrites = RBM_NONE;
        var differentReads = RBM_NONE;
        _ = VarSetOps.VisitBits(_compiler, outgoing, index => {
            var trackedIndex = checked((uint)index);
            var from = getVarReg(outgoingMap, trackedIndex);
            var liveOnSomeOnly = false;
            var liveOnlyAtSplit = true;
            var commonTarget = REG_NA;
            foreach (var successor in block.Succs)
            {
                if (!VarSetOps.IsMember(_compiler, successor.bbLiveIn, index))
                {
                    liveOnSomeOnly = true;
                    continue;
                }
                if (liveOnlyAtSplit)
                {
                    liveOnlyAtSplit = successor.GetUniquePred(_compiler) is not null &&
                        successor != _compiler.fgFirstBB;
                }
                var targetMap = getInVarToRegMap(checked((uint)successor.bbNum));
                if (targetMap is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage297
                    );
                    return default!;
                }
                var target = getVarReg(targetMap, trackedIndex);
                if (commonTarget == REG_NA)
                {
                    commonTarget = target;
                    continue;
                }
                if (target != commonTarget)
                {
                    commonTarget = REG_NA;
                    break;
                }
            }
            if ((commonTarget != REG_NA) && (commonTarget != REG_STK))
            {
                var targetMask = blockLiveRegMask(
                    commonTarget, getIntervalForLocalVar(trackedIndex).registerType);
                if ((liveOnSomeOnly && ((liveOutRegs & targetMask).IsNonEmpty ||
                    (sameWrites & targetMask).IsNonEmpty)) ||
                    (targetMask & consumed).IsNonEmpty ||
                    firstTerminatorLocal == trackedIndex || secondTerminatorLocal == trackedIndex ||
                    (liveOnlyAtSplit && liveOnSomeOnly))
                {
                    commonTarget = REG_NA;
                }
            }
            if (commonTarget == REG_NA)
            {
                VarSetOps.AddElemD(_compiler, different, index);
                if (from != REG_STK)
                {
                    differentReads |= blockLiveRegMask(from, getIntervalForLocalVar(trackedIndex).registerType);
                }
            }
            else if (commonTarget != from)
            {
                VarSetOps.AddElemD(_compiler, same, index);
                setVarReg(sharedMap, trackedIndex, commonTarget);
                if (commonTarget != REG_STK)
                {
                    sameWrites |= blockLiveRegMask(
                        commonTarget, getIntervalForLocalVar(trackedIndex).registerType);
                }
            }
            return true;
        });

        if (!VarSetOps.IsEmpty(_compiler, same))
        {
            if ((sameWrites & differentReads).IsNonEmpty)
            {
                VarSetOps.UnionD(_compiler, different, same);
                VarSetOps.ClearD(_compiler, same);
            }
            else
            {
                resolveEdge(block, null, ResolveType.ResolveSharedCritical, same, consumed);
            }
        }
        if (!VarSetOps.IsEmpty(_compiler, different))
        {
            foreach (var successor in block.Succs)
            {
                if (successor.GetUniquePred(_compiler) is not null && successor != _compiler.fgFirstBB)
                {
                    continue;
                }
                var targetMap = getInVarToRegMap(checked((uint)successor.bbNum));
                if (targetMap is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage297
                    );
                    return;
                }
                var edgeSet = VarSetOps.Intersection(_compiler, different, successor.bbLiveIn);
                _ = VarSetOps.VisitBits(_compiler, edgeSet, index => {
                    if (getVarReg(outgoingMap, checked((uint)index)) ==
                        getVarReg(targetMap, checked((uint)index)))
                    {
                        VarSetOps.RemoveElemD(_compiler, edgeSet, index);
                    }
                    return true;
                });
                if (VarSetOps.IsEmpty(_compiler, edgeSet))
                {
                    continue;
                }
                if (_compiler.compHndBBtabCount > 0 &&
                    VarSetOps.IsSubset(_compiler, edgeSet, _exceptVars))
                {
                    var insertion = successor.FirstNode;
                    _ = VarSetOps.VisitBits(_compiler, edgeSet, index => {
                        var target = getVarReg(targetMap, checked((uint)index));
                        setVarReg(targetMap, checked((uint)index), REG_STK);
                        if (target != REG_STK)
                        {
                            var interval = getIntervalForLocalVar(checked((uint)index));
                            assert(interval.isWriteThru);
                            addResolution(successor, insertion, interval, target, REG_STK,
                                block, successor, "EHvar");
                        }
                        return true;
                    });
                }
                else
                {
                    resolveEdge(block, successor, ResolveType.ResolveCritical, edgeSet, consumed);
                }
            }
        }
    }
}
