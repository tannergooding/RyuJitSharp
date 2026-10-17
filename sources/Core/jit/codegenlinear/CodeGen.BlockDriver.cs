// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCodeForBBlist()
    {
#if DEBUG
        _genInterruptibleUsed = true;
        _compiler.fgSafeBasicBlockCreation = false;
        if (Configuration.TARGET_X86 && Interruptible && _compiler.compStackCheckOnCall)
        {
            _compiler.compStackCheckOnCall = false;
        }
        if (Configuration.TARGET_XARCH && (Interruptible || _compiler.compTailCallUsed) &&
            _compiler.compStackCheckOnRet)
        {
            _compiler.compStackCheckOnRet = false;
        }
#endif
        genMarkLabelsForCodegen();
        genInitialize();

        foreach (ref readonly var funcInfo in _compiler.Funcs)
        {
            genCodeForFunclet(in funcInfo);
        }

        // Keep-alive locals can still be live after the final block.
        genUpdateLife(VarSetOps.MakeEmpty(_compiler));
        _regSet.rsSpillEnd();
        _regSet.tmpEnd();
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"\n# compCycleEstimate = {unchecked((nuint)_compiler.compCycleEstimate),6}, " +
                $"compSizeEstimate = {unchecked((nuint)_compiler.compSizeEstimate),5} {_compiler.info.compFullName}\n");
        }
#endif
    }

    public void genCodeForFunclet(in FuncInfoDsc funcInfo)
    {
        JITDUMP(funcInfo.funKind == FuncKind.FUNC_ROOT
            ? "\n=============== Generating code for main function\n"
            : "\n=============== Generating code for funclet\n");
        foreach (var block in funcInfo.Blocks(_compiler))
        {
            genCodeForBlock(block);
        }
    }

    public unsafe void genCodeForBlock(BasicBlock block)
    {
        if (block.Kind == BBJ_CALLFINALLYRET)
        {
            return;
        }

        JITDUMP("\n=============== Generating ");
#if DEBUG
        if (_compiler.verbose)
        {
            block.dspBlockHeader(showFlags: true, showPreds: true);
            _compiler.fgDispBBLiveness(block);
        }
#endif
        _regSet.ClearMaskVars();
        _gcInfo.gcRegGCrefSetCur = RBM_NONE;
        _gcInfo.gcRegByrefSetCur = RBM_NONE;
        var allocator = _compiler.RegisterAllocator;
        assert(allocator is not null);
        allocator.recordVarLocationsAtStartOfBB(block);
        genUpdateLife(block.bbLiveIn);
        var newLiveRegSet = RBM_NONE;
#if EMIT_GENERATE_GCINFO
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            // Liveness transitions alone do not restore unchanged register roots after the reset.
            var newRegGCrefSet = RBM_NONE;
            var newRegByrefSet = RBM_NONE;
#if DEBUG
            var removedGCVars = VarSetOps.MakeEmpty(_compiler);
            var addedGCVars = VarSetOps.MakeEmpty(_compiler);
#endif
            _ = VarSetOps.VisitBits(_compiler, block.bbLiveIn, varIndex =>
            {
                ref var varDsc = ref _compiler.lvaGetDesc(_compiler.lvaTrackedIndexToLclNum(varIndex));
                if (varDsc.lvIsInReg)
                {
                    var regMask = genGetRegMask(in varDsc);
                    newLiveRegSet |= regMask;
                    if (varDsc.Type == TYP_REF)
                    {
                        newRegGCrefSet |= regMask;
                    }
                    else if (varDsc.Type == TYP_BYREF)
                    {
                        newRegByrefSet |= regMask;
                    }

                    if (!varDsc.IsAlwaysAliveInMemory)
                    {
#if DEBUG
                        if (_verbose && VarSetOps.IsMember(_compiler, _gcInfo.gcVarPtrSetCur, varIndex))
                        {
                            VarSetOps.AddElemD(_compiler, removedGCVars, varIndex);
                        }
#endif
                        VarSetOps.RemoveElemD(_compiler, _gcInfo.gcVarPtrSetCur, varIndex);
                    }
                }
                if ((!varDsc.lvIsInReg || varDsc.IsAlwaysAliveInMemory) && _compiler.lvaIsGCTracked(in varDsc))
                {
#if DEBUG
                    if (_verbose && !VarSetOps.IsMember(_compiler, _gcInfo.gcVarPtrSetCur, varIndex))
                    {
                        VarSetOps.AddElemD(_compiler, addedGCVars, varIndex);
                    }
#endif
                    VarSetOps.AddElemD(_compiler, _gcInfo.gcVarPtrSetCur, varIndex);
                }
                return true;
            });
            _regSet.SetMaskVars(newLiveRegSet);
#if DEBUG
            if (_compiler.verbose)
            {
                if (!VarSetOps.IsEmpty(_compiler, addedGCVars))
                {
                    jitprintf("\t\t\t\t\t\t\tAdded GCVars: ");
                    dumpConvertedVarSet(_compiler, addedGCVars);
                    jitprintf("\n");
                }
                if (!VarSetOps.IsEmpty(_compiler, removedGCVars))
                {
                    jitprintf("\t\t\t\t\t\t\tRemoved GCVars: ");
                    dumpConvertedVarSet(_compiler, removedGCVars);
                    jitprintf("\n");
                }
            }
#endif
            _gcInfo.gcMarkRegSetGCref(newRegGCrefSet
#if DEBUG
                , forceOutput: true
#endif
            );
            _gcInfo.gcMarkRegSetByref(newRegByrefSet
#if DEBUG
                , forceOutput: true
#endif
            );

            if (handlerGetsXcptnObj(block.CatchType))
            {
                foreach (var node in block)
                {
                    if (node.Oper == GT_CATCH_ARG)
                    {
                        _gcInfo.gcMarkRegSetGCref(new regMaskTP(SRBM_EXCEPTION_OBJECT));
                        break;
                    }
                }
            }
        }
