// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER && DUMP_GC_TABLES
namespace RyuJitSharp;

public partial struct GCInfo
{
    internal sealed unsafe partial class Jit32GCDump
    {
        internal nuint DumpInfoHdr(byte* gcInfoBlock, out InfoHdr header, out uint methodSize,
            bool verifyGCTables = false)
        {
            var table = gcInfoBlock;
            var tableStart = table;
            var bp = table;
            if (verifyGCTables)
            {
                VerifyMarker(ref table, 0xFEEF);
            }

            table += decodeUnsigned(table, out methodSize);
            table = decodeHeader(table, gcInfoVersion, out header);

            var hasArgTabOffset = false;
            if (header.untrackedCnt == HAS_UNTRACKED)
            {
                hasArgTabOffset = true;
                table += decodeUnsigned(table, out header.untrackedCnt);
            }
            if (header.varPtrTableSize == HAS_VARPTR)
            {
                hasArgTabOffset = true;
                table += decodeUnsigned(table, out header.varPtrTableSize);
            }
            if (header.gsCookieOffset == HAS_GS_COOKIE_OFFSET)
            {
                table += decodeUnsigned(table, out header.gsCookieOffset);
            }
            if (header.syncStartOffset == HAS_SYNC_OFFSET)
            {
                table += decodeUnsigned(table, out header.syncStartOffset);
                table += decodeUnsigned(table, out header.syncEndOffset);
            }
            if (header.revPInvokeOffset == HAS_REV_PINVOKE_FRAME_OFFSET)
            {
                table += decodeUnsigned(table, out header.revPInvokeOffset);
            }
            if (header.noGCRegionCnt == HAS_NOGCREGIONS)
            {
                hasArgTabOffset = true;
                table += decodeUnsigned(table, out header.noGCRegionCnt);
            }
            else if (header.noGCRegionCnt > 0)
            {
                hasArgTabOffset = true;
            }

            Print($"    method      size   = {methodSize:X4}\n");
            Print($"    prolog      size   = {header.prologSize,2} \n");
            Print($"    epilog      size   = {header.epilogSize,2} \n");
            Print($"    epilog     count   = {header.epilogCount,2} \n");
            Print($"    epilog      end    = {(header.epilogAtEnd != 0 ? "yes" : "no")}  \n");
            Print("    callee-saved regs  = ");
            if (header.ediSaved != 0)
            {
                Print("EDI ");
            }
            if (header.esiSaved != 0)
            {
                Print("ESI ");
            }
            if (header.ebxSaved != 0)
            {
                Print("EBX ");
            }
            if (header.ebpSaved != 0)
            {
                Print("EBP ");
            }
            Print("\n");

            Print($"    ebp frame          = {(header.ebpFrame != 0 ? "yes" : "no")}  \n");
            Print($"    fully interruptible= {(header.interruptible != 0 ? "yes" : "no")}  \n");
            Print($"    double align       = {(header.doubleAlign != 0 ? "yes" : "no")}  \n");
            Print($"    arguments size     = {header.argCount,2} DWORDs\n");
            Print($"    stack frame size   = {header.frameSize,2} DWORDs\n");
            Print($"    untracked count    = {header.untrackedCnt,2} \n");
            Print($"    var ptr tab count  = {header.varPtrTableSize,2} \n");

            if (header.security != 0)
            {
                Print("    security check obj = yes\n");
            }
            if (header.handlers != 0)
            {
                Print("    exception handlers = yes\n");
            }
            if (header.localloc != 0)
            {
                Print("    localloc           = yes\n");
            }
            if (header.editNcontinue != 0)
            {
                Print("    edit & continue    = yes\n");
            }
            if (header.profCallbacks != 0)
            {
                Print("    profiler callbacks = yes\n");
            }
            if (header.varargs != 0)
            {
                Print("    varargs            = yes\n");
            }
            if (header.gsCookieOffset != INVALID_GS_COOKIE_OFFSET)
            {
                Print($"    GuardStack cookie  = [{(header.ebpFrame != 0 ? "EBP-" : "ESP+")}{header.gsCookieOffset}]\n");
            }
            if (header.syncStartOffset != INVALID_SYNC_OFFSET)
            {
                Print($"    Sync region = [{header.syncStartOffset},{header.syncEndOffset}] " +
                    $"([0x{header.syncStartOffset:x},0x{header.syncEndOffset:x}])\n");
            }
            if (header.noGCRegionCnt > 0)
            {
                Print($"    no GC region count = {header.noGCRegionCnt,2} \n");
            }

            if ((header.epilogCount > 1) || ((header.epilogCount != 0) && (header.epilogAtEnd == 0)))
            {
                if (verifyGCTables)
                {
                    VerifyMarker(ref table, 0xFACE);
                }

                var previousOffset = 0u;
                for (var index = 0u; index < header.epilogCount; index++)
                {
                    table += decodeUDelta(table, out var offset, previousOffset);
                    Print($"    epilog #{index,2}    at   {offset:X4}\n");
                    previousOffset = offset;
                }
            }
            else if (header.epilogCount != 0)
            {
                Print($"    epilog        at   {unchecked(methodSize - header.epilogSize):X4}\n");
            }

            if (hasArgTabOffset)
            {
                table += decodeUnsigned(table, out var argTabOffset);
                Print($"    argTabOffset = {argTabOffset:x}  \n");
            }

            nuint current = 0;
            var last = (nuint)(table - bp);
            while (current < last)
            {
                var amount = last - current;
                if (amount > 5)
                {
                    amount = 5;
                }

                _ = DumpEncoding(bp + current, amount);
                Print("\n");
                current += amount;
            }

            return (nuint)(table - tableStart);
        }

