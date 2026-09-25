// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private regMaskTP _actualRegistersMask;

    private void buildIntervalsMinimal()
    {
        if (_enregisterLocalVars)
        {
            throw new FatalJitException("Minimal interval construction cannot run with enregistered locals.");
        }

#if TARGET_AMD64
        JITDUMP("\nbuildIntervals ========\n");
        buildPhysRegRecords();

#if DEBUG
        if (VERBOSE)
        {
            jitprintf("\n-----------------\nLIVENESS:\n-----------------\n");
            foreach (var block in _compiler.Blocks)
            {
                jitprintf($"{FMT_BB(block.bbNum)}\nuse: ");
                dumpCandidateVarSet(block.bbVarUse);
                jitprintf("\ndef: ");
                dumpCandidateVarSet(block.bbVarDef);
                jitprintf("\n in: ");
                dumpCandidateVarSet(block.bbLiveIn);
                jitprintf("\nout: ");
                dumpCandidateVarSet(block.bbLiveOut);
                jitprintf("\n");
            }
        }
#endif

        resetRegStateMinimal();
        identifyCandidatesMinimal();
        setFrameType();
        _lowGprRegs = _availableIntRegs & SRBM_LOWINT;

#if DEBUG
        if (VERBOSE)
        {
            tupleStyleDumpPre();
        }
#endif

        JITDUMP("\nbuildIntervals second part ========\n");
        _referenceBuildLocation = 0;
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
                    var mappedLocalIsLive = !mappedLocal.lvTracked || _compiler.compJmpOpUsed ||
                        (mappedLocal.lvRefCnt() != 0);
                    isLive = mappedLocal.lvIsStructField
                        ? mappedLocalIsLive
                        : parameterIsLive || mappedLocalIsLive;
                }

                JITDUMP($"Arg V{mapping?.LclNum ?? localNumber:D2} is {(isLive ? "live" : "dead")} in reg {segment.Register.Name}\n");
                if (isLive)
                {
                    incomingRegisters |= regMaskTP.CreateFromRegNum(
                        segment.Register, genSingleTypeRegMask(segment.Register));
                }
            }
        }

        // identifyCandidates<false> leaves no local candidates, so the native parameter-definition
        // and parameter-preference passes produce no references in this specialization.
        if (_compiler.info.compPublishStubParam)
        {
            incomingRegisters |= new regMaskTP(SRBM_SECRET_STUB_PARAM);
        }

        _placedArgumentLocalCount = 0;
        _placedArgumentRegisters = RBM_NONE;
        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVariables, VarSetOps.MakeEmpty(_compiler));
        BasicBlock? previousBlock = null;

        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            JITDUMP($"\nNEW BLOCK {FMT_BB(block.bbNum)}\n");
            _compiler.compCurBB = block;
            _needToKillFloatRegisters = false;
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

            foreach (var node in block)
            {
#if DEBUG
                node._seqNum = unchecked((int)_referenceBuildLocation);
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
                GenTreeCall? tailCallNode = null;
                if (isTailCall)
                {
                    var lastNode = block.LastNode
                        ?? throw new FatalJitException("A tailcall block must contain its jump or call.");
                    if (lastNode.Oper is GT_CALL)
                    {
                        tailCallNode = lastNode.AsCall();
                        assert(tailCallNode.IsFastTailCall);
                    }
                }

                _ = addKillForRegs(codeGen.genGetGSCookieTempRegs(isTailCall, tailCallNode),
                    _referenceBuildLocation + 1);
                _referenceBuildLocation += 2;
            }

            markBlockVisited(block);
            if (_definitionList.First is not null)
            {
#if DEBUG
                dumpDefList();
#endif
                assert(false, "Expected empty defList at end of block");
            }
            previousBlock = block;
        }

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
        else if (_availableRegCount < (maskBitCount * 2))
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
        // Native validateIntervals has no work when local variables are not enregistered.
#endif
#else
        NYI("LinearScan.buildIntervalsMinimal outside AMD64");
        throw new FatalJitException("LinearScan.buildIntervalsMinimal outside AMD64.");
#endif
    }
}
