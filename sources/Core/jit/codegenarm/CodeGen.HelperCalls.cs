// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize,
        regNumber callTargetReg = REG_NA)
    {
        void* addr = null;
        void** pAddr = null;

#if DEBUG && PROFILING_SUPPORTED
        if (!_compiler.compProfilerHookNeeded && _compiler.opts.compJitELTHookEnabled &&
            (helper is CORINFO_HELP_PROF_FCN_ENTER or CORINFO_HELP_PROF_FCN_LEAVE or CORINFO_HELP_PROF_FCN_TAILCALL))
        {
            addr = _compiler.compProfilerMethHnd;
        }
        else
#endif
        {
            var helperFunction = _compiler.compGetHelperFtn(helper);
            if (helperFunction.accessType is IAT_VALUE)
            {
                addr = helperFunction.addr;
            }
            else
            {
                assert(helperFunction.accessType is IAT_PVALUE);
                pAddr = (void**)helperFunction.addr;
            }
        }

        var parameters = new EmitCallParams
        {
            methHnd = Compiler.eeFindHelper(helper),
            argSize = argSize,
            retSize = retSize,
        };

        if ((addr == null) || !validImmForBL((nint)addr))
        {
            if (callTargetReg is REG_NA)
            {
                callTargetReg = REG_R12;
            }

            if (addr != null)
            {
                instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, callTargetReg, (nint)addr);
            }
            else
            {
                Emitter.emitIns_R_AI(INS_ldr, EA_PTR_DSP_RELOC, callTargetReg, (nint)pAddr);
                _regSet.verifyRegUsed(callTargetReg);
            }

            parameters.callType = EC_INDIR_R;
            parameters.ireg = callTargetReg;
            genEmitCallWithCurrentGC(ref parameters);
        }
        else
        {
            parameters.callType = EC_FUNC_TOKEN;
            parameters.addr = addr;
            genEmitCallWithCurrentGC(ref parameters);
        }

        _regSet.verifyRegistersUsed(new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH));
    }
}
#endif
