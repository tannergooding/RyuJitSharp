// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int imm1, int imm2, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        var fmt = IF_NONE;
        var sf = INS_FLAGS_DONT_CARE;

        var lsb = imm1;
        var width = imm2;
        var msb = unchecked(lsb + width - 1);
        var imm = 0;

        switch (ins)
        {
            case INS_bfi:
            {
                assert(reg1 != REG_PC); // VM debugging single stepper doesn't support PC register with this instruction.
                assert(reg2 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                assert((lsb >= 0) && (lsb <= 31)); // required for encoding
                assert((width > 0) && (width <= 32)); // required for encoding
                assert((msb >= 0) && (msb <= 31)); // required for encoding
                assert(msb >= lsb); // required for encoding

                imm = unchecked((lsb << 5) | msb);
                fmt = IF_T2_D0;
                sf = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_sbfx:
            case INS_ubfx:
            {
                assert(reg1 != REG_PC); // VM debugging single stepper doesn't support PC register with this instruction.
                assert(reg2 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                assert((lsb >= 0) && (lsb <= 31)); // required for encoding
                assert((width > 0) && (width <= 32)); // required for encoding
                assert((msb >= 0) && (msb <= 31)); // required for encoding
                assert(msb >= lsb); // required for encoding

                imm = unchecked((lsb << 5) | (width - 1));
                fmt = IF_T2_D0;
                sf = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ssat:
            {
                // imm1 is the shift amount (fixed at zero); imm2 is saturation width N (1-32).
                // The encoding stores N-1 in sat_imm and leaves the shift fields zero.
                assert(reg1 != REG_PC); // VM debugging single stepper doesn't support PC register with this instruction.
                assert(reg2 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                assert((imm1 == 0) && (imm2 >= 1) && (imm2 <= 32)); // required for encoding

                imm = unchecked((lsb << 5) | (width - 1));
                fmt = IF_T2_D0;
                sf = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_usat:
            {
                // imm1 is the shift amount (fixed at zero); imm2 is saturation width N (0-31).
                // The encoding stores N directly in sat_imm and leaves the shift fields zero.
                assert(reg1 != REG_PC); // VM debugging single stepper doesn't support PC register with this instruction.
                assert(reg2 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                assert((imm1 == 0) && (imm2 >= 0) && (imm2 <= 31)); // required for encoding

                imm = unchecked((lsb << 5) | width);
                fmt = IF_T2_D0;
                sf = INS_FLAGS_NOT_SET;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(fmt == IF_T2_D0);
        assert(sf != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsSize(ISZ_32BIT); // IF_T2_D0 is always a 32-bit Thumb-2 instruction.
        id.idInsFlags(sf);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
