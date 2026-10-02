// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public bool validImmForInstr(instruction ins, int imm, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if (Emitter.emitInsIsLoadOrStoreForCodeGen(ins) && !instIsFP(ins))
        {
            return validDispForLdSt(imm, TYP_INT);
        }

        var result = false;
        switch (ins)
        {
            case INS_cmp:
            case INS_cmn:
            {
                result = validImmForAlu(imm) || validImmForAlu(unchecked(-imm));
                break;
            }

            case INS_and:
            case INS_bic:
            case INS_orr:
            case INS_orn:
            case INS_mvn:
            {
                result = validImmForAlu(imm) || validImmForAlu(~imm);
                break;
            }

            case INS_mov:
            {
                result = validImmForMov(imm);
                break;
            }

            case INS_addw:
            case INS_subw:
            {
                result = (unchecked((uint)(imm < 0 ? -(long)imm : imm)) <= 0x0fff) &&
                    (flags != INS_FLAGS_SET);
                break;
            }

            case INS_add:
            case INS_sub:
            {
                result = validImmForAdd(imm, flags);
                break;
            }

            case INS_tst:
            case INS_eor:
            case INS_teq:
            case INS_adc:
            case INS_sbc:
            case INS_rsb:
            {
                result = validImmForAlu(imm);
                break;
            }

            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            case INS_ror:
            {
                result = (imm > 0) && (imm <= 32);
                break;
            }

            case INS_vstr:
            case INS_vldr:
            {
                result = (imm & 0x3fc) == imm;
                break;
            }

            default:
            {
                break;
            }
        }

        return result;
    }

    public bool validImmForInstr(instruction ins, nint imm, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if ((imm < int.MinValue) || (imm > int.MaxValue))
        {
            return false;
        }

        return validImmForInstr(ins, checked((int)imm), flags);
    }

    public bool validDispForLdSt(int disp, var_types type)
    {
        if (varTypeIsFloating(type))
        {
            return (disp & 0x3fc) == disp;
        }

        return (disp >= -0xff) && (disp <= 0x0fff);
    }

    public bool validImmForAlu(int imm)
    {
        return RyuJitSharp.Emitter.emitIns_valid_imm_for_alu(imm);
    }

    public bool validImmForMov(int imm)
    {
        return RyuJitSharp.Emitter.emitIns_valid_imm_for_mov(imm);
    }

    public bool validImmForAdd(int imm, insFlags flags)
    {
        return RyuJitSharp.Emitter.emitIns_valid_imm_for_add(imm, flags);
    }
}
#endif
