// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_AMD64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genAmd64EmitterUnitTestsSse2()
    {
        var theEmitter = Emitter;

        genDefineTempLabel(genCreateTempLabel());

        theEmitter.emitIns_R_R_R(INS_haddpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_addss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_addsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_addps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_addpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_addpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_subss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_subsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_subps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_subps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_subpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_subpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_mulss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_mulsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_mulps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_mulpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_mulps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_mulpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_andps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_andpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_andps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_andpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_orps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_orpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_orps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_orpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_divss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_divsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_divss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_divsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        theEmitter.emitIns_R_R_R(INS_cvtss2sd, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_cvtsd2ss, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    }

    public void genAmd64EmitterUnitTestsApx()
    {
        var theEmitter = Emitter;

        genDefineTempLabel(genCreateTempLabel());

        // This test suite needs REX2 enabled.
        if (!theEmitter.UseRex2Encodings && !_compiler.DoJitStressRex2Encoding())
        {
            return;
        }

        theEmitter.emitIns_R_R(INS_add, EA_1BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_add, EA_2BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_add, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_add, EA_8BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_or, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_adc, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_sbb, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_and, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_sub, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_xor, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_cmp, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_test, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_bsf, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_bsr, EA_4BYTE, REG_EAX, REG_ECX);

        theEmitter.emitIns_R_R(INS_cmovo, EA_4BYTE, REG_EAX, REG_ECX);

        _ = theEmitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_EAX, REG_ECX, false);
        _ = theEmitter.emitIns_Mov(INS_movsx, EA_2BYTE, REG_EAX, REG_ECX, false);
        _ = theEmitter.emitIns_Mov(INS_movzx, EA_2BYTE, REG_EAX, REG_ECX, false);

        theEmitter.emitIns_R_R(INS_popcnt, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_lzcnt, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_tzcnt, EA_4BYTE, REG_EAX, REG_ECX);

        theEmitter.emitIns_R_I(INS_add, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_add, EA_2BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_or, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_adc, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_sbb, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_and, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_sub, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_xor, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_cmp, EA_4BYTE, REG_EAX, 0x05);
        theEmitter.emitIns_R_I(INS_test, EA_4BYTE, REG_EAX, 0x05);

        theEmitter.emitIns_R_I(INS_mov, EA_4BYTE, REG_EAX, 0xE0);

        // JIT tend to compress imm64 to imm32 if higher half is all-zero, make sure this test checks the path for imm64.
        theEmitter.emitIns_R_I(INS_mov, EA_8BYTE, REG_RAX, unchecked((nint)0xFFFF000000000000UL));

        // shf reg, cl
        theEmitter.emitIns_R(INS_rol, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_ror, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_rcl, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_rcr, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_shl, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_shr, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_sar, EA_4BYTE, REG_EAX);

        // shf reg, 1
        theEmitter.emitIns_R(INS_rol_1, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_ror_1, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_rcl_1, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_rcr_1, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_shl_1, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_shr_1, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_sar_1, EA_4BYTE, REG_EAX);

        // shf reg, imm8
        theEmitter.emitIns_R_I(INS_shl_N, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_shr_N, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_sar_N, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_rol_N, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_ror_N, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_rcl_N, EA_4BYTE, REG_ECX, 0x05);
        theEmitter.emitIns_R_I(INS_rcr_N, EA_4BYTE, REG_ECX, 0x05);

        theEmitter.emitIns_R(INS_neg, EA_2BYTE, REG_EAX);
        theEmitter.emitIns_R(INS_not, EA_2BYTE, REG_EAX);

        theEmitter.emitIns_R_AR(INS_lea, EA_4BYTE, REG_ECX, REG_EAX, 4);

        theEmitter.emitIns_R_AR(INS_mov, EA_1BYTE, REG_ECX, REG_EAX, 4);
        theEmitter.emitIns_R_AR(INS_mov, EA_2BYTE, REG_ECX, REG_EAX, 4);
        theEmitter.emitIns_R_AR(INS_mov, EA_4BYTE, REG_ECX, REG_EAX, 4);
        theEmitter.emitIns_R_AR(INS_mov, EA_8BYTE, REG_ECX, REG_EAX, 4);

        theEmitter.emitIns_R_AR(INS_add, EA_1BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_add, EA_2BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_add, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_add, EA_8BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_or, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_adc, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_sbb, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_and, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_sub, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_xor, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_cmp, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_test, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_bsf, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_bsr, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_popcnt, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_lzcnt, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_tzcnt, EA_4BYTE, REG_EAX, REG_ECX, 4);

        theEmitter.emitIns_AR_R(INS_add, EA_1BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_add, EA_2BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_add, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_add, EA_8BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_or, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_adc, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_sbb, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_and, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_sub, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_xor, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_cmp, EA_4BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_AR_R(INS_test, EA_4BYTE, REG_EAX, REG_ECX, 4);

        theEmitter.emitIns_R_AR(INS_movsx, EA_2BYTE, REG_ECX, REG_EAX, 4);
        theEmitter.emitIns_R_AR(INS_movzx, EA_2BYTE, REG_EAX, REG_ECX, 4);
        theEmitter.emitIns_R_AR(INS_cmovo, EA_4BYTE, REG_EAX, REG_ECX, 4);

        theEmitter.emitIns_AR_R(INS_xadd, EA_4BYTE, REG_EAX, REG_EDX, 2);

        theEmitter.emitIns_R_R_I(INS_shld, EA_4BYTE, REG_EAX, REG_ECX, 5);
        theEmitter.emitIns_R_R_I(INS_shrd, EA_2BYTE, REG_EAX, REG_ECX, 5);

        theEmitter.emitIns_AR_R(INS_cmpxchg, EA_2BYTE, REG_EAX, REG_EDX, 2);

        theEmitter.emitIns_R(INS_seto, EA_1BYTE, REG_EDX);

        theEmitter.emitIns_R(INS_bswap, EA_8BYTE, REG_EDX);

        // INS_bt only has reg-to-reg form.
        theEmitter.emitIns_R_R(INS_bt, EA_2BYTE, REG_EAX, REG_EDX);

        theEmitter.emitIns_R_R(INS_xchg, EA_8BYTE, REG_EAX, REG_EDX);

        theEmitter.emitIns_R(INS_div, EA_8BYTE, REG_EDX);
        theEmitter.emitIns_R(INS_mulEAX, EA_8BYTE, REG_EDX);

        var physReg = new GenTreePhysReg(REG_EDX) { RegNum = REG_EDX };
        var load = indirForm(TYP_INT, physReg);

        theEmitter.emitIns_R_A(INS_add, EA_1BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_add, EA_2BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_add, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_add, EA_8BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_or, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_adc, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_sbb, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_and, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_sub, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_xor, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_cmp, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_test, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_bsf, EA_4BYTE, REG_EAX, load);
        theEmitter.emitIns_R_A(INS_bsr, EA_4BYTE, REG_EAX, load);

        // Note:
        // All the tests below rely on the runtime status of the stack this unit tests attaching to,
        // it might fail due to stack value unavailable/mismatch, since these tests are mainly for
        // encoding correctness check, this kind of failures may be considered as not harmful.

        theEmitter.emitIns_R_S(INS_add, EA_1BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_add, EA_2BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_add, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_add, EA_8BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_or, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_adc, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_sbb, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_and, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_sub, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_xor, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_cmp, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_test, EA_4BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_S_R(INS_xadd, EA_2BYTE, REG_EAX, 0, 0);

        theEmitter.emitIns_S_I(INS_shl_N, EA_4BYTE, 0, 0, 4);
        theEmitter.emitIns_S(INS_shl_1, EA_4BYTE, 0, 4);

        theEmitter.emitIns_R_S(INS_movsx, EA_2BYTE, REG_ECX, 0, 0);
        theEmitter.emitIns_R_S(INS_movzx, EA_2BYTE, REG_EAX, 0, 0);
        theEmitter.emitIns_R_S(INS_cmovo, EA_4BYTE, REG_EAX, 0, 0);

        theEmitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_EAX);
        theEmitter.emitIns_R(INS_push, EA_PTRSIZE, REG_EAX);
        theEmitter.emitIns_R(INS_pop_hide, EA_PTRSIZE, REG_EAX);
        theEmitter.emitIns_R(INS_push_hide, EA_PTRSIZE, REG_EAX);

        theEmitter.emitIns_S(INS_pop, EA_PTRSIZE, 0, 0);
        theEmitter.emitIns_I(INS_push, EA_PTRSIZE, 50);

        theEmitter.emitIns_R(INS_inc, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_AR(INS_inc, EA_2BYTE, REG_EAX, 2);
        theEmitter.emitIns_S(INS_inc, EA_2BYTE, 0, 0);
        theEmitter.emitIns_R(INS_dec, EA_4BYTE, REG_EAX);
        theEmitter.emitIns_AR(INS_dec, EA_2BYTE, REG_EAX, 2);
        theEmitter.emitIns_S(INS_dec, EA_2BYTE, 0, 0);

        theEmitter.emitIns_S(INS_neg, EA_2BYTE, 0, 0);
        theEmitter.emitIns_S(INS_not, EA_2BYTE, 0, 0);

        // APX-EVEX

        theEmitter.emitIns_R_R_R(INS_add, EA_8BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_R(INS_sub, EA_2BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_R(INS_or, EA_2BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_R(INS_and, EA_2BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_R(INS_xor, EA_1BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R_I(INS_or, EA_2BYTE, REG_R10, REG_EAX, 10565, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_I(INS_or, EA_8BYTE, REG_R10, REG_EAX, 10, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_S(INS_or, EA_8BYTE, REG_R10, REG_EAX, 0, 1, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R(INS_neg, EA_2BYTE, REG_R10, REG_ECX, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R(INS_shl, EA_2BYTE, REG_R11, REG_EAX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R(INS_shl_1, EA_2BYTE, REG_R11, REG_EAX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_I(INS_shl_N, EA_2BYTE, REG_R11, REG_ECX, 7, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_I(INS_shl_N, EA_2BYTE, REG_R11, REG_ECX, 7, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_I(INS_rcr_N, EA_2BYTE, REG_R11, REG_ECX, 7, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_I(INS_rcl_N, EA_2BYTE, REG_R11, REG_ECX, 7, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R(INS_inc, EA_2BYTE, REG_R11, REG_ECX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R(INS_dec, EA_2BYTE, REG_R11, REG_ECX, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R_R(INS_cmovo, EA_4BYTE, REG_R12, REG_R11, REG_EAX, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R_R(INS_imul, EA_4BYTE, REG_R12, REG_R11, REG_ECX, INS_OPTS_EVEX_nd);
        theEmitter.emitIns_R_R_S(INS_imul, EA_4BYTE, REG_R12, REG_R11, 0, 1, INS_OPTS_EVEX_nd);

        theEmitter.emitIns_R_R(INS_add, EA_4BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_sub, EA_4BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_and, EA_4BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_or, EA_4BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_xor, EA_4BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R(INS_inc, EA_4BYTE, REG_R12, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R(INS_dec, EA_4BYTE, REG_R12, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_I(INS_add, EA_4BYTE, REG_R12, 5, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_I(INS_sub, EA_4BYTE, REG_R12, 5, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_I(INS_and, EA_4BYTE, REG_R12, 5, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_I(INS_or, EA_4BYTE, REG_R12, 5, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_I(INS_xor, EA_4BYTE, REG_R12, 5, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_S(INS_add, EA_4BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_sub, EA_4BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_and, EA_4BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_or, EA_4BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_xor, EA_4BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R(INS_neg, EA_2BYTE, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R(INS_shl, EA_2BYTE, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R(INS_shl_1, EA_2BYTE, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_I(INS_shl_N, EA_2BYTE, REG_R11, 7, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_I(INS_shl_N, EA_2BYTE, REG_R11, 7, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_R(INS_imul, EA_4BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_imul, EA_4BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_I(INS_imul_15, EA_4BYTE, REG_R12, 5, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R(INS_imulEAX, EA_8BYTE, REG_R12, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R(INS_mulEAX, EA_8BYTE, REG_R12, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_R(INS_tzcnt_apx, EA_8BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_lzcnt_apx, EA_8BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_popcnt_apx, EA_8BYTE, REG_R12, REG_R11, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_S(INS_tzcnt_apx, EA_8BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_lzcnt_apx, EA_8BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_popcnt_apx, EA_8BYTE, REG_R12, 0, 1, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_R_R(INS_add, EA_2BYTE, REG_R12, REG_R13, REG_R11,
                                  (insOpts)(INS_OPTS_EVEX_nf | INS_OPTS_EVEX_nd));

        theEmitter.emitIns_R_R_R(INS_andn, EA_8BYTE, REG_R11, REG_R13, REG_R11, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R_R(INS_bextr, EA_8BYTE, REG_R11, REG_R13, REG_R11, INS_OPTS_EVEX_nf);

        theEmitter.emitIns_R_R(INS_blsi, EA_8BYTE, REG_R11, REG_R13, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_R(INS_blsmsk, EA_8BYTE, REG_R11, REG_R13, INS_OPTS_EVEX_nf);
        theEmitter.emitIns_R_S(INS_blsr, EA_8BYTE, REG_R11, 0, 1);

        theEmitter.emitIns_AR(INS_inc, EA_4BYTE, REG_EAX, 0, INS_OPTS_EVEX_NoApxPromotion);

        theEmitter.emitIns_BASE_R_R(INS_inc, EA_4BYTE, REG_R11, REG_R12);
        theEmitter.emitIns_BASE_R_R_I(INS_add, EA_4BYTE, REG_R11, REG_R12, 5);

        // testing for EGPR encodings.
        var eGPR = new GenTreePhysReg(REG_R16) { RegNum = REG_R16 };
        var loadGPR = indirForm(TYP_SIMD32, eGPR);

        // SIMD instructions
        // In most of the cases, EGPR will only be used as BASE/INDEX registers in SIMD instructions.
        theEmitter.emitIns_R_R_A(INS_addps, EA_32BYTE, REG_XMM16, REG_XMM16, loadGPR);

        // Legacy instructions
        theEmitter.emitIns_R_ARX(INS_add, EA_4BYTE, REG_R16, REG_R17, REG_R18, 1, 0);

        theEmitter.emitIns_AR_R(INS_movnti64, EA_8BYTE, REG_R17, REG_R16, 10);
        theEmitter.emitIns_R_R_R(INS_andn, EA_8BYTE, REG_R17, REG_R16, REG_R18);

        _ = theEmitter.emitIns_Mov(INS_kmovb_gpr, EA_4BYTE, REG_R16, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovb_msk, EA_4BYTE, REG_K5, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovw_gpr, EA_4BYTE, REG_R16, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovw_msk, EA_4BYTE, REG_K5, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovd_gpr, EA_4BYTE, REG_R16, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovd_msk, EA_4BYTE, REG_K5, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovq_gpr, EA_8BYTE, REG_R16, REG_K0, false);
        _ = theEmitter.emitIns_Mov(INS_kmovq_msk, EA_8BYTE, REG_K5, REG_K0, false);

        theEmitter.emitIns_R_R(INS_crc32_apx, EA_1BYTE, REG_R16, REG_R17);
        theEmitter.emitIns_R_R(INS_crc32_apx, EA_2BYTE, REG_R16, REG_R17);
        theEmitter.emitIns_R_R(INS_crc32_apx, EA_8BYTE, REG_R16, REG_R17);
        theEmitter.emitIns_R_A(INS_crc32_apx, EA_8BYTE, REG_R18, loadGPR);
        theEmitter.emitIns_R_S(INS_crc32_apx, EA_8BYTE, REG_R18, 0, 0);

        // Note that BZHI has a reversed src operands due to special handling at import.
        theEmitter.emitIns_R_R_R(INS_bzhi, EA_4BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_bzhi, EA_8BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_mulx, EA_4BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_mulx, EA_8BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_pdep, EA_4BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_pdep, EA_8BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_pext, EA_4BYTE, REG_R16, REG_R18, REG_R17);
        theEmitter.emitIns_R_R_R(INS_pext, EA_8BYTE, REG_R16, REG_R18, REG_R17);

        theEmitter.emitIns_R_R(INS_push2, EA_PTRSIZE, REG_R17, REG_R18, (insOpts)(INS_OPTS_EVEX_nd | INS_OPTS_APX_ppx));
        theEmitter.emitIns_R_R(INS_pop2, EA_PTRSIZE, REG_R17, REG_R18, (insOpts)(INS_OPTS_EVEX_nd | INS_OPTS_APX_ppx));
        theEmitter.emitIns_R(INS_push, EA_PTRSIZE, REG_R11, INS_OPTS_APX_ppx);
        theEmitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_R11, INS_OPTS_APX_ppx);
        theEmitter.emitIns_R(INS_push, EA_PTRSIZE, REG_R17, INS_OPTS_APX_ppx);
        theEmitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_R17, INS_OPTS_APX_ppx);

        _ = theEmitter.emitIns_Mov(INS_movd32, EA_4BYTE, REG_R16, REG_XMM0, false);
        _ = theEmitter.emitIns_Mov(INS_movd32, EA_4BYTE, REG_R16, REG_XMM16, false);

        theEmitter.emitIns_R(INS_seto_apx, EA_1BYTE, REG_R11, INS_OPTS_EVEX_zu);
        theEmitter.emitIns_I_AR(INS_imul_09, EA_4BYTE, 0x8149a, REG_EAX, 0x14);
        theEmitter.emitIns_I_AR(INS_imul_19, EA_4BYTE, 0x8149a, REG_EAX, 0x14);
        theEmitter.emitIns_I_AR(INS_imul_19, EA_4BYTE, 0x8149a, REG_R16, 0x14);
        theEmitter.emitIns_S_I(INS_imul_19, EA_4BYTE, 0, 20, 30);
        theEmitter.emitIns_S_I(INS_imul_09, EA_4BYTE, 0, 20, 30);
        theEmitter.emitIns_R_AR(INS_crc32_apx, EA_4BYTE, REG_R17, REG_EAX, 0x14);
    }

    public void genAmd64EmitterUnitTestsAvx10v2()
    {
        // All the Avx10.2 instructions are evex and evex only has one size.
        // Also, there is no specialized handling for XMM0 vs XMM9 vs XMM16

        var theEmitter = Emitter;

        genDefineTempLabel(genCreateTempLabel());

        // This test suite needs AVX10.2 enabled.
        if (!_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX10v2))
        {
            return;
        }

        // packed conversion instructions
        theEmitter.emitIns_R_R(INS_vcvttps2dqs, EA_16BYTE, REG_XMM0, REG_XMM1);   // xmm
        theEmitter.emitIns_R_R(INS_vcvttps2dqs, EA_16BYTE, REG_XMM9, REG_XMM10);  // xmm
        theEmitter.emitIns_R_R(INS_vcvttps2dqs, EA_16BYTE, REG_XMM15, REG_XMM16); // xmm
        theEmitter.emitIns_R_R(INS_vcvttps2dqs, EA_32BYTE, REG_XMM0, REG_XMM1);   // ymm
        theEmitter.emitIns_R_R(INS_vcvttps2dqs, EA_64BYTE, REG_XMM0, REG_XMM1);   // zmm

        theEmitter.emitIns_R_R(INS_vcvttps2udqs, EA_16BYTE, REG_XMM0, REG_XMM1);   // xmm
        theEmitter.emitIns_R_R(INS_vcvttps2udqs, EA_16BYTE, REG_XMM9, REG_XMM10);  // xmm
        theEmitter.emitIns_R_R(INS_vcvttps2udqs, EA_16BYTE, REG_XMM15, REG_XMM16); // xmm
        theEmitter.emitIns_R_R(INS_vcvttps2udqs, EA_32BYTE, REG_XMM0, REG_XMM1);   // ymm
        theEmitter.emitIns_R_R(INS_vcvttps2udqs, EA_64BYTE, REG_XMM0, REG_XMM1);   // zmm

        theEmitter.emitIns_R_R(INS_vcvttpd2qqs, EA_16BYTE, REG_XMM0, REG_XMM1);   // xmm
        theEmitter.emitIns_R_R(INS_vcvttpd2qqs, EA_16BYTE, REG_XMM9, REG_XMM10);  // xmm
        theEmitter.emitIns_R_R(INS_vcvttpd2qqs, EA_16BYTE, REG_XMM15, REG_XMM16); // xmm
        theEmitter.emitIns_R_R(INS_vcvttpd2qqs, EA_32BYTE, REG_XMM0, REG_XMM1);   // ymm
        theEmitter.emitIns_R_R(INS_vcvttpd2qqs, EA_64BYTE, REG_XMM0, REG_XMM1);   // zmm

        theEmitter.emitIns_R_R(INS_vcvttpd2uqqs, EA_16BYTE, REG_XMM0, REG_XMM1);   // xmm
        theEmitter.emitIns_R_R(INS_vcvttpd2uqqs, EA_16BYTE, REG_XMM9, REG_XMM10);  // xmm
        theEmitter.emitIns_R_R(INS_vcvttpd2uqqs, EA_16BYTE, REG_XMM15, REG_XMM16); // xmm
        theEmitter.emitIns_R_R(INS_vcvttpd2uqqs, EA_32BYTE, REG_XMM0, REG_XMM1);   // ymm
        theEmitter.emitIns_R_R(INS_vcvttpd2uqqs, EA_64BYTE, REG_XMM0, REG_XMM1);   // zmm

        // scalar conversion instructions
        theEmitter.emitIns_R_R(INS_vcvttsd2sis32, EA_4BYTE, REG_EAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttsd2sis64, EA_8BYTE, REG_RAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttsd2usis32, EA_4BYTE, REG_EAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttsd2usis64, EA_8BYTE, REG_RAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttss2sis32, EA_4BYTE, REG_EAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttss2sis64, EA_8BYTE, REG_RAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttss2usis32, EA_4BYTE, REG_EAX, REG_XMM0);
        theEmitter.emitIns_R_R(INS_vcvttss2usis64, EA_8BYTE, REG_RAX, REG_XMM0);

        // minmax instruction
        theEmitter.emitIns_R_R_R_I(INS_vminmaxss, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxss, EA_16BYTE, REG_XMM8, REG_XMM9, REG_XMM10, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxss, EA_16BYTE, REG_XMM14, REG_XMM15, REG_XMM16, 0);

        theEmitter.emitIns_R_R_R_I(INS_vminmaxsd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxsd, EA_16BYTE, REG_XMM9, REG_XMM10, REG_XMM11, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxsd, EA_16BYTE, REG_XMM16, REG_XMM17, REG_XMM18, 0);

        theEmitter.emitIns_R_R_R_I(INS_vminmaxps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxps, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0);
        theEmitter.emitIns_R_R_R_I(INS_vminmaxpd, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0);

        // VCVT[,T]PS2I[,U]BS
        theEmitter.emitIns_R_R(INS_vcvtps2ibs, EA_16BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvtps2ibs, EA_32BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvtps2ibs, EA_32BYTE, REG_XMM0, REG_XMM1, INS_OPTS_EVEX_er_ru);
        theEmitter.emitIns_R_R(INS_vcvtps2ibs, EA_64BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvtps2ibs, EA_64BYTE, REG_XMM0, REG_XMM1, INS_OPTS_EVEX_er_ru);

        theEmitter.emitIns_R_R(INS_vcvtps2iubs, EA_16BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvtps2iubs, EA_32BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvtps2iubs, EA_32BYTE, REG_XMM0, REG_XMM1, INS_OPTS_EVEX_er_rz);
        theEmitter.emitIns_R_R(INS_vcvtps2iubs, EA_64BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvtps2iubs, EA_64BYTE, REG_XMM0, REG_XMM1, INS_OPTS_EVEX_er_rz);

        theEmitter.emitIns_R_R(INS_vcvttps2ibs, EA_16BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvttps2ibs, EA_32BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvttps2ibs, EA_32BYTE, REG_XMM0, REG_XMM1, INS_OPTS_EVEX_er_rd);
        theEmitter.emitIns_R_R(INS_vcvttps2ibs, EA_64BYTE, REG_XMM0, REG_XMM1);

        theEmitter.emitIns_R_R(INS_vcvttps2iubs, EA_16BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvttps2iubs, EA_32BYTE, REG_XMM0, REG_XMM1);
        theEmitter.emitIns_R_R(INS_vcvttps2iubs, EA_32BYTE, REG_XMM0, REG_XMM1, INS_OPTS_EVEX_er_ru);
        theEmitter.emitIns_R_R(INS_vcvttps2iubs, EA_64BYTE, REG_XMM0, REG_XMM1);

        // VPDPW[SU,US,UU]D[,S]
        theEmitter.emitIns_R_R_R(INS_vpdpwsud, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwsud, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwsud, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwsuds, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwsuds, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwsuds, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        theEmitter.emitIns_R_R_R(INS_vpdpwusd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwusd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwusd, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwusds, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwusds, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwusds, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        theEmitter.emitIns_R_R_R(INS_vpdpwuud, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwuud, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwuud, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwuuds, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwuuds, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpwuuds, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        // VPDPB[SU,UU,SS]D[,S]
        theEmitter.emitIns_R_R_R(INS_vpdpbssd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbssd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbssd, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbssds, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbssds, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbssds, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        theEmitter.emitIns_R_R_R(INS_vpdpbsud, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbsud, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbsud, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbsuds, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbsuds, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbsuds, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        theEmitter.emitIns_R_R_R(INS_vpdpbuud, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbuud, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbuud, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbuuds, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbuuds, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
        theEmitter.emitIns_R_R_R(INS_vpdpbuuds, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

        // VMPSADBW
        theEmitter.emitIns_R_R_R_I(INS_vmpsadbw, EA_64BYTE, REG_XMM0, REG_XMM1, REG_XMM2, 0); // zmm

        // VCOMXSD
        theEmitter.emitIns_R_R(INS_vcomxsd, EA_16BYTE, REG_XMM0, REG_XMM1);

        // VCOMXSS
        theEmitter.emitIns_R_R(INS_vcomxss, EA_16BYTE, REG_XMM0, REG_XMM1);

        // VUCOMXSD
        theEmitter.emitIns_R_R(INS_vucomxsd, EA_16BYTE, REG_XMM0, REG_XMM1);

        // VUCOMXSS
        theEmitter.emitIns_R_R(INS_vucomxss, EA_16BYTE, REG_XMM0, REG_XMM1);

        // VMOVD
        theEmitter.emitIns_R_R(INS_vmovd_simd, EA_16BYTE, REG_XMM0, REG_XMM1);

        // VMOVW
        theEmitter.emitIns_R_R(INS_vmovw_simd, EA_16BYTE, REG_XMM0, REG_XMM1);
    }

    public void genAmd64EmitterUnitTestsCFCMOV()
    {
        var theEmitter = Emitter;
        genDefineTempLabel(genCreateTempLabel());

        var physReg = new GenTreePhysReg(REG_EDX) { RegNum = REG_EDX };
        var load = indirForm(TYP_INT, physReg);

        // Test all CC codes
        for (var ins = FIRST_CFCMOV_INSTRUCTION; ins <= LAST_CFCMOV_INSTRUCTION; ins++)
        {
            theEmitter.emitIns_R_R((instruction)ins, EA_8BYTE, REG_RAX, REG_RCX, INS_OPTS_NONE);
            theEmitter.emitIns_R_R((instruction)ins, EA_4BYTE, REG_RAX, REG_RCX, INS_OPTS_NONE);

            theEmitter.emitIns_R_A((instruction)ins, EA_8BYTE, REG_EAX, load, INS_OPTS_NONE);
            theEmitter.emitIns_R_A((instruction)ins, EA_4BYTE, REG_EAX, load, INS_OPTS_NONE);

            theEmitter.emitIns_R_AR((instruction)ins, EA_8BYTE, REG_EAX, REG_ECX, 4);
            theEmitter.emitIns_R_AR((instruction)ins, EA_4BYTE, REG_EAX, REG_ECX, 4);

            theEmitter.emitIns_R_ARX((instruction)ins, EA_8BYTE, REG_R16, REG_R17, REG_R18, 1, 0);
            theEmitter.emitIns_R_ARX((instruction)ins, EA_4BYTE, REG_R16, REG_R17, REG_R18, 1, 0);
            theEmitter.emitIns_R_ARX((instruction)ins, EA_8BYTE, REG_R16, REG_R17, REG_R18, 2, 4);
            theEmitter.emitIns_R_ARX((instruction)ins, EA_4BYTE, REG_R16, REG_R17, REG_R18, 2, 4);

            theEmitter.emitIns_AR_R((instruction)ins, EA_8BYTE, REG_EAX, REG_ECX, 4, INS_OPTS_EVEX_nf);
            theEmitter.emitIns_AR_R((instruction)ins, EA_4BYTE, REG_EAX, REG_ECX, 4, INS_OPTS_EVEX_nf);

            theEmitter.emitIns_ARX_R((instruction)ins, EA_8BYTE, REG_R16, REG_R17, REG_R18, 2, 4, INS_OPTS_EVEX_nf);
            theEmitter.emitIns_ARX_R((instruction)ins, EA_4BYTE, REG_R16, REG_R17, REG_R18, 2, 4, INS_OPTS_EVEX_nf);

            theEmitter.emitIns_ARX_R((instruction)ins, EA_8BYTE, REG_R16, REG_R17, REG_NA, 2, 0, INS_OPTS_EVEX_nf);
            theEmitter.emitIns_ARX_R((instruction)ins, EA_4BYTE, REG_R16, REG_R17, REG_NA, 2, 0, INS_OPTS_EVEX_nf);

            theEmitter.emitIns_R_R_R((instruction)ins, EA_8BYTE, REG_R10, REG_EAX, REG_ECX,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            theEmitter.emitIns_R_R_R((instruction)ins, EA_4BYTE, REG_R10, REG_EAX, REG_ECX,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            theEmitter.emitIns_R_R_AR((instruction)ins, EA_8BYTE, REG_R16, REG_R17, REG_R18, 2,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            theEmitter.emitIns_R_R_AR((instruction)ins, EA_4BYTE, REG_R16, REG_R17, REG_R18, 2,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            theEmitter.emitIns_R_R_A((instruction)ins, EA_8BYTE, REG_R16, REG_R17, load,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            theEmitter.emitIns_R_R_A((instruction)ins, EA_4BYTE, REG_R16, REG_R17, load,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);

            theEmitter.emitIns_R_R_S((instruction)ins, EA_8BYTE, REG_R10, REG_R16, 0, 0,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            theEmitter.emitIns_R_R_S((instruction)ins, EA_4BYTE, REG_R10, REG_R16, 0, 0,
                INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
        }

        // Test all CC codes
        for (var ins = INS_cmovo; ins <= INS_cmovg; ins++)
        {
            theEmitter.emitIns_R_R((instruction)ins, EA_8BYTE, REG_RAX, REG_RCX);
            theEmitter.emitIns_R_R((instruction)ins, EA_4BYTE, REG_RAX, REG_RCX);
            theEmitter.emitIns_R_R((instruction)ins, EA_8BYTE, REG_R10, REG_RCX);
            theEmitter.emitIns_R_R((instruction)ins, EA_4BYTE, REG_R10, REG_RCX);
            theEmitter.emitIns_R_R((instruction)ins, EA_8BYTE, REG_R16, REG_RCX);
            theEmitter.emitIns_R_R((instruction)ins, EA_4BYTE, REG_R16, REG_RCX);
            theEmitter.emitIns_R_AR((instruction)ins, EA_8BYTE, REG_RAX, REG_RCX, 2);
            theEmitter.emitIns_R_AR((instruction)ins, EA_4BYTE, REG_RAX, REG_RCX, 2);
            theEmitter.emitIns_R_AR((instruction)ins, EA_8BYTE, REG_R10, REG_RCX, 2);
            theEmitter.emitIns_R_AR((instruction)ins, EA_4BYTE, REG_R10, REG_RCX, 2);
            theEmitter.emitIns_R_AR((instruction)ins, EA_8BYTE, REG_R16, REG_RCX, 2);
            theEmitter.emitIns_R_AR((instruction)ins, EA_4BYTE, REG_R16, REG_RCX, 2);
            theEmitter.emitIns_R_S((instruction)ins, EA_8BYTE, REG_RAX, 0, 0);
            theEmitter.emitIns_R_S((instruction)ins, EA_4BYTE, REG_RAX, 0, 0);
            theEmitter.emitIns_R_S((instruction)ins, EA_8BYTE, REG_R10, 0, 0);
            theEmitter.emitIns_R_S((instruction)ins, EA_4BYTE, REG_R10, 0, 0);
            theEmitter.emitIns_R_S((instruction)ins, EA_8BYTE, REG_R16, 0, 0);
            theEmitter.emitIns_R_S((instruction)ins, EA_4BYTE, REG_R16, 0, 0);
            theEmitter.emitIns_R_R_R((instruction)ins, EA_8BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_R((instruction)ins, EA_4BYTE, REG_R10, REG_EAX, REG_ECX, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_AR((instruction)ins, EA_8BYTE, REG_R16, REG_R17, REG_R18, 2,
                INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_AR((instruction)ins, EA_4BYTE, REG_R16, REG_R17, REG_R18, 2,
                INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_A((instruction)ins, EA_8BYTE, REG_R16, REG_R17, load, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_A((instruction)ins, EA_4BYTE, REG_R16, REG_R17, load, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_S((instruction)ins, EA_8BYTE, REG_R17, REG_R10, 0, 0, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_S((instruction)ins, EA_4BYTE, REG_R17, REG_R10, 0, 0, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_S((instruction)ins, EA_8BYTE, REG_R17, REG_R16, 0, 0, INS_OPTS_EVEX_nd);
            theEmitter.emitIns_R_R_S((instruction)ins, EA_4BYTE, REG_R17, REG_R16, 0, 0, INS_OPTS_EVEX_nd);
        }
    }

    public unsafe void genAmd64EmitterUnitTestsCCMP()
    {
        var theEmitter = Emitter;
        genDefineTempLabel(genCreateTempLabel());

        // ============
        // Test RR form
        // ============

        // Test all sizes
        theEmitter.emitIns_R_R(INS_ccmpe, EA_4BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_R(INS_ccmpe, EA_8BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_R(INS_ccmpe, EA_2BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_R(INS_ccmpe, EA_1BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);

        // Test all CC codes
        for (var ins = FIRST_CCMP_INSTRUCTION; ins <= LAST_CCMP_INSTRUCTION; ins++)
        {
            theEmitter.emitIns_R_R((instruction)ins, EA_4BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        }

        // Test all dfv
        for (var i = 0; i < 16; i++)
        {
            theEmitter.emitIns_R_R(INS_ccmpe, EA_4BYTE, REG_RAX, REG_RCX, (insOpts)(i << (int)INS_OPTS_EVEX_dfv_shift));
        }

        // ============
        // Test RS form
        // ============

        // Test all sizes
        theEmitter.emitIns_R_S(INS_ccmpe, EA_4BYTE, REG_RAX, 0, 0, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_S(INS_ccmpe, EA_8BYTE, REG_RAX, 0, 0, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_S(INS_ccmpe, EA_2BYTE, REG_RAX, 0, 0, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_S(INS_ccmpe, EA_1BYTE, REG_RAX, 0, 0, INS_OPTS_EVEX_dfv_cf);

        // Test all CC codes
        for (var ins = FIRST_CCMP_INSTRUCTION; ins <= LAST_CCMP_INSTRUCTION; ins++)
        {
            theEmitter.emitIns_R_S((instruction)ins, EA_4BYTE, REG_RAX, 0, 0, INS_OPTS_EVEX_dfv_cf);
        }

        // Test all dfv
        for (var i = 0; i < 16; i++)
        {
            theEmitter.emitIns_R_S(INS_ccmpe, EA_4BYTE, REG_RAX, 0, 0, (insOpts)(i << (int)INS_OPTS_EVEX_dfv_shift));
        }

        // ============
        // Test RI form (test small and large sizes and constants)
        // ============

        theEmitter.emitIns_R_I(INS_ccmpe, EA_4BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_ccmpe, EA_4BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        theEmitter.emitIns_R_I(INS_ccmpe, EA_8BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_ccmpe, EA_8BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        theEmitter.emitIns_R_I(INS_ccmpe, EA_2BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_ccmpe, EA_2BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        theEmitter.emitIns_R_I(INS_ccmpe, EA_1BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_ccmpe, EA_1BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        // ============
        // Test RC form
        // ============

        var hnd = theEmitter.emitFltOrDblConst(1.0f, EA_4BYTE);
        theEmitter.emitIns_R_C(INS_ccmpe, EA_4BYTE, REG_RAX, hnd, 0, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_C(INS_ccmpe, EA_4BYTE, REG_RAX, hnd, 4, INS_OPTS_EVEX_dfv_cf);
    }

    public void genAmd64EmitterUnitTestsCTEST()
    {
        assert(FIRST_CTEST_INSTRUCTION - FIRST_CCMP_INSTRUCTION == 32);
        var theEmitter = Emitter;
        genDefineTempLabel(genCreateTempLabel());
        var physReg = new GenTreePhysReg(REG_EDX) { RegNum = REG_EDX };
        _ = indirForm(TYP_INT, physReg);

        // ============
        // Test RR form
        // ============

        // Test all sizes
        theEmitter.emitIns_R_R(INS_test, EA_4BYTE, REG_EAX, REG_ECX);
        theEmitter.emitIns_R_R(INS_cteste, EA_4BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_R(INS_cteste, EA_8BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_R(INS_cteste, EA_2BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_R(INS_cteste, EA_1BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);

        // Test all CC codes
        for (var ins = FIRST_CTEST_INSTRUCTION; ins <= LAST_CTEST_INSTRUCTION; ins++)
        {
            theEmitter.emitIns_R_R((instruction)ins, EA_4BYTE, REG_RAX, REG_RCX, INS_OPTS_EVEX_dfv_cf);
        }

        // Test all dfv
        for (var i = 0; i < 16; i++)
        {
            theEmitter.emitIns_R_R(INS_cteste, EA_4BYTE, REG_RAX, REG_RCX, (insOpts)(i << (int)INS_OPTS_EVEX_dfv_shift));
        }

        // ============
        // Test RI form (test small and large sizes and constants)
        // ============

        theEmitter.emitIns_R_I(INS_test, EA_8BYTE, REG_RAX, 123);
        theEmitter.emitIns_R_I(INS_cteste, EA_8BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_cteste, EA_8BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        theEmitter.emitIns_R_I(INS_test, EA_4BYTE, REG_RAX, 123);
        theEmitter.emitIns_R_I(INS_cteste, EA_4BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_cteste, EA_4BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        theEmitter.emitIns_R_I(INS_test, EA_2BYTE, REG_RAX, 123);
        theEmitter.emitIns_R_I(INS_cteste, EA_2BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_cteste, EA_2BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        theEmitter.emitIns_R_I(INS_test, EA_1BYTE, REG_RAX, 123);
        theEmitter.emitIns_R_I(INS_cteste, EA_1BYTE, REG_RAX, 123, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_R_I(INS_cteste, EA_1BYTE, REG_RAX, 270, INS_OPTS_EVEX_dfv_cf);

        // ============
        // Test MR form (test small and large sizes)
        // ============

        theEmitter.emitIns_AR_R(INS_cteste, EA_1BYTE, REG_EAX, REG_ECX, 4, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_AR_R(INS_cteste, EA_2BYTE, REG_EAX, REG_ECX, 4, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_AR_R(INS_cteste, EA_4BYTE, REG_EAX, REG_ECX, 4, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_AR_R(INS_cteste, EA_8BYTE, REG_EAX, REG_ECX, 4, INS_OPTS_EVEX_dfv_cf);

        // ============
        // Test MI form
        // ============

        theEmitter.emitIns_I_AR(INS_cteste, EA_1BYTE, 123, REG_R18, 2, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_I_AR(INS_cteste, EA_2BYTE, 123, REG_R18, 2, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_I_AR(INS_cteste, EA_4BYTE, 123, REG_R18, 2, INS_OPTS_EVEX_dfv_cf);
        theEmitter.emitIns_I_AR(INS_cteste, EA_8BYTE, 123, REG_R18, 2, INS_OPTS_EVEX_dfv_cf);
    }
}
#endif
