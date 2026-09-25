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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "GS-cookie checks require Windows AMD64.");
#else
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
            // CMP r/m64 sign-extends its imm32; an unsigned 32-bit fit is not enough.
            if (unchecked((int)cookie) != cookie)
            {
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, regGSCheck, cookie);
                Emitter.emitIns_S_R(INS_cmp, EA_PTRSIZE, regGSCheck, _compiler.lvaGSSecurityCookie, 0);
            }
            else
            {
                assert(unchecked((int)cookie) == cookie);
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
#endif
    }
}
