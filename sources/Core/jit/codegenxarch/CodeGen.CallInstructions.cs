// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Runtime.CompilerServices;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCallInstruction(GenTreeCall call)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Call-instruction generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var parameters = new EmitCallParams();
        ref readonly var retTypeDesc = ref call.ReturnTypeDesc;

        // Unused values are of no interest to GC.
        if (!call.IsUnusedValue)
        {
            if (call.HasMultiRegRetVal)
            {
                parameters.retSize = retTypeDesc.GetReturnRegType(0).EmitSize;
                parameters.secondRetSize = retTypeDesc.GetReturnRegType(1).EmitSize;

                if (retTypeDesc.GetAbiReturnReg(1, call.UnmanagedCallConv) == REG_INTRET)
                {
                    // The emitter associates retSize with REG_INTRET even when the
                    // ABI places an earlier result in a SIMD register.
                    assert(!EA_IS_GCREF(parameters.retSize) && !EA_IS_BYREF(parameters.retSize));
                    parameters.retSize = parameters.secondRetSize;
                    parameters.secondRetSize = EA_UNKNOWN;
                }
            }
            else
            {
                assert(!varTypeIsStruct(call.Type));
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
            if (target.IsContainedIndir)
            {
                assert(!_compiler.opts.IsCFGEnabled ||
                    call.IsHelperCall(CORINFO_HELP_VALIDATE_INDIRECT_CALL) ||
                    call.IsHelperCall(CORINFO_HELP_DISPATCH_INDIRECT_CALL));

                var indir = target.AsIndir();
                if (indir.HasBase && indir.Base.IsContainedIntOrIImmed)
                {
                    // Lowering contains an absolute target only when it can be
                    // encoded as a PC-relative offset.
                    var address = indir.Base.AsIntConCommon();
                    assert(address.FitsInAddrBase(_compiler));
                    parameters.callType = EC_FUNC_TOKEN_INDIR;
                    parameters.addr = unchecked((void*)address.IconValue);
                    genEmitCallWithCurrentGC(ref parameters);
                }
                else
                {
                    // Fast tailcalls have already consumed the target before the epilog.
                    if (!call.IsFastTailCall)
                    {
                        genConsumeAddress(indir.Addr);
                    }

                    var iReg = indir.HasBase ? indir.Base.RegNum : REG_NA;
                    var xReg = indir.HasIndex ? indir.Index.RegNum : REG_NA;
                    var calleeTrash = new regMaskTP(
                        _compiler.SRBM_INT_CALLEE_TRASH | _compiler.SRBM_FLT_CALLEE_TRASH,
                        _compiler.SRBM_MSK_CALLEE_TRASH);
                    assert(!parameters.isJump || (iReg == REG_NA) ||
                        (calleeTrash & new regMaskTP(iReg.SingleTypeMask)).IsNonEmpty);
                    assert(!parameters.isJump || (xReg == REG_NA) ||
                        (calleeTrash & new regMaskTP(xReg.SingleTypeMask)).IsNonEmpty);

                    parameters.callType = EC_INDIR_ARD;
                    parameters.ireg = iReg;
                    parameters.xreg = xReg;
                    parameters.xmul = indir.Scale;
                    parameters.disp = indir.Offset;
                    genEmitCallWithCurrentGC(ref parameters);
                }
            }
            else if (!_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) || ((call.Flags & GTF_TLS_GET_ADDR) == 0))
            {
                assert(genIsValidIntReg(target.RegNum));
                if (!call.IsFastTailCall)
                {
                    _ = genConsumeReg(target);
                }
                parameters.callType = EC_INDIR_R;
                parameters.ireg = target.RegNum;
                genEmitCallWithCurrentGC(ref parameters);
            }
            else
            {
                // NativeAOT's linker recognizes this TLS call sequence by its prefixes.
                Emitter.emitIns_Data16();
                Emitter.emitIns_Data16();
                parameters.callType = EC_FUNC_TOKEN;
                parameters.methHnd = (CORINFO_METHOD_HANDLE)1;
                parameters.addr = unchecked((void*)target.AsIntCon().IconValue);
                parameters.noSafePoint = true;
                genEmitCallWithCurrentGC(ref parameters);
            }
        }
        else
        {
            // Reuse the cell's argument register instead of addressing the cell again.
            var indirCellReg = getCallIndirectionCellReg(call);
            if (indirCellReg != REG_NA)
            {
                parameters.callType = EC_INDIR_ARD;
                parameters.ireg = indirCellReg;
                genEmitCallWithCurrentGC(ref parameters);
            }
            else
            {
                assert(call.IsHelperCall() || (call._callType == CT_USER_FUNC));
                assert(call._directCallAddress is not null);
                parameters.callType = EC_FUNC_TOKEN;
                parameters.addr = call._directCallAddress;
                genEmitCallWithCurrentGC(ref parameters);
            }
        }
#endif
    }
}
