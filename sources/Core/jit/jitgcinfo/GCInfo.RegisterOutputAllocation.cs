// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    // Mirrors sizeof(regPtrDsc) in jitgcinfo.h; the managed descriptor is a class.
    private static nuint getNativeRegPtrDscSize()
    {
        var pointerSize = (nuint)System.IntPtr.Size;
        var uintSize = (nuint)sizeof(uint);
        var regMaskSize = Configuration.REGMASK_BITS_32 ? (nuint)sizeof(uint) : (nuint)sizeof(ulong);
        var regMaskAlignment = regMaskSize;

        var unionAlignment = regMaskAlignment > uintSize ? regMaskAlignment : uintSize;
        var maskPairSize = unchecked(2 * regMaskSize);
        var callDataSize = alignNativeRegPtrDscOffset(
            unchecked((2 * uintSize) + (nuint)sizeof(ushort)), uintSize);
        var unionSize = alignNativeRegPtrDscOffset(
            maskPairSize > callDataSize ? maskPairSize : callDataSize, unionAlignment);

        var offset = unchecked(alignNativeRegPtrDscOffset(pointerSize, uintSize) + uintSize);
        offset = unchecked(alignNativeRegPtrDscOffset(offset, unionAlignment) + unionSize);
        if (!Configuration.JIT32_GCENCODER)
        {
            offset = unchecked(offset + (nuint)sizeof(byte));
        }
        offset = unchecked(alignNativeRegPtrDscOffset(offset, sizeof(ushort)) + (nuint)sizeof(ushort));

        var structAlignment = pointerSize > unionAlignment ? pointerSize : unionAlignment;
        offset = alignNativeRegPtrDscOffset(offset, structAlignment);

        return offset;
    }

    private static nuint alignNativeRegPtrDscOffset(nuint offset, nuint alignment)
    {
        return unchecked((offset + alignment - 1) & ~(alignment - 1));
    }
    internal regPtrDsc gcRegPtrAllocDsc()
    {
        assert(Compiler.IsFullPtrRegMapRequired);
        var descriptor = new regPtrDsc();
        if (gcRegPtrLast is null)
        {
            assert(gcRegPtrList is null);
            gcRegPtrList = gcRegPtrLast = descriptor;
        }
        else
        {
            assert(gcRegPtrList is not null);
            gcRegPtrLast.rpdNext = descriptor;
            gcRegPtrLast = descriptor;
        }

        if (Configuration.MEASURE_PTRTAB_SIZE)
        {
            unchecked
            {
                PointerTableStatisticsData.s_gcRegPtrDscSize +=
                    PointerTableStatisticsData.s_nativeRegPtrDscSize;
            }
        }

        return descriptor;
    }
}
