// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public static bool IsCTEST(instruction ins)
    {
        return (ins >= FIRST_CTEST_INSTRUCTION) && (ins <= LAST_CTEST_INSTRUCTION);
    }

    public static bool IsSSEOrAVXInstruction(instruction ins)
    {
        return (ins >= FIRST_SSE_INSTRUCTION) && (ins <= LAST_AVX_INSTRUCTION);
    }

    public bool IsSimdVexOrEvexEncodableInstruction(instruction ins)
    {
        if (!IsSimdInstruction(ins))
        {
            return false;
        }

        return IsVexEncodableInstruction(ins) || IsEvexEncodableInstruction(ins);
    }

    public bool Is4ByteSSEInstruction(instruction ins)
    {
        return !UseVexEncodings && EncodedBySSE38orSSE3A(ins);
    }

    public static bool IsExtendedReg(regNumber reg, emitAttr attr)
    {
        if (reg > REG_XMM31)
        {
            return false;
        }

        if (IsExtendedReg(reg))
        {
            return true;
        }

        if (EA_SIZE(attr) != EA_1BYTE)
        {
            return false;
        }

        // SPL/BPL/SIL/DIL share encodings with AH/CH/DH/BH; even a bare REX selects the low bytes.
        return reg >= REG_RSP;
    }

    public static bool IsLegacyMap1(ulong code)
    {
        // Native opcode packing represents map 1 as XX0F, 0F00XX or 0FPP00XX.
        if ((code & 0xFFFF00FF) == 0x0000000F)
        {
            return true;
        }

        if ((code & 0xFFFF0000) == 0x000F0000)
        {
            return true;
        }

        if ((code & 0xFF00FF00) == 0x0F000000)
        {
            var prefix = (byte)((code & 0xFF0000) >> 16);
            return prefix is 0xF2 or 0xF3 or 0x66;
        }

        return false;
    }

    public static bool ImmCanUseSByteEncoding(instruction ins, nint val)
    {
        long targetVal = val;

        if (targetVal != val)
        {
            return false;
        }

        // MOV, TEST and CTEST have no sign-extended byte immediate form.
        return (unchecked((sbyte)targetVal) == targetVal) && (ins != INS_mov) && (ins != INS_test) && !IsCTEST(ins);
    }

    public uint emitGetRexPrefixSize(instruction ins)
    {
        // VEX and EVEX include the REX bits in their own prefix.
        return IsSimdVexOrEvexEncodableInstruction(ins) ? 0u : 1u;
    }

    public uint emitGetRexPrefixSize(instrDesc id, instruction ins)
    {
        if (IsSimdVexOrEvexEncodableInstruction(ins) || TakesEvexPrefix(id) || TakesRex2Prefix(id))
        {
            return 0;
        }

        return 1;
    }

    public uint emitGetEvexPrefixSize(instrDesc id)
    {
        assert(IsEvexEncodableInstruction(id.idIns()));
        return 4;
    }

    public uint emitGetAdjustedSize(instrDesc id, ulong code)
    {
        var ins = id.idIns();
        uint adjustedSize = 0;

        if (IsSimdVexOrEvexEncodableInstruction(ins))
        {
            uint prefixAdjustedSize;

            if (TakesEvexPrefix(id))
            {
                prefixAdjustedSize = emitGetEvexPrefixSize(id);
                assert(prefixAdjustedSize == 4);
            }
            else
            {
                assert(IsVexEncodableInstruction(ins));
                prefixAdjustedSize = emitGetVexPrefixSize(id);
                assert((prefixAdjustedSize == 2) || (prefixAdjustedSize == 3));
            }

            assert(prefixAdjustedSize != 0);

            // The prefix absorbs the opcode escape byte and any SIMD size prefix.
            // A second escape byte and the otherwise uncounted ModRM byte cancel.
            prefixAdjustedSize -= 1;
            var check = (byte)((code >> 24) & 0xFF);

            if (check != 0)
            {
                var sizePrefix = (byte)((code >> 16) & 0xFF);

                if ((sizePrefix != 0) && isPrefix(sizePrefix))
                {
                    prefixAdjustedSize -= 1;
                }
            }

            adjustedSize = prefixAdjustedSize;
        }
        else if (Is4ByteSSEInstruction(ins))
        {
            // These opcodes do not already include the ModRM byte.
            adjustedSize++;
        }
        else if (IsRex2EncodableInstruction(ins) || IsApxExtendedEvexInstruction(ins))
        {
            uint prefixAdjustedSize = 0;

            if (TakesEvexPrefix(id))
            {
                prefixAdjustedSize = 4;

                if (IsLegacyMap1(code))
                {
                    prefixAdjustedSize -= 1;
                }
            }
            else if (TakesRex2Prefix(id))
            {
                prefixAdjustedSize = 2;

                if (IsLegacyMap1(code))
                {
                    prefixAdjustedSize -= 1;
                }
            }

            var attr = id.idOpSize();

            if ((attr == EA_2BYTE) && (ins != INS_movzx) && (ins != INS_movsx) &&
                !IsSimdInstruction(ins) && !TakesEvexPrefix(id))
            {
                prefixAdjustedSize++;
            }

            adjustedSize = prefixAdjustedSize;
        }
        else
        {
            if (ins == INS_crc32)
            {
                adjustedSize++;
            }

            var attr = id.idOpSize();

            if ((attr == EA_2BYTE) && (ins != INS_movzx) && (ins != INS_movsx) && !IsSimdInstruction(ins))
            {
                adjustedSize++;
            }
        }

        return adjustedSize;
    }

    public uint emitInsSize(instrDesc id, ulong code, bool includeRexPrefixSize)
    {
        var size = ((code & 0xFF000000) != 0) ? 4u : ((code & 0x00FF0000) != 0) ? 3u : 2u;
        size += emitGetPrefixSize(id, code, includeRexPrefixSize);
        return size;
    }

    public uint emitInsSizeSVCalcDisp(instrDesc id, ulong code, int var, int dsp)
    {
        assert(_compiler is not null);
        var ins = id.idIns();
        var size = emitInsSize(id, code, includeRexPrefixSize: true);

        var adr = _compiler.lvaFrameAddress(var, out var ebpBased);
        dsp = unchecked(adr + (int)id.idAddr().iiaLclVar.lvaOffset());
        var dspIsZero = dsp == 0;

        if (ebpBased)
        {
            // FP addressing requires a displacement even when it is zero.
            dspIsZero = false;
        }
        else
        {
            // SP addressing requires a SIB byte. AMD64 has fixed outgoing arguments.
            size++;
        }

        bool dspInByte;

        if (IsEvexEncodableInstruction(ins))
        {
            if (TryEvexCompressDisp8Byte(id, dsp, out _, out dspInByte) && hasTupleTypeInfo(ins))
            {
                SetEvexCompressedDisplacement(id);
            }
        }
        else
        {
            dspInByte = unchecked((sbyte)dsp) == dsp;
        }

        if (dspIsZero)
        {
            return size;
        }
        else if (dspInByte)
        {
            return size + 1;
        }
        else
        {
            return size + sizeof(int);
        }
    }

    public uint emitInsSizeSV(instrDesc id, ulong code, int var, int dsp)
    {
        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var attrSize = id.idOpSize();
        var size = emitInsSizeSVCalcDisp(id, code, var, dsp);
        size += emitGetAdjustedSize(id, code);

        if (TakesRexWPrefix(id) || IsExtendedReg(id.idReg1(), attrSize) || IsExtendedReg(id.idReg2(), attrSize))
        {
            size += emitGetRexPrefixSize(id, ins);
        }

        return size;
    }

    public uint emitInsSizeSV(instrDesc id, ulong code, int var, int dsp, int val)
    {
        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var valSize = EA_SIZE_IN_BYTES(id.idOpSize());
        var valInByte = ImmCanUseSByteEncoding(ins, val);

        // Only mov reg, imm64 accepts an eight-byte immediate; memory operands do not.
        noway_assert((valSize <= sizeof(int)) || !id.idIsCnsReloc());

        if (valSize > sizeof(int))
        {
            valSize = sizeof(int);
        }

        if (id.idIsCnsReloc())
        {
            valInByte = false;
            assert(valSize == sizeof(int));
        }

        if (valInByte)
        {
            valSize = 1;
        }
        else
        {
            assert(!IsSSEOrAVXInstruction(ins));
        }

        return valSize + emitInsSizeSV(id, code, var, dsp);
    }
#endif
}
