// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    private static ReadOnlySpan<insFormat> s_instructionFormats => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_X86
#endif
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
        IF_NONE, // invalid
        IF_EN9, // add
        IF_EN9, // sub
        IF_EN8, // ldr
        IF_EN6B, // str
        IF_EN6A, // ldrb
        IF_EN6B, // strb
        IF_EN6A, // ldrh
        IF_EN6B, // strh
        IF_EN6A, // ldrsb
        IF_EN6A, // ldrsh
        IF_EN5A, // mov
        IF_EN5B, // cmp
        IF_EN4A, // lsl
        IF_EN4A, // lsr
        IF_EN4A, // asr
        IF_EN4A, // ror
        IF_EN4B, // pld
        IF_EN4B, // pldw
#if FEATURE_PLI_INSTRUCTION
        IF_EN4B, // pli
#endif
        IF_EN4C, // movt
        IF_EN4C, // movw
        IF_EN3A, // and
        IF_EN3A, // eor
        IF_EN3A, // orr
        IF_EN3A, // orn
        IF_EN3A, // bic
        IF_EN3A, // adc
        IF_EN3A, // sbc
        IF_EN3A, // rsb
        IF_EN3B, // tst
        IF_EN3B, // teq
        IF_EN3B, // cmn
        IF_EN3C, // mvn
        IF_EN3D, // push
        IF_EN3D, // pop
        IF_EN3E, // b
        IF_EN2A, // beq
        IF_EN2A, // bne
        IF_EN2A, // bhs
        IF_EN2A, // blo
        IF_EN2A, // bmi
        IF_EN2A, // bpl
        IF_EN2A, // bvs
        IF_EN2A, // bvc
        IF_EN2A, // bhi
        IF_EN2A, // bls
        IF_EN2A, // bge
        IF_EN2A, // blt
        IF_EN2A, // bgt
        IF_EN2A, // ble
        IF_EN2B, // bx
        IF_EN2C, // blx
        IF_EN2D, // ldm
        IF_EN2D, // stm
        IF_EN2E, // sxtb
        IF_EN2E, // sxth
        IF_EN2E, // uxtb
        IF_EN2E, // uxth
        IF_EN2F, // mul
        IF_EN2G, // adr
        IF_T2_M0, // addw
        IF_T2_D1, // bfc
        IF_T2_D0, // bfi
        IF_T2_J3, // bl
        IF_T1_A, // bkpt
        IF_T1_I, // cbnz
        IF_T1_I, // cbz
        IF_T2_C10, // clz
        IF_T2_B, // dmb
        IF_T2_B, // ism
        IF_T2_I0, // ldmdb
        IF_T2_G0, // ldrd
        IF_T2_H1, // ldrex
        IF_T2_E1, // ldrexb
        IF_T2_G1, // ldrexd
        IF_T2_E1, // ldrexh
        IF_T2_F2, // mla
        IF_T2_F2, // mls
        IF_T1_A, // nop
        IF_T2_A, // nopw
        IF_T2_D0, // sbfx
        IF_T2_C5, // sdiv
        IF_T2_D0, // ssat
        IF_T2_F1, // smlal
        IF_T2_F1, // smull
        IF_EN2D, // stmdb
        IF_T2_G0, // strd
        IF_T2_H1, // strex
        IF_T2_E1, // strexb
        IF_T2_G1, // strexd
        IF_T2_E1, // strexh
        IF_T2_M0, // subw
        IF_T2_C9, // tbb
        IF_T2_C9, // tbh
        IF_T2_D0, // ubfx
        IF_T2_C5, // udiv
        IF_T2_F1, // umlal
        IF_T2_F1, // umull
        IF_T2_D0, // usat
#if FEATURE_ITINSTRUCTION
        IF_T1_B, // it
        IF_T1_B, // itt
        IF_T1_B, // ite
        IF_T1_B, // ittt
        IF_T1_B, // itte
        IF_T1_B, // itet
        IF_T1_B, // itee
        IF_T1_B, // itttt
        IF_T1_B, // ittte
        IF_T1_B, // ittet
        IF_T1_B, // ittee
        IF_T1_B, // itett
        IF_T1_B, // itete
        IF_T1_B, // iteet
        IF_T1_B, // iteee
#endif
        IF_T2_VLDST, // vstr
        IF_T2_VLDST, // vldr
        IF_T2_VLDST, // vstm
        IF_T2_VLDST, // vldm
        IF_T2_VLDST, // vpush
        IF_T2_VLDST, // vpop
        IF_T2_E2, // vmrs
        IF_T2_VFP3, // vadd
        IF_T2_VFP3, // vsub
        IF_T2_VFP3, // vmul
        IF_T2_VFP3, // vdiv
        IF_T2_VFP2, // vmov
        IF_T2_VFP2, // vabs
        IF_T2_VFP2, // vsqrt
        IF_T2_VFP2, // vneg
        IF_T2_VFP2, // vcmp
        IF_T2_VFP2, // vcmp0
        IF_T2_VFP2, // vcvt_d2i
        IF_T2_VFP2, // vcvt_f2i
        IF_T2_VFP2, // vcvt_d2u
        IF_T2_VFP2, // vcvt_f2u
        IF_T2_VFP2, // vcvt_i2f
        IF_T2_VFP2, // vcvt_i2d
        IF_T2_VFP2, // vcvt_u2f
        IF_T2_VFP2, // vcvt_u2d
        IF_T2_VFP2, // vcvt_d2f
        IF_T2_VFP2, // vcvt_f2d
        IF_T2_VMOVD, // vmov_i2d
        IF_T2_VMOVD, // vmov_d2i
        IF_T2_VMOVS, // vmov_i2f
        IF_T2_VMOVS, // vmov_f2i
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif

    ];
#endif
}