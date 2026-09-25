// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public unsafe ulong insEncodeMRreg(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        assert((code & 0xC000) == 0);
        code |= 0xC000;
        var regcode = insEncodeReg012(id, reg, size, &code) << 8;
        code |= regcode;

        return code;
    }

    public unsafe uint insEncodeReg012(instrDesc id, regNumber reg, emitAttr size, ulong* code)
    {
        assert(reg < REG_STK);
        var ins = id.idIns();
        assert((code != null) || !IsExtendedReg(reg));

        if (IsExtendedReg(reg))
        {
            if (isHighSimdReg(reg))
            {
                *code = AddRexXPrefix(id, *code);
            }
            if (((uint)AbsRegNumber(reg) & 0x8) != 0)
            {
                *code = AddRexBPrefix(id, *code);
            }
            if (IsExtendedGPReg(reg))
            {
                // B3 is handled above. The fifth GPR bit is separate in REX2 and APX-EVEX.
                assert(TakesRex2Prefix(id) || TakesEvexPrefix(id));
                if (hasRex2Prefix(*code))
                {
                    *code |= 0x001000000000UL;
                }
                else if (hasEvexPrefix(*code))
                {
                    *code |= 0x0008000000000000UL;
                }
                // Some callers compute register bits before attaching a prefix.
            }
        }
        else if ((EA_SIZE(size) == EA_1BYTE) && (reg > REG_RBX) && (code != null))
        {
            // A bare REX selects SPL/BPL/SIL/DIL instead of AH/CH/DH/BH.
            *code = (hasRex2Prefix(*code) || hasEvexPrefix(*code)) ? *code : AddRexPrefix(ins, *code);
        }

        var regBits = RegEncoding(reg);
        assert(regBits < 8);

        return regBits;
    }

    public ulong AddRexXPrefix(instrDesc id, ulong code)
    {
        // VEX/EVEX store X inverted; REX and REX2 store it directly.
        if (hasEvexPrefix(code))
        {
            return code & 0xFFBFFFFFFFFFFFFFUL;
        }
        else if (hasVexPrefix(code))
        {
            return code & 0xFFBFFFFFFFFFFFUL;
        }
        else if (TakesRex2Prefix(id))
        {
            assert(IsRex2EncodableInstruction(id.idIns()));
            return code | 0xD50200000000UL;
        }

        return code | 0x4200000000UL;
    }

    public ulong AddRexBPrefix(instrDesc id, ulong code)
    {
        // VEX/EVEX store B inverted; REX and REX2 store it directly.
        if (hasEvexPrefix(code))
        {
            return code & 0xFFDFFFFFFFFFFFFFUL;
        }
        else if (hasVexPrefix(code))
        {
            return code & 0xFFDFFFFFFFFFFFUL;
        }
        else if (TakesRex2Prefix(id))
        {
            assert(IsRex2EncodableInstruction(id.idIns()));
            return code | 0xD50100000000UL;
        }

        return code | 0x4100000000UL;
    }

    public ulong AddRexPrefix(instruction ins, ulong code)
    {
        assert(!IsVexEncodableInstruction(ins));
        return code | 0x4000000000UL;
    }
#endif
}
