// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
#if DEBUG
using System.Runtime.CompilerServices;
#endif
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.gtCallTypes;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCall(GenTreeCall call)
    {
        genCallPlaceRegArgs(call);

        if (call.NeedsNullCheck)
        {
            var thisReg = genGetThisArgReg(call);
            Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, REG_R0, thisReg, 0);
        }

        if (call.IsFastTailCall)
        {
            var target = getCallTarget(call, out _);
            if (target is not null)
            {
                _ = genConsumeReg(target);
            }
#if FEATURE_READYTORUN
            else if (call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir)
            {
                assert((call.IsR2RRelativeIndir && (call._entryPoint.accessType is IAT_PVALUE)) ||
                    (call.IsVirtualStubRelativeIndir && (call._entryPoint.accessType is IAT_VALUE)));
                assert(call.ControlExpr is null);

                var tempReg = InternalRegisters.GetSingle(call);
                assert(new regMaskTP(SRBM_INT_CALLEE_TRASH & ~SRBM_RA).IsRegNumInMask(tempReg));

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

                Emitter.emitIns_R_R_I(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize, tempReg, callAddressReg, 0);
                InternalRegisters.Add(call, genRegMask(tempReg));
            }
#endif
            return;
        }

        if (_compiler.killGCRefs(call))
        {
            genDefineTempLabel(genCreateTempLabel());
        }

        genCallInstruction(call);
        genDefinePendingCallLabel(call);

#if DEBUG
        var killMask = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
        if (call.IsHelperCall())
        {
            killMask = _compiler.compHelperCallKillSet(call.HelperNum);
        }

        assert((GCInfo.gcRegGCrefSetCur & killMask).IsEmpty);
        assert((GCInfo.gcRegByrefSetCur & killMask).IsEmpty);
#endif

        var returnType = call.Type;
        if (returnType is not TYP_VOID)
        {
            regNumber returnReg;
            if (call.HasMultiRegRetVal)
            {
                ref readonly var returnTypeDesc = ref call.ReturnTypeDesc;
                var returnRegCount = returnTypeDesc.ReturnRegCount;
                for (byte i = 0; i < returnRegCount; i++)
                {
                    var registerType = returnTypeDesc.GetReturnRegType(i);
                    returnReg = returnTypeDesc.GetAbiReturnReg(i, call.UnmanagedCallConv);
                    var allocatedReg = call.GetRegNumByIdx(i);
                    inst_Mov(registerType, allocatedReg, returnReg, canSkip: true);
                }
            }
            else
            {
                returnReg = varTypeUsesFloatArgReg(returnType) ? REG_FLOATRET : REG_INTRET;
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

    public unsafe void genCallInstruction(GenTreeCall call)
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
                    assert(!EA_IS_GCREF(parameters.retSize) && !EA_IS_BYREF(parameters.retSize));
                    parameters.retSize = parameters.secondRetSize;
                    parameters.secondRetSize = EA_UNKNOWN;
                }
            }
            else
            {
                assert(!varTypeIsStruct(call.Type));

                if (call.Type is TYP_REF)
                {
                    parameters.retSize = EA_GCREF;
                }
                else if (call.Type is TYP_BYREF)
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
            if (!call.IsFastTailCall && !target.IsContainedIntOrIImmed)
            {
                _ = genConsumeReg(target);
            }

            if (target.IsContainedIntOrIImmed)
            {
                parameters.callType = EC_FUNC_TOKEN;
                parameters.ireg = InternalRegisters.GetSingle(call);
                parameters.addr = (void*)target.AsIntCon().IconValue;
            }
            else
            {
                parameters.callType = EC_INDIR_R;
                parameters.ireg = target.RegNum;
            }

            assert(genIsValidIntReg(parameters.ireg));
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
                var targetAddrReg = InternalRegisters.GetSingle(call);
                if (!call.IsFastTailCall)
                {
                    Emitter.emitIns_R_R_I(
                        ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize, targetAddrReg, callThroughIndirReg, 0);
                }
                else
                {
                    assert(new regMaskTP(SRBM_INT_CALLEE_TRASH & ~SRBM_RA).IsRegNumInMask(targetAddrReg));
                }

                assert(genIsValidIntReg(targetAddrReg));
                parameters.callType = EC_INDIR_R;
                parameters.ireg = targetAddrReg;
                genEmitCallWithCurrentGC(ref parameters);
            }
            else
            {
                assert(call.IsHelperCall() || (call._callType is CT_USER_FUNC));
                assert(call._directCallAddress is not null);

                parameters.callType = EC_FUNC_TOKEN;
                parameters.addr = call._directCallAddress;
                parameters.ireg = parameters.isJump ? rsGetRsvdReg() : REG_RA;
                genEmitCallWithCurrentGC(ref parameters);
            }
        }
    }
}
#endif
