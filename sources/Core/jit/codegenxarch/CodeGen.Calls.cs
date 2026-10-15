// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCallXarch(GenTreeCall call)
    {
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        genAlignStackBeforeCall(call);
        genCallPlaceRegArgs(call);

        var stackArgBytes = 0;
        if (Configuration.TARGET_X86)
        {
            foreach (var arg in call.Args.EarlyArgs)
            {
                var argNode = arg.EarlyNode;
                if ((argNode is not null) && (argNode.Oper == GT_PUTARG_STK) && (arg.LateNode is null))
                {
                    var putArgStk = argNode.AsPutArgStk();
                    var source = putArgStk.Op1;
                    var argSize = putArgStk.StackByteSize;
                    stackArgBytes = unchecked(stackArgBytes + argSize);

                    if (Configuration.DEBUG)
                    {
                        var stackBytesConsumed = 0;
                        foreach (ref readonly var segment in arg.AbiInfo.Segments)
                        {
                            if (!segment.IsPassedInRegister)
                            {
                                stackBytesConsumed = unchecked(stackBytesConsumed + segment.StackSize);
                            }
                        }
                        assert(argSize == stackBytesConsumed);
                        if ((source.Type == TYP_STRUCT) && (source.Oper != GT_FIELD_LIST))
                        {
                            var loadSize = source.GetLayout(_compiler).Size;
                            assert(argSize == roundUp(unchecked((int)loadSize), Configuration.TARGET_POINTER_SIZE));
                        }
                    }
                }
            }
        }

        if (call.NeedsNullCheck)
        {
            var regThis = genGetThisArgReg(call);
            Emitter.emitIns_AR_R(INS_cmp, EA_4BYTE, regThis, regThis, 0);
        }

        if (call.IsFastTailCall)
        {
            var target = getCallTarget(call, out _);
            if (target is not null)
            {
                if (target.IsContainedIndir)
                {
                    var indir = target.AsIndir();
                    genConsumeAddress(indir.Addr);

                    // Consumption kills these roots, but the address remains live through the epilog.
                    if (indir.HasBase && varTypeIsGC(indir.Base.Type))
                    {
                        GCInfo.gcMarkRegPtrVal(indir.Base.RegNum, indir.Base.Type);
                    }
                    if (indir.HasIndex && varTypeIsGC(indir.Index.Type))
                    {
                        GCInfo.gcMarkRegPtrVal(indir.Index.RegNum, indir.Index.Type);
                    }
                }
                else
                {
                    assert(!target.IsContained);
                    _ = genConsumeReg(target);
                }
            }

            return;
        }

        // P/Invoke needs a GC-state boundary before the call, not lazy killing at the callsite.
        if (_compiler.killGCRefs(call))
        {
            genDefineTempLabel(genCreateTempLabel());

            // The runtime can't find callee-saved registers through an InlinedCallFrame.
            assert(((GCInfo.gcRegGCrefSetCur | GCInfo.gcRegByrefSetCur)
                & new regMaskTP(Configuration.TARGET_AMD64
                    ? SRBM_INT_CALLEE_SAVED_AMD64 : SRBM_INT_CALLEE_SAVED_X86)).IsEmpty);
        }

#if DEBUG
        if (Configuration.TARGET_X86 && _compiler.opts.compStackCheckOnCall && (call._callType == CT_USER_FUNC))
        {
            assert(_compiler.lvaCallSpCheck != BAD_VAR_NUM);
            ref var local = ref _compiler.lvaGetDesc(_compiler.lvaCallSpCheck);
            assert(local.lvDoNotEnregister);
            assert(local.lvOnFrame);
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_X86_ESP, _compiler.lvaCallSpCheck, 0);
        }
