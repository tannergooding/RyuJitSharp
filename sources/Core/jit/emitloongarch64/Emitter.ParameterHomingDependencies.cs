// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    // Arithmetic right shift rejects negative values and bits outside the unsigned immediate field.
    public static bool isValidUimm11(nint value)
    {
        return (value >> 11) == 0;
    }

    public static bool isValidUimm12(nint value)
    {
        return (value >> 12) == 0;
    }

    public static bool isValidSimm12(nint value)
    {
        return (-2048 <= value) && (value < 2048);
    }

    public static bool isGeneralRegister(regNumber reg)
    {
        return (reg >= Globals.REG_INT_FIRST) && (reg <= Globals.REG_INT_LAST);
    }

    public unsafe void emitIns_I_la(emitAttr attr, regNumber reg, nint immediate)
    {
        assert(!EA_IS_RELOC(attr));
        assert(isGeneralRegister(reg));

        if ((immediate >> 11) is -1 or 0)
        {
            emitIns_R_R_I(INS_addi_w, attr, reg, REG_R0, immediate);
            return;
        }

        if ((immediate >> 12) == 0)
        {
            emitIns_R_R_I(INS_ori, attr, reg, REG_R0, immediate);
            return;
        }

        var descriptor = emitNewInstr(attr);

        if ((immediate == nint.MaxValue) || (immediate == unchecked((nint)uint.MaxValue)))
        {
            descriptor.idReg2((regNumber)1);
            descriptor.idCodeSize(8);
        }
        else if ((immediate >> 31) is -1 or 0)
        {
            descriptor.idCodeSize(8);
        }
        else if ((immediate >> 51) is -1 or 0)
        {
            descriptor.idCodeSize(12);
        }
        else
        {
            descriptor.idCodeSize(16);
        }

        descriptor.idIns(INS_lu12i_w);
        descriptor.idReg1(reg);
        assert(reg != REG_R0);

        descriptor.idInsOpt(INS_OPTS_I);
        descriptor.idAddr().iiaAddr = (byte*)immediate;

        appendToCurIG(descriptor);
    }
}
#endif
