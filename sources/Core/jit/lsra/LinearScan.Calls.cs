// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildCall(GenTreeCall call)
    {
#if TARGET_XARCH
        assert(!call.IsContained);

        var hasMultiRegRetVal = false;
        var dstCount = 0;
        var singleDstCandidates = SRBM_NONE;
        if (call.Type is not TYP_VOID)
        {
            hasMultiRegRetVal = call.HasMultiRegRetVal;
            dstCount = hasMultiRegRetVal ? call.ReturnTypeDesc.ReturnRegCount : 1;
        }

        if (!hasMultiRegRetVal && (dstCount != 0))
        {
#if TARGET_X86
            if (call.IsHelperCall(CORINFO_HELP_INIT_PINVOKE_FRAME))
            {
                singleDstCandidates = SRBM_ESI;
            }
            else if (varTypeUsesFloatReg(call.Type))
            {
                singleDstCandidates = allRegs(regType(call.Type));
            }
            else
            {
                assert(varTypeUsesIntReg(call.Type));
                singleDstCandidates = (call.Type is TYP_LONG) ? SRBM_EAX | SRBM_EDX : SRBM_EAX;
            }
#else
            if (varTypeUsesFloatReg(call.Type))
            {
                singleDstCandidates = SRBM_FLOATRET;
            }
            else
            {
                assert(varTypeUsesIntReg(call.Type));
                singleDstCandidates = (call.Type is TYP_LONG) ? SRBM_LNGRET : SRBM_INTRET;
            }
#endif
        }

#if WINDOWS_AMD64_ABI
        var callHasFloatRegArgs = false;
        if (compFeatureVarArg() && call.Args.IsVarArgs)
        {
            foreach (var arg in call.Args.LateArgs)
            {
                foreach (ref readonly var segment in arg.AbiInfo.Segments)
                {
                    if (segment.IsPassedInRegister && genIsValidFloatReg(segment.Register))
                    {
                        var correspondingReg = getVarargsIntRegister(segment.Register);
                        _ = buildInternalIntRegisterDefForNode(call, genSingleTypeRegMask(correspondingReg));
                        callHasFloatRegArgs = true;
                    }
                }
            }
        }
#endif

        var srcCount = buildCallArgUses(call);
        var ctrlExpr = call.ControlExpr;
        if (ctrlExpr is not null)
        {
            var ctrlExprCandidates = SRBM_NONE;
            if (call.IsFastTailCall)
            {
                ctrlExprCandidates = _rbmIntCalleeTrash;
                if (_compiler.NeedsGSSecurityCookie)
                {
                    assert(_compiler.codeGen is not null);
                    ctrlExprCandidates &= ~_compiler.codeGen.genGetGSCookieTempRegs(true, call).IntRegSet;
                }
            }
#if TARGET_X86
            else if (call.IsVirtualStub && (call._callType is CT_INDIRECT) &&
                !_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
            {
                assert(ctrlExpr.IsContainedIndir);
                ctrlExprCandidates = SRBM_EAX;
            }
#endif

#if WINDOWS_AMD64_ABI
            if (compFeatureVarArg() && call.Args.IsVarArgs && callHasFloatRegArgs &&
                (ctrlExprCandidates == SRBM_NONE))
            {
                ctrlExprCandidates = _availableIntRegs & ~SRBM_ARG_REGS;
            }
#endif

            srcCount += buildOperandUses(ctrlExpr, ctrlExprCandidates);
        }

        if (call.NeedsVzeroupper(_compiler))
        {
            var codeGen = _compiler.codeGen
                ?? throw new FatalJitException("Call reference building requires initialized codegen state.");
            codeGen.Emitter.ContainsCallNeedingVzeroupper = true;
        }

        buildInternalRegisterUses();

        if (call.IsAsync && _compiler.compIsAsync && !call.IsFastTailCall)
        {
            markAsyncContinuationBusyForCall(call);
        }

        var killMask = getKillSetForCall(call);
        if (dstCount > 0)
        {
            if (hasMultiRegRetVal)
            {
                var multiDstCandidates = new regMaskTP(SRBM_NONE);
                for (var index = 0; index < dstCount; index++)
                {
                    var register = call.ReturnTypeDesc.GetAbiReturnReg(
                        checked((byte)index), call.UnmanagedCallConv);
                    multiDstCandidates |= regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
                }

                assert(countRegisterMaskBits(multiDstCandidates) == dstCount);
                buildCallDefsWithKills(call, dstCount, multiDstCandidates, killMask);
            }
            else
            {
                assert(dstCount == 1);
                buildDefWithKills(call, dstCount, singleDstCandidates, killMask);
            }
        }
        else
        {
            buildKills(call, killMask);
        }

#if SWIFT_SUPPORT
        if ((call.UnmanagedCallConv is CorInfoCallConvExtension.Swift) &&
            (call.Args.FindWellKnownArg(WellKnownArg.SwiftError) is not null))
        {
            markSwiftErrorBusyForCall(call);
        }
#endif

        _placedArgumentRegisters = new regMaskTP(SRBM_NONE);
        _placedArgumentLocalCount = 0;
        return srcCount;
#elif TARGET_ARM64
        assert(!call.IsContained);

        var hasMultiRegRetVal = call.Type is not TYP_VOID && call.HasMultiRegRetVal;
        var dstCount = call.Type is TYP_VOID
            ? 0
            : hasMultiRegRetVal ? call.ReturnTypeDesc.ReturnRegCount : 1;
        var ctrlExpr = call.ControlExpr;
        var ctrlExprCandidates = SRBM_NONE;

        if (ctrlExpr is not null)
        {
            assert(ctrlExpr.Type is not TYP_VOID);
            if (call.IsFastTailCall)
            {
                ctrlExprCandidates = _availableIntRegs & _rbmIntCalleeTrash & ~SRBM_LR;
                if (_compiler.NeedsGSSecurityCookie)
                {
                    assert(_compiler.codeGen is not null);
                    ctrlExprCandidates &= ~_compiler.codeGen.genGetGSCookieTempRegs(true, call).IntRegSet;
                }
                assert(ctrlExprCandidates != SRBM_NONE);
            }
        }
        else if ((call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir) && call.IsFastTailCall)
        {
            var candidates = _availableIntRegs & _rbmIntCalleeTrash;
            assert(candidates != SRBM_NONE);
            _ = buildInternalIntRegisterDefForNode(call, candidates);
        }

        var singleDstCandidates = SRBM_NONE;
        if (call.IsHelperCall(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT))
        {
            singleDstCandidates = SRBM_INTERFACELOOKUP_FOR_SLOT_RETURN;
        }
        else if (!hasMultiRegRetVal)
        {
            singleDstCandidates = varTypeUsesFloatArgReg(call.Type)
                ? SRBM_FLOATRET
                : call.Type is TYP_LONG ? SRBM_LNGRET : SRBM_INTRET;
        }

        var srcCount = buildCallArgUses(call);
        if (ctrlExpr is not null)
        {
            if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && TargetOS.IsUnix &&
                (call.Args.CountArgs() == 0) && ctrlExpr.Oper.IsCnsIntOrI &&
                (ctrlExpr.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL) ||
                 ctrlExpr.AsIntCon().IsIconHandle(GTF_ICON_TLSGD_OFFSET)))
            {
                assert((call.Flags & GTF_TLS_GET_ADDR) != 0);
                _ = newRefPosition(REG_R0, _referenceBuildLocation, RefType.RefTypeFixedReg,
                    null, genSingleTypeRegMask(REG_R0));
                _ = newRefPosition(REG_R1, _referenceBuildLocation, RefType.RefTypeFixedReg,
                    null, genSingleTypeRegMask(REG_R1));
                ctrlExprCandidates = SRBM_R2;
            }

            _ = buildUse(ctrlExpr, ctrlExprCandidates);
            srcCount++;
        }

        buildInternalRegisterUses();

        if (call.IsAsync && _compiler.compIsAsync && !call.IsFastTailCall)
        {
            markAsyncContinuationBusyForCall(call);
        }

        var killMask = getKillSetForCall(call);
        if (dstCount == 0)
        {
            buildKills(call, killMask);
        }
        else if (hasMultiRegRetVal)
        {
            var multiDstCandidates = call.ReturnTypeDesc.GetAbiReturnRegs(call.UnmanagedCallConv);
            assert(countRegisterMaskBits(multiDstCandidates) == dstCount);
            buildCallDefsWithKills(call, dstCount, multiDstCandidates, killMask);
        }
        else
        {
            buildDefWithKills(call, singleDstCandidates, killMask);
        }

