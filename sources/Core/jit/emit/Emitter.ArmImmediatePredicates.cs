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
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 relocatable move instruction recording is not ported.");
    }
}
#endif
