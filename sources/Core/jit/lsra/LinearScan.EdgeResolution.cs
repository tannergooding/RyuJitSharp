// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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

    private static readonly string[] s_resolveTypeName = ["Split", "Join", "Critical", "SharedCritical"];

    private void requireWindowsAmd64EdgeResolution()
    {
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        if (!_enregisterLocalVars || !_blockSequencingDone ||
            (_inVarToRegMaps is null) || (_outVarToRegMaps is null) ||
            (_splitOrSpilledVars is null))
        {
            throw new FatalJitException("LSRA edge resolution requires allocated local maps and block sequence.");
        }
#else
        throw new FatalJitException("LSRA edge resolution is not implemented outside Windows AMD64.");
#endif
    }

    private void resolveEdges()
    {
        requireWindowsAmd64EdgeResolution();
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
                    predecessor = predecessor.GetUniquePred(_compiler)
                        ?? throw new FatalJitException("Split edge requires a unique original predecessor.");
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
                    successor = successor.UniqueSucc
                        ?? throw new FatalJitException("A split edge requires a single successor.");
                } while (((uint)successor.bbNum > _bbNumMaxBeforeResolution) && successor.isEmpty());

                var predecessor = block;
                do
                {
                    predecessor = predecessor.GetUniquePred(_compiler)
                        ?? throw new FatalJitException("A split edge requires a single predecessor.");
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
                splitMap.Add(checked((uint)block.bbNum), new SplitEdgeInfo
                {
                    fromBBNum = predecessorNumber,
                    toBBNum = successorNumber,
                });
                VarSetOps.Assign(_compiler, ref block.bbLiveIn, successor.bbLiveIn);
                VarSetOps.Assign(_compiler, ref block.bbLiveOut, successor.bbLiveIn);
            }
        }

#if DEBUG
        var foundMismatch = false;
        foreach (var block in _compiler.Blocks)
        {
            if (block.isEmpty() && (uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                continue;
            }
            var incomingMap = getInVarToRegMap(checked((uint)block.bbNum))
                ?? throw new FatalJitException("Resolution validation requires an incoming map.");
            foreach (var predBlock in block.PredBlocks)
            {
                var outgoingMap = getOutVarToRegMap(checked((uint)predBlock.bbNum))
                    ?? throw new FatalJitException("Resolution validation requires an outgoing map.");
                _ = VarSetOps.VisitBits(_compiler, block.bbLiveIn, index =>
                {
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
#endif
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
        var outgoingMap = getOutVarToRegMap(checked((uint)block.bbNum))
            ?? throw new FatalJitException("Critical edge requires an outgoing register map.");
        assert(block.NumSucc > 1);

        var liveOutRegs = RBM_NONE;
        _ = VarSetOps.VisitBits(_compiler, block.bbLiveOut, index =>
        {
            var from = getVarReg(outgoingMap, checked((uint)index));
            if (from != REG_STK)
            {
                liveOutRegs |= regMaskTP.CreateFromRegNum(from, genSingleTypeRegMask(from));
            }
            return true;
        });
        var consumed = RBM_NONE;
        uint? firstTerminatorLocal = null;
        uint? secondTerminatorLocal = null;
        if (block.Kind is BBJ_SWITCH)
        {
            var terminator = block.LastNode
                ?? throw new FatalJitException("A switch edge requires its switch table.");
            assert(terminator.Oper is GT_SWITCH_TABLE);
            var codeGen = _compiler.codeGen
                ?? throw new FatalJitException("Switch edge resolution requires codegen state.");
            consumed = new regMaskTP(codeGen.InternalRegisters.GetAll(terminator).GetRegSetForType(TYP_INT));
            var first = terminator.AsOp().Op1;
            var second = terminator.AsOp().Op2;
            assert(first is not null && second is not null);
            assert(first.RegNum != REG_NA && second.RegNum != REG_NA);
            assert(varTypeIsIntegralOrI(first.Type) && varTypeIsIntegralOrI(second.Type));
            consumed |= regMaskTP.CreateFromRegNum(first.RegNum, genSingleTypeRegMask(first.RegNum));
            consumed |= regMaskTP.CreateFromRegNum(second.RegNum, genSingleTypeRegMask(second.RegNum));
            MarkTerminatorOperand(first, ref firstTerminatorLocal);
            MarkTerminatorOperand(second, ref secondTerminatorLocal);
        }
        else if (block.Kind is BBJ_COND)
        {
            var terminator = block.LastNode
                ?? throw new FatalJitException("A conditional edge requires its branch.");
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

        var sharedMap = _sharedCriticalVarToRegMap
            ?? throw new FatalJitException("Critical edge resolution requires the shared map.");
        var sameWrites = RBM_NONE;
        var differentReads = RBM_NONE;
        _ = VarSetOps.VisitBits(_compiler, outgoing, index =>
        {
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
                var targetMap = getInVarToRegMap(checked((uint)successor.bbNum))
                    ?? throw new FatalJitException("Critical edge requires a successor register map.");
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
            if (commonTarget is not REG_NA and not REG_STK)
            {
                var targetMask = regMaskTP.CreateFromRegNum(commonTarget, genSingleTypeRegMask(commonTarget));
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
                    differentReads |= regMaskTP.CreateFromRegNum(from, genSingleTypeRegMask(from));
                }
            }
            else if (commonTarget != from)
            {
                VarSetOps.AddElemD(_compiler, same, index);
                setVarReg(sharedMap, trackedIndex, commonTarget);
                if (commonTarget != REG_STK)
                {
                    sameWrites |= regMaskTP.CreateFromRegNum(commonTarget, genSingleTypeRegMask(commonTarget));
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
                var targetMap = getInVarToRegMap(checked((uint)successor.bbNum))
                    ?? throw new FatalJitException("Critical edge requires a successor register map.");
                var edgeSet = VarSetOps.Intersection(_compiler, different, successor.bbLiveIn);
                _ = VarSetOps.VisitBits(_compiler, edgeSet, index =>
                {
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
                    _ = VarSetOps.VisitBits(_compiler, edgeSet, index =>
                    {
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