#endif

        genLogLabel(block);
        _compiler.compCurBB = block;
        block.bbEmitCookie = null;
        var needLabel = block.HasFlag(BBF_HAS_LABEL);
        if (block.IsFirstColdBlock(_compiler))
        {
            JITDUMP("\nThis is the start of the cold region of the method\n");
            noway_assert(!block.isBBCallFinallyPairTail);
            needLabel = true;
        }
        if (!block.IsFirst && (block.Prev.Kind == BBJ_COND) && (block.bbWeight != block.Prev.bbWeight))
        {
            JITDUMP($"Adding label due to BB weight difference: BBJ_COND {FMT_BB(block.Prev.bbNum)} " +
                $"with weight {FMT_WT(block.Prev.bbWeight)} different from {FMT_BB(block.bbNum)} " +
                $"with weight {FMT_WT(block.bbWeight)}\n");
            needLabel = true;
        }
        if (Configuration.FEATURE_LOOP_ALIGN && Emitter.emitEndsWithAlignInstr())
        {
            needLabel = true;
        }
        if (needLabel)
        {
            block.bbEmitCookie = Emitter.emitAddLabel(_gcInfo.gcVarPtrSetCur, _gcInfo.gcRegGCrefSetCur,
                _gcInfo.gcRegByrefSetCur, block.Prev);
        }
        if (block.IsFirstColdBlock(_compiler))
        {
            noway_assert(block.bbEmitCookie is not null);
            Emitter.emitSetFirstColdIGCookie(block.bbEmitCookie);
        }

        assert(genStackLevel == 0);
        genAdjustStackLevel(block);
        var savedStkLvl = genStackLevel;
        siBeginBlock(block);
        if (_compiler.opts.compDbgInfo && block.HasFlag(BBF_INTERNAL) && !block.IsFirst)
        {
            genIPmappingAdd(IPmappingDscKind.NoMapping, default, true);
        }
        if (_compiler.bbIsFuncletBeg(block))
        {
            genUpdateCurrentFunclet(block);
            genReserveFuncletProlog(block);
        }

        genEmitStartBlock(block);
        _compiler.compCurStmt = null;
        _compiler.compCurLifeTree = null;
        if (Configuration.SWIFT_SUPPORT)
        {
            // Reassembly can require arbitrary amounts of code, so it belongs in the first block after the prolog.
            if (block.IsFirst && _compiler.lvaHasAnySwiftStackParamToReassemble())
            {
                genHomeSwiftStructStackParameters();
            }
        }
        if (Configuration.TARGET_ARM64 && _compiler.compUsesUnknownSizeFrame && block.IsFirst)
        {
            genZeroInitializeUnknownSizeFrame();
        }
        if (!Configuration.TARGET_WASM && _compiler.compShouldPoisonFrame() && block.IsFirst)
        {
            genPoisonFrame(newLiveRegSet);
        }