#endif
        if (Emitter.Contains256BitOrMoreAvxInstruction && call.NeedsVzeroupper(_compiler))
        {
            // A single prolog vzeroupper is insufficient when this method also uses wide vectors.
            instGen(INS_vzeroupper);
        }

        if (Configuration.TARGET_X86)
        {
            genCallInstructionXarch(call, stackArgBytes);
        }
        else
        {
            genCallInstructionXarch(call);
        }
        genDefinePendingCallLabel(call);

        if (Configuration.DEBUG)
        {
            var killMask = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                var firstMaskReg = Configuration.TARGET_AMD64 ? REG_MASK_FIRST_AMD64 : REG_MASK_FIRST_X86;
                killMask |= regMaskTP.CreateFromRegNum(firstMaskReg, SRBM_MSK_CALLEE_TRASH);
            }

            if (call.IsHelperCall())
            {
                killMask = _compiler.compHelperCallKillSet(call.HelperNum);
            }

            assert((GCInfo.gcRegGCrefSetCur & killMask).IsEmpty);
            assert((GCInfo.gcRegByrefSetCur & killMask).IsEmpty);
        }
        var returnType = call.Type;
        if (returnType != TYP_VOID)
        {
            if (Configuration.TARGET_X86 && varTypeIsFloating(returnType))
            {
                call.Flags |= GTF_SPILL;
                RegSet.rsSpillFPStack(call);
                call.Flags |= GTF_SPILLED;
                call.Flags &= ~GTF_SPILL;
            }
            else
            {
                regNumber returnReg;
                if (call.HasMultiRegRetVal)
                {
                    ref readonly var retTypeDesc = ref call.ReturnTypeDesc;
                    var regCount = retTypeDesc.ReturnRegCount;
                    for (byte i = 0; i < regCount; i++)
                    {
                        var regType = retTypeDesc.GetReturnRegType(i);
                        returnReg = retTypeDesc.GetAbiReturnReg(i, call.UnmanagedCallConv);
                        var allocatedReg = call.GetRegNumByIdx(i);
                        inst_Mov(regType, allocatedReg, returnReg, canSkip: true);
                    }

                    if (Configuration.FEATURE_SIMD && call.IsUnmanaged && (returnType == TYP_SIMD12))
                    {
                        returnReg = retTypeDesc.GetAbiReturnReg(1, call.UnmanagedCallConv);
                        genSimd12UpperClear(returnReg);
                    }
                }
                else
                {
                    if (Configuration.TARGET_X86 && call.IsHelperCall(CORINFO_HELP_INIT_PINVOKE_FRAME))
                    {
                        returnReg = REG_PINVOKE_TCB_X86;
                    }
                    else
                    {
                        returnReg = varTypeIsFloating(returnType) ? REG_FLOATRET : REG_X86_EAX;
                    }
                    inst_Mov(returnType, call.RegNum, returnReg, canSkip: true);
                }

                genProduceReg(call);
            }
        }

        // Keep unused return values visible to the debugger in minopts/debuggable methods.
        if ((call.Next is null) && _compiler.opts.OptimizationEnabled)
        {
            GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_X86_EAX));
        }

#if DEBUG
        if (Configuration.TARGET_X86)
        {
            genStackPointerCheck(_compiler.opts.compStackCheckOnCall && (call._callType == CT_USER_FUNC),
                _compiler.lvaCallSpCheck, call.CallerPop ? 0 : stackArgBytes, REG_ARG_0_X86);
        }
#endif
        var stackAdjustBias = 0u;
        if (Configuration.TARGET_X86)
        {
            if (call.CallerPop && (stackArgBytes != 0))
            {
                stackAdjustBias = unchecked((uint)stackArgBytes);
            }

            SubtractStackLevel(unchecked((uint)stackArgBytes));
        }

        genRemoveAlignmentAfterCall(call, stackAdjustBias);
    }

    private void genAlignStackBeforeCall(GenTreePutArgStk putArgStk)
    {
        if (Configuration.UNIX_X86_ABI)
        {
            var call = putArgStk.Call;
            assert(call is not null);
            genAlignStackBeforeCall(call);
        }
    }

    private void genAlignStackBeforeCall(GenTreeCall call)
    {
        if (Configuration.UNIX_X86_ABI && !call.Args.IsStkAlignmentDone)
        {
            var stackLevel = unchecked(genStackLevel + call.Args.GetStkSizeBytes());
            call.Args.ComputeStackAlignment(stackLevel);
            var padding = call.Args.GetStkAlign();
            if (padding != 0)
            {
                inst_RV_IV(INS_sub, REG_X86_ESP, unchecked((nint)padding), EA_PTRSIZE);
                AddStackLevel(padding);
                AddNestedAlignment(padding);
            }
            call.Args.IsStkAlignmentDone = true;
        }
    }

    private void genRemoveAlignmentAfterCall(GenTreeCall call, uint bias = 0)
    {
        if (Configuration.TARGET_X86)
        {
            if (Configuration.UNIX_X86_ABI)
            {
                var padding = call.Args.GetStkAlign();
                var adjustment = unchecked(padding + bias);
                if (adjustment != 0)
                {
                    inst_RV_IV(INS_add, REG_X86_ESP, unchecked((nint)adjustment), EA_PTRSIZE);
                    SubtractStackLevel(padding);
                    SubtractNestedAlignment(padding);
                }
            }
            else if (bias != 0)
            {
                if (bias == sizeof(int))
                {
                    inst_RV(INS_pop, REG_X86_ECX, TYP_INT);
                }
                else
                {
                    inst_RV_IV(INS_add, REG_X86_ESP, unchecked((nint)bias), EA_PTRSIZE);
                }
            }
        }
        else
        {
            assert(bias == 0);
        }
    }

    public void genSimd12UpperClear(regNumber targetReg)
    {
        if (!Configuration.FEATURE_SIMD || !Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 register clearing requires xarch SIMD support.");
        }

        Emitter.RequireSupportedInstructionRecording();
        assert(genIsValidFloatReg(targetReg));

        // INSERTPS selects element 3 as source/destination (0xF0), then zeros that lane (0x08).
        Emitter.emitIns_SIMD_R_R_R_I(INS_insertps, EA_16BYTE, targetReg, targetReg, targetReg,
            unchecked((sbyte)0xF8), INS_OPTS_NONE);
    }
}
