// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    internal static uint emitInsCode(instruction ins)
    {
        assert((uint)ins < (uint)insCodes.Length);
        var code = insCodes[(int)ins];
        assert(code != BAD_CODE);

        return code;
    }

    // Values follow the 32-bit opcode map and then the 16-bit RVC opcode map.
    private enum MajorOpcode
    {
        Load,
        LoadFp,
        Custom0,
        MiscMem,
        OpImm,
        Auipc,
        OpImm32,
        Encoding48Bit1,
        Store,
        StoreFp,
        Custom1,
        Amo,
        Op,
        Lui,
        Op32,
        Encoding64Bit,
        MAdd,
        MSub,
        NmSub,
        NmAdd,
        OpFp,
        OpV,
        Custom2Rv128,
        Encoding48Bit2,
        Branch,
        Jalr,
        Reserved,
        Jal,
        System,
        OpVe,
        Custom3Rv128,
        Encoding80Bit,
        Addi4Spn,
        Fld,
        Lw,
        Ld,
        Reserved2,
        Fsd,
        Sw,
        Sd,
        Addi,
        Addiw,
        Li,
        LuiAddi16Sp,
        MiscAlu,
        J,
        Beqz,
        Bnez,
        Slli,
        FldSp,
        LwSp,
        LdSp,
        JrJalrMvAdd,
        FsdSp,
        SwSp,
        SdSp,
    }

    private static MajorOpcode GetMajorOpcode(uint code)
    {
        var is32BitInstruction = (code & 0x3) == 0x3;
        assert(!is32BitInstruction || ((code & 0x1F) != 0x1F));

        if (is32BitInstruction)
        {
            return (MajorOpcode)((code >> 2) & 0x1F);
        }

        var opcode = code & 0x3;
        var funct3 = (code >> 13) & 0x7;
        return (MajorOpcode)(32u + (opcode << 3) + funct3);
    }

    internal static uint WordMask(byte bits)
    {
        return unchecked((uint)((1UL << bits) - 1));
    }

    internal static ulong BitMask64(byte bits)
    {
        return bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
    }

    internal static uint LowerNBitsOfWord(nint word, byte maskSize)
    {
        assert(maskSize < 32);
        assert(maskSize > 0);

        var mask = WordMask(maskSize);

        return unchecked((uint)word) & mask;
    }

    internal static uint UpperNBitsOfWord(nint word, byte maskSize)
    {
        var shift = 32 - maskSize;

        return LowerNBitsOfWord(word >> shift, maskSize);
    }

    internal static uint UpperNBitsOfWordSignExtend(nint word, byte maskSize)
    {
        // Round before extracting the upper bits so the signed low immediate reconstructs the original word.
        var signExtend = unchecked((nint)(1u << (31 - maskSize)));
        var adjustedWord = unchecked(word + signExtend);

        return UpperNBitsOfWord(adjustedWord, maskSize);
    }

    private static bool isValidSignedImmediate(nint value, byte bits)
    {
        var limit = (nint)1 << (bits - 1);

        return (-limit <= value) && (value < limit);
    }

    internal static bool isValidSimm13(nint value)
    {
        return isValidSignedImmediate(value, 13);
    }

    public static bool isValidUimm12(nint value)
    {
        return value >> 12 == 0;
    }

    public static bool isValidUimm11(nint value)
    {
        return value >> 11 == 0;
    }

    public static bool isValidUimm5(nint value)
    {
        return value >> 5 == 0;
    }

    public static bool isValidSimm20(nint value)
    {
        return (-((nint)1 << 19) <= value) && (value < ((nint)1 << 19));
    }

    public static bool isValidUimm20(nint value)
    {
        return value >> 20 == 0;
    }

    public static bool isValidSimm21(nint value)
    {
        return (-((nint)1 << 20) <= value) && (value < ((nint)1 << 20));
    }

    public static bool isValidSimm32(nint value)
    {
        var minValue = -((nint)1 << 31) - 0x800;
        var maxValueExclusive = ((nint)1 << 31) - 0x800;

        return (minValue <= value) && (value < maxValueExclusive);
    }

    public static uint getBitWidth(emitAttr size)
    {
        assert(size <= EA_8BYTE);

        return (uint)size * BITS_PER_BYTE;
    }

    public static bool isGeneralRegisterOrR0(regNumber reg)
    {
        return (reg >= REG_FIRST) && (reg <= REG_INT_LAST);
    }

    public static bool isFloatReg(regNumber reg)
    {
        return (reg >= REG_FP_FIRST) && (reg <= REG_FP_LAST);
    }

    internal static uint TrimSignedToImm12(nint immediate)
    {
        assert(isValidSimm12(immediate));

        return LowerNBitsOfWord(immediate, 12);
    }

    internal static uint TrimSignedToImm13(nint immediate)
    {
        assert(isValidSignedImmediate(immediate, 13));

        return LowerNBitsOfWord(immediate, 13);
    }

    internal static uint TrimSignedToImm20(nint immediate)
    {
        assert(isValidSignedImmediate(immediate, 20));

        return LowerNBitsOfWord(immediate, 20);
    }

    internal static uint TrimSignedToImm21(nint immediate)
    {
        assert(isValidSignedImmediate(immediate, 21));

        return LowerNBitsOfWord(immediate, 21);
    }

    internal static bool isSingleInstructionFpImm(double value, emitAttr size, out long outBits)
    {
        assert(size == EA_4BYTE || size == EA_8BYTE);

        outBits = size == EA_4BYTE
            ? unchecked((int)SingleToUInt32Bits(FloatingPointUtils.convertToSingle(value)))
            : unchecked((long)DoubleToUInt64Bits(value));

        return isValidSimm12(unchecked((nint)outBits)) ||
               (((outBits & 0xFFF) == 0) && isValidSignedImmediate(unchecked((nint)(outBits >> 12)), 20));
    }

    private static void assertCodeLength(ulong code, byte size)
    {
        assert((code >> size) == 0);
    }

    internal static instruction tryGetCompressedIns_R_R_R(
        instruction ins,
        emitAttr attr,
        regNumber rd,
        regNumber rs1,
        regNumber rs2,
        insOpts opt)
    {
        switch (ins)
        {
            case INS_add:
            {
                if ((rs1 == REG_R0) && (rd != REG_R0) && (rs2 != REG_R0))
                {
                    return INS_c_mv;
                }
                else if ((rd == rs1) && (rd != REG_R0) && (rs2 != REG_R0))
                {
                    return INS_c_add;
                }

                break;
            }
            case INS_and:
            case INS_or:
            case INS_xor:
            case INS_sub:
            case INS_addw:
            case INS_subw:
            {
                var rdRvc = tryGetRvcRegisterNumber(rd);
                var rs2Rvc = tryGetRvcRegisterNumber(rs2);

                if ((rd == rs1) && (rdRvc != uint.MaxValue) && (rs2Rvc != uint.MaxValue))
                {
                    return getCompressedArithmeticIns(ins);
                }

                break;
            }
            default:
            {
                break;
            }
        }

        return INS_none;
    }

    internal static uint tryGetRvcRegisterNumber(regNumber reg)
    {
        // The compressed register field encodes x8 through x15 as 0 through 7.
        return reg switch
        {
            REG_FP => 0,
            REG_S1 => 1,
            REG_A0 => 2,
            REG_A1 => 3,
            REG_A2 => 4,
            REG_A3 => 5,
            REG_A4 => 6,
            REG_A5 => 7,
            _ => uint.MaxValue,
        };
    }

    internal static regNumber getRegNumberFromRvcReg(uint rvcReg)
    {
        assert((rvcReg >> 3) == 0);

        switch (rvcReg)
        {
            case 0:
            {
                return REG_FP;
            }
            case 1:
            {
                return REG_S1;
            }
            case 2:
            {
                return REG_A0;
            }
            case 3:
            {
                return REG_A1;
            }
            case 4:
            {
                return REG_A2;
            }
            case 5:
            {
                return REG_A3;
            }
            case 6:
            {
                return REG_A4;
            }
            case 7:
            {
                return REG_A5;
            }
            default:
            {
                unreached();
                throw new FatalJitException(CORJIT_INTERNALERROR, "Invalid compressed register number.");
            }
        }
    }

    internal static instruction getCompressedArithmeticIns(instruction ins)
    {
        assert((ins == INS_and) || (ins == INS_or) || (ins == INS_xor) || (ins == INS_sub) || (ins == INS_addw) ||
               (ins == INS_subw));

        switch (ins)
        {
            case INS_and:
            {
                return INS_c_and;
            }
            case INS_or:
            {
                return INS_c_or;
            }
            case INS_xor:
            {
                return INS_c_xor;
            }
            case INS_sub:
            {
                return INS_c_sub;
            }
            case INS_addw:
            {
                return INS_c_addw;
            }
            case INS_subw:
            {
                return INS_c_subw;
            }
            default:
            {
                unreached();
                throw new FatalJitException(CORJIT_INTERNALERROR, "Invalid compressed arithmetic instruction.");
            }
        }
    }

    internal static uint insEncodeCRTypeInstr(instruction ins, uint rdRs1, uint rs2)
    {
        assert((INS_c_mv <= ins) && (ins <= INS_c_add));

        var code = emitInsCode(ins);
        assertCodeLength(code, 16);
        assertCodeLength(rdRs1, 5);
        assertCodeLength(rs2, 5);

        return code | (rs2 << 2) | (rdRs1 << 7);
    }

    internal static uint insEncodeCATypeInstr(instruction ins, uint rdRs1Rvc, uint rs2Rvc)
    {
        assert((INS_c_and <= ins) && (ins <= INS_c_subw));

        var code = emitInsCode(ins);
        assertCodeLength(code, 16);
        assertCodeLength(rdRs1Rvc, 3);
        assertCodeLength(rs2Rvc, 3);

        return code | (rs2Rvc << 2) | (rdRs1Rvc << 7);
    }

    internal static uint insEncodeRTypeInstr(
        uint opcode,
        uint rd,
        uint funct3,
        uint rs1,
        uint rs2,
        uint funct7)
    {
        // R-type fields occupy funct7[31:25], rs2[24:20], rs1[19:15], funct3[14:12], rd[11:7], and opcode[6:0].
        assertCodeLength(opcode, 7);
        assertCodeLength(rd, 5);
        assertCodeLength(funct3, 3);
        assertCodeLength(rs1, 5);
        assertCodeLength(rs2, 5);
        assertCodeLength(funct7, 7);

        return opcode | (rd << 7) | (funct3 << 12) | (rs1 << 15) | (rs2 << 20) | (funct7 << 25);
    }

    internal static uint insEncodeITypeInstr(uint opcode, uint rd, uint funct3, uint rs1, uint imm12)
    {
        // I-type immediates occupy bits 31:20 above the shared register and opcode fields.
        assertCodeLength(opcode, 7);
        assertCodeLength(rd, 5);
        assertCodeLength(funct3, 3);
        assertCodeLength(rs1, 5);
        assertCodeLength(imm12, 12);

        return opcode | (rd << 7) | (funct3 << 12) | (rs1 << 15) | (imm12 << 20);
    }

    internal static uint insEncodeSTypeInstr(uint opcode, uint funct3, uint rs1, uint rs2, uint imm12)
    {
        const uint loMask = 0x1f;
        const uint hiMask = 0x7f;

        // S-type splits the 12-bit immediate around the register fields: imm[4:0] goes to 11:7, imm[11:5] to 31:25.
        assertCodeLength(opcode, 7);
        assertCodeLength(funct3, 3);
        assertCodeLength(rs1, 5);
        assertCodeLength(rs2, 5);
        assertCodeLength(imm12, 12);

        var imm12Lo = imm12 & loMask;
        var imm12Hi = (imm12 >> 5) & hiMask;

        return opcode | (imm12Lo << 7) | (funct3 << 12) | (rs1 << 15) | (rs2 << 20) | (imm12Hi << 25);
    }

    internal static uint insEncodeUTypeInstr(uint opcode, uint rd, uint imm20)
    {
        // U-type places the 20-bit immediate in instruction bits 31:12.
        assertCodeLength(opcode, 7);
        assertCodeLength(rd, 5);
        assertCodeLength(imm20, 20);

        return opcode | (rd << 7) | (imm20 << 12);
    }

    internal static uint insEncodeBTypeInstr(uint opcode, uint funct3, uint rs1, uint rs2, uint imm13)
    {
        const uint loSectionMask = 0x0f;
        const uint hiSectionMask = 0x3f;
        const uint bitMask = 0x01;

        // Branch displacements have an implicit zero bit; imm[12|10:5] and imm[4:1|11] occupy the split fields.
        assertCodeLength(opcode, 7);
        assertCodeLength(funct3, 3);
        assertCodeLength(rs1, 5);
        assertCodeLength(rs2, 5);
        assertCodeLength(imm13, 13);
        assert((imm13 & 0x01) == 0);

        var imm12 = imm13 >> 1;
        var imm12LoSection = imm12 & loSectionMask;
        var imm12LoBit = (imm12 >> 10) & bitMask;
        var imm12HiSection = (imm12 >> 4) & hiSectionMask;
        var imm12HiBit = (imm12 >> 11) & bitMask;

        return opcode | (imm12LoBit << 7) | (imm12LoSection << 8) | (funct3 << 12) | (rs1 << 15) | (rs2 << 20) |
               (imm12HiSection << 25) | (imm12HiBit << 31);
    }

    internal static uint insEncodeJTypeInstr(uint opcode, uint rd, uint imm21)
    {
        const uint hiSectionMask = 0x3ff;
        const uint loSectionMask = 0xff;
        const uint bitMask = 0x01;

        // Jump displacements have an implicit zero bit and are reordered into imm[20|10:1|11|19:12].
        assertCodeLength(opcode, 7);
        assertCodeLength(rd, 5);
        assertCodeLength(imm21, 21);
        assert((imm21 & 0x01) == 0);

        var imm20 = imm21 >> 1;
        var imm20HiSection = imm20 & hiSectionMask;
        var imm20HiBit = (imm20 >> 19) & bitMask;
        var imm20LoSection = (imm20 >> 11) & loSectionMask;
        var imm20LoBit = (imm20 >> 10) & bitMask;

        return opcode | (rd << 7) | (imm20LoSection << 12) | (imm20LoBit << 20) | (imm20HiSection << 21) |
               (imm20HiBit << 31);
    }
}
#endif