#if DEBUG
        var useNum = 0;
        foreach (var node in block)
        {
            assert((node._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) == 0);
            node.UseNum = -1;
            if (node.IsContained || node.Oper.IsCopyOrReload)
            {
                continue;
            }
            foreach (var operand in node.Operands)
            {
                genNumberOperandUse(operand, ref useNum);
            }
        }
#endif
        var producedLabelMapping = false;
        var addRichMappings = JitConfig.RichDebugInfo != 0;
#if DEBUG
        addRichMappings |= JitConfig.JitDisasmWithDebugInfo != 0;
        addRichMappings |= JitConfig.WriteRichDebugInfoFile is not null;
#endif
        DebugInfo currentDI = default;
        foreach (var node in block)
        {
            if (node.Oper == GT_IL_OFFSET)
            {
                var ilOffset = node.AsILOffset();
                var rootDI = ilOffset.StmtDebugInfo.GetRoot();
                if (rootDI.IsValid)
                {
                    genEnsureCodeEmitted(in currentDI);
                    currentDI = rootDI;
                    // Joins use the earliest mapping; synthetic async offsets are not join points.
                    var isLabel = !producedLabelMapping && !currentDI.Location.IsAsync;
                    genIPmappingAdd(IPmappingDscKind.Normal, in currentDI, isLabel);
                    producedLabelMapping |= isLabel;
                }
                if (addRichMappings && ilOffset.StmtDebugInfo.IsValid)
                {
                    genAddRichIPMappingHere(in ilOffset.StmtDebugInfo);
                }
#if DEBUG
                assert((ilOffset.StmtLastILOffset <= _compiler.info.compILCodeSize) ||
                    (ilOffset.StmtLastILOffset == BAD_IL_OFFSET));
                if (_compiler.opts.dspCode && _compiler.opts.dspInstrs &&
                    ilOffset.StmtLastILOffset != BAD_IL_OFFSET)
                {
                    while (_genCurDispOffset <= ilOffset.StmtLastILOffset)
                    {
                        _genCurDispOffset = unchecked(_genCurDispOffset +
                            (uint)dumpSingleInstr(_compiler.info.compCode, (IL_OFFSET)_genCurDispOffset, ">    "));
                    }
                }
#endif
            }
            if (Configuration.TARGET_WASM)
            {
                genCodeForTreeNodeWasm(node);
            }
            else
            {
                genCodeForTreeNode(node);
            }
            if (node.HasReg(_compiler) && node.IsUnusedValue)
            {
                _ = genConsumeReg(node);
            }
        }
        if (Configuration.DEBUG)
        {
            _regSet.rsSpillChk();
#if EMIT_GENERATE_GCINFO
            if (Configuration.HAS_FIXED_REGISTER_SET)
            {
                var ptrRegs = _gcInfo.gcRegGCrefSetCur | _gcInfo.gcRegByrefSetCur;
                var nonVarPtrRegs = ptrRegs & ~_regSet.GetMaskVars();
                if (_compiler.compMethodReturnsRetBufAddr)
                {
                    nonVarPtrRegs &= ~new regMaskTP(TargetRuntimeMetadata.IntegerReturnMask);
                }
                else
                {
                    ref readonly var retTypeDesc = ref _compiler.compRetTypeDesc;
                    var regCount = retTypeDesc.ReturnRegCount;
                    for (byte i = 0; i < regCount; i++)
                    {
                        var reg = retTypeDesc.GetAbiReturnReg(i, _compiler.info.compCallConv);
                        nonVarPtrRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
                    }
                }
                if (_compiler.compIsAsync)
                {
                    nonVarPtrRegs &= ~regMaskTP.CreateFromRegNum(TargetRuntimeMetadata.AsyncContinuationReturnRegister,
                        TargetRuntimeMetadata.AsyncContinuationReturnRegister.SingleTypeMask);
                }
                if (block.HasFlag(BBF_HAS_JMP))
                {
                    nonVarPtrRegs = RBM_NONE;
                }
                if (nonVarPtrRegs.IsNonEmpty)
                {
                    jitprintf($"Regset after {FMT_BB(block.bbNum)} gcr=");
                    printRegMaskInt(_gcInfo.gcRegGCrefSetCur & ~_regSet.GetMaskVars());
                    Emitter.emitDispRegSet(_gcInfo.gcRegGCrefSetCur & ~_regSet.GetMaskVars());
                    jitprintf(", byr=");
                    printRegMaskInt(_gcInfo.gcRegByrefSetCur & ~_regSet.GetMaskVars());
                    Emitter.emitDispRegSet(_gcInfo.gcRegByrefSetCur & ~_regSet.GetMaskVars());
                    jitprintf(", regVars=");
                    printRegMaskInt(_regSet.GetMaskVars());
                    Emitter.emitDispRegSet(_regSet.GetMaskVars());
                    jitprintf("\n");
                }
                noway_assert(nonVarPtrRegs.IsEmpty);
            }
#endif
        }
