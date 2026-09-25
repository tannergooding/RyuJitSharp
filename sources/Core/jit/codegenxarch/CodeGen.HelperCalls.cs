// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize,
        regNumber callTargetReg = REG_NA)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Helper-call generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var parameters = new EmitCallParams { callType = EC_FUNC_TOKEN };
        var helperFunction = _compiler.compGetHelperFtn(helper);
        var killMask = _compiler.compHelperCallKillSet(helper);

        if (helperFunction.accessType == IAT_VALUE)
        {
            parameters.addr = helperFunction.addr;
        }
        else
        {
            parameters.addr = null;
            assert(helperFunction.accessType == IAT_PVALUE);
            var address = helperFunction.addr;
            assert(address != null);

            // PC-relative encoding is one byte smaller, so query it before zero-relative encoding.
            if (genCodeIndirAddrCanBeEncodedAsPCRelOffset((nuint)address) ||
                genCodeIndirAddrCanBeEncodedAsZeroRelOffset((nuint)address))
            {
                parameters.callType = EC_FUNC_TOKEN_INDIR;
                parameters.addr = address;
            }
            else
            {
                if (callTargetReg == REG_NA)
                {
                    callTargetReg = REG_DEFAULT_HELPER_CALL_TARGET;
                    var targetMask = regMaskTP.CreateFromRegNum(callTargetReg, callTargetReg.SingleTypeMask);
                    noway_assert((targetMask & killMask) == targetMask);
                }
                else
                {
                    var targetMask = regMaskTP.CreateFromRegNum(callTargetReg, callTargetReg.SingleTypeMask);
                    noway_assert((targetMask & _regSet.GetMaskVars()) == RBM_NONE);
                }

                instGen_Set_Reg_To_Imm(EA_PTRSIZE | EA_CNS_RELOC_FLG, callTargetReg, unchecked((nint)address));
                parameters.ireg = callTargetReg;
                parameters.callType = EC_INDIR_ARD;
            }
        }

        parameters.methHnd = Compiler.eeFindHelper(helper);
        parameters.argSize = argSize;
        parameters.retSize = retSize;
        genEmitCallWithCurrentGC(ref parameters);
        _regSet.verifyRegistersUsed(killMask);
#endif
    }

    public bool genCodeIndirAddrCanBeEncodedAsPCRelOffset(nuint address)
    {
#if TARGET_AMD64
        return genAddrRelocTypeHint(address) == CorInfoReloc.RELATIVE32;
#else
        return true;
#endif
    }

    public static bool genCodeIndirAddrCanBeEncodedAsZeroRelOffset(nuint address)
    {
        return FitsInI32(unchecked((nint)address));
    }

    public bool genCodeIndirAddrNeedsReloc(nuint address)
    {
        if (_compiler.opts.compReloc)
        {
            return true;
        }
#if TARGET_AMD64
        if (genCodeIndirAddrCanBeEncodedAsZeroRelOffset(address))
        {
            return false;
        }

        return true;
#else
        return false;
#endif
    }

    public bool genCodeAddrNeedsReloc(nuint address)
    {
        if (_compiler.opts.compReloc)
        {
            return true;
        }
#if TARGET_AMD64
        // The VM supplies a jump stub when a direct target is outside the relative-call range.
        return true;
#else
        return false;
#endif
    }
}
#endif
