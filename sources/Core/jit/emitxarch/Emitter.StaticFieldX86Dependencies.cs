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

    public uint emitGetAdjustedSize(instrDesc id, ulong code)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 emitGetAdjustedSize is not ported.");

    public uint emitInsSize(instrDesc id, ulong code, bool includeRexPrefixSize)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 emitInsSize is not ported.");

    public static bool ImmCanUseSByteEncoding(instruction ins, nint val)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 ImmCanUseSByteEncoding is not ported.");

    private void dispIns(instrDesc id)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 dispIns is not ported.");
}
#endif
