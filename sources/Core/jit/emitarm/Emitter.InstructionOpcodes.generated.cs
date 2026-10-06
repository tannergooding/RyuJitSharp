// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    private static ReadOnlySpan<uint> ordinaryInsCodes1 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x4400)), // INS_add
        unchecked((uint)(BAD_CODE)), // INS_sub
        unchecked((uint)(0x5800)), // INS_ldr
        unchecked((uint)(0x5000)), // INS_str
        unchecked((uint)(0x5C00)), // INS_ldrb
        unchecked((uint)(0x5400)), // INS_strb
        unchecked((uint)(0x5A00)), // INS_ldrh
        unchecked((uint)(0x5200)), // INS_strh
        unchecked((uint)(0x5600)), // INS_ldrsb
        unchecked((uint)(0x5E00)), // INS_ldrsh
        unchecked((uint)(0x0000)), // INS_mov
        unchecked((uint)(0x4280)), // INS_cmp
        unchecked((uint)(0x4080)), // INS_lsl
        unchecked((uint)(0x40C0)), // INS_lsr
        unchecked((uint)(0x4100)), // INS_asr
        unchecked((uint)(0x41C0)), // INS_ror
        unchecked((uint)(0xF890F000)), // INS_pld
        unchecked((uint)(0xF8B0F000)), // INS_pldw
#if FEATURE_PLI_INSTRUCTION
        unchecked((uint)(0xF990F000)), // INS_pli
