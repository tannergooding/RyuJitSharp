// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitGSCookieCheck(bool tailCall)
    {
#if TARGET_ARMARCH
        noway_assert((_compiler.gsGlobalSecurityCookieAddr is not null) ||
            (_compiler.gsGlobalSecurityCookieVal != 0));

        // The check has no IR node, so LSRA cannot reserve registers for it.
        // Keep tailcall argument registers intact and use callee-trash registers.
        var tempRegs = genGetGSCookieTempRegs(tailCall, null);
        assert(tempRegs != RBM_NONE);
        var regGSConst = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);
        tempRegs &= ~regMaskTP.CreateFromRegNum(regGSConst, regGSConst.SingleTypeMask);
        assert(tempRegs != RBM_NONE);
        var regGSValue = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);

        if (_compiler.gsGlobalSecurityCookieAddr is null)
        {
            // Load the cookie constant into a register.
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, regGSConst, _compiler.gsGlobalSecurityCookieVal);
        }
        else
        {
            // AOT cookie constants are accessed through an indirection.
            instGen_Set_Reg_To_Imm(EA_PTRSIZE | EA_CNS_RELOC_FLG, regGSConst,
                unchecked((nint)_compiler.gsGlobalSecurityCookieAddr), INS_FLAGS_DONT_CARE
#if DEBUG
                , (nuint)THT_GSCookieCheck, GTF_EMPTY
#endif
                );
            Emitter.emitIns_R_R_I(
                INS_ldr, EA_PTRSIZE, regGSConst, regGSConst, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
        }

        // Load this method's cookie from the stack frame and compare it to the constant.
        Emitter.emitIns_R_S(INS_ldr, EA_PTRSIZE, regGSValue, _compiler.lvaGSSecurityCookie, 0);
        Emitter.emitIns_R_R(INS_cmp, EA_PTRSIZE, regGSConst, regGSValue);

        var gsCheckBlk = genCreateTempLabel();
        inst_JMP(EJ_eq, gsCheckBlk);
        // These registers are dead after the comparison and can be reused by the helper call.
        genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN, regGSConst);
        genDefineTempLabel(gsCheckBlk);
#elif TARGET_XARCH
        Emitter.RequireSupportedInstructionRecording();
        noway_assert((_compiler.gsGlobalSecurityCookieAddr != null) || (_compiler.gsGlobalSecurityCookieVal != 0));

        GenTreeCall? tailCallNode = null;
        if (tailCall)
        {
            assert(_compiler.compCurBB is not null);
            var lastNode = _compiler.compCurBB.GetLastNode();
            assert(lastNode is not null);
            if (lastNode.Oper == GT_CALL)
            {
                tailCallNode = lastNode.AsCall();
                assert(tailCallNode.IsFastTailCall);
            }
        }

        var tempRegs = genGetGSCookieTempRegs(tailCall, tailCallNode);
        assert(tempRegs != RBM_NONE);
        // The cookie helper supplies only GPRs, all in the low register-mask word.
        var regGSCheck = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);
        var cookie = _compiler.gsGlobalSecurityCookieVal;
        if (_compiler.gsGlobalSecurityCookieAddr == null)
        {
#if TARGET_AMD64
            // CMP r/m64 sign-extends its imm32; an unsigned 32-bit fit is not enough.
            if (unchecked((int)cookie) != cookie)
            {
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, regGSCheck, cookie);
                Emitter.emitIns_S_R(INS_cmp, EA_PTRSIZE, regGSCheck, _compiler.lvaGSSecurityCookie, 0);
            }
            else
#endif
            {
#if TARGET_AMD64
                assert(unchecked((int)cookie) == cookie);
#endif
                Emitter.emitIns_S_I(INS_cmp, EA_PTRSIZE, _compiler.lvaGSSecurityCookie, 0, unchecked((int)cookie));
            }
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE | EA_CNS_RELOC_FLG, regGSCheck,
                (nint)_compiler.gsGlobalSecurityCookieAddr);
            Emitter.emitIns_R_AR(ins_Load(TYP_I_IMPL), EA_PTRSIZE, regGSCheck, regGSCheck, 0);
            Emitter.emitIns_S_R(INS_cmp, EA_PTRSIZE, regGSCheck, _compiler.lvaGSSecurityCookie, 0);
        }

        var gsCheckBlk = genCreateTempLabel();
        inst_JMP(EJ_je, gsCheckBlk);
        genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);
        genDefineTempLabel(gsCheckBlk);