#if SWIFT_SUPPORT
        if ((call.UnmanagedCallConv is CorInfoCallConvExtension.Swift) &&
            (call.Args.FindWellKnownArg(WellKnownArg.SwiftError) is not null))
        {
            markSwiftErrorBusyForCall(call);
        }
#endif

        _placedArgumentRegisters = new regMaskTP(SRBM_NONE);
        _placedArgumentLocalCount = 0;
        return srcCount;
#elif TARGET_RISCV64 || TARGET_LOONGARCH64
        var hasMultiRegRetVal = false;
        var dstCount = 0;
        var singleDstCandidates = SRBM_NONE;
        if (call.Type is not TYP_VOID)
        {
            hasMultiRegRetVal = call.HasMultiRegRetVal;
            dstCount = hasMultiRegRetVal ? call.ReturnTypeDesc.ReturnRegCount : 1;
        }

        var ctrlExpr = call.ControlExpr;
        var ctrlExprCandidates = SRBM_NONE;
        if (ctrlExpr is not null)
        {
            assert(ctrlExpr.Type is not TYP_VOID);
            if (call.IsFastTailCall)
            {
                ctrlExprCandidates = allRegs(TYP_INT) & _rbmIntCalleeTrash;
                if (_compiler.NeedsGSSecurityCookie)
                {
                    assert(_compiler.codeGen is not null);
                    ctrlExprCandidates &= ~_compiler.codeGen.genGetGSCookieTempRegs(true, call).IntRegSet;
                }
                assert(ctrlExprCandidates != SRBM_NONE);
            }

#if TARGET_RISCV64
            if (ctrlExpr.IsContainedIntOrIImmed)
            {
                _ = buildInternalIntRegisterDefForNode(call);
            }
#endif
        }
        else if (call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir)
        {
            var candidates = SRBM_NONE;
            if (call.IsFastTailCall)
            {
                candidates = allRegs(TYP_INT) & _rbmIntCalleeTrash;
#if TARGET_LOONGARCH64
                if (_compiler.NeedsGSSecurityCookie)
                {
                    assert(_compiler.codeGen is not null);
                    ctrlExprCandidates &= ~_compiler.codeGen.genGetGSCookieTempRegs(true, call).IntRegSet;
                }
#endif
                assert(candidates != SRBM_NONE);
            }

            _ = buildInternalIntRegisterDefForNode(call, candidates);
        }

        if (!hasMultiRegRetVal)
        {
            if (varTypeUsesFloatArgReg(call.Type))
            {
                singleDstCandidates = SRBM_FLOATRET;
            }
            else if (call.Type is TYP_LONG)
            {
                singleDstCandidates = SRBM_LNGRET;
            }
            else
            {
                singleDstCandidates = SRBM_INTRET;
            }
        }

        var srcCount = buildCallArgUses(call);
        if (ctrlExpr is not null)
        {
#if TARGET_RISCV64
            if (!ctrlExpr.IsContainedIntOrIImmed)
            {
#endif
                _ = buildUse(ctrlExpr, ctrlExprCandidates);
                srcCount++;
#if TARGET_RISCV64
            }
#endif
        }

        buildInternalRegisterUses();
        if (call.IsAsync && _compiler.compIsAsync && !call.IsFastTailCall)
        {
            markAsyncContinuationBusyForCall(call);
        }

        var killMask = getKillSetForCall(call);
        if (dstCount > 0)
        {
            if (hasMultiRegRetVal)
            {
                var multiDstCandidates = call.ReturnTypeDesc.GetAbiReturnRegs(call.UnmanagedCallConv);
                assert(countRegisterMaskBits(multiDstCandidates) > 0);
                buildCallDefsWithKills(call, dstCount, multiDstCandidates, killMask);
            }
            else
            {
                assert(dstCount == 1);
                buildDefWithKills(call, singleDstCandidates, killMask);
            }
        }
        else
        {
            buildKills(call, killMask);
        }

        _placedArgumentRegisters = new regMaskTP(SRBM_NONE);
        _placedArgumentLocalCount = 0;
        return srcCount;
