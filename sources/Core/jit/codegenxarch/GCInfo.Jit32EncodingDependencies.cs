// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER
namespace RyuJitSharp;

public partial struct GCInfo
{
    // The x86 header's native layout belongs to the unported JIT32 encoder.
    internal struct InfoHdr
    {
    }

    internal static void gcInitEncoderLookupTable()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC encoder lookup initialization is not ported.");
    }

    internal readonly unsafe nuint gcInfoBlockHdrSave(byte* destination, int write,
        uint codeSize, uint prologSize, uint epilogSize, ref InfoHdr header, ref int cached)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC header encoding is not ported.");
    }

    internal readonly nuint gcPtrTableSize(InfoHdr header, uint codeSize, ref nuint argTabOffset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table sizing is not ported.");
    }

    internal readonly unsafe byte* gcPtrTableSave(byte* destination, InfoHdr header,
        uint codeSize, ref nuint argTabOffset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table encoding is not ported.");
    }

#if DUMP_GC_TABLES
    internal readonly unsafe nuint gcInfoBlockHdrDump(byte* source, ref InfoHdr header, out uint codeSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC header decoding is not ported.");
    }

    internal readonly unsafe nuint gcDumpPtrTable(byte* source, InfoHdr header, uint codeSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table decoding is not ported.");
    }
#endif
}
#endif
