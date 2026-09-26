// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void buildIntervalsWithLocals()
    {
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        if (!_enregisterLocalVars)
        {
            throw new FatalJitException("Local interval construction requires enregistered locals.");
        }

        JITDUMP("\nbuildIntervals ========\n");
        buildPhysRegRecords();

#if DEBUG
        if (VERBOSE)
        {
            jitprintf("\n-----------------\nLIVENESS:\n-----------------\n");
            foreach (var block in _compiler.Blocks)
            {
                jitprintf($"{FMT_BB(block.bbNum)}\nuse: ");
                dumpConvertedVarSet(_compiler, block.bbVarUse);
                jitprintf("\ndef: ");
                dumpConvertedVarSet(_compiler, block.bbVarDef);
                jitprintf("\n in: ");
                dumpConvertedVarSet(_compiler, block.bbLiveIn);
                jitprintf("\nout: ");
                dumpConvertedVarSet(_compiler, block.bbLiveOut);
                jitprintf("\n");
            }
        }
#endif

        resetRegStateWithLocals();
        identifyCandidatesWithLocals();
        setFrameType();
        _lowGprRegs = _availableIntRegs & SRBM_LOWINT;

#if DEBUG
        if (VERBOSE)
        {
            tupleStyleDumpPre();
        }
#endif

        JITDUMP("\nbuildIntervals second part ========\n");
        _referenceBuildLocation = MinLocation;
        if (!_blockSequencingDone)
        {
            setBlockSequence();
        }

        _currentBlockNumber = 0;
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Interval construction requires initialized codegen state.");
        ref var incomingRegisters = ref codeGen.CalleeRegArgMaskLiveIn;
        incomingRegisters = RBM_NONE;
        _regsInUseThisLocation = RBM_NONE;
        _regsInUseNextLocation = RBM_NONE;

        for (var localNumber = 0; localNumber < _compiler.info.compArgsCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(localNumber);
            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (!segment.IsPassedInRegister)
                {
                    continue;
                }

                var mapping = _compiler.FindParameterRegisterLocalMappingByRegister(segment.Register);
                var parameterIsLive = !local.lvTracked || _compiler.compJmpOpUsed || (local.lvRefCnt() != 0);
                var isLive = parameterIsLive;
                if (mapping is ParameterRegisterLocalMapping registerMapping)
                {
                    ref var mappedLocal = ref _compiler.lvaGetDesc(registerMapping.LclNum);
                    var mappedIsLive = !mappedLocal.lvTracked || _compiler.compJmpOpUsed ||
                        (mappedLocal.lvRefCnt() != 0);
                    isLive = mappedLocal.lvIsStructField ? mappedIsLive : parameterIsLive || mappedIsLive;
                }

                JITDUMP($"Arg V{mapping?.LclNum ?? localNumber:D2} is {(isLive ? "live" : "dead")} in reg {segment.Register.Name}\n");
                if (isLive)
                {
                    incomingRegisters |= regMaskTP.CreateFromRegNum(
                        segment.Register, genSingleTypeRegMask(segment.Register));
                }
            }
        }

        for (var variableIndex = 0; variableIndex < _compiler.lvaTrackedCount; variableIndex++)
        {
            assert(_compiler.lvaTrackedToVarNum is not null);
            var localNumber = _compiler.lvaTrackedToVarNum[variableIndex];
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            if (!local.lvLRACandidate ||
                (!_compiler.compJmpOpUsed && (local.lvRefCnt() == 0) && !_compiler.opts.compDbgCode))
            {
                continue;
            }

            var parameterRegister = REG_NA;
            if (local.lvIsParamRegTarget)
            {
                var mapping = findParameterRegisterLocalMappingByLocal(localNumber, 0);
                assert(mapping is not null);
                parameterRegister = mapping.Value.RegisterSegment.Register;
            }
            else if (local.lvIsParam)
            {
                if (!_compiler.opts.IsOSR && !local.lvIsStructField)
                {
                    ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(localNumber);
                    foreach (ref readonly var segment in abiInfo.Segments)
                    {
                        if (segment.IsPassedInRegister)
                        {
                            parameterRegister = segment.Register;
                            break;
                        }
                    }
                }
                else if (local.lvIsStructField && !_compiler.opts.IsOSR)
                {
                    assert(!_compiler.lvaGetParameterAbiInfo(local.lvParentLcl).HasAnyRegisterSegment);
                }
            }
            else
            {
                continue;
            }

            buildInitialParamDef(in local, parameterRegister);
        }

        if (_compiler.info.compPublishStubParam)
        {
            incomingRegisters |= new regMaskTP(SRBM_SECRET_STUB_PARAM);
            ref var stubParameter = ref _compiler.lvaGetDesc(_compiler.lvaStubArgumentVar);
            if (stubParameter.lvLRACandidate)
            {
                buildInitialParamDef(in stubParameter, REG_SECRET_STUB_PARAM);
            }
        }

