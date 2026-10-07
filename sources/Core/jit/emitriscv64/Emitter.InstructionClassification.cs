// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    private const byte RISCV64_LD = 1;
    private const byte RISCV64_ST = 2;

    public bool emitInsIsLoad(instruction ins)
    {
        // Pseudo instructions such as LEA are not represented in the instruction table.
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & RISCV64_LD) != 0);
    }

    public bool emitInsIsLoadOrStore(instruction ins)
    {
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & (RISCV64_LD | RISCV64_ST)) != 0);
    }

    public bool emitInsWritesToLclVarStackLoc(instrDesc id)
    {
        if (!id.idIsLclVar())
        {
            return false;
        }

        // This list mirrors the integer stores accepted by emitIns_S_R and excludes floating-point locals.
        return id.idIns() switch
        {
            INS_sd or
            INS_sw or
            INS_sb or
            INS_sh => true,
            _ => false,
        };
    }

    public bool emitInsMayWriteToGCReg(instruction ins)
    {
        assert(ins != INS_invalid);
        if (ins is INS_nop or INS_j)
        {
            return false;
        }

        if (ins == INS_lea)
        {
            return true;
        }

        var code = emitInsCode(ins);
        switch (GetMajorOpcode(code))
        {
            case MajorOpcode.Store:
            case MajorOpcode.StoreFp:
            case MajorOpcode.MiscMem:
            case MajorOpcode.Branch:
            case MajorOpcode.LoadFp:
            case MajorOpcode.MAdd:
            case MajorOpcode.MSub:
            case MajorOpcode.NmSub:
            case MajorOpcode.NmAdd:
            {
                return false;
            }

            case MajorOpcode.System:
            {
                var funct3 = (code >> 12) & 0b111;
                return funct3 != 0;
            }

            case MajorOpcode.OpFp:
            {
                // The low two funct7 bits select the floating-point width and do not affect GC writes.
                var funct7 = code >> (25 + 2);
                return funct7 is 0b10100 or 0b11100 or 0b11000;
            }

            case MajorOpcode.Custom0:
            case MajorOpcode.Custom1:
            case MajorOpcode.Custom2Rv128:
            case MajorOpcode.Custom3Rv128:
            case MajorOpcode.OpV:
            case MajorOpcode.OpVe:
            case MajorOpcode.Reserved:
            {
                assert(false);
                return true;
            }

            default:
            {
                return true;
            }
        }
    }
}
#endif
