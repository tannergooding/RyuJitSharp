// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genSetGSSecurityCookie(regNumber initReg, ref bool initRegZeroed)
    {
        if (Configuration.TARGET_ARM || Configuration.TARGET_ARM64)
        {
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
                // Store the cookie value from initReg into the stack frame.
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, cookie);
                Emitter.emitIns_S_R(INS_str, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
            }
            else
            {
                // The AOT cookie address must be loaded indirectly.
                instGen_Set_Reg_To_Imm(EA_PTRSIZE | EA_DSP_RELOC_FLG, initReg,
                    unchecked((nint)_compiler.gsGlobalSecurityCookieAddr), INS_FLAGS_DONT_CARE
                    , Configuration.DEBUG ? (nuint)THT_SetGSCookie : 0, GTF_EMPTY
                    );
                if (Configuration.TARGET_ARM)
                {
                    Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, initReg, initReg, 0, INS_FLAGS_DONT_CARE);
                }
                else
                {
                    Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, initReg, initReg, 0);
                }
                _regSet.verifyRegUsed(initReg);
                Emitter.emitIns_S_R(INS_str, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
            }

            initRegZeroed = false;
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (!_compiler.NeedsGSSecurityCookie)
            {
                return;
            }

            if (_compiler.opts.IsOSR && _compiler.info.compPatchpointInfo->HasSecurityCookie)
            {
                return;
            }

            if (_compiler.gsGlobalSecurityCookieAddr is null)
            {
                noway_assert(_compiler.gsGlobalSecurityCookieVal != 0);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, _compiler.gsGlobalSecurityCookieVal);
                Emitter.emitIns_S_R(INS_st_d, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
            }
            else
            {
                var address = unchecked((nint)_compiler.gsGlobalSecurityCookieAddr);
                if (_compiler.opts.compReloc)
                {
                    Emitter.emitIns_R_AI(INS_bl, EA_PTR_DSP_RELOC, initReg, address);
                }
                else
                {
                    Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, initReg,
                        (address & unchecked((nint)0xfffff000u)) >> 12);
                    Emitter.emitIns_R_I(INS_lu32i_d, EA_PTRSIZE, initReg, address >> 32);
                    Emitter.emitIns_R_R_I(INS_ldptr_d, EA_PTRSIZE, initReg, initReg, (address & 0xfff) >> 2);
                }

                _regSet.verifyRegUsed(initReg);
                Emitter.emitIns_S_R(INS_st_d, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
            }

            initRegZeroed = false;
        }
        else if (Configuration.TARGET_RISCV64)
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (!_compiler.NeedsGSSecurityCookie)
            {
                return;
            }

            if (_compiler.opts.IsOSR && _compiler.info.compPatchpointInfo->HasSecurityCookie)
            {
                return;
            }

            if (_compiler.gsGlobalSecurityCookieAddr is null)
            {
                noway_assert(_compiler.gsGlobalSecurityCookieVal != 0);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, _compiler.gsGlobalSecurityCookieVal);
                Emitter.emitIns_S_R(INS_sd, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
            }
            else
            {
                Emitter.emitIns_R_AI(INS_ld, EA_PTR_DSP_RELOC, initReg, initReg,
                    unchecked((nint)_compiler.gsGlobalSecurityCookieAddr));
                _regSet.verifyRegUsed(initReg);
                Emitter.emitIns_S_R(INS_sd, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
            }

            initRegZeroed = false;
        }
        else if (Configuration.TARGET_XARCH)
        {
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
                if (Configuration.TARGET_AMD64 && (unchecked((int)cookie) != cookie))
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, cookie);
                    Emitter.emitIns_S_R(INS_mov, EA_PTRSIZE, initReg, _compiler.lvaGSSecurityCookie, 0);
                    initRegZeroed = false;
                }
                else
                {
                    if (Configuration.TARGET_AMD64)
                    {
                        assert(unchecked((int)cookie) == cookie);
                    }

                    Emitter.emitIns_S_I(INS_mov, EA_PTRSIZE, _compiler.lvaGSSecurityCookie, 0, unchecked((int)cookie));
                }
            }
            else
            {
                var cookieReg = Configuration.TARGET_X86 ? REG_X86_EAX : REG_X64_RAX;
                Emitter.emitIns_R_AI(INS_mov, EA_PTRSIZE | EA_DSP_RELOC_FLG, cookieReg, (nint)_compiler.gsGlobalSecurityCookieAddr);
                _regSet.verifyRegUsed(cookieReg);
                Emitter.emitIns_S_R(INS_mov, EA_PTRSIZE, cookieReg, _compiler.lvaGSSecurityCookie, 0);
                if (initReg == cookieReg)
                {
                    initRegZeroed = false;
                }
            }
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED, "GS-cookie initialization is not ported for this target.");
        }
    }
}
