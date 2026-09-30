// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public bool arm_Valid_Imm_For_Instr(instruction ins, int imm, insFlags flags)
    {
        return validImmForInstr(ins, imm, flags);
    }

    public bool arm_Valid_Imm_For_Add(int imm, insFlags flags)
    {
        return RyuJitSharp.Emitter.emitIns_valid_imm_for_add(imm, flags);
    }

    public bool arm_Valid_Imm_For_Add_SP(int imm)
    {
        return RyuJitSharp.Emitter.emitIns_valid_imm_for_add_sp(imm);
    }

    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        assert(!genIsValidFloatReg(reg));

        if (!_compiler.opts.compReloc)
        {
            size = EA_SIZE(size);
        }

        if (EA_IS_RELOC(size))
        {
            // The relocation address is host-sized; only nonrelocatable immediates narrow below.
            genMov32RelocatableImmediate(size, unchecked((nuint)imm), reg);
        }
        else if (imm == 0)
        {
            instGen_Set_Reg_To_Zero(size, reg, flags);
        }
        else
        {
            var val32 = unchecked((int)imm);
            if (validImmForMov(val32))
            {
                Emitter.emitIns_R_I(INS_mov, size, reg, val32, flags);
            }
            else
            {
                var immLo16 = val32 & 0xffff;
                var immHi16 = (val32 >> 16) & 0xffff;

                assert(validImmForMov(immLo16));
                assert(immHi16 != 0);

                Emitter.emitIns_R_I(INS_movw, size, reg, immLo16);

                // For -32768 through -1, a low register can sign-extend the
                // low halfword with SXTH instead of loading an all-ones high halfword.
                if (RyuJitSharp.Emitter.isLowRegister(reg) && (immHi16 == 0xffff) && ((immLo16 & 0x8000) == 0x8000))
                {
                    _ = Emitter.emitIns_Mov(INS_sxth, EA_4BYTE, reg, reg, canSkip: false);
                }
                else
                {
                    Emitter.emitIns_R_I(INS_movt, size, reg, immHi16);
                }

                if (flags == INS_FLAGS_SET)
                {
                    _ = Emitter.emitIns_Mov(INS_mov, size, reg, reg, canSkip: false, INS_FLAGS_SET);
                }
            }
        }

        _regSet.verifyRegUsed(reg);
    }

    public unsafe void genMov32RelocatableImmediate(emitAttr size, nuint address, regNumber reg)
    {
        assert(EA_IS_RELOC(size));

        Emitter.emitIns_MovRelocatableImmediate(INS_movw, size, reg, address);
        Emitter.emitIns_MovRelocatableImmediate(INS_movt, size, reg, address);

        if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS))
        {
            Emitter.emitIns_R_R_R(INS_add, size, reg, reg, REG_PC);
        }
    }
}
#endif
