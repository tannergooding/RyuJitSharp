// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public bool IsSimdVexOrEvexEncodableInstruction(instruction ins)
    {
        if (!IsSimdInstruction(ins))
        {
            return false;
        }

        return IsVexEncodableInstruction(ins) || IsEvexEncodableInstruction(ins);
    }

    public static bool IsExtendedReg(regNumber reg, emitAttr attr)
        => false;

    public uint emitInsSizeCV(instrDesc id, ulong code)
    {
        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var attrSize = id.idOpSize();

        uint size = sizeof(int);
        size += emitGetAdjustedSize(id, code);
        var includeRexPrefixSize = true;

        if (TakesRexWPrefix(id) || IsExtendedReg(id.idReg1(), attrSize) || IsExtendedReg(id.idReg2(), attrSize))
        {
            size += emitGetRexPrefixSize(id, ins);
            includeRexPrefixSize = false;
        }

        return size + emitInsSize(id, code, includeRexPrefixSize);
    }

    public uint emitInsSizeCV(instrDesc id, ulong code, int val)
    {
        var ins = id.idIns();
        var valSize = EA_SIZE_IN_BYTES(id.idOpSize());
        var valInByte = ImmCanUseSByteEncoding(ins, val);

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
            assert(!IsSimdInstruction(ins));
        }

        return valSize + emitInsSizeCV(id, code);
    }

    public uint emitGetRexPrefixSize(instruction ins)
    {
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

    public static bool IsCTEST(instruction ins) => false;

    public bool Is4ByteSSEInstruction(instruction ins)
    {
        return !UseVexEncodings && EncodedBySSE38orSSE3A(ins);
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
                assert(prefixAdjustedSize is 2 or 3);
            }

            assert(prefixAdjustedSize != 0);
            prefixAdjustedSize--;
            var check = (byte)((code >> 24) & 0xFF);

            if (check != 0)
            {
                var sizePrefix = (byte)((code >> 16) & 0xFF);

                if ((sizePrefix != 0) && isPrefix(sizePrefix))
                {
                    prefixAdjustedSize--;
                }
            }

            adjustedSize = prefixAdjustedSize;
        }
        else if (Is4ByteSSEInstruction(ins))
        {
            adjustedSize++;
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
        return ((code & 0xFF000000) != 0) ? 4u : ((code & 0x00FF0000) != 0) ? 3u : 2u;
    }

    public static bool ImmCanUseSByteEncoding(instruction ins, nint val)
    {
        var targetVal = unchecked((int)val);

        if (targetVal != val)
        {
            return false;
        }

        return (unchecked((sbyte)targetVal) == targetVal) &&
            (ins != INS_mov) && (ins != INS_test) && !IsCTEST(ins);
    }
}
#endif
