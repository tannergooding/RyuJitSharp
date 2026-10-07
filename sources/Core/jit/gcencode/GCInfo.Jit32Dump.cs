// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER && DUMP_GC_TABLES
using System;
using System.Diagnostics;

namespace RyuJitSharp;

public partial struct GCInfo
{
    // src/coreclr/inc/gcinfo.h: the JIT always emits the current (v5) format.
    private const uint GCINFO_VERSION = 5;

#if VERIFY_GC_TABLES
    private const bool verifyGCTables = true;
#else
    private const bool verifyGCTables = false;
#endif

    internal readonly unsafe nuint gcInfoBlockHdrDump(byte* table, ref InfoHdr header, out uint methodSize)
    {
        var gcDump = new Jit32GCDump(GCINFO_VERSION);
#if DEBUG
        gcDump.gcPrintf = gcDump_logf;
#else
        gcDump.gcPrintf = Console.Write;
#endif

#pragma warning disable CA1303 // Native GC dump banners are a fixed, nonlocalized output format.
        Console.Write("Method info block:\n");
#pragma warning restore CA1303

        return gcDump.DumpInfoHdr(table, out header, out methodSize, verifyGCTables);
    }

    internal readonly unsafe nuint gcDumpPtrTable(byte* table, InfoHdr header, uint methodSize)
    {
        if (header.noGCRegionCnt > 0)
        {
#pragma warning disable CA1303
            Console.Write("No GC regions and pointer table:\n");
#pragma warning restore CA1303
        }
        else
        {
#pragma warning disable CA1303
            Console.Write("Pointer table:\n");
#pragma warning restore CA1303
        }

        var gcDump = new Jit32GCDump(GCINFO_VERSION);
#if DEBUG
        gcDump.gcPrintf = gcDump_logf;
#else
        gcDump.gcPrintf = Console.Write;
#endif

        return gcDump.DumpGCTable(table, in header, methodSize, verifyGCTables);
    }

    internal readonly unsafe void gcFindPtrsInFrame(void* infoBlock, void* codeBlock, uint offs)
    {
        var gcDump = new Jit32GCDump(GCINFO_VERSION);
#if DEBUG
        gcDump.gcPrintf = gcDump_logf;
#else
        gcDump.gcPrintf = Console.Write;
#endif

        gcDump.DumpPtrsInFrame((byte*)infoBlock, (byte*)codeBlock, offs, verifyGCTables);
    }

    // GCDump support is kept with its GCInfo callers. Its native definitions in
    // gcdump/ and inc/gcdecoder.cpp are outside this residual retirement batch.
    internal sealed unsafe partial class Jit32GCDump
    {
        private const uint OFFSET_MASK = 0x3;
        private const uint byref_OFFSET_FLAG = 0x1;
        private const uint pinned_OFFSET_FLAG = 0x2;

        internal Action<string> gcPrintf;
        private readonly uint gcInfoVersion;
        private readonly bool fDumpEncBytes;
        private readonly uint cMaxEncBytes;
        private readonly bool fDumpCodeOffsets;

        internal Jit32GCDump(uint gcInfoVersion, bool encBytes = true, uint maxEncBytes = 5,
            bool dumpCodeOffs = true)
        {
            this.gcInfoVersion = gcInfoVersion;
            fDumpEncBytes = encBytes;
            cMaxEncBytes = maxEncBytes;
            fDumpCodeOffsets = dumpCodeOffs;
            gcPrintf = Console.Write;
        }

        private void Print(string message)
        {
            gcPrintf(message);
        }

        private void PrintSigned(FormattableString message)
        {
            gcPrintf(FormattableString.Invariant(message));
        }

        private byte* DumpEncoding(byte* gcInfoBlock, nuint cDumpBytes)
        {
            assert(cMaxEncBytes < 256);

            if (fDumpEncBytes)
            {
                var current = gcInfoBlock;
                var bytesLeft = cDumpBytes;
                for (var count = cMaxEncBytes; count > 0; count--, current++, bytesLeft = unchecked(bytesLeft - 1))
                {
                    if (bytesLeft > 0)
                    {
                        if ((bytesLeft > 1) && (count == 1))
                        {
                            Print("...");
                        }
                        else
                        {
                            Print($"{*current:X2} ");
                        }
                    }
                    else
                    {
                        Print("   ");
                    }
                }

                Print("| ");
            }

            return gcInfoBlock + cDumpBytes;
        }

        private void DumpOffset(uint offset)
        {
            Print($"{offset:X4}");
        }

        private void DumpOffsetEx(uint offset)
        {
            if (fDumpCodeOffsets)
            {
                DumpOffset(offset);
            }
        }

        private static string RegName(uint reg)
        {
            assert(reg < 8);

            return reg switch
            {
                0 => "EAX",
                1 => "ECX",
                2 => "EDX",
                3 => "EBX",
                4 => "ESP",
                5 => "EBP",
                6 => "ESI",
                7 => "EDI",
                _ => throw new InvalidOperationException("Invalid GC dump register."),
            };
        }

        private static string CalleeSavedRegName(uint reg)
        {
            assert(reg < 4);

            return reg switch
            {
                0 => "EDI",
                1 => "ESI",
                2 => "EBX",
                3 => "EBP",
                _ => throw new InvalidOperationException("Invalid GC dump callee-saved register."),
            };
        }

        // Native marker consumption is inside _ASSERTE and vanishes in free builds.
        [Conditional("DEBUG")]
        private static void VerifyMarker(ref byte* table, ushort marker)
        {
            var actual = *(ushort*)table;
            table += sizeof(ushort);
            assert(actual == marker);
        }
    }
}
#endif