#if DEBUG
        if (stressInitialParamReg())
        {
            stressSetRandomParameterPreferences();
        }
#endif

        _placedArgumentLocalCount = 0;
        _placedArgumentRegisters = RBM_NONE;
        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVariables, VarSetOps.MakeEmpty(_compiler));
        BasicBlock? previousBlock = null;

        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            JITDUMP($"\nNEW BLOCK {FMT_BB(block.bbNum)}\n");
            _compiler.compCurBB = block;
            _needToKillFloatRegisters = _compiler.compFloatingPointUsed;
            var predecessorAllocated = false;
            var predecessor = findPredBlockForLiveIn(block, previousBlock, ref predecessorAllocated);
            if (predecessor is not null)
            {
                JITDUMP($"\n\nSetting {FMT_BB(predecessor.bbNum)} as the predecessor for determining incoming variable registers of {FMT_BB(block.bbNum)}\n");
                assert((uint)predecessor.bbNum <= _bbNumMaxBeforeResolution);
                assert(_blockInfo is not null);
                _blockInfo[block.bbNum].predBBNum = (uint)predecessor.bbNum;
            }

            VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVariables,
                VarSetOps.Intersection(_compiler, _registerCandidateVars, block.bbLiveIn));
            if (block == _compiler.fgFirstBB)
            {
                insertZeroInitRefPositions();
                // BB0 owns entry definitions at location 0; real blocks start at 1.
                _referenceBuildLocation = 1;
            }

            // An EH incoming value comes from the stack, not a synthesized register definition.
            assert(_blockInfo is not null);
            if (!_blockInfo[block.bbNum].hasEHBoundaryIn)
            {
                VarSetOps.UnionD(_compiler, _resolutionCandidateVars, _currentLiveVariables);
                if (block != _compiler.fgFirstBB)
                {
                    // Potentially uninitialized first-block locals remain in memory instead.
                    var newLiveIn = VarSetOps.MakeCopy(_compiler, _currentLiveVariables);
                    if (predecessor is not null)
                    {
                        VarSetOps.DiffD(_compiler, newLiveIn, predecessor.bbLiveOut);
                    }
                    // Exception-live locals reload from their stack homes as needed.
                    VarSetOps.DiffD(_compiler, newLiveIn, _exceptVars);

                    if (!VarSetOps.IsEmpty(_compiler, newLiveIn))
                    {
                        assert(!predecessorAllocated);
                        JITDUMP("Creating dummy definitions\n");
                        _ = VarSetOps.VisitBits(_compiler, newLiveIn, variableIndex => {
                            ref var local = ref getTrackedLocal(variableIndex);
                            assert(local.lvLRACandidate);
                            var interval = getIntervalForLocalVar(checked((uint)variableIndex));
                            var position = newRefPosition(interval, _referenceBuildLocation,
                                RefType.RefTypeDummyDef, null, allRegs(interval.registerType));
                            position.setRegOptional(true);
                            return true;
                        });
                        JITDUMP("Finished creating dummy definitions\n\n");
                    }
                }
            }

            _ = newRefPosition(null, _referenceBuildLocation, RefType.RefTypeBB, null, SRBM_NONE);
            _referenceBuildLocation += 2;
            JITDUMP("\n");

            if ((_firstColdLocation == MaxLocation) && block.isRunRarely)
            {
                _firstColdLocation = _referenceBuildLocation;
                JITDUMP($"firstColdLoc = {_firstColdLocation}\n");
            }

            if ((block == _compiler.fgFirstBB) && _compiler.lvaHasAnySwiftStackParamToReassemble())
            {
                _ = addKillForRegs(regMaskTP.CreateFromRegNum(REG_SCRATCH, genSingleTypeRegMask(REG_SCRATCH)),
                    _referenceBuildLocation + 1);
                _referenceBuildLocation += 2;
            }

            if (_compiler.compShouldPoisonFrame() && (block == _compiler.fgFirstBB))
            {
                _ = addKillForRegs(RBM_EDI | RBM_ECX | RBM_EAX, _referenceBuildLocation + 1);
                _referenceBuildLocation += 2;
            }

            // Two locations per node keep definitions separate from operand uses.
            foreach (var node in block)
            {
#if DEBUG
                node._seqNum = unchecked((int)_referenceBuildLocation);
                // Self-assignment sets the register tag used by subsequent DEBUG dumps.
                node.RegNum = node.Reg;
#endif
                buildRefPositionsForNode(node, _referenceBuildLocation);
#if DEBUG
                if (_referenceBuildLocation > _maxNodeLocation)
                {
                    _maxNodeLocation = _referenceBuildLocation;
                }
#endif
                _referenceBuildLocation += 2;
            }

            if (_compiler.NeedsGSSecurityCookie && (block.Kind is BBJ_RETURN))
            {
                var isTailCall = block.HasFlag(BBF_HAS_JMP);
                GenTreeCall? tailCall = null;
                if (isTailCall)
                {
                    var lastNode = block.LastNode
                        ?? throw new FatalJitException("A tailcall block must contain its jump or call.");
                    if (lastNode.Oper is GT_CALL)
                    {
                        tailCall = lastNode.AsCall();
                        assert(tailCall.IsFastTailCall);
                    }
                }
                _ = addKillForRegs(codeGen.genGetGSCookieTempRegs(isTailCall, tailCall),
                    _referenceBuildLocation + 1);
                _referenceBuildLocation += 2;
            }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            _ = VarSetOps.VisitBits(_compiler, _largeVectorVars, variableIndex => {
                var interval = getIntervalForLocalVar(checked((uint)variableIndex));
                buildUpperVectorRestoreRefPosition(interval, _referenceBuildLocation, null, false, 0);
                return true;
            });
