// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static regNumber encodingZRtoSP(regNumber reg)
    {
        return (reg == REG_ZR) ? REG_SP : reg;
    }

    public static bool isPredicateRegister(regNumber reg)
    {
        return (reg >= REG_PREDICATE_FIRST) && (reg <= REG_PREDICATE_LAST);
    }

    public void emitIns_R_R_Imm(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, nint imm)
    {
        assert(isGeneralRegister(reg1));
        assert(reg1 != reg2);

        var immFits = true;

        switch (ins)
        {
            case INS_add:
            case INS_adds:
            case INS_sub:
            case INS_subs:
            {
                immFits = emitIns_valid_imm_for_add(imm, attr);
                break;
            }

            case INS_ands:
            case INS_and:
            case INS_eor:
            case INS_orr:
            {
                immFits = emitIns_valid_imm_for_alu(imm, attr);
                break;
            }

            default:
            {
                assert(false, "Unsupported instruction in emitIns_R_R_Imm");
                break;
            }
        }

        if (immFits)
        {
            emitIns_R_R_I(ins, attr, reg1, reg2, imm);
        }
        else
        {
            codeGen.instGen_Set_Reg_To_Imm(attr, reg1, imm);
            emitIns_R_R_R(ins, attr, reg1, reg2, reg1);
        }
    }
}
#endif
