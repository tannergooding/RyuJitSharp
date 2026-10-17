// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_ImmArm64(emitAttr size, regNumber reg, nint imm,
        insFlags flags = insFlags.INS_FLAGS_DONT_CARE
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
        )
    {
        assert(!genIsValidFloatReg(reg));

        if (!_compiler.opts.compReloc)
        {
            size = EA_SIZE(size);
        }

        if (EA_IS_RELOC(size))
        {
            Emitter.emitIns_R_AI(INS_adrp, size, reg, imm, targetHandle, gtFlags);
        }
        else if (imm == 0)
        {
            instGen_Set_Reg_To_Zero(size, reg);
        }
        else
        {
            var immSize = EA_SIZE(size);
            if (Emitter.emitIns_valid_imm_for_mov(imm, immSize))
            {
                Emitter.emitIns_R_I(INS_mov, size, reg, imm, INS_OPTS_NONE, targetHandle, gtFlags);
            }
            else
            {
                // MOVN fills untouched halfwords with ones; MOVZ fills them with zeroes.
                // Count the halfwords each choice lets us omit, preferring MOVZ on ties.
                var preferMovn = 0;
                for (var i = (immSize == EA_8BYTE) ? 48 : 16; i >= 0; i -= 16)
                {
                    if (unchecked((ushort)(imm >> i)) == 0xffff)
                    {
                        preferMovn++;
                    }
                    else if (unchecked((ushort)(imm >> i)) == 0x0000)
                    {
                        preferMovn--;
                    }
                }

                var ins = (preferMovn > 0) ? INS_movn : INS_movz;
                var skipVal = (preferMovn > 0) ? ushort.MaxValue : (ushort)0;
                var bits = (immSize == EA_8BYTE) ? 64 : 32;

                for (var i = 0; i < bits; i += 16)
                {
                    var imm16 = unchecked((ushort)(imm >> i));
                    if (imm16 != skipVal)
                    {
                        if (ins == INS_movn)
                        {
                            imm16 = unchecked((ushort)~imm16);
                        }

                        Emitter.emitIns_R_I_I(ins, size, reg, imm16, i, INS_OPTS_LSL,
                            i == 0 ? targetHandle : 0, i == 0 ? gtFlags : GTF_EMPTY);
                        ins = INS_movk;
                    }
                }

                assert(ins == INS_movk);
            }

            if (flags == insFlags.INS_FLAGS_SET)
            {
                Emitter.emitIns_R_I(INS_tst, size, reg, 0);
            }
        }

        _regSet.verifyRegUsed(reg);
    }

    public void instGen_Set_Reg_To_Base_Plus_Imm(emitAttr size, regNumber dstReg, regNumber baseReg, nint imm,
        insFlags flags = insFlags.INS_FLAGS_DONT_CARE
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
        )
    {
        if (Emitter.emitIns_valid_imm_for_add(imm, size))
        {
            Emitter.emitIns_R_R_IArm64(INS_add, size, dstReg, baseReg, imm);
        }
        else
        {
            instGen_Set_Reg_To_ImmArm64(size, dstReg, imm);
            // Keep the base in the second operand: it may be SP.
            Emitter.emitIns_R_R_R(INS_add, size, dstReg, baseReg, dstReg);
        }
    }
}
