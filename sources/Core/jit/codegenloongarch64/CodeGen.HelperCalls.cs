// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize,
        regNumber callTargetReg = REG_NA)
    {
        var parameters = new EmitCallParams { callType = EC_FUNC_TOKEN };
        var helperFunction = _compiler.compGetHelperFtn(helper);
        var killMask = _compiler.compHelperCallKillSet(helper);

        if (helperFunction.accessType is IAT_VALUE)
        {
            parameters.addr = helperFunction.addr;
        }
        else
        {
            parameters.addr = null;
            assert(helperFunction.accessType is IAT_PVALUE);
            var address = helperFunction.addr;

            if (callTargetReg is REG_NA)
            {
                callTargetReg = REG_DEFAULT_HELPER_CALL_TARGET;
            }

            var callTargetMask = genRegMask(callTargetReg);
            noway_assert((callTargetMask & killMask) == callTargetMask);

            if (_compiler.opts.compReloc)
            {
                Emitter.emitIns_R_AI(INS_bl, EA_PTR_DSP_RELOC, callTargetReg, unchecked((nint)address)
#if DEBUG
                    , unchecked((nuint)Compiler.eeFindHelper(helper)), GTF_ICON_METHOD_HDL
#endif
                );
            }
            else
            {
                var addressValue = unchecked((nint)address);
                Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, callTargetReg,
                    (addressValue & unchecked((nint)0xfffff000u)) >> 12);
                Emitter.emitIns_R_I(INS_lu32i_d, EA_PTRSIZE, callTargetReg, addressValue >> 32);
                Emitter.emitIns_R_R_I(INS_ldptr_d, EA_PTRSIZE, callTargetReg, callTargetReg,
                    (addressValue & 0xfff) >> 2);
            }

            _regSet.verifyRegUsed(callTargetReg);
            parameters.callType = EC_INDIR_R;
            parameters.ireg = callTargetReg;
        }

        parameters.methHnd = Compiler.eeFindHelper(helper);
        parameters.argSize = argSize;
        parameters.retSize = retSize;

        genEmitCallWithCurrentGC(ref parameters);
        _regSet.verifyRegistersUsed(killMask);
    }
}
#endif
