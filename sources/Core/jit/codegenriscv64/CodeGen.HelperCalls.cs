// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize,
        regNumber callTargetReg = REG_NA)
    {
        var parameters = new EmitCallParams();
        var helperFunction = _compiler.compGetHelperFtn(helper);
        var killMask = _compiler.compHelperCallKillSet(helper);

        if (callTargetReg is REG_NA)
        {
            callTargetReg = REG_DEFAULT_HELPER_CALL_TARGET;
        }
        parameters.ireg = callTargetReg;

        if (helperFunction.accessType is IAT_VALUE)
        {
            parameters.callType = EC_FUNC_TOKEN;
            parameters.addr = helperFunction.addr;
        }
        else
        {
            parameters.addr = null;
            assert(helperFunction.accessType is IAT_PVALUE);
            var address = helperFunction.addr;

            var callTargetMask = genRegMask(callTargetReg);
            noway_assert((callTargetMask & killMask) == callTargetMask);

            if (_compiler.opts.compReloc)
            {
                Emitter.emitIns_R_AI(INS_ld, EA_PTR_DSP_RELOC, callTargetReg, callTargetReg,
                    unchecked((nint)address));
            }
            else
            {
                Emitter.emitIns_R_R_Addr(INS_ld, EA_PTRSIZE, callTargetReg, callTargetReg, address);
            }

            _regSet.verifyRegUsed(callTargetReg);
            parameters.callType = EC_INDIR_R;
        }

        parameters.methHnd = Compiler.eeFindHelper(helper);
        parameters.argSize = argSize;
        parameters.retSize = retSize;

        genEmitCallWithCurrentGC(ref parameters);
        _regSet.verifyRegistersUsed(killMask);
    }
}
#endif
