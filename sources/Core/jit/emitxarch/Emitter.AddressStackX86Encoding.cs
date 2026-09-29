// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe uint insEncodeReg012(instrDesc id, regNumber reg, emitAttr size, ulong* code)
        => RegEncoding(reg);

    private unsafe uint insEncodeRegSIB(instrDesc id, regNumber reg, ulong* code)
        => RegEncoding(reg);

    private unsafe uint insEncodeReg345(instrDesc id, regNumber reg, emitAttr size, ulong* code)
        => RegEncoding(reg) << 3;

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

    private ulong AddEvexPrefix(instrDesc id, ulong code, emitAttr size)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 EVEX prefix encoding is not ported.");

    private ulong insEncodeReg3456(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        assert(reg < REG_STK);
        assert(IsSimdVexOrEvexEncodableInstruction(id.idIns()));

        if (TakesEvexPrefix(id))
        {
            throw new FatalJitException(CORJIT_SKIPPED, "x86 EVEX register encoding is not ported.");
        }

        assert(hasVexPrefix(code));
        return code ^ ((ulong)RegEncoding(reg) << 35);
    }

    private bool TryEvexCompressDisp8Byte(instrDesc id, nint dsp, out nint compressedDsp, out bool fitsInByte)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 EVEX displacement compression is not ported.");

    private static bool hasTupleTypeInfo(instruction ins)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 EVEX tuple metadata is not ported.");
}
#endif
