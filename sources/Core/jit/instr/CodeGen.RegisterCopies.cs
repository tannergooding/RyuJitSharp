// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Copy(var_types dstType)
    {
#if TARGET_XARCH
        assert(dstType.EmitActualSize != 0);

        if (varTypeUsesIntReg(dstType))
        {
            return INS_mov;
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
            return INS_kmovq_msk;
        }
#endif

        assert(varTypeUsesFloatReg(dstType));
        return INS_movaps;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Copy instruction selection outside xarch is not implemented.");
#endif
    }

    public instruction ins_Copy(regNumber srcReg, var_types dstType)
    {
#if TARGET_AMD64
        assert(srcReg != REG_NA);

        if (varTypeUsesIntReg(dstType))
        {
            if (genIsValidIntOrFakeReg(srcReg))
            {
                return ins_Copy(dstType);
            }

#if FEATURE_MASKED_HW_INTRINSICS
            if (genIsValidMaskReg(srcReg))
            {
                return INS_kmovq_gpr;
            }
#endif

            assert(genIsValidFloatReg(srcReg));
            return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movd32 : INS_movd64;
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
            if (genIsValidMaskReg(srcReg))
            {
                return ins_Copy(dstType);
            }

            assert(genIsValidIntOrFakeReg(srcReg));
            return INS_kmovq_gpr;
        }
#endif

        assert(varTypeUsesFloatReg(dstType));

        if (genIsValidFloatReg(srcReg))
        {
            return ins_Copy(dstType);
        }

        assert(genIsValidIntOrFakeReg(srcReg));
        return EA_SIZE(dstType.EmitActualSize) == EA_4BYTE ? INS_movd32 : INS_movd64;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Cross-register-class copies outside AMD64 are not implemented.");
#endif
    }

    public void inst_Mov(var_types dstType, regNumber dstReg, regNumber srcReg, bool canSkip,
        emitAttr size = EA_UNKNOWN, insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if TARGET_AMD64
        var ins = ins_Copy(srcReg, dstType);

        if (size == EA_UNKNOWN)
        {
            size = dstType.EmitActualSize;
        }
        #endif

        _ = Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Register moves outside AMD64 are not implemented.");
#endif
    }
}
