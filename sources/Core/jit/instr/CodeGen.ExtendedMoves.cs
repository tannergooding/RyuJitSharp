// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Move_Extend(var_types srcType, bool srcInReg)
    {
        if (varTypeUsesIntReg(srcType))
        {
#if TARGET_XARCH
            return !varTypeIsSmall(srcType) ? INS_mov : varTypeIsUnsigned(srcType) ? INS_movzx : INS_movsx;
#elif TARGET_ARMARCH
            if (srcInReg)
            {
                if (!varTypeIsSmall(srcType))
                {
                    // An int/long fills its register; on ARM64 a four-byte mov
                    // also clears the unused upper bits of the destination.
                    return INS_mov;
                }

                if (varTypeIsUnsigned(srcType))
                {
                    return varTypeIsByte(srcType) ? INS_uxtb : INS_uxth;
                }

                return varTypeIsByte(srcType) ? INS_sxtb : INS_sxth;
            }

            return ins_Load(srcType);
#else
            NYI("ins_Move_Extend");
            throw new FatalJitException(CORJIT_SKIPPED, "Integer extension instruction selection is not implemented on this target.");
#endif
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(srcType))
        {
#if TARGET_XARCH
            return INS_kmovq_msk;
#elif TARGET_ARM64
            return INS_sve_mov;
#endif
        }
#endif
        assert(varTypeUsesFloatReg(srcType));

#if TARGET_XARCH
        if (srcInReg)
        {
            return INS_movaps;
        }

        var srcSize = srcType.Size;
        if (srcSize == 4)
        {
            return INS_movss;
        }
        else if (srcSize == 8)
        {
            return INS_movsd_simd;
        }

        assert(srcSize is 12 or 16 or 32 or 64);

        return INS_movups;
#elif TARGET_ARM64
        return srcInReg ? INS_mov : ins_Load(srcType);
#elif TARGET_ARM
        assert(!varTypeIsSimd(srcType));
        return INS_vmov;
#else
        NYI("ins_Move_Extend");
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-point extension instruction selection is not implemented on this target.");
#endif
    }

    public void inst_Mov_Extend(var_types srcType, bool srcInReg, regNumber dstReg, regNumber srcReg,
        bool canSkip, emitAttr size, insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        var ins = ins_Move_Extend(srcType, srcInReg);
        if (size == EA_UNKNOWN)
        {
            size = srcType.EmitActualSize;
        }

#if TARGET_ARM
        _ = Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip, flags);
#elif TARGET_ARM64
        Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip);
#else
        _ = Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip);
#endif
    }
}
