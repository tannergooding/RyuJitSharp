// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
#if DEBUG
using System.Runtime.CompilerServices;
#endif
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.gtCallTypes;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_JMP(emitJumpKind jumpKind, BasicBlock targetBlock)
    {
#if !FEATURE_FIXED_OUT_ARGS
        assert((targetBlock.bbTgtStkDepth * sizeof(int) == genStackLevel) || IsFramePointerUsed);
#endif

        Emitter.emitIns_J(Emitter.emitJumpKindToIns(jumpKind), targetBlock, 0);
    }

    private void genCallFinallyLoongArch64(BasicBlock block)
    {
        assert(block.Kind == BBJ_CALLFINALLY);

        var nextBlock = block.Next;
        if (block.HasFlag(BBF_RETLESS_CALL))
        {
            Emitter.emitIns_J(INS_bl, block.Target);

            if ((nextBlock is null) || !BasicBlock.sameEHRegion(block, nextBlock))
            {
                instGen(INS_break);
            }

            return;
        }

        Emitter.emitDisableGC();
        Emitter.emitIns_J(INS_bl, block.Target);

        assert(nextBlock is not null);
        var finallyContinuation = nextBlock.Target;
        if ((nextBlock.Next == finallyContinuation) &&
            !_compiler.fgInDifferentRegions(nextBlock, finallyContinuation))
        {
            instGen(INS_nop);
        }
        else
        {
            inst_JMP(EJ_jmp, finallyContinuation);
        }

        Emitter.emitEnableGC();
    }

    private void genEHCatchRetLoongArch64(BasicBlock block)
    {
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE, block.Target, REG_INTRET);
    }

    private unsafe void genCallLoongArch64(GenTreeCall call)
    {
        genCallPlaceRegArgs(call);

        if (call.NeedsNullCheck)
        {
            var thisReg = genGetThisArgReg(call);
            Emitter.emitIns_R_R_I(INS_ld_w, EA_4BYTE, REG_R0, thisReg, 0);
        }

        if (call.IsFastTailCall)
        {
            var target = getCallTarget(call, out _);
            if (target is not null)
            {
                genConsumeReg(target);
            }
#if FEATURE_READYTORUN
            else if (call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir)
            {
                assert((call.IsR2RRelativeIndir && (call._entryPoint.accessType is IAT_PVALUE)) ||
                    (call.IsVirtualStubRelativeIndir && (call._entryPoint.accessType is IAT_VALUE)));
                assert(call.ControlExpr is null);

                var tempReg = InternalRegisters.GetSingle(call);
                assert((genRegMask(tempReg) &
                    (new regMaskTP(SRBM_INT_CALLEE_TRASH) & ~new regMaskTP(SRBM_RA))) == genRegMask(tempReg));

                regNumber callAddressReg;
                if (call.IsVirtualStubRelativeIndir)
                {
                    assert(_compiler.virtualStubParamInfo is not null);
                    callAddressReg = _compiler.virtualStubParamInfo.Reg;
                }
                else
                {
                    callAddressReg = REG_R2R_INDIRECT_PARAM;
                }
                Emitter.emitIns_R_R_I(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize,
                    tempReg, callAddressReg, 0);
                InternalRegisters.Add(call, genRegMask(tempReg));
            }
#endif

            return;
        }

        if (_compiler.killGCRefs(call))
        {
            genDefineTempLabel(genCreateTempLabel());
        }

        genCallInstructionLoongArch64(call);
        genDefinePendingCallLabel(call);

#if DEBUG
        var killMask = new regMaskTP(SRBM_CALLEE_TRASH);
        if (call.IsHelperCall())
        {
            killMask = _compiler.compHelperCallKillSet(call.HelperNum);
        }

        assert((GCInfo.gcRegGCrefSetCur & killMask).IsEmpty);
        assert((GCInfo.gcRegByrefSetCur & killMask).IsEmpty);
#endif

        var returnType = call.Type;
        if (returnType != TYP_VOID)
        {
            if (call.HasMultiRegRetVal)
            {
                ref readonly var returnTypeDesc = ref call.ReturnTypeDesc;
                var registerCount = returnTypeDesc.ReturnRegCount;

                for (byte i = 0; i < registerCount; i++)
                {
                    var registerType = returnTypeDesc.GetReturnRegType(i);
                    var returnReg = returnTypeDesc.GetAbiReturnReg(i, call.UnmanagedCallConv);
                    var allocatedReg = call.GetRegNumByIdx(i);
                    inst_Mov(registerType, allocatedReg, returnReg, canSkip: true);
                }
            }
            else
            {
                var returnReg = varTypeUsesFloatArgReg(returnType) ? REG_FLOATRET : REG_INTRET;
                if (call.RegNum != returnReg)
                {
                    inst_Mov(returnType, call.RegNum, returnReg, canSkip: false);
                }
            }

            genProduceReg(call);
        }

        if ((call.Next is null) && !_compiler.opts.MinOpts && !_compiler.opts.compDbgCode)
        {
            GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_INTRET));
        }
    }

    private unsafe void genCallInstructionLoongArch64(GenTreeCall call)
    {
        ref readonly var returnTypeDesc = ref call.ReturnTypeDesc;
        var parameters = new EmitCallParams();

        if (!call.IsUnusedValue)
        {
            if (call.HasMultiRegRetVal)
            {
                parameters.retSize = returnTypeDesc.GetReturnRegType(0).EmitSize;
                parameters.secondRetSize = returnTypeDesc.GetReturnRegType(1).EmitSize;

                if (returnTypeDesc.GetAbiReturnReg(1, call.UnmanagedCallConv) is REG_INTRET)
                {
                    assert(!EA_IS_GCREF_OR_BYREF(parameters.retSize));
                    parameters.retSize = parameters.secondRetSize;
                    parameters.secondRetSize = EA_UNKNOWN;
                }
            }
            else
            {
                assert(call.Type != TYP_STRUCT);
                if (call.Type == TYP_REF)
                {
                    parameters.retSize = EA_GCREF;
                }
                else if (call.Type == TYP_BYREF)
                {
                    parameters.retSize = EA_BYREF;
                }
            }
        }

        parameters.isJump = call.IsFastTailCall;
        parameters.hasAsyncRet = call.IsAsync;
        parameters.returnValueCall = call;

#if DEBUG
        if (!call.IsHelperCall())
        {
            parameters.sigInfo = new StrongBox<CORINFO_SIG_INFO>(call._callSig);
        }

        genCheckTailCallEpilogRegisters(call);
#endif

        var target = getCallTarget(call, out parameters.methHnd);
        if (target is not null)
        {
            assert(!target.IsContainedIndir);
            if (!call.IsFastTailCall)
            {
                genConsumeReg(target);
            }

            assert(genIsValidIntReg(target.RegNum));
            parameters.callType = EC_INDIR_R;
            parameters.ireg = target.RegNum;
            genEmitCallWithCurrentGC(ref parameters);
        }
        else
        {
            var callThroughIndirReg = REG_NA;
            if (!call.IsHelperCall(CORINFO_HELP_DISPATCH_INDIRECT_CALL))
            {
                callThroughIndirReg = getCallIndirectionCellReg(call);
            }

            if (callThroughIndirReg != REG_NA)
            {
                assert(call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir);
                var targetAddressReg = InternalRegisters.GetSingle(call);
                if (!call.IsFastTailCall)
                {
                    Emitter.emitIns_R_R_I(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize,
                        targetAddressReg, callThroughIndirReg, 0);
                }
                else
                {
                    assert((genRegMask(targetAddressReg) &
                        (new regMaskTP(SRBM_INT_CALLEE_TRASH) & ~new regMaskTP(SRBM_RA))) ==
                        genRegMask(targetAddressReg));
                }

                assert(genIsValidIntReg(targetAddressReg));
                parameters.callType = EC_INDIR_R;
                parameters.ireg = targetAddressReg;
                genEmitCallWithCurrentGC(ref parameters);
            }
            else
            {
                assert(call.IsHelperCall() || (call._callType is CT_USER_FUNC));
                assert(call._directCallAddress is not null);
                parameters.callType = EC_FUNC_TOKEN;
                parameters.addr = call._directCallAddress;
                genEmitCallWithCurrentGC(ref parameters);
            }
        }
    }

    private void genJmpPlaceVarArgsLoongArch64()
    {
        NYI("Varargs not supported");
        throw new FatalJitException(CORJIT_SKIPPED, "Varargs are not supported on LoongArch64.");
    }
}
#endif
