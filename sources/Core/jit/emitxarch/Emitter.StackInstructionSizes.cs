// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    public uint emitInsSizeSVCalcDisp(instrDesc id, ulong code, int var, int dsp)
    {
        assert(_compiler is not null);
        var ins = id.idIns();
        var size = emitInsSize(id, code, includeRexPrefixSize: true);

        var adr = _compiler.lvaFrameAddress(var, out var ebpBased);
        dsp = unchecked(adr + (int)id.idAddr().iiaLclVar.lvaOffset());
        var dspIsZero = dsp == 0;
        var tryCompress = true;

        if (ebpBased)
        {
            // FP addressing requires a displacement even when it is zero.
            dspIsZero = false;
        }
        else
        {
            // SP addressing requires a SIB byte.
            size++;

#if !FEATURE_FIXED_OUT_ARGS
            dsp = unchecked((int)(unchecked((uint)dsp) + unchecked((uint)emitCurStackLvl)));

            // The pushed-stack estimate cannot reliably establish zero or compressible displacement.
            tryCompress = false;
            dspIsZero = false;
#endif
        }

        bool dspInByte;

        if (IsEvexEncodableInstruction(ins))
        {
            if (tryCompress)
            {
                if (TryEvexCompressDisp8Byte(id, dsp, out _, out dspInByte) && hasTupleTypeInfo(ins))
                {
                    SetEvexCompressedDisplacement(id);
                }
            }
            else if (TakesEvexPrefix(id) && !(IsApxExtendedEvexInstruction(ins) && !IsSimdInstruction(ins)))
            {
                dspInByte = false;
            }
            else
            {
                dspInByte = unchecked((sbyte)dsp) == dsp;
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

#if TARGET_AMD64
        // Only mov reg, imm64 accepts an eight-byte immediate; memory operands do not.
        noway_assert((valSize <= sizeof(int)) || !id.idIsCnsReloc());
#endif

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
}
#endif
