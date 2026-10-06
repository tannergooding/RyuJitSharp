// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
    public bool emitInsIsLoadOrStoreForCodeGen(instruction ins)
    {
        return emitInsIsLoadOrStore(ins);
    }

    public static bool isLowRegister(regNumber reg)
    {
        return reg <= REG_R7;
    }

    public static bool IsMovInstruction(instruction ins)
    {
        return ins is INS_mov or INS_sxtb or INS_sxth or INS_uxtb or INS_uxth or
            INS_vmov or INS_vmov_i2f or INS_vmov_f2i;
    }

    public static bool isModImmConst(int value)
    {
        var unsignedValue = unchecked((uint)value);
        var imm8 = unsignedValue & 0xffu;
        if (imm8 == unsignedValue)
        {
            return true;
        }

        var imm32A = (imm8 << 16) | imm8;
        if (imm32A == unsignedValue)
        {
            return true;
        }

        var imm32B = imm32A << 8;
        if (imm32B == unsignedValue)
        {
            return true;
        }

        if ((imm32A | imm32B) == unsignedValue)
        {
            return true;
        }

        var mask32 = 0xffu;
        for (var encode = 31; encode >= 8; encode--)
        {
            mask32 <<= 1;
            if ((unsignedValue & ~mask32) == 0)
            {
                return true;
            }
        }

        return false;
    }

    public static int encodeModImmConst(int value)
    {
        var unsignedValue = unchecked((uint)value);
        var imm8 = unsignedValue & 0xff;
        var encode = imm8 >> 7;

        if (imm8 == unsignedValue)
        {
            return unchecked((int)((encode << 7) | (imm8 & 0x7f)));
        }

        var imm32A = (imm8 << 16) | imm8;
        if (imm32A == unsignedValue)
        {
            encode += 2;
            return unchecked((int)((encode << 7) | (imm8 & 0x7f)));
        }

        var imm32B = imm32A << 8;
        if (imm32B == unsignedValue)
        {
            encode += 4;
            return unchecked((int)((encode << 7) | (imm8 & 0x7f)));
        }

        var imm32C = imm32A | imm32B;
        if (imm32C == unsignedValue)
        {
            encode += 6;
            return unchecked((int)((encode << 7) | (imm8 & 0x7f)));
        }

        var mask32 = 0xffu;
        encode = 31;
        for (; encode >= 8; encode--)
        {
            mask32 <<= 1;
            if ((unsignedValue & ~mask32) == 0)
            {
                imm8 = (unsignedValue & mask32) >> (32 - (int)encode);
                assert((imm8 & 0x80) != 0);

                var result = (encode << 7) | (imm8 & 0x7f);
                assert(result <= 0x0fff);
                return (int)result;
            }
        }

        assert(false, "ARM modified-immediate encoding failed.");
        return unchecked((int)BAD_CODE);
    }

    public static bool emitIns_valid_imm_for_small_mov(regNumber reg, int imm, insFlags flags)
    {
        return isLowRegister(reg) && insSetsFlags(flags) && ((imm & 0x00ff) == imm);
    }

    public static bool emitIns_valid_imm_for_cmp(int imm, insFlags flags)
    {
        return isModImmConst(imm) || isModImmConst(unchecked(-imm));
    }

    public static bool emitIns_valid_imm_for_ldst_offset(int imm, emitAttr size)
    {
        return ((imm & 0x0fff) == imm) || (imm is >= -255 and <= 255);
    }

    public static bool emitIns_valid_imm_for_vldst_offset(int imm)
    {
        return (imm & 0x03fc) == imm;
    }

    public static bool emitIns_valid_imm_for_alu(int imm)
    {
        return isModImmConst(imm);
    }

    public static bool emitIns_valid_imm_for_mov(int imm)
    {
        return ((imm & 0xffff) == imm) || isModImmConst(imm) || isModImmConst(~imm);
    }

    public static bool emitIns_valid_imm_for_add(int imm, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        return ((unchecked((uint)(imm < 0 ? -(long)imm : imm)) <= 0x0fff) && (flags != INS_FLAGS_SET)) ||
            isModImmConst(imm) || isModImmConst(unchecked(-imm));
    }

    public static bool emitIns_valid_imm_for_add_sp(int imm)
    {
        return (imm & 0x03fc) == imm;
    }

    public void emitIns_MovRelocatableImmediate(instruction ins, emitAttr attr, regNumber reg, nuint address)
    {
        assert(EA_IS_RELOC(attr));
        assert((ins == INS_movw) || (ins == INS_movt));

        var id = emitNewInstrReloc(attr, address);
        id.idIns(ins);
        id.idInsFmt(insFormat.IF_T2_N3);
        id.idInsSize(emitInsSize(insFormat.IF_T2_N3));
        id.idInsFlags(INS_FLAGS_NOT_SET);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
