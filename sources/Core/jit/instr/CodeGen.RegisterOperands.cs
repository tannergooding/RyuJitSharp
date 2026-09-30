// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_RV_RV(instruction ins, regNumber reg1, regNumber reg2, var_types type = TYP_I_IMPL,
        emitAttr size = EA_UNKNOWN, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if (size == EA_UNKNOWN)
        {
            size = type.EmitActualSize;
        }

#if TARGET_ARM
        Emitter.emitIns_R_R(ins, size, reg1, reg2, flags);
#else
        Emitter.emitIns_R_R(ins, size, reg1, reg2);
#endif
    }

    public void inst_RV(instruction ins, regNumber reg, var_types type, emitAttr size = EA_UNKNOWN)
    {
        if (size == EA_UNKNOWN)
        {
            size = type.EmitActualSize;
        }

#if TARGET_LOONGARCH64
        NYI_LOONGARCH64("inst_RV-----unused on LOONGARCH64----");
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 inst_RV is unused and not implemented.");
#elif TARGET_RISCV64
        NYI_RISCV64("inst_RV-----unused on RISCV64----");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V inst_RV is unused and not implemented.");
#else
        Emitter.emitIns_R(ins, size, reg);
#endif
    }

    public void inst_RV_IV(instruction ins, regNumber reg, nint value, emitAttr size,
        insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if !TARGET_64BIT
        assert(size != EA_8BYTE);
        value = unchecked((int)value);
#endif

#if TARGET_ARM
        if (arm_Valid_Imm_For_Instr(ins, unchecked((int)value), flags))
        {
            Emitter.emitIns_R_I(ins, size, reg, unchecked((int)value), flags);
        }
        else if (ins == INS_mov)
        {
            instGen_Set_Reg_To_Imm(size, reg, value);
        }
        else
        {
            unreached();
        }
#elif TARGET_ARM64
        assert(ins != INS_cmp);
        assert(ins != INS_tst);
        assert(ins != INS_mov);
        Emitter.emitIns_R_R_I(ins, size, reg, reg, value);
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
        Emitter.emitIns_R_R_I(ins, size, reg, reg, value);
#else
#if TARGET_AMD64
        if ((size == EA_8BYTE) && (ins == INS_mov) && ((unchecked((ulong)value) & 0xFFFFFFFF00000000UL) == 0))
        {
            size = EA_4BYTE;
            Emitter.emitIns_R_I(ins, size, reg, value);
        }
        else if ((EA_SIZE(size) == EA_8BYTE) && (ins != INS_mov) &&
            ((unchecked((int)value) != value) || EA_IS_CNS_RELOC(size)))
        {
            assert(false, "Invalid immediate for inst_RV_IV");
        }
        else
#endif
        {
            Emitter.emitIns_R_I(ins, size, reg, value);
        }
#endif
    }
}