#endif
        unchecked((uint)(0xF2C00000)), // INS_movt
        unchecked((uint)(0xF2400000)), // INS_movw
        unchecked((uint)(0x4000)), // INS_and
        unchecked((uint)(0x4040)), // INS_eor
        unchecked((uint)(0x4300)), // INS_orr
        unchecked((uint)(BAD_CODE)), // INS_orn
        unchecked((uint)(0x4380)), // INS_bic
        unchecked((uint)(0x4140)), // INS_adc
        unchecked((uint)(0x4180)), // INS_sbc
        unchecked((uint)(0x4240)), // INS_rsb
        unchecked((uint)(0x4200)), // INS_tst
        unchecked((uint)(BAD_CODE)), // INS_teq
        unchecked((uint)(0x42C0)), // INS_cmn
        unchecked((uint)(0x43C0)), // INS_mvn
        unchecked((uint)(0xB400)), // INS_push
        unchecked((uint)(0xBC00)), // INS_pop
        unchecked((uint)(0xE000)), // INS_b
        unchecked((uint)(0xD000)), // INS_beq
        unchecked((uint)(0xD100)), // INS_bne
        unchecked((uint)(0xD200)), // INS_bhs
        unchecked((uint)(0xD300)), // INS_blo
        unchecked((uint)(0xD400)), // INS_bmi
        unchecked((uint)(0xD500)), // INS_bpl
        unchecked((uint)(0xD600)), // INS_bvs
        unchecked((uint)(0xD700)), // INS_bvc
        unchecked((uint)(0xD800)), // INS_bhi
        unchecked((uint)(0xD900)), // INS_bls
        unchecked((uint)(0xDA00)), // INS_bge
        unchecked((uint)(0xDB00)), // INS_blt
        unchecked((uint)(0xDC00)), // INS_bgt
        unchecked((uint)(0xDD00)), // INS_ble
        unchecked((uint)(0x4700)), // INS_bx
        unchecked((uint)(0x4780)), // INS_blx
        unchecked((uint)(0xC800)), // INS_ldm
        unchecked((uint)(0xC000)), // INS_stm
        unchecked((uint)(0xB240)), // INS_sxtb
        unchecked((uint)(0xB200)), // INS_sxth
        unchecked((uint)(0xB2C0)), // INS_uxtb
        unchecked((uint)(0xB280)), // INS_uxth
        unchecked((uint)(0x4340)), // INS_mul
        unchecked((uint)(0xA000)), // INS_adr
        unchecked((uint)(0xF2000000)), // INS_addw
        unchecked((uint)(0xF36F0000)), // INS_bfc
        unchecked((uint)(0xF3600000)), // INS_bfi
        unchecked((uint)(0xF000D000)), // INS_bl
        unchecked((uint)(0xDEFE)), // INS_bkpt
        unchecked((uint)(0xB900)), // INS_cbnz
        unchecked((uint)(0xB100)), // INS_cbz
        unchecked((uint)(0xFAB0F000)), // INS_clz
        unchecked((uint)(0xF3BF8F50)), // INS_dmb
        unchecked((uint)(0xF3BF8F60)), // INS_ism
        unchecked((uint)(0xE9100000)), // INS_ldmdb
        unchecked((uint)(0xE8500000)), // INS_ldrd
        unchecked((uint)(0xE8500F00)), // INS_ldrex
        unchecked((uint)(0xE8D00F4F)), // INS_ldrexb
        unchecked((uint)(0xE8D0007F)), // INS_ldrexd
        unchecked((uint)(0xE8D00F5F)), // INS_ldrexh
        unchecked((uint)(0xFB000000)), // INS_mla
        unchecked((uint)(0xFB000010)), // INS_mls
        unchecked((uint)(0xBF00)), // INS_nop
        unchecked((uint)(0xF3AF8000)), // INS_nopw
        unchecked((uint)(0xF3400000)), // INS_sbfx
        unchecked((uint)(0xFB90F0F0)), // INS_sdiv
        unchecked((uint)(0xF3000000)), // INS_ssat
        unchecked((uint)(0xFBC00000)), // INS_smlal
        unchecked((uint)(0xFB800000)), // INS_smull
        unchecked((uint)(0xE9000000)), // INS_stmdb
        unchecked((uint)(0xE8400000)), // INS_strd
        unchecked((uint)(0xE8400F00)), // INS_strex
        unchecked((uint)(0xE8C00F4F)), // INS_strexb
        unchecked((uint)(0xE8C0007F)), // INS_strexd
        unchecked((uint)(0xE8C00F5F)), // INS_strexh
        unchecked((uint)(0xF2A00000)), // INS_subw
        unchecked((uint)(0xE8D0F000)), // INS_tbb
        unchecked((uint)(0xE8D0F010)), // INS_tbh
        unchecked((uint)(0xF3C00000)), // INS_ubfx
        unchecked((uint)(0xFBB0F0F0)), // INS_udiv
        unchecked((uint)(0xFBE00000)), // INS_umlal
        unchecked((uint)(0xFBA00000)), // INS_umull
        unchecked((uint)(0xF3800000)), // INS_usat
#if FEATURE_ITINSTRUCTION
        unchecked((uint)(0xBF08)), // INS_it
        unchecked((uint)(0xBF04)), // INS_itt
        unchecked((uint)(0xBF0C)), // INS_ite
        unchecked((uint)(0xBF02)), // INS_ittt
        unchecked((uint)(0xBF06)), // INS_itte
        unchecked((uint)(0xBF0A)), // INS_itet
        unchecked((uint)(0xBF0E)), // INS_itee
        unchecked((uint)(0xBF01)), // INS_itttt
        unchecked((uint)(0xBF01)), // INS_ittte
        unchecked((uint)(0xBF01)), // INS_ittet
        unchecked((uint)(0xBF01)), // INS_ittee
        unchecked((uint)(0xBF01)), // INS_itett
        unchecked((uint)(0xBF01)), // INS_itete
        unchecked((uint)(0xBF01)), // INS_iteet
        unchecked((uint)(0xBF01)), // INS_iteee
