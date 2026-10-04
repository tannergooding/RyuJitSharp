// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && DEBUG
using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static partial class Globals
{
    private static regNumber GetRegisterAtOffset(regNumber first, uint offset)
    {
        return unchecked((regNumber)((uint)first + offset));
    }

    public static uint GetUnwindSizeFromUnwindHeader(byte b1)
    {
        ReadOnlySpan<byte> unwindSize =
        [
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 00-0F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 10-1F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 20-2F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 30-3F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 40-4F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 50-5F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 60-6F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 70-7F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 80-8F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 90-9F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // A0-AF
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // B0-BF
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, // C0-CF
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, // D0-DF
            4, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // E0-EF
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // F0-FF
        ];
        var size = (uint)unwindSize[b1];
        assert(1 <= size && size <= 4);

        return size;
    }

    // start is a zero-based bit index from the least significant bit.
    public static uint ExtractBits(uint dw, uint start, uint length)
    {
        return (dw >> (int)start) & unchecked((uint)((1 << (int)length) - 1));
    }

    public static unsafe void DumpUnwindInfo(
        Compiler comp, bool isHotCode, uint startOffset, uint endOffset, byte* pHeader, uint unwindBlockSize)
    {
        jitprintf($"Unwind Info{(isHotCode ? "" : " COLD")}:\n");

        // Headers need not be aligned. The producer pads the final code word with end codes.
        var pdw = (uint*)pHeader;
        var dw = Unsafe.ReadUnaligned<uint>(pdw++);
        var codeWords = ExtractBits(dw, 27, 5);
        var epilogCount = ExtractBits(dw, 22, 5);
        var eBit = ExtractBits(dw, 21, 1);
        var xBit = ExtractBits(dw, 20, 1);
        var vers = ExtractBits(dw, 18, 2);
        var functionLength = ExtractBits(dw, 0, 18);

        jitprintf($"  >> Start offset   : 0x{comp.dspOffset((nint)startOffset):x6} (not in unwind data)\n");
        jitprintf($"  >>   End offset   : 0x{comp.dspOffset((nint)endOffset):x6} (not in unwind data)\n");
        jitprintf($"  Code Words        : {codeWords}\n");
        jitprintf($"  Epilog Count      : {epilogCount}\n");
        jitprintf($"  E bit             : {eBit}\n");
        jitprintf($"  X bit             : {xBit}\n");
        jitprintf($"  Vers              : {vers}\n");
        jitprintf($"  Function Length   : {functionLength} (0x{functionLength:x5}) " +
            $"Actual length = {functionLength * 4} (0x{functionLength * 4:x6})\n");
        assert(functionLength * 4 == unchecked(endOffset - startOffset));

        if (codeWords == 0 && epilogCount == 0)
        {
            // The extension word carries counts too large for the primary header.
            dw = Unsafe.ReadUnaligned<uint>(pdw++);
            codeWords = ExtractBits(dw, 16, 8);
            epilogCount = ExtractBits(dw, 0, 16);
            assert((dw & 0xF0000000) == 0);

            jitprintf("  ---- Extension word ----\n");
            jitprintf($"  Extended Code Words        : {codeWords}\n");
            jitprintf($"  Extended Epilog Count      : {epilogCount}\n");
        }

        Span<bool> epilogStartAt = stackalloc bool[1024];
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
                    var res = ExtractBits(dw, 18, 4);
                    var epilogStartIndex = ExtractBits(dw, 22, 10);

                    // Funclet scopes are relative to the funclet, so also display their main-function offset.
                    var epilogStartOffsetFromMainFunctionBegin = unchecked(epilogStartOffset * 4 + startOffset);
                    assert(res == 0);

                    jitprintf($"  ---- Scope {scope}\n");
                    jitprintf($"  Epilog Start Offset        : {comp.dspOffset((nint)epilogStartOffset)} " +
                        $"(0x{comp.dspOffset((nint)epilogStartOffset):x5}) " +
                        $"Actual offset = {comp.dspOffset((nint)(epilogStartOffset * 4))} " +
                        $"(0x{comp.dspOffset((nint)(epilogStartOffset * 4)):x6}) " +
                        $"Offset from main function begin = {comp.dspOffset((nint)epilogStartOffsetFromMainFunctionBegin)} " +
                        $"(0x{comp.dspOffset((nint)epilogStartOffsetFromMainFunctionBegin):x6})\n");
                    jitprintf($"  Epilog Start Index         : {epilogStartIndex} (0x{epilogStartIndex:x2})\n");

                    epilogStartAt[(int)epilogStartIndex] = true;
                }
            }
        }
        else
        {
            jitprintf($"  --- One epilog, unwind codes at {epilogCount}\n");
            assert(epilogCount < epilogStartAt.Length);
            epilogStartAt[(int)epilogCount] = true;
        }

        jitprintf("  ---- Unwind codes ----\n");
        var countOfUnwindCodes = codeWords * 4;
        var pUnwindCode = (byte*)pdw;
        for (uint i = 0; i < countOfUnwindCodes; i++)
        {
            if (epilogStartAt[(int)i])
            {
                jitprintf($"    ---- Epilog start at index {i} ----\n");
            }

            var b1 = *pUnwindCode++;
            uint x;
            uint z;
            if ((b1 & 0xE0) == 0)
            {
                x = (uint)(b1 & 0x1F);
                jitprintf($"    {b1:X2}          alloc_s #{x} (0x{x:X2}); " +
                    $"sub sp, sp, #{x * 16} (0x{x * 16:X3})\n");
            }
            else if ((b1 & 0xE0) == 0x20)
            {
                z = (uint)(b1 & 0x1F);
                jitprintf($"    {b1:X2}          save_r19r20_x #{z} (0x{z:X2}); " +
                    $"stp {REG_R19.Name}, {REG_R20.Name}, [sp, #-{z * 8}]!\n");
            }
            else if ((b1 & 0xC0) == 0x40)
            {
                z = (uint)(b1 & 0x3F);
                jitprintf($"    {b1:X2}          save_fplr #{z} (0x{z:X2}); " +
                    $"stp {REG_FP.Name}, {REG_LR.Name}, [sp, #{z * 8}]\n");
            }
            else if ((b1 & 0xC0) == 0x80)
            {
                z = (uint)(b1 & 0x3F);
                jitprintf($"    {b1:X2}          save_fplr_x #{z} (0x{z:X2}); " +
                    $"stp {REG_FP.Name}, {REG_LR.Name}, [sp, #-{(z + 1) * 8}]!\n");
            }
            else if ((b1 & 0xF8) == 0xC0)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x7) << 8) | b2;

                jitprintf($"    {b1:X2} {b2:X2}       alloc_m #{x} (0x{x:X3}); " +
                    $"sub sp, sp, #{x * 16} (0x{x * 16:X4})\n");
            }
            else if ((b1 & 0xFC) == 0xC8)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x3) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_regp X#{x} Z#{z} (0x{z:X2}); " +
                    $"stp {GetRegisterAtOffset(REG_R19, x).Name}, " +
                    $"{GetRegisterAtOffset(REG_R19, x + 1).Name}, [sp, #{z * 8}]\n");
            }
            else if ((b1 & 0xFC) == 0xCC)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x3) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_regp_x X#{x} Z#{z} (0x{z:X2}); " +
                    $"stp {GetRegisterAtOffset(REG_R19, x).Name}, " +
                    $"{GetRegisterAtOffset(REG_R19, x + 1).Name}, [sp, #-{(z + 1) * 8}]!\n");
            }
            else if ((b1 & 0xFC) == 0xD0)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x3) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_reg X#{x} Z#{z} (0x{z:X2}); " +
                    $"str {GetRegisterAtOffset(REG_R19, x).Name}, [sp, #{z * 8}]\n");
            }
            else if ((b1 & 0xFE) == 0xD4)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x1) << 3) | (uint)(b2 >> 5);
                z = (uint)(b2 & 0x1F);

                jitprintf($"    {b1:X2} {b2:X2}       save_reg_x X#{x} Z#{z} (0x{z:X2}); " +
                    $"str {GetRegisterAtOffset(REG_R19, x).Name}, [sp, #-{(z + 1) * 8}]!\n");
            }
            else if ((b1 & 0xFE) == 0xD6)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x1) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_lrpair X#{x} Z#{z} (0x{z:X2}); " +
                    $"stp {GetRegisterAtOffset(REG_R19, 2 * x).Name}, {REG_LR.Name}, [sp, #{z * 8}]\n");
            }
            else if ((b1 & 0xFE) == 0xD8)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x1) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_fregp X#{x} Z#{z} (0x{z:X2}); " +
                    $"stp {GetRegisterAtOffset(REG_V8, x).Name}, " +
                    $"{GetRegisterAtOffset(REG_V8, x + 1).Name}, [sp, #{z * 8}]\n");
            }
            else if ((b1 & 0xFE) == 0xDA)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x1) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_fregp_x X#{x} Z#{z} (0x{z:X2}); " +
                    $"stp {GetRegisterAtOffset(REG_V8, x).Name}, " +
                    $"{GetRegisterAtOffset(REG_V8, x + 1).Name}, [sp, #-{(z + 1) * 8}]!\n");
            }
            else if ((b1 & 0xFE) == 0xDC)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = ((uint)(b1 & 0x1) << 2) | (uint)(b2 >> 6);
                z = (uint)(b2 & 0x3F);

                jitprintf($"    {b1:X2} {b2:X2}       save_freg X#{x} Z#{z} (0x{z:X2}); " +
                    $"str {GetRegisterAtOffset(REG_V8, x).Name}, [sp, #{z * 8}]\n");
            }
            else if (b1 == 0xDE)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = (uint)(b2 >> 5);
                z = (uint)(b2 & 0x1F);

                jitprintf($"    {b1:X2} {b2:X2}       save_freg_x X#{x} Z#{z} (0x{z:X2}); " +
                    $"str {GetRegisterAtOffset(REG_V8, x).Name}, [sp, #-{(z + 1) * 8}]!\n");
            }
            else if (b1 == 0xE0)
            {
                assert(i + 3 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                var b3 = *pUnwindCode++;
                var b4 = *pUnwindCode++;
                i += 3;
                x = ((uint)b2 << 16) | ((uint)b3 << 8) | b4;

                jitprintf($"    {b1:X2} {b2:X2} {b3:X2} {b4:X2} alloc_l {x} (0x{x:X6}); " +
                    $"sub sp, sp, #{x * 16} ({x * 16:X6})\n");
            }
            else if (b1 == 0xE1)
            {
                jitprintf($"    {b1:X2}          set_fp; mov {REG_FP.Name}, sp\n");
            }
            else if (b1 == 0xE2)
            {
                assert(i + 1 < countOfUnwindCodes);
                var b2 = *pUnwindCode++;
                i++;
                x = b2;

                jitprintf($"    {b1:X2} {b2:X2}       add_fp {x} (0x{x:X2}); " +
                    $"add {REG_FP.Name}, sp, #{x * 8}\n");
            }
            else if (b1 == 0xE3)
            {
                jitprintf($"    {b1:X2}          nop\n");
            }
            else if (b1 == 0xE4)
            {
                jitprintf($"    {b1:X2}          end\n");
            }
            else if (b1 == 0xE5)
            {
                jitprintf($"    {b1:X2}          end_c\n");
            }
            else if (b1 == 0xE6)
            {
                jitprintf($"    {b1:X2}          save_next\n");
            }
            else if (b1 == 0xFC)
            {
                jitprintf($"    {b1:X2}          pac_sign_lr\n");
            }
            else
            {
                assert(false, "Internal error decoding unwind codes");
            }
        }

        pdw += codeWords;
        assert((byte*)pdw == pUnwindCode);
        assert((byte*)pdw == pHeader + unwindBlockSize);
        assert(xBit == 0); // Exception data, including an exception-handler RVA, is unsupported.
        jitprintf("\n");
    }
}
#endif
