// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Copy(var_types dstType)
    {
        assert(dstType.EmitActualSize != 0);

        if (varTypeUsesIntReg(dstType))
        {
            if (Configuration.TARGET_XARCH || Configuration.TARGET_ARMARCH ||
                Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                return INS_mov;
            }

            throw new FatalJitException(CORJIT_SKIPPED,
                "Integer copy instruction selection is not implemented on this target.");
        }

        if (varTypeUsesMaskReg(dstType))
        {
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                throw new FatalJitException(CORJIT_SKIPPED,
                    "Masked-register copies require masked hardware intrinsics.");
            }

            if (Configuration.TARGET_XARCH)
            {
                return INS_kmovq_msk;
            }

            if (Configuration.TARGET_ARM64)
            {
                return INS_sve_mov;
            }

            throw new FatalJitException(CORJIT_SKIPPED,
                "Masked-register copies are not implemented for this target.");
        }

        assert(varTypeUsesFloatReg(dstType));

        if (Configuration.TARGET_XARCH)
        {
            return INS_movaps;
        }

        if (Configuration.TARGET_ARM64)
        {
            if (varTypeIsSimd(dstType))
            {
                return INS_mov;
            }
            else
            {
                assert(varTypeIsFloating(dstType));
                return INS_fmov;
            }
        }

        if (Configuration.TARGET_ARM)
        {
            assert(!varTypeIsSimd(dstType));
            return INS_vmov;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
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
        }

        if (Configuration.TARGET_RISCV64)
        {
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
        }

        throw new FatalJitException(CORJIT_SKIPPED,
            "Floating-point copy instruction selection is not implemented on this target.");
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

            if (genIsValidMaskReg(srcReg))
            {
                if (Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_XARCH)
                {
                    return INS_kmovq_gpr;
                }

                throw new FatalJitException(CORJIT_SKIPPED,
                    "Masked-register to integer copies are not implemented for this target.");
            }

            assert(genIsValidFloatReg(srcReg));

            if (Configuration.TARGET_AMD64)
            {
                return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movd32 : INS_movd64;
            }

            if (Configuration.TARGET_X86)
            {
                return INS_movd32;
            }

            if (Configuration.TARGET_ARM64)
            {
                return INS_mov;
            }

            if (Configuration.TARGET_ARM)
            {
                assert(dstType == TYP_INT);
                assert(!varTypeIsSimd(dstType));
                return INS_vmov_f2i;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                assert(!varTypeIsSimd(dstType));
                return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movfr2gr_s : INS_movfr2gr_d;
            }

            if (Configuration.TARGET_RISCV64)
            {
                assert(!varTypeIsSimd(dstType));
                return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_fmv_x_w : INS_fmv_x_d;
            }

            throw new FatalJitException(CORJIT_SKIPPED,
                "Floating-point to integer copies are not implemented on this target.");
        }

        if (varTypeUsesMaskReg(dstType))
        {
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                throw new FatalJitException(CORJIT_SKIPPED,
                    "Masked-register copies require masked hardware intrinsics.");
            }

            if (genIsValidMaskReg(srcReg))
            {
                return ins_Copy(dstType);
            }

            assert(genIsValidIntOrFakeReg(srcReg));
            if (Configuration.TARGET_XARCH)
            {
                return INS_kmovq_gpr;
            }

            if (Configuration.TARGET_ARM64)
            {
                return INS_sve_mov;
            }

            throw new FatalJitException(CORJIT_SKIPPED,
                "Masked-register copies are not implemented for this target.");
        }

        assert(varTypeUsesFloatReg(dstType));

        if (genIsValidFloatReg(srcReg))
        {
            return ins_Copy(dstType);
        }

        assert(genIsValidIntOrFakeReg(srcReg));

        if (Configuration.TARGET_AMD64)
        {
            return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movd32 : INS_movd64;
        }

        if (Configuration.TARGET_X86)
        {
            return INS_movd32;
        }

        if (Configuration.TARGET_ARM64)
        {
            return INS_fmov;
        }

        if (Configuration.TARGET_ARM)
        {
            assert(dstType == TYP_FLOAT);
            assert(!varTypeIsSimd(dstType));
            return INS_vmov_i2f;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
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
        }

        if (Configuration.TARGET_RISCV64)
        {
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
        }

        throw new FatalJitException(CORJIT_SKIPPED,
            "Integer to floating-point copies are not implemented on this target.");
    }

    public void inst_Mov(var_types dstType, regNumber dstReg, regNumber srcReg, bool canSkip,
        emitAttr size = EA_UNKNOWN, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if ((Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64) &&
            (varTypeUsesFloatReg(dstType) != genIsValidFloatReg(dstReg)))
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

        var ins = ins_Copy(srcReg, dstType);

        if (size == EA_UNKNOWN)
        {
            size = dstType.EmitActualSize;
        }

        if (Configuration.TARGET_ARM)
        {
            _ = Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip, flags);
        }
        else
        {
            _ = Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip);
        }
    }
}
