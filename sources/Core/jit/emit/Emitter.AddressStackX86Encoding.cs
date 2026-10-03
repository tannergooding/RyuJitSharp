// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe uint insEncodeReg012(instrDesc id, regNumber reg, emitAttr size, ulong* code)
    {
        assert(reg < REG_STK);
        var bits = RegEncoding(reg);
        assert(bits < 8);

        return bits;
    }

    private unsafe uint insEncodeRegSIB(instrDesc id, regNumber reg, ulong* code)
    {
        assert(reg < REG_STK);
        var bits = (uint)reg;
        assert(bits < 8);

        return bits;
    }

    private unsafe uint insEncodeReg345(instrDesc id, regNumber reg, emitAttr size, ulong* code)
    {
        assert(reg < REG_STK);
        var bits = RegEncoding(reg);
        assert(bits < 8);

        return bits << 3;
    }

    private unsafe ulong insEncodeMRreg(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        assert((code & 0xC000) == 0);
        code |= 0xC000;
        code |= (ulong)insEncodeReg012(id, reg, size, &code) << 8;
        return code;
    }

    private static bool IsSSEInstruction(instruction ins)
        => ins >= FIRST_SSE_INSTRUCTION && ins <= LAST_SSE_INSTRUCTION;

    // APX CFCMOV has no entries in the x86 instruction table.
    private static bool IsCFCMOV(instruction ins)
        => false;

    private bool IsDstDstSrcAVXInstruction(instruction ins)
        => UseVexEncodings && (prefixFlags(ins) & INS_FLAGS_IsDstDstSrcAVXInstruction) != 0;

    private bool IsDstSrcSrcAVXInstruction(instruction ins)
        => UseVexEncodings && (prefixFlags(ins) & INS_FLAGS_IsDstSrcSrcAVXInstruction) != 0;

    private static regNumber getBmiRegNumber(instruction ins)
    {
        var reg = ins switch
        {
            INS_blsi => (regNumber)3,
            INS_blsmsk => (regNumber)2,
            INS_blsr => (regNumber)1,
            _ => REG_NA,
        };

        if (reg == REG_NA)
        {
            assert(IsBMIInstruction(ins));
        }

        return reg;
    }

    private static bool instrIs3opImul(instruction ins)
        => ins >= INS_imul_AX && ins <= INS_imul_DI;

    private ulong AddX86PrefixIfNeeded(instrDesc id, ulong code, emitAttr size)
    {
        if (TakesEvexPrefix(id))
        {
            return AddEvexPrefix(id, code, size);
        }
        if (TakesVexPrefix(id.idIns()))
        {
            return AddVexPrefix(id.idIns(), code, size);
        }
        assert(!TakesRex2Prefix(id));
        return code;
    }

    private ulong AddX86PrefixIfNeededAndNotPresent(instrDesc id, ulong code, emitAttr size)
    {
        if (TakesEvexPrefix(id))
        {
            return hasEvexPrefix(code) ? code : AddEvexPrefix(id, code, size);
        }
        if (TakesVexPrefix(id.idIns()))
        {
            return hasVexPrefix(code) ? code : AddVexPrefix(id.idIns(), code, size);
        }
        assert(!TakesRex2Prefix(id));
        return code;
    }

    public ulong AddVexPrefix(instruction ins, ulong code, emitAttr attr)
    {
        assert(IsVexEncodableInstruction(ins));
        assert(!hasVexPrefix(code));
        assert((code & 0xFFFFFF00000000UL) == 0);

        // Carry the three-byte VEX form; prefix output chooses the two-byte form when possible.
        code |= 0xC4E07800000000UL;

        if ((attr == EA_32BYTE) || (prefixFlags(ins) & KInstructionWithLBit) != 0)
        {
            code |= 0x00000400000000UL;
        }
        return code;
    }

    private ulong insEncodeReg3456(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        var ins = id.idIns();
        assert(reg < REG_STK);
        assert(IsSimdVexOrEvexEncodableInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(hasVexPrefix(code) || hasEvexPrefix(code));
        ulong bits = RegEncoding(reg);
        if (IsExtendedReg(reg))
        {
            bits |= 8;
        }
        assert(bits <= 0xF);

        if (IsSimdVexOrEvexEncodableInstruction(ins))
        {
            if (TakesEvexPrefix(id) && hasEvexPrefix(code))
            {
                assert(hasEvexPrefix(code));
                return code ^ (bits << 43);
            }

            assert(IsVexEncodableInstruction(ins));
            assert(hasVexPrefix(code));
            return code ^ (bits << 35);
        }

        assert(TakesEvexPrefix(id));
        assert(hasEvexPrefix(code));
        return code ^ (bits << 43);
    }

    private static uint GetCCFromCCMPOrCTEST(instruction ins)
    {
        assert(IsCTEST(ins) || IsCCMP(ins));
        unreached();
        throw new System.InvalidOperationException("Invalid CCMP or CTEST condition.");
    }
}
#endif
