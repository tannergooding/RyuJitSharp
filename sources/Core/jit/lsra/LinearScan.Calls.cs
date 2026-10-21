// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildCall(GenTreeCall call)
    {
        if (Configuration.TARGET_XARCH)
        {
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
                if (Configuration.TARGET_X86)
                {
                    if (call.IsHelperCall(CORINFO_HELP_INIT_PINVOKE_FRAME))
                    {
                        singleDstCandidates = SRBM_X86_ESI;
                    }
                    else if (varTypeUsesFloatReg(call.Type))
                    {
                        singleDstCandidates = allRegs(regType(call.Type));
                    }
                    else
                    {
                        assert(varTypeUsesIntReg(call.Type));
                        singleDstCandidates = (call.Type is TYP_LONG)
                            ? SRBM_X86_EAX | SRBM_X86_EDX
                            : SRBM_X86_EAX;
                    }
                }
                else if (varTypeUsesFloatReg(call.Type))
                {
                    singleDstCandidates = TargetRuntimeMetadata.FloatingReturnMask;
                }
                else
                {
                    assert(varTypeUsesIntReg(call.Type));
                    singleDstCandidates = (call.Type is TYP_LONG)
                        ? TargetRuntimeMetadata.LongReturnMask
                        : TargetRuntimeMetadata.IntegerReturnMask;
                }
            }

            var callHasFloatRegArgs = false;
            if (Configuration.WINDOWS_AMD64_ABI && compFeatureVarArg() && call.Args.IsVarArgs)
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
                else if (Configuration.TARGET_X86 &&
                    call.IsVirtualStub && (call._callType is CT_INDIRECT) &&
                    !_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
                {
                    assert(ctrlExpr.IsContainedIndir);
                    ctrlExprCandidates = SRBM_X86_EAX;
                }

                if (Configuration.WINDOWS_AMD64_ABI &&
                    compFeatureVarArg() && call.Args.IsVarArgs && callHasFloatRegArgs &&
                    (ctrlExprCandidates == SRBM_NONE))
                {
                    ctrlExprCandidates = _availableIntRegs & ~SRBM_ARG_REGS;
                }

                srcCount += buildOperandUses(ctrlExpr, ctrlExprCandidates);
            }

            if (call.NeedsVzeroupper(_compiler))
            {
                var codeGen = _compiler.codeGen;
                if (codeGen is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage279
                    );
                    return default!;
                }
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

            if (Configuration.SWIFT_SUPPORT &&
                (call.UnmanagedCallConv is CorInfoCallConvExtension.Swift) &&
                (call.Args.FindWellKnownArg(WellKnownArg.SwiftError) is not null))
            {
                markSwiftErrorBusyForCall(call);
            }

            _placedArgumentRegisters = new regMaskTP(SRBM_NONE);
            _placedArgumentLocalCount = 0;
            return srcCount;
        }

        if (Configuration.TARGET_ARM64)
        {
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
                    ctrlExprCandidates = _availableIntRegs & _rbmIntCalleeTrash & ~SRBM_ARM64_LR;
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
                    ? TargetRuntimeMetadata.FloatingReturnMask
                    : call.Type is TYP_LONG
                        ? TargetRuntimeMetadata.LongReturnMask
                        : TargetRuntimeMetadata.IntegerReturnMask;
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
                    _ = newRefPosition(TargetRuntimeMetadata.Register0, _referenceBuildLocation, RefType.RefTypeFixedReg,
                        null, genSingleTypeRegMask(TargetRuntimeMetadata.Register0));
                    _ = newRefPosition(REG_ARM64_R1, _referenceBuildLocation, RefType.RefTypeFixedReg,
                        null, genSingleTypeRegMask(REG_ARM64_R1));
                    ctrlExprCandidates = SRBM_ARM64_R2;
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

            if (Configuration.SWIFT_SUPPORT &&
                (call.UnmanagedCallConv is CorInfoCallConvExtension.Swift) &&
                (call.Args.FindWellKnownArg(WellKnownArg.SwiftError) is not null))
            {
                markSwiftErrorBusyForCall(call);
            }

            _placedArgumentRegisters = new regMaskTP(SRBM_NONE);
            _placedArgumentLocalCount = 0;
            return srcCount;
        }

        if (Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64)
        {
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

                if (Configuration.TARGET_RISCV64 && ctrlExpr.IsContainedIntOrIImmed)
                {
                    _ = buildInternalIntRegisterDefForNode(call);
                }
            }
            else if (call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir)
            {
                var candidates = SRBM_NONE;
                if (call.IsFastTailCall)
                {
                    candidates = allRegs(TYP_INT) & _rbmIntCalleeTrash;
                    if (Configuration.TARGET_LOONGARCH64 && _compiler.NeedsGSSecurityCookie)
                    {
                        assert(_compiler.codeGen is not null);
                        ctrlExprCandidates &= ~_compiler.codeGen.genGetGSCookieTempRegs(true, call).IntRegSet;
                    }
                    assert(candidates != SRBM_NONE);
                }

                _ = buildInternalIntRegisterDefForNode(call, candidates);
            }

            if (!hasMultiRegRetVal)
            {
                if (varTypeUsesFloatArgReg(call.Type))
                {
                    singleDstCandidates = TargetRuntimeMetadata.FloatingReturnMask;
                }
                else if (call.Type is TYP_LONG)
                {
                    singleDstCandidates = TargetRuntimeMetadata.LongReturnMask;
                }
                else
                {
                    singleDstCandidates = TargetRuntimeMetadata.IntegerReturnMask;
                }
            }

            var srcCount = buildCallArgUses(call);
            if (ctrlExpr is not null)
            {
                if (!Configuration.TARGET_RISCV64 || !ctrlExpr.IsContainedIntOrIImmed)
                {
                    _ = buildUse(ctrlExpr, ctrlExprCandidates);
                    srcCount++;
                }
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
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "LSRA call reference building is not implemented on this target.");
        return default!;
    }

    private static regNumber getVarargsIntRegister(regNumber floatRegister)
    {
        if (!Configuration.TARGET_AMD64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED);
            return default!;
        }

        switch (floatRegister)
        {
            case REG_X64_XMM0:
            {
                return REG_X64_RCX;
            }

            case REG_X64_XMM1:
            {
                return REG_X64_RDX;
            }

            case REG_X64_XMM2:
            {
                return REG_X64_R8;
            }

            case REG_X64_XMM3:
            {
                return REG_X64_R9;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage280
                );
                return default!;
            }
        }
    }

    private bool supportsSpecialPutArg()
    {
        if (Configuration.DEBUG && Configuration.TARGET_X86)
        {
            // Caller-only stress leaves EAX, ECX and EDX. Reserving a pass-through ECX/EDX
            // can leave too few registers for operands computed before the remaining arguments.
            return (_lsraStressMask & 0x3) != 0x2;
        }
        return true;
    }

    private int buildPutArgReg(GenTreeUnOp node)
    {
        assert(node is not null);
        assert(node.Oper.IsPutArgReg);
        var argReg = node.RegNum;
        assert(argReg != REG_NA);
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
            _placedArgumentLocals[_placedArgumentLocalCount++] = new PlacedArgumentLocal {
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
        SingleTypeRegSet addrCandidates;
        SingleTypeRegSet srcCandidates;
        if (Configuration.TARGET_X86)
        {
            addrCandidates = SRBM_X86_ECX;
            srcCandidates = SRBM_X86_EDX;
        }
        else if (Configuration.TARGET_ARM)
        {
            addrCandidates = genSingleTypeRegMask(TargetRuntimeMetadata.Register0);
            srcCandidates = genSingleTypeRegMask(REG_ARM32_R1);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            addrCandidates = genSingleTypeRegMask(REG_LOONGARCH64_T6);
            srcCandidates = genSingleTypeRegMask(REG_LOONGARCH64_T7);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            addrCandidates = genSingleTypeRegMask(REG_RISCV64_T3);
            srcCandidates = genSingleTypeRegMask(REG_RISCV64_T4);
        }
        else if (Configuration.TARGET_WASM)
        {
            addrCandidates = SRBM_NONE;
            srcCandidates = SRBM_NONE;
        }
        else if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
        {
            addrCandidates = Configuration.TARGET_AMD64
                ? SRBM_WRITE_BARRIER_DST_AMD64
                : SRBM_WRITE_BARRIER_DST_ARM64;
            srcCandidates = Configuration.TARGET_AMD64
                ? SRBM_WRITE_BARRIER_SRC_AMD64
                : SRBM_WRITE_BARRIER_SRC_ARM64;
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "The write-barrier register contract is not available on this target.");
            return default!;
        }

        if (Configuration.TARGET_X86 && Configuration.NOGC_WRITE_BARRIERS)
        {
            var codeGen = _compiler.codeGen;
            if (codeGen is null)
            {
                FatalJitException.ThrowFatalJitException("Optimized write barriers require initialized CodeGen.");
                return default!;
            }
            var form = codeGen.GCInfo.gcIsWriteBarrierCandidate(tree.AsStoreInd());
            if (codeGen.genUseOptimizedWriteBarriers(form))
            {
                addrCandidates = SRBM_X86_EDX;
                srcCandidates = SRBM_X86_EAX | SRBM_X86_ECX | SRBM_X86_EBX | SRBM_X86_ESI | SRBM_X86_EDI;
            }
        }

        _ = buildUse(addr, addrCandidates);
        _ = buildUse(src, srcCandidates);

        var killMask = getKillSetForStoreInd(tree.AsStoreInd());
        _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, killMask);
        return 2;
    }

    private void markSwiftErrorBusyForCall(GenTreeCall call)
    {
        if (Configuration.SWIFT_SUPPORT &&
            (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64))
        {
            assert((call.UnmanagedCallConv is CorInfoCallConvExtension.Swift) &&
                (call.Args.FindWellKnownArg(WellKnownArg.SwiftError) is not null));
            assert(call.Next?.Oper is GT_SWIFT_ERROR);

            var use = getRegisterRecord(TargetRuntimeMetadata.SwiftErrorRegister).lastRefPosition;
            if (use is null)
            {
                FatalJitException.ThrowFatalJitException("Swift error register needs an argument use at the call.");
                return;
            }
            assert(use.nodeLocation == _referenceBuildLocation);
            setDelayFree(use);
            return;
        }
        FatalJitException.ThrowFatalJitException("LSRA Swift error lifetime is not implemented on this target.");
        return;
    }

    private void markAsyncContinuationBusyForCall(GenTreeCall call)
    {
        if (Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_AMD64)
        {
            assert(call.Next?.Oper is GT_ASYNC_CONTINUATION);
            var kill = addKillForRegs(new regMaskTP(genSingleTypeRegMask(TargetRuntimeMetadata.AsyncContinuationReturnRegister)),
                _referenceBuildLocation + 1);
            setDelayFree(kill);
            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "LSRA async continuation register is not defined on this target.");
        return;
    }
}
