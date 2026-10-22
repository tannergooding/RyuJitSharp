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
            if (Configuration.TARGET_XARCH)
            {
                return !varTypeIsSmall(srcType) ? INS_mov : varTypeIsUnsigned(srcType) ? INS_movzx : INS_movsx;
            }

            if (Configuration.TARGET_ARMARCH)
            {
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
            }

            NYI("ins_Move_Extend");
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.IntegerExtensionInstructionSelectionIsNotImplementedOnThisTarget
    );
            return default!;
        }

        if (varTypeUsesMaskReg(srcType))
        {
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.MaskedRegisterExtensionRequiresMaskedHardwareIntrinsics
    );
                return default!;
            }

            if (Configuration.TARGET_XARCH)
            {
                return INS_kmovq_msk;
            }

            if (Configuration.TARGET_ARM64)
            {
                return INS_sve_mov;
            }

            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.MaskedRegisterExtensionIsNotImplementedForThisTarget
    );
            return default!;
        }

        assert(varTypeUsesFloatReg(srcType));

        if (Configuration.TARGET_XARCH)
        {
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

        if (Configuration.TARGET_ARM64)
        {
            return srcInReg ? INS_mov : ins_Load(srcType);
        }

        if (Configuration.TARGET_ARM)
        {
            assert(!varTypeIsSimd(srcType));
            return INS_vmov;
        }

        NYI("ins_Move_Extend");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.FloatingPointExtensionInstructionSelectionIsNotImplementedOnThisTarget
    );
        return default!;
    }

    public void inst_Mov_Extend(var_types srcType, bool srcInReg, regNumber dstReg, regNumber srcReg,
        bool canSkip, emitAttr size, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var ins = ins_Move_Extend(srcType, srcInReg);
        if (size == EA_UNKNOWN)
        {
            size = srcType.EmitActualSize;
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
