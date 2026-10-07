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
        internal nuint DumpGCTable(byte* table, in InfoHdr header, uint methodSize,
            bool verifyGCTables = false)
        {
            var tableStart = table;
            byte* bp;
            if (verifyGCTables)
            {
                VerifyMarker(ref table, 0xBEEF);
            }

            var calleeSavedRegs = 0u;
            if (header.doubleAlign != 0)
            {
                if (header.ediSaved != 0)
                {
                    calleeSavedRegs++;
                }
                if (header.esiSaved != 0)
                {
                    calleeSavedRegs++;
                }
                if (header.ebxSaved != 0)
                {
                    calleeSavedRegs++;
                }
            }

            var count = header.noGCRegionCnt;
            while (unchecked(count--) > 0)
            {
                table += decodeUnsigned(table, out var regionOffset);
                table += decodeUnsigned(table, out var regionSize);
                Print($"[{regionOffset:X4}-{unchecked(regionOffset + regionSize):X4}) no GC region\n");
            }

            count = header.untrackedCnt;
            var lastStackOffset = 0;
            while (unchecked(count--) > 0)
            {
                var reg = header.ebpFrame != 0 ? 'B' : 'S';
                var size = unchecked((int)decodeSigned(table, out var stackOffsetDelta));
                var stackOffset = unchecked(lastStackOffset - stackOffsetDelta);
                lastStackOffset = stackOffset;
                table = DumpEncoding(table, unchecked((nuint)size));

                assert((~OFFSET_MASK % sizeof(uint)) == 0);
                var lowBits = unchecked((uint)stackOffset) & OFFSET_MASK;
                stackOffset &= unchecked((int)~OFFSET_MASK);
                assert((header.doubleAlign == 0) || (stackOffset >= 0));

                var alignedFrameSize = unchecked((nuint)sizeof(int) * (header.frameSize + calleeSavedRegs));
                if ((header.doubleAlign != 0) && (unchecked((uint)stackOffset) >= alignedFrameSize))
                {
                    reg = 'B';
                    stackOffset = unchecked((int)((nuint)stackOffset - alignedFrameSize));
                    assert(stackOffset >= (2 * sizeof(int)));
                }

                if (stackOffset < 0)
                {
                    Print($"            [E{reg}P-{unchecked((uint)-stackOffset):X2}H] ");
                }
                else
                {
                    Print($"            [E{reg}P+{stackOffset:X2}H] ");
                }

                Print($"an untracked {((lowBits & pinned_OFFSET_FLAG) != 0 ? "pinned " : "")}" +
                    $"{((lowBits & byref_OFFSET_FLAG) != 0 ? "byref" : "")} local\n");
            }

            if (verifyGCTables)
            {
                VerifyMarker(ref table, 0xCAFE);
            }

            count = header.varPtrTableSize;
            var currentOffset = 0u;
            while (unchecked(count--) > 0)
            {
                bp = table;
                table += decodeUnsigned(table, out var varOffset);
                table += decodeUDelta(table, out var beginOffset, currentOffset);
                table += decodeUDelta(table, out var endOffset, beginOffset);
                _ = DumpEncoding(bp, (nuint)(table - bp));

                assert((~OFFSET_MASK % sizeof(uint)) == 0);
                var lowBits = varOffset & 0x3;
                varOffset &= ~OFFSET_MASK;
                assert((header.ebpFrame == 0) || (varOffset != 0));
                currentOffset = beginOffset;

                DumpOffset(beginOffset);
                Print("..");
                DumpOffset(endOffset);
                Print($"  [E{(header.ebpFrame != 0 ? "BP-" : "SP+")}{varOffset:X2}H] a ");
                Print($"{((lowBits & byref_OFFSET_FLAG) != 0 ? "byref " : "")}" +
                    $"{((lowBits & pinned_OFFSET_FLAG) != 0 ? "pinned" : "")} pointer\n");
                assert(endOffset <= methodSize);
            }

            if (verifyGCTables)
            {
                VerifyMarker(ref table, 0xBABE);
            }

            currentOffset = 0;
            bp = table;
            if (header.interruptible != 0)
            {
                var argCount = 0u;
                var isThis = false;
                var iptr = false;
                for (;;)
                {
                    uint value = *table++;
                    uint isPop;
                    uint argOffset;
                    assert(currentOffset <= methodSize);

                    if ((value & 0x80) == 0)
                    {
                        currentOffset = unchecked(currentOffset + (value & 0x7));
                        _ = DumpEncoding(bp, (nuint)(table - bp));
                        bp = table;
                        DumpOffsetEx(currentOffset);
                        Print($"        reg {RegName((value >> 3) & 7)} becoming " +
                            $"{((value & 0x40) != 0 ? "live" : "dead")}");
                        if (isThis)
                        {
                            Print(" 'this'");
                        }
                        if (iptr)
                        {
                            Print(" (iptr)");
                        }
                        Print("\n");
                        isThis = false;
                        iptr = false;
                        continue;
                    }

                    argOffset = (value & 0x38) >> 3;
                    if (argOffset < 6)
                    {
                        currentOffset = unchecked(currentOffset + (value & 0x07));
                        isPop = value & 0x40;
                        goto ARG;
                    }
                    else if (argOffset == 6)
                    {
                        if ((value & 0x40) != 0)
                        {
                            currentOffset = unchecked(currentOffset + (((value & 0x07) + 1) << 3));
                        }
                        else
                        {
                            currentOffset = unchecked(currentOffset + (value & 0x07));
                            argCount = unchecked(argCount + 1);
                            _ = DumpEncoding(bp, (nuint)(table - bp));
                            bp = table;
                            DumpOffsetEx(currentOffset);
                            PrintSigned($"        push non-ptr ({unchecked((int)argCount)})\n");
                        }
                        continue;
                    }

                    assert(argOffset == 7);
                    switch (value)
                    {
                        case 0xFF:
                        {
                            goto DONE_REGTAB;
                        }

                        case 0xBC:
                        {
                            isThis = true;
                            break;
                        }

                        case 0xBF:
                        {
                            iptr = true;
                            break;
                        }

                        case 0xB8:
                        {
                            table += decodeUnsigned(table, out value);
                            currentOffset = unchecked(currentOffset + value);
                            break;
                        }

                        case 0xF8:
                        case 0xFC:
                        {
                            isPop = value & 0x04;
                            table += decodeUnsigned(table, out argOffset);
                            goto ARG;
                        }

                        case 0xFD:
                        {
                            table += decodeUnsigned(table, out argOffset);
                            assert(argOffset != 0);
                            _ = DumpEncoding(bp, (nuint)(table - bp));
                            bp = table;
                            DumpOffsetEx(currentOffset);
                            PrintSigned($"        kill args {unchecked((int)argOffset),2}\n");
                            break;
                        }

                        case 0xF9:
                        {
                            table += decodeUnsigned(table, out argOffset);
                            argCount = unchecked(argCount + argOffset);
                            break;
                        }

                        default:
                        {
                            Print($"Unexpected special code {value:X4}\n");
                            assert(false);
                            break;
                        }
                    }
                    continue;

                ARG:
                    if (isPop != 0)
                    {
                        if (argOffset != 0)
                        {
                            assert((header.ebpFrame != 0) || (argOffset <= argCount));
                            _ = DumpEncoding(bp, (nuint)(table - bp));
                            bp = table;
                            DumpOffsetEx(currentOffset);
                            PrintSigned($"        pop {unchecked((int)argOffset),2} ");
                            if (header.ebpFrame == 0)
                            {
                                argCount = unchecked(argCount - argOffset);
                                PrintSigned($"args ({unchecked((int)argCount)})");
                            }
                            else
                            {
                                Print("ptrs");
                            }
                            Print("\n");
                        }
                    }
                    else
                    {
                        assert((header.ebpFrame != 0) || (argOffset >= argCount));
                        _ = DumpEncoding(bp, (nuint)(table - bp));
                        bp = table;
                        DumpOffsetEx(currentOffset);
                        PrintSigned($"        push ptr {unchecked((int)argOffset),2}");
                        if (header.ebpFrame == 0)
                        {
                            argCount = unchecked(argOffset + 1);
                            PrintSigned($"  ({unchecked((int)argCount)})");
                        }
                        if (isThis)
                        {
                            Print(" 'this'");
                        }
                        if (iptr)
                        {
                            Print(" (iptr)");
                        }
                        Print("\n");
                        isThis = false;
                        iptr = false;
                    }
                }
            }
            else if (header.ebpFrame != 0)
            {
                for (;;)
                {
                    var argMask = 0u;
                    var byrefArgMask = 0u;
                    uint regMask;
                    var byrefRegMask = 0u;
                    var argCount = 0u;
                    byte* argTable = null;
                    uint value;
                    uint encodingType = *table++;
                    assert(currentOffset <= methodSize);

                    switch (encodingType)
                    {
                        case 0xFD:
                        {
                            argMask = *table++;
                            value = *table++;
                            argMask |= (value & 0xF0) << 4;
                            uint next = *table++;
                            currentOffset = unchecked(currentOffset + (value & 0x0F) + ((next & 0x1F) << 4));
                            regMask = next >> 5;
                            break;
                        }

                        case 0xF9:
                        {
                            currentOffset = unchecked(currentOffset + *table++);
                            value = *table++;
                            argMask = value & 0x1F;
                            regMask = value >> 5;
                            value = *table++;
                            byrefArgMask = value & 0x1F;
                            byrefRegMask = value >> 5;
                            break;
                        }

                        case 0xFE:
                        case 0xFA:
                        {
                            value = *table++;
                            regMask = value & 0x7;
                            byrefRegMask = value >> 4;
                            currentOffset = unchecked(currentOffset + *(uint*)table);
                            table += sizeof(uint);
                            argMask = *(uint*)table;
                            table += sizeof(uint);
                            if (encodingType == 0xFA)
                            {
                                byrefArgMask = *(uint*)table;
                                table += sizeof(uint);
                            }
                            break;
                        }

                        case 0xFB:
                        {
                            value = *table++;
                            regMask = value & 0x7;
                            byrefRegMask = value >> 4;
                            currentOffset = *(uint*)table;
                            table += sizeof(uint);
                            argCount = *(uint*)table;
                            table += sizeof(uint);
                            var argTableSize = *(uint*)table;
                            table += sizeof(uint);
                            argTable = table;
                            table += argTableSize;
                            break;
                        }

                        case 0xFF:
                        {
                            goto DONE_REGTAB;
                        }

                        default:
                        {
                            value = encodingType;
                            if ((value & 0x80) == 0)
                            {
                                if ((value & 0x0F) != 0)
                                {
                                    currentOffset = unchecked(currentOffset + (value & 0x0F));
                                    regMask = (value & 0x70) >> 4;
                                    argMask = 0;
                                }
                                else
                                {
                                    _ = DumpEncoding(bp, (nuint)(table - bp));
                                    bp = table;
                                    Print("            thisptr in ");
                                    if ((value & 0x10) != 0)
                                    {
                                        Print("EDI\n");
                                    }
                                    else if ((value & 0x20) != 0)
                                    {
                                        Print("ESI\n");
                                    }
                                    else if ((value & 0x40) != 0)
                                    {
                                        Print("EBX\n");
                                    }
                                    else
                                    {
                                        assert(false, "Reserved GC encoding");
                                    }
                                    continue;
                                }
                            }
                            else
                            {
                                currentOffset = unchecked(currentOffset + (value & 0x7F));
                                value = *table++;
                                regMask = value >> 5;
                                argMask = value & 0x1F;
                            }
                            break;
                        }
                    }

                    assert((byrefArgMask & argMask) == byrefArgMask);
                    assert((byrefRegMask & regMask) == byrefRegMask);
                    _ = DumpEncoding(bp, (nuint)(table - bp));
                    bp = table;
                    DumpOffsetEx(currentOffset);
                    Print("        call [ ");
                    if ((regMask & 1) != 0)
                    {
                        Print($"EDI{((byrefRegMask & 1) != 0 ? '\'' : ' ')}");
                    }
                    if ((regMask & 2) != 0)
                    {
                        Print($"ESI{((byrefRegMask & 2) != 0 ? '\'' : ' ')}");
                    }
                    if ((regMask & 4) != 0)
                    {
                        Print($"EBX{((byrefRegMask & 4) != 0 ? '\'' : ' ')}");
                    }
                    if ((header.ebpFrame == 0) && ((regMask & 8) != 0))
                    {
                        Print("EBP ");
                    }

                    if (argCount != 0)
                    {
                        Print("] ptrArgs=[");
                        do
                        {
                            argTable += decodeUnsigned(argTable, out value);
                            var stackOffset = value & ~byref_OFFSET_FLAG;
                            var lowBit = value & byref_OFFSET_FLAG;
                            Print($"{stackOffset}{(lowBit != 0 ? "i" : "")}");
                            if (argCount > 1)
                            {
                                Print(" ");
                            }
                        }
                        while (--argCount != 0);
                        assert(argTable == table);
                        Print("]");
                    }
                    else
                    {
                        Print($"] argMask={argMask:X2}");
                        if (byrefArgMask != 0)
                        {
                            Print($" (iargs={byrefArgMask:X2})");
                        }
                    }
                    Print("\n");
                }
            }
            else
            {
                var lastSkip = 0u;
                var imask = 0u;
                for (;;)
                {
                    uint value = *table++;
                    assert(currentOffset <= methodSize);
                    if ((value & 0x80) == 0)
                    {
                        if ((value & 0x40) == 0)
                        {
                            if ((value & 0x20) == 0)
                            {
                                currentOffset = unchecked(currentOffset + (value & 0x1F));
                                _ = DumpEncoding(bp, (nuint)(table - bp));
                                bp = table;
                                DumpOffsetEx(currentOffset);
                                Print("        push\n");
                            }
                            else
                            {
                                assert(value == 0x20);
                                table += decodeUnsigned(table, out var pushCount);
                                _ = DumpEncoding(bp, (nuint)(table - bp));
                                bp = table;
                                DumpOffsetEx(currentOffset);
                                PrintSigned($"       push {unchecked((int)pushCount)}\n");
                            }
                        }
                        else if ((value & 0x3F) == 0)
                        {
                            table += decodeUnsigned(table, out var skip);
                            currentOffset = unchecked(currentOffset + skip);
                            lastSkip = skip;
                        }
                        else
                        {
                            var popSize = (value & 0x30) >> 4;
                            var skip = value & 0x0F;
                            currentOffset = unchecked(currentOffset + skip);
                            if (popSize > 0)
                            {
                                _ = DumpEncoding(bp, (nuint)(table - bp));
                                bp = table;
                                DumpOffsetEx(currentOffset);
                                PrintSigned($"        pop {unchecked((int)popSize)}\n");
                            }
                            else
                            {
                                lastSkip = skip;
                            }
                        }
                    }
                    else
                    {
                        uint callArgCount;
                        uint callRegMask;
                        var callPendingTable = false;
                        var callPendingMask = 0u;
                        var callPendingTableCount = 0u;
                        var callPendingTableSize = 0u;
                        switch ((value & 0x70) >> 4)
                        {
                            case 5:
                            {
                                callRegMask = value & 0xF;
                                value = *table++;
                                callPendingMask = value & 0x7;
                                callArgCount = (value >> 3) & 0x7;
                                lastSkip = CallCommonDelta[value >> 6];
                                currentOffset = unchecked(currentOffset + lastSkip);
                                break;
                            }

                            case 6:
                            {
                                callRegMask = value & 0xF;
                                table += decodeUnsigned(table, out callArgCount);
                                table += decodeUnsigned(table, out callPendingMask);
                                break;
                            }

                            case 7:
                            {
                                switch (value & 0x0C)
                                {
                                    case 0x00:
                                    {
                                        assert(value == 0xF0);
                                        table += decodeUnsigned(table, out imask);
                                        _ = DumpEncoding(bp, (nuint)(table - bp));
                                        bp = table;
                                        Print($"            iptrMask = {imask:X2}\n");
                                        continue;
                                    }

                                    case 0x04:
                                    {
                                        _ = DumpEncoding(bp, (nuint)(table - bp));
                                        bp = table;
                                        Print($"            thisptr in {CalleeSavedRegName(value & 0x3)}\n");
                                        continue;
                                    }

                                    case 0x08:
                                    {
                                        value = *table++;
                                        callRegMask = value & 0xF;
                                        imask = value >> 4;
                                        lastSkip = *(uint*)table;
                                        table += sizeof(uint);
                                        currentOffset = unchecked(currentOffset + lastSkip);
                                        callArgCount = *(uint*)table;
                                        table += sizeof(uint);
                                        callPendingTableCount = *(uint*)table;
                                        table += sizeof(uint);
                                        callPendingTableSize = *(uint*)table;
                                        table += sizeof(uint);
                                        callPendingTable = true;
                                        break;
                                    }

                                    case 0x0C:
                                    {
                                        assert(value == 0xFF);
                                        goto DONE_REGTAB;
                                    }

                                    default:
                                    {
                                        assert(false, "reserved GC encoding");
                                        continue;
                                    }
                                }
                                break;
                            }

                            default:
                            {
                                decodeCallPattern(value & 0x7F, out callArgCount, out callRegMask,
                                    out callPendingMask, out lastSkip);
                                currentOffset = unchecked(currentOffset + lastSkip);
                                break;
                            }
                        }

                        _ = DumpEncoding(bp, (nuint)(table - bp));
                        bp = table;
                        DumpOffsetEx(currentOffset);
                        PrintSigned($"        call {unchecked((int)callArgCount)} [ ");
                        var interiorRegMask = imask & 0xF;
                        var interiorArgMask = imask >> 4;
                        assert((callRegMask & 0x0F) == callRegMask);
                        if ((callRegMask & 1) != 0)
                        {
                            Print($"EDI{((interiorRegMask & 1) != 0 ? '\'' : ' ')}");
                        }
                        if ((callRegMask & 2) != 0)
                        {
                            Print($"ESI{((interiorRegMask & 2) != 0 ? '\'' : ' ')}");
                        }
                        if ((callRegMask & 4) != 0)
                        {
                            Print($"EBX{((interiorRegMask & 4) != 0 ? '\'' : ' ')}");
                        }
                        if ((callRegMask & 8) != 0)
                        {
                            Print($"EBP{((interiorRegMask & 8) != 0 ? '\'' : ' ')}");
                        }
                        Print("]");

                        if (callPendingTable)
                        {
                            var offsetsStart = table;
                            PrintSigned($" argOffs({unchecked((int)callPendingTableCount)}) =");
                            for (var index = 0u; index < callPendingTableCount; index++)
                            {
                                table += decodeUnsigned(table, out var pendingOffset);
                                Print($" {pendingOffset,4:X}");
                            }
                            assert((offsetsStart + callPendingTableSize) == table);
                            bp = table;
                        }
                        else
                        {
                            if (callPendingMask != 0)
                            {
                                Print($" argMask={callPendingMask:X2}");
                            }
                            if (interiorArgMask != 0)
                            {
                                Print($" (iargs={interiorArgMask:X2})");
                            }
                        }
                        Print("\n");
                        imask = 0;
                        lastSkip = 0;
                    }
                }
            }

        DONE_REGTAB:
            assert(currentOffset <= methodSize);
            if (verifyGCTables)
            {
                VerifyMarker(ref table, 0xBEEB);
            }
            assert(table > bp);
            _ = DumpEncoding(bp, (nuint)(table - bp));
            Print("\n");

            return (nuint)(table - tableStart);
        }
    }
}
#endif
