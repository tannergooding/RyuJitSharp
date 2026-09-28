// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildCall(GenTreeCall call)
    {
#if TARGET_AMD64
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
            if (varTypeUsesFloatReg(call.Type))
            {
                singleDstCandidates = SRBM_FLOATRET;
            }
            else
            {
                assert(varTypeUsesIntReg(call.Type));
                singleDstCandidates = (call.Type is TYP_LONG) ? SRBM_LNGRET : SRBM_INTRET;
            }
        }

        var callHasFloatRegArgs = false;
#if WINDOWS_AMD64_ABI
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

            if (compFeatureVarArg() && call.Args.IsVarArgs && callHasFloatRegArgs &&
                (ctrlExprCandidates == SRBM_NONE))
            {
                ctrlExprCandidates = _availableIntRegs & ~SRBM_ARG_REGS;
            }

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
            var multiDstCandidates = new regMaskTP(SRBM_NONE);
            for (var index = 0; index < dstCount; index++)
            {
                var register = call.ReturnTypeDesc.GetAbiReturnReg(checked((byte)index), call.UnmanagedCallConv);
                multiDstCandidates |= regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
            }
            assert(countRegisterMaskBits(multiDstCandidates) > 0);
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

    private int buildPutArgReg(GenTreeUnOp node)
    {
#if TARGET_AMD64 || TARGET_ARM64
        assert(node.Oper.IsPutArgReg);
        var argReg = node.RegNum;
        assert(argReg is not REG_NA);
        var op1 = node.Op1;
        var argMask = genSingleTypeRegMask(argReg);
        var use = buildUse(op1, argMask);

        _placedArgumentRegisters |= regMaskTP.CreateFromRegNum(argReg, argMask);
        var isSpecialPutArg = isCandidateLocalRef(op1) && ((op1.Flags & GTF_VAR_DEATH) == 0);
        if (isSpecialPutArg)
        {
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
#else
        throw new FatalJitException("LSRA register argument reference building is not implemented outside AMD64.");
#endif
    }

    private int buildGCWriteBarrier(GenTree tree)
    {
#if TARGET_AMD64 || TARGET_ARM64
        var addr = tree.AsStoreInd().Addr;
        var src = tree.AsStoreInd().Data;
        assert(!addr.IsContained && !src.IsContained);
        _ = buildUse(addr, SRBM_WRITE_BARRIER_DST);
        _ = buildUse(src, SRBM_WRITE_BARRIER_SRC);

        var killMask = getKillSetForStoreInd(tree.AsStoreInd());
        _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, killMask);
        return 2;
#else
        throw new FatalJitException("LSRA write-barrier reference building is not implemented outside AMD64.");
#endif
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
#if TARGET_AMD64
        assert(call.Next?.Oper is GT_ASYNC_CONTINUATION);
        var kill = addKillForRegs(new regMaskTP(genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET)),
            _referenceBuildLocation + 1);
        setDelayFree(kill);
#else
        throw new FatalJitException("LSRA async continuation register is not defined on this target.");
#endif
    }
}
