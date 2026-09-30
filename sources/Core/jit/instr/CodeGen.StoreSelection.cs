// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_TT_RV(instruction ins, emitAttr size, GenTree tree, regNumber reg)
    {
#if DEBUG
        assert(reg != REG_STK);
        var isValidInReg = (tree.Flags & GTF_SPILLED) == 0;

        if (!isValidInReg && ((tree.Flags & GTF_SPILL) != 0) && (tree.Oper == GT_STORE_LCL_VAR))
        {
            isValidInReg = true;
        }

        assert(isValidInReg);
        assert(size != EA_UNKNOWN);
        assert(tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR);
#endif
        var varNum = tree.AsLclVarCommon().LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);
#if DEBUG && CPU_LOAD_STORE_ARCH
#if TARGET_ARM64
        // Workaround until https://github.com/dotnet/runtime/issues/105512 is fixed.
        assert(Emitter.emitInsIsStore(ins) || ins == INS_sve_str);
#else
        assert(Emitter.emitInsIsStore(ins));
#endif
#endif
        Emitter.emitIns_S_R(ins, size, reg, varNum, 0);
    }

    public instruction ins_Store(var_types dstType, bool aligned = false)
    {
#if TARGET_WASM
        switch (dstType)
        {
            case TYP_REF:
            case TYP_BYREF:
            {
                return ins_Store(TYP_I_IMPL, aligned);
            }

            case TYP_BYTE:
            case TYP_UBYTE:
            {
                return INS_i32_store8;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                return INS_i32_store16;
            }

            case TYP_INT:
            {
                return INS_i32_store;
            }

            case TYP_LONG:
            {
                return INS_i64_store;
            }

            case TYP_FLOAT:
            {
                return INS_f32_store;
            }

            case TYP_DOUBLE:
            {
                return INS_f64_store;
            }

#if FEATURE_SIMD
            case TYP_SIMD16:
            {
                return INS_v128_store;
            }
#endif
            default:
            {
                unreached();
                break;
            }
        }
#endif

        if (varTypeUsesIntReg(dstType))
        {
#if TARGET_XARCH
            return INS_mov;
#elif TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
            var ins = INS_invalid;
#if TARGET_ARMARCH
            if (!varTypeIsSmall(dstType))
            {
                ins = INS_str;
            }
            else if (varTypeIsByte(dstType))
            {
                ins = INS_strb;
            }
            else if (varTypeIsShort(dstType))
            {
                ins = INS_strh;
            }
#elif TARGET_LOONGARCH64
            if (varTypeIsByte(dstType))
            {
                ins = aligned ? INS_stx_b : INS_st_b;
            }
            else if (varTypeIsShort(dstType))
            {
                ins = aligned ? INS_stx_h : INS_st_h;
            }
            else if (dstType == TYP_INT)
            {
                ins = aligned ? INS_stx_w : INS_st_w;
            }
            else
            {
                ins = aligned ? INS_stx_d : INS_st_d;
            }
#elif TARGET_RISCV64
            if (varTypeIsByte(dstType))
            {
                ins = INS_sb;
            }
            else if (varTypeIsShort(dstType))
            {
                ins = INS_sh;
            }
            else if (dstType == TYP_INT)
            {
                ins = INS_sw;
            }
            else
            {
                ins = INS_sd;
            }
#endif
            assert(ins != INS_invalid);
            return ins;
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Integer store selection requires a supported target.");
#endif
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
#if TARGET_XARCH
            return INS_kmovq_msk;
#elif TARGET_ARM64
            return INS_sve_str;
#endif
        }
#endif

        assert(varTypeUsesFloatReg(dstType));

#if TARGET_XARCH
        var dstSize = dstType.Size;

        if (dstSize == 4)
        {
            return INS_movss;
        }
        else if (dstSize == 8)
        {
            return INS_movsd_simd;
        }
        else
        {
            assert(dstSize is 12 or 16 or 32 or 64);
            // Prefer movaps/movups over movapd/movupd: they avoid a 66h prefix.
            return aligned ? INS_movaps : INS_movups;
        }
#elif TARGET_ARM64
        return INS_str;
#elif TARGET_ARM
        assert(!varTypeIsSIMD(dstType));
        return INS_vstr;
#elif TARGET_LOONGARCH64
        assert(!varTypeIsSIMD(dstType));
        if (dstType == TYP_DOUBLE)
        {
            return aligned ? INS_fstx_d : INS_fst_d;
        }
        else
        {
            assert(dstType == TYP_FLOAT);
            return aligned ? INS_fstx_s : INS_fst_s;
        }
#elif TARGET_RISCV64
        assert(!varTypeIsSIMD(dstType));
        if (dstType == TYP_DOUBLE)
        {
            return INS_fsd;
        }
        else
        {
            assert(dstType == TYP_FLOAT);
            return INS_fsw;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-point store selection requires a supported target.");
#endif
    }
}