#else
        throw new FatalJitException("LSRA call reference building is not implemented outside AMD64.");
#endif
    }

#if TARGET_AMD64
    private static regNumber getVarargsIntRegister(regNumber floatRegister) => floatRegister switch
    {
        REG_XMM0 => REG_RCX,
        REG_XMM1 => REG_RDX,
        REG_XMM2 => REG_R8,
        REG_XMM3 => REG_R9,
        _ => throw new FatalJitException("Windows x64 varargs requires an XMM0-XMM3 argument register."),
    };
#endif

    private bool supportsSpecialPutArg()
    {
#if DEBUG && TARGET_X86
        // Caller-only stress leaves EAX, ECX and EDX. Reserving a pass-through ECX/EDX
        // can leave too few registers for operands computed before the remaining arguments.
        return (_lsraStressMask & 0x3) != 0x2;
#else
        return true;
#endif
    }

    private int buildPutArgReg(GenTreeUnOp node)
    {
        assert(node is not null);
        assert(node.Oper.IsPutArgReg);
        var argReg = node.RegNum;
        assert(argReg is not REG_NA);
        var op1 = node.Op1;
        var argMask = genSingleTypeRegMask(argReg);
        var use = buildUse(op1, argMask);

        _placedArgumentRegisters |= regMaskTP.CreateFromRegNum(argReg, argMask);
        var isSpecialPutArg = supportsSpecialPutArg() && isCandidateLocalRef(op1) &&
            ((op1.Flags & GTF_VAR_DEATH) == 0);
        if (isSpecialPutArg)
        {
            JITDUMP("Setting putarg_reg as a pass-through of a non-last use lclVar\n");
            var localInterval = use.getInterval();
            assert(localInterval.isLocalVar);
            assert(_placedArgumentLocalCount < _placedArgumentLocals.Length);
            _placedArgumentLocals[_placedArgumentLocalCount++] = new PlacedArgumentLocal
            {
                VarIndex = localInterval.getVarIndex(_compiler),
                Register = argReg,
            };
        }

        var definition = buildDef(node, argMask);
        if (isSpecialPutArg)
        {
            definition.getInterval().isSpecialPutArg = true;
            definition.getInterval().assignRelatedInterval(use.getInterval());
        }

        return 1;
    }

    private int buildGCWriteBarrier(GenTree tree)
    {
        var addr = tree.AsStoreInd().Addr;
        var src = tree.AsStoreInd().Data;
        assert(!addr.IsContained && !src.IsContained);
#if TARGET_X86
        var addrCandidates = SRBM_ECX;
        var srcCandidates = SRBM_EDX;
#elif TARGET_ARM
        var addrCandidates = genSingleTypeRegMask(REG_R0);
        var srcCandidates = genSingleTypeRegMask(REG_R1);
#elif TARGET_LOONGARCH64
        var addrCandidates = genSingleTypeRegMask(REG_T6);
        var srcCandidates = genSingleTypeRegMask(REG_T7);
#elif TARGET_RISCV64
        var addrCandidates = genSingleTypeRegMask(REG_T3);
        var srcCandidates = genSingleTypeRegMask(REG_T4);
#elif TARGET_WASM
        var addrCandidates = SRBM_NONE;
        var srcCandidates = SRBM_NONE;
#elif TARGET_AMD64 || TARGET_ARM64
        var addrCandidates = SRBM_WRITE_BARRIER_DST;
        var srcCandidates = SRBM_WRITE_BARRIER_SRC;
#else
        throw new FatalJitException("The write-barrier register contract is not available on this target.");
#endif

#if TARGET_X86 && NOGC_WRITE_BARRIERS
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Optimized write barriers require initialized CodeGen.");
        var form = codeGen.GCInfo.gcIsWriteBarrierCandidate(tree.AsStoreInd());
        if (codeGen.genUseOptimizedWriteBarriers(form))
        {
            addrCandidates = SRBM_EDX;
            srcCandidates = SRBM_EAX | SRBM_ECX | SRBM_EBX | SRBM_ESI | SRBM_EDI;
        }
#endif

        _ = buildUse(addr, addrCandidates);
        _ = buildUse(src, srcCandidates);

        var killMask = getKillSetForStoreInd(tree.AsStoreInd());
        _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, killMask);
        return 2;
    }

    private void markSwiftErrorBusyForCall(GenTreeCall call)
    {
#if (TARGET_AMD64 || TARGET_ARM64) && SWIFT_SUPPORT
        assert((call.UnmanagedCallConv is CorInfoCallConvExtension.Swift) &&
            (call.Args.FindWellKnownArg(WellKnownArg.SwiftError) is not null));
        assert(call.Next?.Oper is GT_SWIFT_ERROR);

        var use = getRegisterRecord(REG_SWIFT_ERROR).lastRefPosition
            ?? throw new FatalJitException("Swift error register needs an argument use at the call.");
        assert(use.nodeLocation == _referenceBuildLocation);
        setDelayFree(use);
#else
        throw new FatalJitException("LSRA Swift error lifetime is not implemented on this target.");
#endif
    }

    private void markAsyncContinuationBusyForCall(GenTreeCall call)
    {
#if TARGET_RISCV64 || TARGET_LOONGARCH64
        assert(call.Next?.Oper is GT_ASYNC_CONTINUATION);
        var kill = addKillForRegs(
            new regMaskTP(genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET)),
            _referenceBuildLocation + 1);
        setDelayFree(kill);
#elif TARGET_AMD64
        assert(call.Next?.Oper is GT_ASYNC_CONTINUATION);
        var kill = addKillForRegs(new regMaskTP(genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET)),
            _referenceBuildLocation + 1);
        setDelayFree(kill);
#else
        throw new FatalJitException("LSRA async continuation register is not defined on this target.");
#endif
    }
}
