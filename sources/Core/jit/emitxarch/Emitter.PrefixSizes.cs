// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool hasRexPrefix(ulong code) => (code & 0xFF00000000UL) != 0;

    private static bool hasVexPrefix(ulong code) => (code & 0xFF000000000000UL) == 0xC4000000000000UL;

    private static bool hasRex2Prefix(ulong code) => (code & 0xFF0000000000UL) == 0xD50000000000UL;

    private static bool hasEvexPrefix(ulong code) => (code & 0xFF00000000000000UL) == 0x6200000000000000UL;

    private static bool EncodedBySSE38orSSE3A(instruction ins)
    {
        if (!IsSimdInstruction(ins))
        {
            return false;
        }

        nuint insCode = 0;
        if (hasCodeRM(ins))
        {
            insCode = insCodeRM(ins);
        }
        else if (hasCodeMI(ins))
        {
            insCode = insCodeMI(ins);
        }
        else if (hasCodeMR(ins))
        {
            insCode = insCodeMR(ins);
        }

        var maskedCode = (ulong)insCode & 0xFF0000FFUL;
        if (maskedCode != 0x0F000038UL && maskedCode != 0x0F00003AUL)
        {
            return false;
        }

#if DEBUG
        var leadingByte = (insCode >> 16) & 0xFF;
        assert(leadingByte is 0x00 or 0x66 or 0xF2 or 0xF3);
#endif
        return true;
    }

    private uint emitGetVexPrefixSize(instrDesc id)
    {
        var ins = id.idIns();
        assert(IsVexEncodableInstruction(ins));

        if (EncodedBySSE38orSSE3A(ins))
        {
            return 3;
        }

        if (ins is INS_crc32 or INS_sarx or INS_shrx)
        {
            return 3;
        }

        if (TakesRexWPrefix(id))
        {
            return 3;
        }

        regNumber regFor012Bits;
        if (id.idHasMemAdr())
        {
            if (IsExtendedReg(id.idAddr().iiaAddrMode.amIndxReg))
            {
                return 3;
            }
            regFor012Bits = id.idAddr().iiaAddrMode.amBaseReg;
        }
        else if (id.idHasMemGen() || id.idHasMemStk())
        {
            return 2;
        }
        else if (id.idHasReg3())
        {
            regFor012Bits = id.idReg3();
        }
        else if (id.idHasReg2())
        {
            regFor012Bits = id.idReg2();
            var idOp = (ID_OPS)emitFmtToOps[(int)id.idInsFmt()];

            if (idOp == ID_OP_SCNS)
            {
                if (hasCodeMR(ins))
                {
                    regFor012Bits = id.idReg1();
                }
            }
            else if (ins is INS_movd32 or INS_movd64)
            {
                if (regFor012Bits >= REG_XMM0 && regFor012Bits <= REG_XMM31)
                {
                    regFor012Bits = id.idReg1();
                }
            }
        }
        else
        {
            assert(id.idHasReg1());
            regFor012Bits = id.idReg1();
        }

        return IsExtendedReg(regFor012Bits) ? 3u : 2u;
    }

    private uint emitGetPrefixSize(instrDesc id, ulong code, bool includeRexPrefixSize)
    {
        if (hasEvexPrefix(code))
        {
            return emitGetEvexPrefixSize(id);
        }
        if (hasVexPrefix(code))
        {
            return emitGetVexPrefixSize(id);
        }
        if (hasRex2Prefix(code))
        {
            assert(IsRex2EncodableInstruction(id.idIns()));
            return 2;
        }
        if (includeRexPrefixSize && hasRexPrefix(code))
        {
            if (id.idIns() >= INS_imul_08 && id.idIns() <= INS_imul_31 && TakesEvexPrefix(id))
            {
                return 0;
            }
            return 1;
        }
        return 0;
    }
}
#endif
