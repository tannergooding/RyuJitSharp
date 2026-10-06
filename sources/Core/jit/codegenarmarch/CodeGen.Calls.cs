// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
#if DEBUG
using System.Runtime.CompilerServices;
#endif
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCall(GenTreeCall call)
    {
        genCallPlaceRegArgs(call);

        if (call.NeedsNullCheck)
        {
            var thisReg = genGetThisArgReg(call);
#if TARGET_ARM
            var tempReg = InternalRegisters.Extract(call);
            Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, tempReg, thisReg, 0, INS_FLAGS_DONT_CARE);
#else
            Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, REG_ZR, thisReg, 0);
#endif
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
                // The saved target must survive the epilog's callee-saved register restoration.
                assert(new regMaskTP(SRBM_INT_CALLEE_TRASH & ~SRBM_LR).IsRegNumInMask(tempReg));

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
                Emitter.emitIns_R_R(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize, tempReg, callAddressReg);
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
#if TARGET_ARM
#if CONFIGURABLE_ARM_ABI
                var compUseSoftFP = _compiler.opts.compUseSoftFP;
#else
                var compUseSoftFP = Compiler.Options.compUseSoftFP;
#endif
#endif
#if TARGET_ARM
                if (call.IsHelperCall(CORINFO_HELP_INIT_PINVOKE_FRAME))
                {
                    returnReg = REG_PINVOKE_TCB;
                }
                else if (compUseSoftFP)
                {
                    returnReg = REG_INTRET;
                }
                else
#else
                if (call.IsHelperCall(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT))
                {
                    returnReg = REG_R15;
                }
                else
#endif
                if (varTypeUsesFloatArgReg(returnType))
                {
                    returnReg = REG_FLOATRET;
                }
                else
                {
                    returnReg = REG_INTRET;
                }

                if (call.RegNum != returnReg)
                {
#if TARGET_ARM
                    if (compUseSoftFP && (returnType is TYP_DOUBLE))
                    {
                        inst_RV_RV_RV(INS_vmov_i2d, call.RegNum, returnReg, REG_NEXT(returnReg), EA_8BYTE);
                    }
                    else if (compUseSoftFP && (returnType is TYP_FLOAT))
                    {
                        inst_Mov(returnType, call.RegNum, returnReg, canSkip: false);
                    }
                    else
#endif
                    {
                        inst_Mov(returnType, call.RegNum, returnReg, canSkip: false);
                    }
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

#if TARGET_ARM
        assert(parameters.secondRetSize is not EA_GCREF);
        assert(parameters.secondRetSize is not EA_BYREF);
#endif

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
                _ = genConsumeReg(target);
            }

            assert(genIsValidIntReg(target.RegNum));

#if TARGET_ARM64
            var isTlsHandleTarget =
                _compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && TargetOS.IsUnix && target.IsTlsIconHandle();

            if (isTlsHandleTarget)
            {
                assert((call.Flags & GTF_TLS_GET_ADDR) != 0);
                // NativeAOT's linker recognizes this contiguous TLSDESC sequence as one call pattern.
                var attr = EA_SET_FLG(EA_SET_FLG(parameters.retSize, EA_CNS_TLSGD_RELOC), EA_CNS_RELOC_FLG);
                var icon = target.AsIntCon();
                parameters.methHnd = (CORINFO_METHOD_HANDLE)icon.IconVal;
                parameters.retSize = EA_SET_FLG(parameters.retSize, EA_CNS_TLSGD_RELOC);
                parameters.noSafePoint = true;

                Emitter.emitIns_R(INS_mrs_tpid0, attr, REG_R1);
                GCInfo.gcMarkRegSetNpt(RBM_R0);
#if DEBUG
                Emitter.emitIns_Adrp_Ldr_Add(attr, REG_R0, target.RegNum, (nint)parameters.methHnd,
                    unchecked((nuint)icon.TargetHandle), icon.Flags);
#else
                Emitter.emitIns_Adrp_Ldr_Add(attr, REG_R0, target.RegNum, (nint)parameters.methHnd);
#endif
            }
#endif

            parameters.callType = EC_INDIR_R;
            parameters.ireg = target.RegNum;
            genEmitCallWithCurrentGC(ref parameters);

#if TARGET_ARM64
            if (isTlsHandleTarget)
            {
                Emitter.emitIns_R_R_R(INS_add, EA_8BYTE, REG_R0, REG_R1, REG_R0);
            }
#endif
        }
        else
        {
            var callThroughIndirectionReg = REG_NA;
            if (!call.IsHelperCall(CORINFO_HELP_DISPATCH_INDIRECT_CALL))
            {
                callThroughIndirectionReg = getCallIndirectionCellReg(call);
            }

            if (callThroughIndirectionReg != REG_NA)
            {
                assert(call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir);
                regNumber targetAddressReg;
                if (!call.IsFastTailCall)
                {
#if TARGET_ARM
                    // ARM32 allocates a temporary to avoid the larger IP-based load encoding.
                    targetAddressReg = InternalRegisters.GetSingle(call);
#else
                    // ARM64 uses IP0 directly and saves the temporary register.
                    targetAddressReg = REG_INDIRECT_CALL_TARGET_REG;
#endif
                    Emitter.emitIns_R_R(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize, targetAddressReg,
                        callThroughIndirectionReg);
                }
                else
                {
                    targetAddressReg = InternalRegisters.GetSingle(call);
                    assert(new regMaskTP(SRBM_INT_CALLEE_TRASH & ~SRBM_LR).IsRegNumInMask(targetAddressReg));
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
                parameters.addr = call._directCallAddress;

#if TARGET_ARM
                if (!validImmForBL((nint)parameters.addr))
                {
                    var tempReg = InternalRegisters.GetSingle(call);
                    instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, tempReg, (nint)parameters.addr);
                    parameters.callType = EC_INDIR_R;
                    parameters.addr = null;
                    parameters.ireg = tempReg;
                    genEmitCallWithCurrentGC(ref parameters);
                }
                else
#endif
                {
                    parameters.callType = EC_FUNC_TOKEN;
                    genEmitCallWithCurrentGC(ref parameters);
                }
            }
        }
    }
}
#endif
