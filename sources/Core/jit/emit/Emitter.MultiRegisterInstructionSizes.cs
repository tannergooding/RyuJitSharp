// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public uint emitInsSizeRR(instrDesc id, ulong code)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Register-register instruction sizing requires xarch.");
        }

        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var attr = id.idOpSize();
        var sz = emitGetAdjustedSize(id, code);
        var includeRexPrefixSize = true;

        if (TakesRexWPrefix(id) || IsExtendedReg(id.idReg1(), attr) || IsExtendedReg(id.idReg2(), attr) ||
            (!id.idIsSmallDsc() && (IsExtendedReg(id.idReg3(), attr) || IsExtendedReg(id.idReg4(), attr))))
        {
            sz += emitGetRexPrefixSize(id, ins);
            includeRexPrefixSize = !IsVexEncodableInstruction(ins);
        }

        sz += emitInsSize(id, code, includeRexPrefixSize);
        return sz;
    }

    public uint emitInsSizeRR(instrDesc id, ulong code, int val)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Register-immediate instruction sizing requires xarch.");
        }

        var ins = id.idIns();
        var valSize = EA_SIZE_IN_BYTES(id.idOpSize());
        var valInByte = ImmCanUseSByteEncoding(ins, val);

        if (Configuration.TARGET_AMD64)
        {
            // Only mov reg,imm64 supports an eight-byte immediate; the remaining
            // instructions sign-extend a dword and cannot carry a pointer relocation.
            noway_assert((valSize <= sizeof(int)) || !id.idIsCnsReloc());
        }

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
        return valSize + emitInsSizeRR(id, code);
    }
}