#endif
        unchecked((uint)(0xED000A00)), // INS_vstr
        unchecked((uint)(0xED100A00)), // INS_vldr
        unchecked((uint)(0xEC800A00)), // INS_vstm
        unchecked((uint)(0xEC900A00)), // INS_vldm
        unchecked((uint)(0xED2D0A00)), // INS_vpush
        unchecked((uint)(0xECBD0A00)), // INS_vpop
        unchecked((uint)(0xEEF10A10)), // INS_vmrs
        unchecked((uint)(0xEE300A00)), // INS_vadd
        unchecked((uint)(0xEE300A40)), // INS_vsub
        unchecked((uint)(0xEE200A00)), // INS_vmul
        unchecked((uint)(0xEE800A00)), // INS_vdiv
        unchecked((uint)(0xEEB00A40)), // INS_vmov
        unchecked((uint)(0xEEB00AC0)), // INS_vabs
        unchecked((uint)(0xEEB10AC0)), // INS_vsqrt
        unchecked((uint)(0xEEB10A40)), // INS_vneg
        unchecked((uint)(0xEEB40A40)), // INS_vcmp
        unchecked((uint)(0xEEB50A40)), // INS_vcmp0
        unchecked((uint)(0xEEBD0BC0)), // INS_vcvt_d2i
        unchecked((uint)(0xEEBD0AC0)), // INS_vcvt_f2i
        unchecked((uint)(0xEEBC0BC0)), // INS_vcvt_d2u
        unchecked((uint)(0xEEBC0AC0)), // INS_vcvt_f2u
        unchecked((uint)(0xEEB80AC0)), // INS_vcvt_i2f
        unchecked((uint)(0xEEB80BC0)), // INS_vcvt_i2d
        unchecked((uint)(0xEEB80A40)), // INS_vcvt_u2f
        unchecked((uint)(0xEEB80B40)), // INS_vcvt_u2d
        unchecked((uint)(0xEEB70BC0)), // INS_vcvt_d2f
        unchecked((uint)(0xEEB70AC0)), // INS_vcvt_f2d
        unchecked((uint)(0xEC400B10)), // INS_vmov_i2d
        unchecked((uint)(0xEC500B10)), // INS_vmov_d2i
        unchecked((uint)(0xEE000A10)), // INS_vmov_i2f
        unchecked((uint)(0xEE100A10)), // INS_vmov_f2i
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes2 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x1800)), // INS_add
        unchecked((uint)(0x1A00)), // INS_sub
        unchecked((uint)(0x6800)), // INS_ldr
        unchecked((uint)(0x6000)), // INS_str
        unchecked((uint)(0x7800)), // INS_ldrb
        unchecked((uint)(0x7000)), // INS_strb
        unchecked((uint)(0x8800)), // INS_ldrh
        unchecked((uint)(0x8000)), // INS_strh
        unchecked((uint)(BAD_CODE)), // INS_ldrsb
        unchecked((uint)(BAD_CODE)), // INS_ldrsh
        unchecked((uint)(0x4600)), // INS_mov
        unchecked((uint)(0x4500)), // INS_cmp
        unchecked((uint)(0x0000)), // INS_lsl
        unchecked((uint)(0x0800)), // INS_lsr
        unchecked((uint)(0x1000)), // INS_asr
        unchecked((uint)(BAD_CODE)), // INS_ror
        unchecked((uint)(0xF810FC00)), // INS_pld
        unchecked((uint)(0xF830FC00)), // INS_pldw
#if FEATURE_PLI_INSTRUCTION
        unchecked((uint)(0xF910FC00)), // INS_pli
