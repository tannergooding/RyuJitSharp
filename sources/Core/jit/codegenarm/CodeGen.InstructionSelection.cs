// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        nint imm, regNumber tmpReg, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        var immFitsInIns = false;

        assert(tmpReg != reg2);

        switch (ins)
        {
            case INS_add:
            case INS_sub:
            {
                if (imm < 0)
                {
                    imm = unchecked(-imm);
                    ins = ins == INS_add ? INS_sub : INS_add;
                }

                immFitsInIns = arm_Valid_Imm_For_Instr(ins, unchecked((int)imm), flags);
                break;
            }

            default:
            {
                assert(false, "!\"Unexpected instruction in genInstrWithConstant\"");
                break;
            }
        }

        if (immFitsInIns)
        {
            Emitter.emitIns_R_R_I(
                ins, attr, reg1, reg2, unchecked((int)imm), INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
        }
        else
        {
            assert(tmpReg != REG_NA);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, imm);
            Emitter.emitIns_R_R_R(ins, attr, reg1, reg2, tmpReg);
        }

        return immFitsInIns;
    }

    private bool genStackPointerAdjustment(nint spDelta, regNumber tmpReg)
    {
        return genInstrWithConstant(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, spDelta, tmpReg,
            INS_FLAGS_DONT_CARE);
    }
}
#endif