        internal void DumpPtrsInFrame(byte* gcInfoBlock, byte* codeBlock, uint offs,
            bool verifyGCTables = false)
        {
            var table = gcInfoBlock;
            if (verifyGCTables)
            {
                VerifyMarker(ref table, 0xFEEF);
            }

            table += decodeUnsigned(table, out var methodSizeTemp);
            nuint methodSize = methodSizeTemp;
            table = decodeHeader(table, gcInfoVersion, out var header);
            if (header.untrackedCnt == HAS_UNTRACKED)
            {
                table += decodeUnsigned(table, out header.untrackedCnt);
            }
            if (header.varPtrTableSize == HAS_VARPTR)
            {
                table += decodeUnsigned(table, out header.varPtrTableSize);
            }
            if (header.gsCookieOffset == HAS_GS_COOKIE_OFFSET)
            {
                table += decodeUnsigned(table, out header.gsCookieOffset);
                assert(header.gsCookieOffset != INVALID_GS_COOKIE_OFFSET);
            }
            if (header.syncStartOffset == HAS_SYNC_OFFSET)
            {
                table += decodeUnsigned(table, out header.syncStartOffset);
                assert(header.syncStartOffset != INVALID_SYNC_OFFSET);
                table += decodeUnsigned(table, out header.syncEndOffset);
                assert(header.syncEndOffset != INVALID_SYNC_OFFSET);
            }
            if (header.revPInvokeOffset == HAS_REV_PINVOKE_FRAME_OFFSET)
            {
                table += decodeUnsigned(table, out header.revPInvokeOffset);
                assert(header.revPInvokeOffset != INVALID_REV_PINVOKE_OFFSET);
            }
            if (header.noGCRegionCnt == HAS_NOGCREGIONS)
            {
                table += decodeUnsigned(table, out header.noGCRegionCnt);
            }

            nuint prologSize = header.prologSize;
            nuint epilogSize = header.epilogSize;
            uint epilogCount = header.epilogCount;
            var epilogEnd = header.epilogAtEnd != 0;

#if DEBUG
            nuint stackSize = header.frameSize;
            if (offs == 0)
            {
                Print($"    method      size = {methodSize:X4}\n");
                Print($"    stack frame size = {stackSize,3} \n");
                Print($"    prolog      size = {prologSize,3} \n");
                Print($"    epilog      size = {epilogSize,3} \n");
                Print($"    epilog      end  = {(epilogEnd ? "yes" : "no")}  \n");
                Print($"    epilog     count = {epilogCount,3} \n");
                Print($"    security         = {(header.security != 0 ? "yes" : "no")}  \n");
                Print($"    dblAlign         = {(header.doubleAlign != 0 ? "yes" : "no")}  \n");
                Print($"    untracked count  = {header.untrackedCnt,3} \n");
                Print($"    var ptr tab count= {header.varPtrTableSize,3} \n");
                Print("\n");
            }
#endif

            if (offs < prologSize)
            {
                Print($"    Offset {offs:X4} is within the method's prolog\n");

                return;
            }

            if (epilogCount != 0)
            {
                if ((epilogCount > 1) || !epilogEnd)
                {
                    if (verifyGCTables)
                    {
                        VerifyMarker(ref table, 0xFACE);
                    }

                    // The pinned DumpPtrsInFrame leaves prevEps at zero, unlike
                    // DumpInfoHdr. Preserve that behavior rather than fixing it here.
                    var previousEpilog = 0u;
                    for (var index = 0u; index < epilogCount; index++)
                    {
                        table += decodeUDelta(table, out var epilog, previousEpilog);
                        if ((offs >= epilog) && (offs < unchecked(epilog + epilogSize)))
                        {
                            Print($"    Offset {offs:X4} is within the method's epilog" +
                                $" ({unchecked(offs - epilog):X2} bytes into it)\n");

                            return;
                        }
                    }
                }
                else
                {
                    var epilog = unchecked((uint)(int)(methodSize - epilogSize));
                    if ((offs >= epilog) && (offs < unchecked(epilog + epilogSize)))
                    {
                        Print($"    Offset {offs:X4} is within the method's epilog" +
                            $" ({unchecked(offs - epilog):X2} bytes into it)\n");

                        return;
                    }
                }
            }

            Print($"    Offset {offs:X4} is within the method's body\n");
        }
    }
}
#endif