#if DEBUG
        if (block.IsLast)
        {
            genEmitterUnitTests();
        }
#endif
        genEnsureCodeEmitted(in currentDI);
        var isLastBlockProcessed = block.IsLast;
        if (block.isBBCallFinallyPair)
        {
            assert(block.Next is not null);
            isLastBlockProcessed = block.Next.IsLast;
        }
        if (_compiler.opts.compDbgInfo && isLastBlockProcessed)
        {
            getVariableLiveKeeper().siEndAllVariableLiveRange(_compiler.compCurLife);
        }
        if (_compiler.opts.compScopeInfo && (_compiler.info.compVarScopesCount > 0))
        {
            siEndBlock(block);
        }
        SubtractStackLevel(savedStkLvl);
        if (Configuration.DEBUG)
        {
            var mismatchLiveVars = VarSetOps.Diff(_compiler, block.bbLiveOut, _compiler.compCurLife);
            VarSetOps.UnionD(_compiler, mismatchLiveVars, VarSetOps.Diff(_compiler, _compiler.compCurLife, block.bbLiveOut));
            var foundMismatchedRegVar = false;
            _ = VarSetOps.VisitBits(_compiler, mismatchLiveVars, varIndex =>
            {
                var varNum = _compiler.lvaTrackedIndexToLclNum(varIndex);
                ref var varDsc = ref _compiler.lvaGetDesc(varNum);
                if (varDsc.lvIsRegCandidate)
                {
                    if (!foundMismatchedRegVar)
                    {
                        JITDUMP($"Mismatched live reg vars after {FMT_BB(block.bbNum)}:");
                        foundMismatchedRegVar = true;
                    }
                    JITDUMP($" V{varNum:D2}");
                }
                return true;
            });
            if (foundMismatchedRegVar)
            {
                JITDUMP("\n");
                assert(false, "Found mismatched live reg var(s) after block");
            }
        }
        noway_assert(genStackLevel == 0);
        genEmitEndBlock(block);
        if (Configuration.DEBUG && _compiler.verbose)
        {
            getVariableLiveKeeper().dumpBlockVariableLiveRanges(block);
        }
        _compiler.compCurBB = null;
    }

    private void genEmitStartBlock(BasicBlock block)
    {
        if (Configuration.TARGET_WASM)
        {
            genEmitStartBlockWasm(block);
        }
    }

    private void SubtractStackLevel(uint adjustment)
    {
        assert(genStackLevel >= adjustment);
        var newStackLevel = genStackLevel - adjustment;
        if (genStackLevel != newStackLevel)
        {
            JITDUMP($"Adjusting stack level from {genStackLevel} to {newStackLevel}\n");
        }
        genStackLevel = newStackLevel;
    }
}
