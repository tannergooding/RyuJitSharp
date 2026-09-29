// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Copy(var_types dstType)
    {
        assert(dstType.EmitActualSize != 0);

        if (varTypeUsesIntReg(dstType))
        {
#if TARGET_XARCH || TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
            return INS_mov;
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Integer copy instruction selection is not implemented on this target.");
#endif
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
#if TARGET_XARCH
            return INS_kmovq_msk;
#elif TARGET_ARM64
            return INS_sve_mov;
#endif
        }
#endif

        assert(varTypeUsesFloatReg(dstType));

#if TARGET_XARCH
        return INS_movaps;
#elif TARGET_ARM64
        if (varTypeIsSimd(dstType))
        {
            return INS_mov;
        }
        else
        {
            assert(varTypeIsFloating(dstType));
            return INS_fmov;
        }
#elif TARGET_ARM
        assert(!varTypeIsSimd(dstType));
        return INS_vmov;
#elif TARGET_LOONGARCH64
        assert(!varTypeIsSimd(dstType));

        if (dstType == TYP_DOUBLE)
        {
            return INS_fmov_d;
        }
        else
        {
            assert(dstType == TYP_FLOAT);
            return INS_fmov_s;
        }
#elif TARGET_RISCV64
        assert(!varTypeIsSimd(dstType));

        if (dstType == TYP_DOUBLE)
        {
            return INS_fsgnj_d;
        }
        else
        {
            assert(dstType == TYP_FLOAT);
            return INS_fsgnj_s;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-point copy instruction selection is not implemented on this target.");
#endif
    }

    public instruction ins_Copy(regNumber srcReg, var_types dstType)
    {
        assert(srcReg != REG_NA);

        if (varTypeUsesIntReg(dstType))
        {
            if (genIsValidIntOrFakeReg(srcReg))
            {
                return ins_Copy(dstType);
            }

#if FEATURE_MASKED_HW_INTRINSICS && TARGET_XARCH
            if (genIsValidMaskReg(srcReg))
            {
                return INS_kmovq_gpr;
            }
#endif

            assert(genIsValidFloatReg(srcReg));

#if TARGET_AMD64
            return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movd32 : INS_movd64;
#elif TARGET_X86
            return INS_movd32;
#elif TARGET_ARM64
            return INS_mov;
#elif TARGET_ARM
            assert(dstType == TYP_INT);
            assert(!varTypeIsSimd(dstType));
            return INS_vmov_f2i;
#elif TARGET_LOONGARCH64
            assert(!varTypeIsSimd(dstType));
            return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movfr2gr_s : INS_movfr2gr_d;
#elif TARGET_RISCV64
            assert(!varTypeIsSimd(dstType));
            return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_fmv_x_w : INS_fmv_x_d;
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Floating-point to integer copies are not implemented on this target.");
#endif
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
            if (genIsValidMaskReg(srcReg))
            {
                return ins_Copy(dstType);
            }

            assert(genIsValidIntOrFakeReg(srcReg));
#if TARGET_XARCH
            return INS_kmovq_gpr;
#elif TARGET_ARM64
            return INS_sve_mov;
#endif
        }
#endif

        assert(varTypeUsesFloatReg(dstType));

        if (genIsValidFloatReg(srcReg))
        {
            return ins_Copy(dstType);
        }

        assert(genIsValidIntOrFakeReg(srcReg));

#if TARGET_AMD64
        return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movd32 : INS_movd64;
#elif TARGET_X86
        return INS_movd32;
#elif TARGET_ARM64
        return INS_fmov;
#elif TARGET_ARM
        assert(dstType == TYP_FLOAT);
        assert(!varTypeIsSimd(dstType));
        return INS_vmov_i2f;
#elif TARGET_LOONGARCH64
        assert(!varTypeIsSimd(dstType));

        if (dstType == TYP_DOUBLE)
        {
            return INS_movgr2fr_d;
        }
        else
        {
            assert(dstType == TYP_FLOAT);
            return INS_movgr2fr_w;
        }
#elif TARGET_RISCV64
        assert(!varTypeIsSimd(dstType));
        assert(!genIsValidFloatReg(srcReg));

        if (dstType == TYP_DOUBLE)
        {
            return INS_fmv_d_x;
        }
        else
        {
            assert(dstType == TYP_FLOAT);
            return INS_fmv_w_x;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Integer to floating-point copies are not implemented on this target.");
#endif
    }

    public void inst_Mov(var_types dstType, regNumber dstReg, regNumber srcReg, bool canSkip,
        emitAttr size = EA_UNKNOWN, insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if TARGET_LOONGARCH64 || TARGET_RISCV64
        if (varTypeUsesFloatReg(dstType) != genIsValidFloatReg(dstReg))
        {
            if (dstType == TYP_FLOAT)
            {
                dstType = TYP_INT;
            }
            else if (dstType == TYP_DOUBLE)
            {
                dstType = TYP_LONG;
            }
            else if (dstType == TYP_INT)
            {
                dstType = TYP_FLOAT;
            }
            else if (dstType == TYP_LONG)
            {
                dstType = TYP_DOUBLE;
            }
            else
            {
                NYI_LOONGARCH64("CodeGen::inst_Mov dstType");
                NYI_RISCV64("CodeGen::inst_Mov dstType");
                throw new FatalJitException(CORJIT_SKIPPED, "Register move destination type is not implemented.");
            }
        }
#endif
        var ins = ins_Copy(srcReg, dstType);

        if (size == EA_UNKNOWN)
        {
            size = dstType.EmitActualSize;
        }

#if TARGET_ARM
        Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip, flags);
#else
        Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip);
#endif
    }
}
#endif