#endif
        unchecked((uint)(0xF2C00000)), // INS_movt
        unchecked((uint)(0xF2400000)), // INS_movw
        unchecked((uint)(0xEA000000)), // INS_and
        unchecked((uint)(0xEA800000)), // INS_eor
        unchecked((uint)(0xEA400000)), // INS_orr
        unchecked((uint)(0xEA600000)), // INS_orn
        unchecked((uint)(0xEA200000)), // INS_bic
        unchecked((uint)(0xEB400000)), // INS_adc
        unchecked((uint)(0xEB600000)), // INS_sbc
        unchecked((uint)(0xEBC00000)), // INS_rsb
        unchecked((uint)(0xEA100F00)), // INS_tst
        unchecked((uint)(0xEA900F00)), // INS_teq
        unchecked((uint)(0xEB100F00)), // INS_cmn
        unchecked((uint)(0xEA6F0000)), // INS_mvn
        unchecked((uint)(0xF84D0D04)), // INS_push
        unchecked((uint)(0xF85D0B04)), // INS_pop
        unchecked((uint)(0xF0009000)), // INS_b
        unchecked((uint)(0xF0008000)), // INS_beq
        unchecked((uint)(0xF0408000)), // INS_bne
        unchecked((uint)(0xF0808000)), // INS_bhs
        unchecked((uint)(0xF0C08000)), // INS_blo
        unchecked((uint)(0xF1008000)), // INS_bmi
        unchecked((uint)(0xF1408000)), // INS_bpl
        unchecked((uint)(0xF1808000)), // INS_bvs
        unchecked((uint)(0xF1C08000)), // INS_bvc
        unchecked((uint)(0xF2008000)), // INS_bhi
        unchecked((uint)(0xF2408000)), // INS_bls
        unchecked((uint)(0xF2808000)), // INS_bge
        unchecked((uint)(0xF2C08000)), // INS_blt
        unchecked((uint)(0xF3008000)), // INS_bgt
        unchecked((uint)(0xF3408000)), // INS_ble
        unchecked((uint)(0x4700)), // INS_bx
        unchecked((uint)(0xF000C000)), // INS_blx
        unchecked((uint)(0xE8900000)), // INS_ldm
        unchecked((uint)(0xE8800000)), // INS_stm
        unchecked((uint)(0xFA4FF080)), // INS_sxtb
        unchecked((uint)(0xFA0FF080)), // INS_sxth
        unchecked((uint)(0xFA5FF080)), // INS_uxtb
        unchecked((uint)(0xFA1FF080)), // INS_uxth
        unchecked((uint)(0xFB00F000)), // INS_mul
        unchecked((uint)(0xF20F0000)), // INS_adr
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes3 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x3000)), // INS_add
        unchecked((uint)(0x3800)), // INS_sub
        unchecked((uint)(0xF8500000)), // INS_ldr
        unchecked((uint)(0xF8400000)), // INS_str
        unchecked((uint)(0xF8100000)), // INS_ldrb
        unchecked((uint)(0xF8000000)), // INS_strb
        unchecked((uint)(0xF8300000)), // INS_ldrh
        unchecked((uint)(0xF8200000)), // INS_strh
        unchecked((uint)(0xF9100000)), // INS_ldrsb
        unchecked((uint)(0xF9300000)), // INS_ldrsh
        unchecked((uint)(0x2000)), // INS_mov
        unchecked((uint)(0x2800)), // INS_cmp
        unchecked((uint)(0xFA00F000)), // INS_lsl
        unchecked((uint)(0xFA20F000)), // INS_lsr
        unchecked((uint)(0xFA40F000)), // INS_asr
        unchecked((uint)(0xFA60F000)), // INS_ror
        unchecked((uint)(0xF810F000)), // INS_pld
        unchecked((uint)(0xF830F000)), // INS_pldw
#if FEATURE_PLI_INSTRUCTION
        unchecked((uint)(0xF910F000)), // INS_pli
