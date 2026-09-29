// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genSetGSSecurityCookie(regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "GS-cookie initialization requires xarch.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (!_compiler.NeedsGSSecurityCookie)
        {
            return;
        }

        if (_compiler.opts.IsOSR && _compiler.info.compPatchpointInfo->HasSecurityCookie)
        {
            // The original frame already owns the initialized security cookie.
            return;
        }

        if (_compiler.gsGlobalSecurityCookieAddr is null)
        {
            var cookie = _compiler.gsGlobalSecurityCookieVal;
            noway_assert(cookie != 0);
#if TARGET_AMD64
            if (unchecked((int)cookie) != cookie)
            {
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, cookie);
                Emitter.emitIns_S_R(INS_mov, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
                initRegZeroed = false;
            }
            else
#endif
            {
                Emitter.emitIns_S_I(INS_mov, EA_PTRSIZE, _compiler.lvaGSSecurityCookie, 0, unchecked((int)cookie));
            }
        }
        else
        {
            Emitter.emitIns_R_AI(INS_mov, EA_PTRSIZE | EA_DSP_RELOC_FLG, REG_EAX, (nint)_compiler.gsGlobalSecurityCookieAddr);
            _regSet.verifyRegUsed(REG_EAX);
            Emitter.emitIns_S_R(INS_mov, EA_PTRSIZE, REG_EAX, _compiler.lvaGSSecurityCookie, 0);
            if (initReg == REG_EAX)
            {
                initRegZeroed = false;
            }
        }
#endif
    }
}
