// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Move_Extend(var_types srcType, bool srcInReg)
    {
        if (varTypeUsesIntReg(srcType))
        {
            return !varTypeIsSmall(srcType) ? INS_mov : varTypeIsUnsigned(srcType) ? INS_movzx : INS_movsx;
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(srcType))
        {
            return INS_kmovq_msk;
        }
#endif
        assert(varTypeUsesFloatReg(srcType));

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
    }

    public void inst_Mov_Extend(var_types srcType, bool srcInReg, regNumber dstReg, regNumber srcReg,
        bool canSkip, emitAttr size, insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Extended register moves require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var ins = ins_Move_Extend(srcType, srcInReg);
        if (size == EA_UNKNOWN)
        {
            size = srcType.EmitActualSize;
        }

        _ = Emitter.emitIns_Mov(ins, size, dstReg, srcReg, canSkip);
#endif
    }
}
#endif
