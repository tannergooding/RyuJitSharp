// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSveMovOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isFloatReg(regNumber reg)
    {
        return isVectorRegister(reg);
    }

    private static bool insScalableOptsWithPredicatePair(insScalableOpts sopt)
    {
        return sopt == INS_SCALABLE_OPTS_WITH_PREDICATE_PAIR;
    }

    private static bool insScalableOptsWithVectorLength(insScalableOpts sopt)
    {
        return (sopt == INS_SCALABLE_OPTS_VL_2X) || (sopt == INS_SCALABLE_OPTS_VL_4X);
    }

    private static bool insSveMovOptsUnpredicated(insSveMovOpts mopt)
    {
        return mopt == INS_SVE_MOV_OPTS_UNPRED;
    }

    private static bool isValidMovprfxReg(insSveMovOpts mopt, regNumber dstReg, regNumber srcReg,
        regNumber op2Reg = REG_NA, regNumber op3Reg = REG_NA, regNumber op4Reg = REG_NA)
    {
        if (insSveMovOptsUnpredicated(mopt) && (dstReg == srcReg))
        {
            // movprfx is skipped, so assume valid.
            return true;
        }

        return (dstReg != op2Reg) && (dstReg != op3Reg) && (dstReg != op4Reg);
    }

    private static bool isValidUimm_MultipleOf(nint value, int bits, nuint mod)
    {
        // The native template's size_t divisor converts value to unsigned before division and remainder.
        return isValidUimm(unchecked((nint)(unchecked((nuint)value) / mod)), bits)
            && ((unchecked((nuint)value) % mod) == 0);
    }

    private static bool isValidSimm_MultipleOf(nint value, int bits, nint mod)
    {
        return isValidSimm(value / mod, bits) && ((value % mod) == 0);
    }

    private static bool isValidRot(nint value)
    {
        return (value == 0) || (value == 90) || (value == 180) || (value == 270);
    }

    private static nint emitDecodeRotationImm0_to_270(nint imm)
    {
        assert(emitIsValidEncodedRotationImm0_to_270(imm));
        switch (imm)
        {
            case 0:
            {
                return 0;
            }

            case 1:
            {
                return 90;
            }

            case 2:
            {
                return 180;
            }

            case 3:
            {
                return 270;
            }

            default:
            {
                break;
            }
        }

        return 0;
    }

    private void emitInsSve_Mov(instruction ins, emitAttr attr, regNumber dstReg, regNumber srcReg,
        bool canSkip, insOpts opt, insSveMovOpts mopt, regNumber mskReg = REG_NA)
    {
        assert(IsMovInstruction(ins));
        var size = EA_SIZE(attr);
        switch (ins)
        {
            case INS_sve_mov:
            {
                if (isPredicateRegister(dstReg))
                {
                    assert((opt == INS_OPTS_SCALABLE_B) || insOptsNone(opt));
                    opt = INS_OPTS_SCALABLE_B;
                    attr = EA_SCALABLE;
                }
                break;
            }

            case INS_sve_movprfx:
            {
                if (mopt == INS_SVE_MOV_OPTS_UNPRED)
                {
                    opt = INS_OPTS_NONE;
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        if (mopt == INS_SVE_MOV_OPTS_UNPRED)
        {
            if (IsRedundantMov(ins, size, dstReg, srcReg, canSkip))
            {
                return;
            }
            emitInsSve_R_R(ins, attr, dstReg, srcReg, opt);
        }
        else
        {
            assert(isPredicateRegister(mskReg));
            var sopt = mopt == INS_SVE_MOV_OPTS_MERGING ? INS_SCALABLE_OPTS_PREDICATE_MERGE : INS_SCALABLE_OPTS_NONE;
            emitInsSve_R_R_R(ins, attr, dstReg, mskReg, srcReg, opt, sopt);
        }
    }
}
#endif
