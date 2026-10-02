// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Load(var_types srcType, bool aligned = false)
    {
#if TARGET_WASM
        switch (srcType)
        {
            case TYP_REF:
            case TYP_BYREF:
            {
                return ins_Load(TYP_I_IMPL, aligned);
            }

            case TYP_BYTE:
            {
                return INS_i32_load8_s;
            }

            case TYP_UBYTE:
            {
                return INS_i32_load8_u;
            }

            case TYP_SHORT:
            {
                return INS_i32_load16_s;
            }

            case TYP_USHORT:
            {
                return INS_i32_load16_u;
            }

            case TYP_INT:
            {
                return INS_i32_load;
            }

            case TYP_LONG:
            {
                return INS_i64_load;
            }

            case TYP_FLOAT:
            {
                return INS_f32_load;
            }

            case TYP_DOUBLE:
            {
                return INS_f64_load;
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            {
                return INS_v128_load64_zero;
            }

            case TYP_SIMD16:
            {
                return INS_v128_load;
            }
#endif
            default:
            {
                unreached();
                break;
            }
        }
#endif

        if (varTypeUsesIntReg(srcType))
        {
#if TARGET_XARCH
            if (!varTypeIsSmall(srcType))
            {
                return INS_mov;
            }
            else if (varTypeIsUnsigned(srcType))
            {
                return INS_movzx;
            }
            else
            {
                return INS_movsx;
            }
#elif TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
            var ins = INS_invalid;
#if TARGET_ARMARCH
            if (!varTypeIsSmall(srcType))
            {
                ins = INS_ldr;
            }
            else if (varTypeIsByte(srcType))
            {
                ins = varTypeIsUnsigned(srcType) ? INS_ldrb : INS_ldrsb;
            }
            else if (varTypeIsShort(srcType))
            {
                ins = varTypeIsUnsigned(srcType) ? INS_ldrh : INS_ldrsh;
            }
#elif TARGET_LOONGARCH64
            if (varTypeIsByte(srcType))
            {
                ins = varTypeIsUnsigned(srcType) ? INS_ld_bu : INS_ld_b;
            }
            else if (varTypeIsShort(srcType))
            {
                ins = varTypeIsUnsigned(srcType) ? INS_ld_hu : INS_ld_h;
            }
            else if (srcType == TYP_INT)
            {
                ins = INS_ld_w;
            }
            else
            {
                ins = INS_ld_d;
            }
#elif TARGET_RISCV64
            if (varTypeIsByte(srcType))
            {
                ins = varTypeIsUnsigned(srcType) ? INS_lbu : INS_lb;
            }
            else if (varTypeIsShort(srcType))
            {
                ins = varTypeIsUnsigned(srcType) ? INS_lhu : INS_lh;
            }
            else if (srcType == TYP_INT)
            {
                ins = INS_lw;
            }
            else
            {
                ins = INS_ld;
            }
#endif
            assert(ins != INS_invalid);
            return ins;
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Integer load selection requires a supported target.");
#endif
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(srcType))
        {
#if TARGET_XARCH
            return INS_kmovq_msk;
#elif TARGET_ARM64
            return INS_sve_ldr;
#endif
        }
#endif

        assert(varTypeUsesFloatReg(srcType));

#if TARGET_XARCH
        var srcSize = srcType.Size;

        if (srcSize == 4)
        {
            return INS_movss;
        }
        else if (srcSize == 8)
        {
            return INS_movsd_simd;
        }
        else
        {
            assert(srcSize is 12 or 16 or 32 or 64);
            // Prefer movaps/movups over movapd/movupd: they avoid a 66h prefix.
            return aligned ? INS_movaps : INS_movups;
        }
#elif TARGET_ARM64
        return INS_ldr;
#elif TARGET_ARM
#if FEATURE_SIMD
        assert(!varTypeIsSIMD(srcType));
#endif
        return INS_vldr;
#elif TARGET_LOONGARCH64
        assert(!varTypeIsSIMD(srcType));
        if (srcType == TYP_DOUBLE)
        {
            return INS_fld_d;
        }
        else
        {
            assert(srcType == TYP_FLOAT);
            return INS_fld_s;
        }
#elif TARGET_RISCV64
        assert(!varTypeIsSIMD(srcType));
        if (srcType == TYP_DOUBLE)
        {
            return INS_fld;
        }
        else
        {
            assert(srcType == TYP_FLOAT);
            return INS_flw;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-point load selection requires a supported target.");
#endif
    }
}