#endif
        unchecked((uint)(0xF2C00000)), // INS_movt
        unchecked((uint)(0xF2400000)), // INS_movw
        unchecked((uint)(0xF0000000)), // INS_and
        unchecked((uint)(0xF0800000)), // INS_eor
        unchecked((uint)(0xF0400000)), // INS_orr
        unchecked((uint)(0xF0600000)), // INS_orn
        unchecked((uint)(0xF0200000)), // INS_bic
        unchecked((uint)(0xF1400000)), // INS_adc
        unchecked((uint)(0xF1600000)), // INS_sbc
        unchecked((uint)(0xF1C00000)), // INS_rsb
        unchecked((uint)(0xF0100F00)), // INS_tst
        unchecked((uint)(0xF0900F00)), // INS_teq
        unchecked((uint)(0xF1100F00)), // INS_cmn
        unchecked((uint)(0xF06F0000)), // INS_mvn
        unchecked((uint)(0xE92D0000)), // INS_push
        unchecked((uint)(0xE8BD0000)), // INS_pop
        unchecked((uint)(0xF0009000)), // INS_b
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes4 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x1C00)), // INS_add
        unchecked((uint)(0x1E00)), // INS_sub
        unchecked((uint)(0xF8500800)), // INS_ldr
        unchecked((uint)(0xF8400800)), // INS_str
        unchecked((uint)(0xF8100800)), // INS_ldrb
        unchecked((uint)(0xF8000800)), // INS_strb
        unchecked((uint)(0xF8300800)), // INS_ldrh
        unchecked((uint)(0xF8200800)), // INS_strh
        unchecked((uint)(0xF9100800)), // INS_ldrsb
        unchecked((uint)(0xF9300800)), // INS_ldrsh
        unchecked((uint)(0xF04F0000)), // INS_mov
        unchecked((uint)(0xF1B00F00)), // INS_cmp
        unchecked((uint)(0xEA4F0000)), // INS_lsl
        unchecked((uint)(0xEA4F0010)), // INS_lsr
        unchecked((uint)(0xEA4F0020)), // INS_asr
        unchecked((uint)(0xEA4F0030)), // INS_ror
        unchecked((uint)(0xF81FF000)), // INS_pld
        unchecked((uint)(BAD_CODE)), // INS_pldw
#if FEATURE_PLI_INSTRUCTION
        unchecked((uint)(0xF91FF000)), // INS_pli
#endif
        unchecked((uint)(0xF2C00000)), // INS_movt
        unchecked((uint)(0xF2400000)), // INS_movw
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes5 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0xF1000000)), // INS_add
        unchecked((uint)(0xF1A00000)), // INS_sub
        unchecked((uint)(0xF8D00000)), // INS_ldr
        unchecked((uint)(0xF8C00000)), // INS_str
        unchecked((uint)(0xF8900000)), // INS_ldrb
        unchecked((uint)(0xF8800000)), // INS_strb
        unchecked((uint)(0xF8B00000)), // INS_ldrh
        unchecked((uint)(0xF8a00000)), // INS_strh
        unchecked((uint)(0xF9900000)), // INS_ldrsb
        unchecked((uint)(0xF9B00000)), // INS_ldrsh
        unchecked((uint)(0xEA5F0000)), // INS_mov
        unchecked((uint)(0xEBB00F00)), // INS_cmp
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes6 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0xEB000000)), // INS_add
        unchecked((uint)(0xEBA00000)), // INS_sub
        unchecked((uint)(0xF85F0000)), // INS_ldr
        unchecked((uint)(0x9000)), // INS_str
        unchecked((uint)(0xF81F0000)), // INS_ldrb
        unchecked((uint)(BAD_CODE)), // INS_strb
        unchecked((uint)(0xF83F0000)), // INS_ldrh
        unchecked((uint)(BAD_CODE)), // INS_strh
        unchecked((uint)(0xF91F0000)), // INS_ldrsb
        unchecked((uint)(0xF93F0000)), // INS_ldrsh
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes7 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0xB000)), // INS_add
        unchecked((uint)(0xB080)), // INS_sub
        unchecked((uint)(0x9800)), // INS_ldr
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes8 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0xA800)), // INS_add
        unchecked((uint)(BAD_CODE)), // INS_sub
        unchecked((uint)(0x4800)), // INS_ldr
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
    ];

    private static ReadOnlySpan<uint> ordinaryInsCodes9 => [
#if !TARGET_ARM
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0xA000)), // INS_add
        unchecked((uint)(BAD_CODE)), // INS_sub
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
    ];

#endif
}