#endif

            markBlockVisited(block);
            if (_definitionList.First is not null)
            {
#if DEBUG
                dumpDefList();
#endif
                assert(false, "Expected empty defList at end of block");
            }

            // Backedges and GT_JMP can keep locals live without another explicit LIR use.
            var exposedUses = VarSetOps.Intersection(_compiler, block.bbLiveOut, _registerCandidateVars);
            var nextBlock = getNextBlock();
            if (nextBlock is not null)
            {
                VarSetOps.DiffD(_compiler, exposedUses, nextBlock.bbLiveIn);
            }

            BasicBlockVisit ExcludeUnvisitedSuccessor(BasicBlock successor)
            {
                if (VarSetOps.IsEmpty(_compiler, exposedUses))
                {
                    return BasicBlockVisit.Abort;
                }
                if (!isBlockVisited(successor))
                {
                    VarSetOps.DiffD(_compiler, exposedUses, successor.bbLiveIn);
                }
                return BasicBlockVisit.Continue;
            }

            if (block.VisitRegularSuccs(_compiler, ExcludeUnvisitedSuccessor) is not BasicBlockVisit.Abort)
            {
                _ = block.VisitEHSuccs(_compiler, ExcludeUnvisitedSuccessor);
            }

            if (!VarSetOps.IsEmpty(_compiler, exposedUses))
            {
                JITDUMP("Exposed uses:\n");
                _ = VarSetOps.VisitBits(_compiler, exposedUses, variableIndex => {
                    ref var local = ref getTrackedLocal(variableIndex);
                    assert(local.lvLRACandidate);
                    var interval = getIntervalForLocalVar(checked((uint)variableIndex));
                    var position = newRefPosition(interval, _referenceBuildLocation,
                        RefType.RefTypeExpUse, null, allRegs(interval.registerType));
                    position.setRegOptional(true);
                    return true;
                });
            }

            var liveOutCandidates = VarSetOps.Intersection(_compiler, _registerCandidateVars, block.bbLiveOut);
            _ = VarSetOps.VisitBits(_compiler, liveOutCandidates, variableIndex => {
                ref var local = ref getTrackedLocal(variableIndex);
                assert(local.lvLRACandidate);
                var lastReference = getIntervalForLocalVar(checked((uint)variableIndex)).lastRefPosition;
                if ((lastReference is not null) && (lastReference.bbNum == block.bbNum))
                {
                    lastReference.lastUse = false;
                }
                return true;
            });

#if DEBUG
            checkLastUses(block);
            if (VERBOSE)
            {
                jitprintf("use: ");
                dumpConvertedVarSet(_compiler, block.bbVarUse);
                jitprintf("\ndef: ");
                dumpConvertedVarSet(_compiler, block.bbVarDef);
                jitprintf("\n");
            }
