// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCallArmArch(GenTreeCall call)
    {
        var intCalleeTrashMask = Configuration.TARGET_ARM64
            ? SRBM_INT_CALLEE_TRASH_ARM64
            : SRBM_INT_CALLEE_TRASH_ARM;
        var intCalleeSavedMask = Configuration.TARGET_ARM64
            ? SRBM_INT_CALLEE_SAVED_ARM64
            : SRBM_INT_CALLEE_SAVED_ARM;
        var intReturnMask = Configuration.TARGET_ARM64 ? SRBM_INTRET_ARM64 : SRBM_INTRET_ARM;
        var linkRegisterMask = Configuration.TARGET_ARM64 ? SRBM_ARM64_LR : SRBM_ARM32_LR;

        genCallPlaceRegArgs(call);

        if (call.NeedsNullCheck)
        {
            var thisReg = genGetThisArgReg(call);
            if (Configuration.TARGET_ARM)
            {
                var tempReg = InternalRegisters.Extract(call);
                Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, tempReg, thisReg, 0, INS_FLAGS_DONT_CARE);
            }
            else
            {
                genEmitCallNullCheckArm64(thisReg);
            }
        }

        if (call.IsFastTailCall)
        {
            var target = getCallTarget(call, out _);
            if (target is not null)
            {
                _ = genConsumeReg(target);
            }
            else if (Configuration.FEATURE_READYTORUN && (call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir))
            {
                assert((call.IsR2RRelativeIndir && (call._entryPoint.accessType is IAT_PVALUE)) ||
                    (call.IsVirtualStubRelativeIndir && (call._entryPoint.accessType is IAT_VALUE)));
                assert(call.ControlExpr is null);

                var tempReg = InternalRegisters.GetSingle(call);
                // The saved target must survive the epilog's callee-saved register restoration.
                assert(new regMaskTP(intCalleeTrashMask & ~linkRegisterMask).IsRegNumInMask(tempReg));

                regNumber callAddressReg;
                if (call.IsVirtualStubRelativeIndir)
                {
                    assert(_compiler.virtualStubParamInfo is not null);
                    callAddressReg = _compiler.virtualStubParamInfo.Reg;
                }
                else
                {
                    callAddressReg = Configuration.TARGET_ARM64
                        ? REG_R2R_INDIRECT_PARAM_ARM64
                        : REG_R2R_INDIRECT_PARAM_ARM;
                }
                Emitter.emitIns_R_R(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize, tempReg, callAddressReg);
                InternalRegisters.Add(call, genRegMask(tempReg));
            }
            return;
        }

        if (_compiler.killGCRefs(call))
        {
            genDefineTempLabel(genCreateTempLabel());

            // The runtime can't find callee-saved registers through an InlinedCallFrame.
            assert(((GCInfo.gcRegGCrefSetCur | GCInfo.gcRegByrefSetCur)
                & new regMaskTP(intCalleeSavedMask)).IsEmpty);
        }

        genCallInstructionArmArch(call);
        genDefinePendingCallLabel(call);

        if (Configuration.DEBUG)
        {
            var floatCalleeTrashMask = Configuration.TARGET_ARM64
                ? SRBM_FLT_CALLEE_TRASH_ARM64
                : SRBM_FLT_CALLEE_TRASH_ARM;
            var killMask = new regMaskTP(intCalleeTrashMask | floatCalleeTrashMask);
            if (call.IsHelperCall())
            {
                killMask = _compiler.compHelperCallKillSet(call.HelperNum);
            }

            assert((GCInfo.gcRegGCrefSetCur & killMask).IsEmpty);
            assert((GCInfo.gcRegByrefSetCur & killMask).IsEmpty);
        }

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
                var compUseSoftFP = Configuration.TARGET_ARM && _compiler.opts.compUseSoftFP;
                if (Configuration.TARGET_ARM && call.IsHelperCall(CORINFO_HELP_INIT_PINVOKE_FRAME))
                {
                    returnReg = REG_PINVOKE_TCB_ARM;
                }
                else if (compUseSoftFP)
                {
                    returnReg = REG_INTRET_ARM;
                }
                else if (!Configuration.TARGET_ARM &&
                    call.IsHelperCall(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT))
                {
                    returnReg = REG_ARM64_R15;
                }
                else if (varTypeUsesFloatArgReg(returnType))
                {
                    returnReg = REG_FLOATRET;
                }
                else
                {
                    returnReg = Configuration.TARGET_ARM64 ? REG_INTRET_ARM64 : REG_INTRET_ARM;
                }

                if (call.RegNum != returnReg)
                {
                    if (Configuration.TARGET_ARM && compUseSoftFP && (returnType is TYP_DOUBLE))
                    {
                        inst_RV_RV_RV(INS_vmov_i2d, call.RegNum, returnReg, REG_NEXT(returnReg), EA_8BYTE);
                    }
                    else if (Configuration.TARGET_ARM && compUseSoftFP && (returnType is TYP_FLOAT))
                    {
                        inst_Mov(returnType, call.RegNum, returnReg, canSkip: false);
                    }
                    else
                    {
                        inst_Mov(returnType, call.RegNum, returnReg, canSkip: false);
                    }
                }
            }

            genProduceReg(call);
        }

        if ((call.Next is null) && !_compiler.MinOpts && !_compiler.opts.compDbgCode)
        {
            GCInfo.gcMarkRegSetNpt(new regMaskTP(intReturnMask));
        }
    }

    public unsafe void genCallInstructionArmArch(GenTreeCall call)
    {
        ref readonly var returnTypeDesc = ref call.ReturnTypeDesc;
        var parameters = new EmitCallParams();
        var linkRegisterMask = Configuration.TARGET_ARM64 ? SRBM_ARM64_LR : SRBM_ARM32_LR;

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

        if (Configuration.TARGET_ARM)
        {
            assert(parameters.secondRetSize != EA_GCREF);
            assert(parameters.secondRetSize != EA_BYREF);
        }

        parameters.isJump = call.IsFastTailCall;
        parameters.hasAsyncRet = call.IsAsync;
        parameters.returnValueCall = call;

        if (Configuration.DEBUG)
        {
            if (!call.IsHelperCall())
            {
                parameters.sigInfo = new System.Runtime.CompilerServices.StrongBox<CORINFO_SIG_INFO>(
                    call.CallSigInfo.Value);
            }
        }

        genCheckTailCallEpilogRegisters(call);

        var target = getCallTarget(call, out parameters.methHnd);
        if (target is not null)
        {
            assert(!target.IsContainedIndir);
            if (!call.IsFastTailCall)
            {
                _ = genConsumeReg(target);
            }

            assert(genIsValidIntReg(target.RegNum));

            var isTlsHandleTarget = Configuration.TARGET_ARM64 &&
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

                Emitter.emitIns_R(INS_mrs_tpid0, attr, REG_ARM64_R1);
                GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_ARM64_R0));
                genEmitTlsDescriptorAddressArm64(
                    attr,
                    REG_ARM64_R0,
                    target.RegNum,
                    (nint)parameters.methHnd,
                    Configuration.DEBUG ? unchecked((nuint)icon.TargetHandle) : 0,
                    Configuration.DEBUG ? icon.Flags : GTF_EMPTY);
            }
            parameters.callType = EC_INDIR_R;
            parameters.ireg = target.RegNum;
            genEmitCallWithCurrentGC(ref parameters);

            if (isTlsHandleTarget)
            {
                Emitter.emitIns_R_R_R(INS_add, EA_8BYTE, REG_ARM64_R0, REG_ARM64_R1, REG_ARM64_R0);
            }
        }
        else
        {
            var callThroughIndirectionReg = regNumberExtensions.NotAvailable;
            if (!call.IsHelperCall(CORINFO_HELP_DISPATCH_INDIRECT_CALL))
            {
                callThroughIndirectionReg = getCallIndirectionCellReg(call);
            }

            if (callThroughIndirectionReg != regNumberExtensions.NotAvailable)
            {
                assert(call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir);
                regNumber targetAddressReg;
                if (!call.IsFastTailCall)
                {
                    if (Configuration.TARGET_ARM)
                    {
                        // ARM32 allocates a temporary to avoid the larger IP-based load encoding.
                        targetAddressReg = InternalRegisters.GetSingle(call);
                    }
                    else
                    {
                        // ARM64 uses IP0 directly and saves the temporary register.
                        targetAddressReg = REG_INDIRECT_CALL_TARGET_REG_ARM64;
                    }
                    Emitter.emitIns_R_R(ins_Load(TYP_I_IMPL), TYP_I_IMPL.EmitActualSize, targetAddressReg,
                        callThroughIndirectionReg);
                }
                else
                {
                    targetAddressReg = InternalRegisters.GetSingle(call);
                    var intCalleeTrashMask = Configuration.TARGET_ARM64
                        ? SRBM_INT_CALLEE_TRASH_ARM64
                        : SRBM_INT_CALLEE_TRASH_ARM;
                    assert(new regMaskTP(intCalleeTrashMask & ~linkRegisterMask).IsRegNumInMask(targetAddressReg));
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

                if (Configuration.TARGET_ARM && !validImmForBL((nint)parameters.addr))
                {
                    var tempReg = InternalRegisters.GetSingle(call);
                    instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, tempReg, (nint)parameters.addr);
                    parameters.callType = EC_INDIR_R;
                    parameters.addr = null;
                    parameters.ireg = tempReg;
                    genEmitCallWithCurrentGC(ref parameters);
                }
                else
                {
                    parameters.callType = EC_FUNC_TOKEN;
                    genEmitCallWithCurrentGC(ref parameters);
                }
            }
        }
    }
}