#elif TARGET_LOONGARCH64
        noway_assert((_compiler.gsGlobalSecurityCookieAddr is not null) ||
            (_compiler.gsGlobalSecurityCookieVal != 0));

        var tempRegs = genGetGSCookieTempRegs(tailCall, null);
        assert(tempRegs != RBM_NONE);
        var regGSConst = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);
        tempRegs &= ~regMaskTP.CreateFromRegNum(regGSConst, regGSConst.SingleTypeMask);
        assert(tempRegs != RBM_NONE);
        var regGSValue = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);

        if (_compiler.gsGlobalSecurityCookieAddr is null)
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, regGSConst, _compiler.gsGlobalSecurityCookieVal);
        }
        else
        {
            var cookieAddress = unchecked((nint)_compiler.gsGlobalSecurityCookieAddr);
            if (_compiler.opts.compReloc)
            {
                Emitter.emitIns_R_AI(INS_bl, EA_PTR_DSP_RELOC, regGSConst, cookieAddress);
            }
            else
            {
                Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, regGSConst,
                    (cookieAddress & unchecked((nint)0xfffff000u)) >> 12);
                Emitter.emitIns_R_I(INS_lu32i_d, EA_PTRSIZE, regGSConst, cookieAddress >> 32);
                Emitter.emitIns_R_R_I(INS_ldptr_d, EA_PTRSIZE, regGSConst, regGSConst,
                    (cookieAddress & 0xfff) >> 2);
            }

            _regSet.verifyRegUsed(regGSConst);
        }

        Emitter.emitIns_R_S(INS_ld_d, EA_PTRSIZE, regGSValue, _compiler.lvaGSSecurityCookie, 0);

        var gsCheckBlk = genCreateTempLabel();
        Emitter.emitIns_J_cond_la(INS_beq, gsCheckBlk, regGSConst, regGSValue);
        genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN, regGSConst);
        genDefineTempLabel(gsCheckBlk);
#elif TARGET_RISCV64
        noway_assert((_compiler.gsGlobalSecurityCookieAddr is not null) ||
            (_compiler.gsGlobalSecurityCookieVal != 0));

        var tempRegs = genGetGSCookieTempRegs(tailCall, null);
        assert(tempRegs != RBM_NONE);
        var regGSConst = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);
        tempRegs &= ~regMaskTP.CreateFromRegNum(regGSConst, regGSConst.SingleTypeMask);
        assert(tempRegs != RBM_NONE);
        var regGSValue = (regNumber)BitOperations.TrailingZeroCount((ulong)tempRegs.IntRegSet);

        if (_compiler.gsGlobalSecurityCookieAddr is null)
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, regGSConst, _compiler.gsGlobalSecurityCookieVal);
        }
        else
        {
            Emitter.emitIns_R_AI(INS_ld, EA_PTR_DSP_RELOC, regGSConst,
                unchecked((nint)_compiler.gsGlobalSecurityCookieAddr));
            _regSet.verifyRegUsed(regGSConst);
        }

        Emitter.emitIns_R_S(INS_ld, EA_PTRSIZE, regGSValue, _compiler.lvaGSSecurityCookie, 0);

        var gsCheckBlk = genCreateTempLabel();
        Emitter.emitIns_J_cond_la(INS_beq, gsCheckBlk, regGSConst, regGSValue);
        genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN, regGSConst);
        genDefineTempLabel(gsCheckBlk);
#elif TARGET_WASM
        // TODO-WASM: GS cookie checks have limited utility on WASM since they can only help
        // with detecting linear memory stack corruption. Decide if we want them anyway.
        NYI_WASM("genEmitGSCookieCheck");
#else
        throw new FatalJitException(CORJIT_SKIPPED, "GS-cookie checks require xarch.");
#endif
    }
}