#endif
            previousBlock = block;
        }

        if (_compiler.lvaKeepAliveAndReportThis())
        {
            assert(!_compiler.info.compIsStatic);
            ref var thisLocal = ref _compiler.lvaGetDesc(_compiler.info.compThisArg);
            if (thisLocal.lvLRACandidate)
            {
                JITDUMP("Adding exposed use of this, for lvaKeepAliveAndReportThis\n");
                var interval = getIntervalForLocalVar(thisLocal._varIndex);
                var position = newRefPosition(interval, _referenceBuildLocation,
                    RefType.RefTypeExpUse, null, allRegs(interval.registerType));
                position.setRegOptional(true);
            }
        }

        if (_compiler.compHndBBtabCount > 0)
        {
            var blockInfo = _blockInfo
                ?? throw new FatalJitException("Write-through heuristics require block sequencing.");
            _ = VarSetOps.VisitBits(_compiler, _exceptVars, variableIndex => {
                ref var local = ref getTrackedLocal(variableIndex);
                var interval = getIntervalForLocalVar(checked((uint)variableIndex));
                assert(interval.isWriteThru);
                var weight = local.lvRefCntWtd();
                var firstReference = interval.firstRefPosition;
                assert(firstReference is not null);
                var initialWeight = firstReference.refType is RefType.RefTypeParamDef
                    ? 2 * BB_UNITY_WEIGHT
                    : blockInfo[firstReference.bbNum].weight;
                weight -= initialWeight;

                if (interval.preferCalleeSave)
                {
                    // Write-through EH values pay stack/copy costs as well as callee-save
                    // save/restore; seven weighted uses is the conservative cutoff.
                    var calleeSaveCount = varTypeUsesIntReg(interval.registerType)
                        ? CNT_CALLEE_ENREG
                        : varTypeUsesMaskReg(interval.registerType)
                            ? CNT_CALLEE_ENREG_MASK
                            : CNT_CALLEE_ENREG_FLOAT;
                    if ((weight <= (BB_UNITY_WEIGHT * 7)) || (local._varIndex >= calleeSaveCount))
                    {
                        interval.preferCalleeSave = false;
                    }
                    else
                    {
                        interval.registerPreferences |= calleeSaveRegs(interval.registerType);
                    }
                }
                return true;
            });
        }

#if DEBUG
        if (extendLifetimes())
        {
            for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
            {
                ref var local = ref _compiler.lvaGetDesc(localNumber);
                if (local.lvLRACandidate)
                {
                    JITDUMP($"Adding exposed use of V{localNumber:D2} for LsraExtendLifetimes\n");
                    var interval = getIntervalForLocalVar(local._varIndex);
                    var position = newRefPosition(interval, _referenceBuildLocation,
                        RefType.RefTypeExpUse, null, allRegs(interval.registerType));
                    position.setRegOptional(true);
                }
            }
        }
#endif

        assert(previousBlock is not null);
        if (previousBlock.NumSucc > 0)
        {
            _ = newRefPosition(null, _referenceBuildLocation, RefType.RefTypeBB, null, SRBM_NONE);
        }

        _needNonIntegerRegisters |= _compiler.compFloatingPointUsed;
        if (!_needNonIntegerRegisters)
        {
            _availableRegCount = _regIntLast - REG_INT_FIRST + 1;
        }

        const int maskBitCount = sizeof(ulong) * 8;
        if (_availableRegCount < maskBitCount)
        {
            _actualRegistersMask = new regMaskTP((regMask)((1UL << _availableRegCount) - 1));
        }
#if HAS_MORE_THAN_64_REGISTERS
        else if (_availableRegCount < maskBitCount * 2)
        {
            _actualRegistersMask = new regMaskTP(~SRBM_NONE, _availableMaskRegs);
        }
        else
        {
            _actualRegistersMask = new regMaskTP(~SRBM_NONE, ~SRBM_NONE);
        }
#else
        else
        {
            _actualRegistersMask = new regMaskTP(~SRBM_NONE);
        }
#endif

#if DEBUG
        foreach (var block in _compiler.Blocks)
        {
            assert(isBlockVisited(block));
        }
        if (VERBOSE)
        {
            dumpLsraIntervals("BEFORE VALIDATING INTERVALS");
            dumpRefPositions("BEFORE VALIDATING INTERVALS");
        }
        validateIntervals();
#endif
#else
        throw new FatalJitException("Local interval construction is not implemented outside Windows AMD64.");
#endif
    }

    private void resetRegStateWithLocals()
    {
        initializeAvailableRegs();
        _regsBusyUntilKill = RBM_NONE;
    }

    private ref LclVarDsc getTrackedLocal(int variableIndex)
    {
        var trackedToLocal = _compiler.lvaTrackedToVarNum
            ?? throw new FatalJitException("Tracked local mapping is required for interval construction.");
        return ref _compiler.lvaGetDesc(trackedToLocal[variableIndex]);
    }

    private ParameterRegisterLocalMapping? findParameterRegisterLocalMappingByLocal(int localNumber, uint offset)
    {
        if (_compiler._paramRegLocalMappings is not null)
        {
            foreach (var mapping in _compiler._paramRegLocalMappings)
            {
                if ((mapping.LclNum == localNumber) && (mapping.Offset == offset))
                {
                    return mapping;
                }
            }
        }

        return null;
    }
}
