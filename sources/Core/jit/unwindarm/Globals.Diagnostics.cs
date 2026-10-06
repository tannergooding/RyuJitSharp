// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM && DEBUG
using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static partial class Globals
{
    // start is a zero-based bit index from the least significant bit.
    public static uint ExtractBits(uint dw, uint start, uint length)
    {
        return (dw >> (int)start) & unchecked((uint)((1 << (int)length) - 1));
    }

    private static uint DisplayOffset(Compiler comp, uint offset)
    {
        return unchecked((uint)comp.dspOffset(unchecked((nint)offset)));
    }

    private static uint DumpIntRegSet(uint registers, uint lr)
    {
        assert(registers != 0 || lr != 0);
        assert((registers & 0xE000) == 0);

        var printed = 0u;
        jitprintf("{");
        printed++;

        var first = true;
        var bitMask = 1u;
        for (var bitNum = 0u; bitNum < 12; bitNum++)
        {
            if ((registers & bitMask) != 0)
            {
                if (!first)
                {
                    jitprintf(",");
                    printed++;
                }

                jitprintf($"r{bitNum}");
                printed += bitNum < 10 ? 2u : 3u;
                first = false;
            }

            bitMask <<= 1;
        }

        if (lr != 0)
        {
            if (!first)
            {
                jitprintf(",");
                printed++;
            }

            jitprintf("lr");
            printed += 2;
        }

        jitprintf("}");
        printed++;

        return printed;
    }

    private static uint DumpRegSetRange(string registerType, uint start, uint end, uint lr)
    {
        assert(start <= end);

        var printed = 0u;
        var registerTypeLength = unchecked((uint)registerType.Length);
        jitprintf("{");
        printed++;

        var first = true;
        for (var register = start; register <= end; register++)
        {
            if (!first)
            {
                jitprintf(",");
                printed++;
            }

            jitprintf($"{registerType}{register}");
            printed += registerTypeLength + (register < 10 ? 1u : 2u);
            first = false;
        }

        if (lr != 0)
        {
            assert(!first);
            jitprintf(",lr");
            printed += 3;
        }

        jitprintf("}");
        printed++;

        return printed;
    }

    private static uint DumpOpsize(uint padding, uint opsize)
    {
        if (padding > 100)
        {
            padding = 4;
        }

        for (var count = padding; count > 0; count--)
        {
            jitprintf(" ");
        }

        jitprintf($"; opsize {opsize}\n");

        return padding + 11;
    }

    public static unsafe void DumpUnwindInfo(
        Compiler comp, bool isHotCode, uint startOffset, uint endOffset, byte* pHeader, uint unwindBlockSize)
    {
        jitprintf($"Unwind Info{(isHotCode ? "" : " COLD")}:\n");

        // The unwind blob can be unaligned; read its words without imposing alignment on the caller.
        var pdw = (uint*)pHeader;
        var dw = Unsafe.ReadUnaligned<uint>(pdw++);

        var codeWords = ExtractBits(dw, 28, 4);
        var epilogCount = ExtractBits(dw, 23, 5);
        var fBit = ExtractBits(dw, 22, 1);
        var eBit = ExtractBits(dw, 21, 1);
        var xBit = ExtractBits(dw, 20, 1);
        var version = ExtractBits(dw, 18, 2);
        var functionLength = ExtractBits(dw, 0, 18);

        jitprintf($"  >> Start offset   : 0x{DisplayOffset(comp, startOffset):x6} (not in unwind data)\n");
        jitprintf($"  >>   End offset   : 0x{DisplayOffset(comp, endOffset):x6} (not in unwind data)\n");
        jitprintf($"  Code Words        : {codeWords}\n");
        jitprintf($"  Epilog Count      : {epilogCount}\n");
        jitprintf($"  F bit             : {fBit}\n");
        jitprintf($"  E bit             : {eBit}\n");
        jitprintf($"  X bit             : {xBit}\n");
        jitprintf($"  Vers              : {version}\n");
        jitprintf($"  Function Length   : {functionLength} (0x{functionLength:x5}) " +
            $"Actual length = {unchecked(functionLength * 2)} (0x{unchecked(functionLength * 2):x6})\n");

        assert(unchecked(functionLength * 2) == unchecked(endOffset - startOffset));

        if (codeWords == 0 && epilogCount == 0)
        {
            dw = Unsafe.ReadUnaligned<uint>(pdw++);

            codeWords = ExtractBits(dw, 16, 8);
            epilogCount = ExtractBits(dw, 0, 16);
            assert((dw & 0xF0000000) == 0);

            jitprintf("  ---- Extension word ----\n");
            jitprintf($"  Extended Code Words        : {codeWords}\n");
            jitprintf($"  Extended Epilog Count      : {epilogCount}\n");
        }

        var countOfUnwindCodes = unchecked(codeWords * 4);
        // Scope indices are bytes, while an extended code stream can exceed 256 bytes.
        Span<bool> epilogStartAt = stackalloc bool[Math.Max(256, (int)countOfUnwindCodes)];
        epilogStartAt.Clear();
        if (eBit == 0)
        {
            jitprintf("  ---- Epilog scopes ----\n");
            if (epilogCount == 0)
            {
                jitprintf("  No epilogs\n");
            }
            else
            {
                for (var scope = 0u; scope < epilogCount; scope++)
                {
                    dw = Unsafe.ReadUnaligned<uint>(pdw++);

                    var epilogStartOffset = ExtractBits(dw, 0, 18);
                    var reserved = ExtractBits(dw, 18, 2);
                    var condition = ExtractBits(dw, 20, 4);
                    var epilogStartIndex = ExtractBits(dw, 24, 8);
                    var epilogStartOffsetFromMainFunctionBegin =
                        unchecked(epilogStartOffset * 2 + startOffset);

                    assert(reserved == 0);

                    jitprintf($"  ---- Scope {scope}\n");
                    jitprintf($"  Epilog Start Offset        : {DisplayOffset(comp, epilogStartOffset)} " +
                        $"(0x{DisplayOffset(comp, epilogStartOffset):x5}) " +
                        $"Actual offset = {DisplayOffset(comp, unchecked(epilogStartOffset * 2))} " +
                        $"(0x{DisplayOffset(comp, unchecked(epilogStartOffset * 2)):x6}) " +
                        $"Offset from main function begin = " +
                        $"{DisplayOffset(comp, epilogStartOffsetFromMainFunctionBegin)} " +
                        $"(0x{DisplayOffset(comp, epilogStartOffsetFromMainFunctionBegin):x6})\n");
                    jitprintf($"  Condition                  : {condition} (0x{condition:x})" +
                        $"{(condition == 0xE ? " (always)" : "")}\n");
                    jitprintf($"  Epilog Start Index         : {epilogStartIndex} (0x{epilogStartIndex:x2})\n");

                    epilogStartAt[(int)epilogStartIndex] = true;
                }
            }
        }
        else
        {
            jitprintf($"  --- One epilog, unwind codes at {epilogCount}\n");
            assert(epilogCount < 256);
            epilogStartAt[(int)epilogCount] = true;
        }

        if (fBit != 0)
        {
            jitprintf("  ---- Note: 'F' bit is set. Prolog codes are for a 'phantom' prolog.\n");
        }

        jitprintf("  ---- Unwind codes ----\n");

        var pUnwindCode = (byte*)pdw;
        var opColumn = 52u;
        for (var index = 0u; index < countOfUnwindCodes; index++)
        {
            if (epilogStartAt[(int)index])
            {
                jitprintf($"    ---- Epilog start at index {index} ----\n");
            }

            var b1 = *pUnwindCode++;
            uint x;
            uint y;
            uint printed;
            uint opsize;
            if ((b1 & 0x80) == 0)
            {
                x = (uint)(b1 & 0x7F);
                jitprintf($"    {b1:X2}          add sp, sp, #{unchecked(x * 4),-8}");
                _ = DumpOpsize(unchecked(opColumn - 37), 16);
            }
            else if ((b1 & 0xC0) == 0x80)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                var lr = ExtractBits(b1, 5, 1);
                x = ((uint)(b1 & 0x1F) << 8) | b2;

                jitprintf($"    {b1:X2} {b2:X2}       pop ");
                printed = 20;
                printed += DumpIntRegSet(x, lr);
                _ = DumpOpsize(unchecked(opColumn - printed), 32);
            }
            else if ((b1 & 0xF0) == 0xC0)
            {
                x = (uint)(b1 & 0xF);
                jitprintf($"    {b1:X2}          mov sp, r{x}");
                printed = 25 + (x > 10 ? 2u : 1u);
                _ = DumpOpsize(unchecked(opColumn - printed), 16);
            }
            else if ((b1 & 0xF8) == 0xD0)
            {
                x = (uint)(b1 & 0x3);
                var lr = (uint)(b1 & 0x4);
                jitprintf($"    {b1:X2}          pop ");
                printed = 20;
                printed += DumpRegSetRange("r", 4, x + 4, lr);
                _ = DumpOpsize(unchecked(opColumn - printed), 16);
            }
            else if ((b1 & 0xF8) == 0xD8)
            {
                x = (uint)(b1 & 0x3);
                var lr = (uint)(b1 & 0x4);
                jitprintf($"    {b1:X2}          pop ");
                printed = 20;
                printed += DumpRegSetRange("r", 4, x + 8, lr);
                _ = DumpOpsize(unchecked(opColumn - printed), 32);
            }
            else if ((b1 & 0xF8) == 0xE0)
            {
                x = (uint)(b1 & 0x7);
                jitprintf($"    {b1:X2}          vpop ");
                printed = 21;
                printed += DumpRegSetRange("d", 8, x + 8, 0);
                _ = DumpOpsize(unchecked(opColumn - printed), 32);
            }
            else if ((b1 & 0xFC) == 0xE8)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                x = ((uint)(b1 & 0x3) << 8) | b2;
                jitprintf($"    {b1:X2} {b2:X2}       addw sp, sp, #{unchecked(x * 4),-8}");
                _ = DumpOpsize(unchecked(opColumn - 38), 32);
            }
            else if ((b1 & 0xFE) == 0xEC)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                var lr = ExtractBits(b1, 0, 1);
                x = b2;
                jitprintf($"    {b1:X2} {b2:X2}       pop ");
                printed = 20;
                printed += DumpIntRegSet(x, lr);
                _ = DumpOpsize(unchecked(opColumn - printed), 16);
            }
            else if (b1 == 0xEE)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                if ((b2 & 0xF0) == 0)
                {
                    x = (uint)(b2 & 0xF);
                    jitprintf($"    {b1:X2} {b2:X2}       Microsoft-specific (x = {x:X2})");
                    _ = DumpOpsize(4, 16);
                }
                else
                {
                    x = ExtractBits(b2, 4, 4);
                    y = ExtractBits(b2, 0, 4);
                    jitprintf($"    {b1:X2} {b2:X2}       Available (x = {x:X2}, y = {y:X2})");
                    _ = DumpOpsize(4, 16);
                }
            }
            else if (b1 == 0xEF)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                if ((b2 & 0xF0) == 0)
                {
                    x = (uint)(b2 & 0xF);
                    jitprintf($"    {b1:X2} {b2:X2}       ldr lr, [sp], #{unchecked(x * 4),-8}");
                    _ = DumpOpsize(unchecked(opColumn - 39), 32);
                }
                else
                {
                    x = ExtractBits(b2, 4, 4);
                    y = ExtractBits(b2, 0, 4);
                    jitprintf($"    {b1:X2} {b2:X2}       Available (x = {x:X2}, y = {y:X2})");
                    _ = DumpOpsize(4, 32);
                }
            }
            else if (b1 is >= 0xF0 and <= 0xF4)
            {
                x = (uint)(b1 & 0x7);
                jitprintf($"    {b1:X2}          Available (x = {x:X2})\n");
            }
            else if (b1 == 0xF5)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                var start = ExtractBits(b2, 4, 4);
                var end = ExtractBits(b2, 0, 4);
                jitprintf($"    {b1:X2} {b2:X2}       vpop ");
                printed = 21;
                printed += DumpRegSetRange("d", start, end, 0);
                _ = DumpOpsize(unchecked(opColumn - printed), 32);
            }
            else if (b1 == 0xF6)
            {
                assert(index + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                index++;

                var start = ExtractBits(b2, 4, 4);
                var end = ExtractBits(b2, 0, 4);
                jitprintf($"    {b1:X2} {b2:X2}       vpop ");
                printed = 21;
                printed += DumpRegSetRange("d", start + 16, end + 16, 0);
                _ = DumpOpsize(unchecked(opColumn - printed), 32);
            }
            else if (b1 is 0xF7 or 0xF9)
            {
                assert(index + 2 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                var b3 = *pUnwindCode++;
                index += 2;

                x = ((uint)b2 << 8) | b3;
                opsize = b1 == 0xF7 ? 16u : 32u;
                jitprintf($"    {b1:X2} {b2:X2} {b3:X2}    add sp, sp, #{unchecked(x * 4),-8}");
                _ = DumpOpsize(unchecked(opColumn - 37), opsize);
            }
            else if (b1 is 0xF8 or 0xFA)
            {
                assert(index + 3 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                var b3 = *pUnwindCode++;
                var b4 = *pUnwindCode++;
                index += 3;

                x = ((uint)b2 << 16) | ((uint)b3 << 8) | b4;
                opsize = b1 == 0xF8 ? 16u : 32u;
                jitprintf($"    {b1:X2} {b2:X2} {b3:X2} {b4:X2} add sp, sp, #{unchecked(x * 4),-8}");
                _ = DumpOpsize(unchecked(opColumn - 37), opsize);
            }
            else if (b1 is 0xFB or 0xFC)
            {
                opsize = b1 == 0xFB ? 16u : 32u;
                jitprintf($"    {b1:X2}          nop");
                _ = DumpOpsize(unchecked(opColumn - 19), opsize);
            }
            else if (b1 is 0xFD or 0xFE)
            {
                opsize = b1 == 0xFD ? 16u : 32u;
                jitprintf($"    {b1:X2}          end + nop");
                _ = DumpOpsize(unchecked(opColumn - 25), opsize);
            }
            else if (b1 == 0xFF)
            {
                jitprintf($"    {b1:X2}          end\n");
            }
            else
            {
                assert(false);
            }
        }

        pdw += (int)codeWords;
        assert((byte*)pdw == pUnwindCode);
        assert((byte*)pdw == pHeader + (int)unwindBlockSize);
        assert(xBit == 0);

        jitprintf("\n");
    }
}
#endif
