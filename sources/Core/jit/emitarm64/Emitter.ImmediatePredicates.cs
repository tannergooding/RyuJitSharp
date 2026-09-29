// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public unsafe partial class Emitter
{
    public static bool emitIns_valid_imm_for_unscaled_ldst_offset(long imm)
    {
        return (imm >= -256) && (imm <= 255);
    }

    public static bool emitIns_valid_imm_for_ldst_offset(long imm, emitAttr attr)
    {
        if (imm == 0)
        {
            return true;
        }

        if (emitIns_valid_imm_for_unscaled_ldst_offset(imm))
        {
            return true;
        }

        if (imm < 0)
        {
            return false;
        }

        var size = EA_SIZE(attr);
        assert(size is EA_1BYTE or EA_2BYTE or EA_4BYTE or EA_8BYTE or EA_16BYTE);
        var scale = System.Numerics.BitOperations.Log2((uint)size);
        var mask = (long)size - 1;
        return ((imm & mask) == 0) && ((imm >> scale) < 0x1000);
    }

    public static bool emitIns_valid_imm_for_mov(long imm, emitAttr size)
    {
        if (canEncodeHalfwordImm(imm, size))
        {
            return true;
        }

        var notOfImm = NOT_helper(imm, getBitWidth(size));
        if (canEncodeHalfwordImm(notOfImm, size))
        {
            return true;
        }

        if (canEncodeBitMaskImm(imm, size))
        {
            return true;
        }

        return false;
    }

    public static bool emitIns_valid_imm_for_movi(long imm, emitAttr elementSize)
    {
        if (elementSize is EA_8BYTE)
        {
            var value = unchecked((ulong)imm);
            while (value != 0)
            {
                var lowByte = value & 0xFF;
                if ((lowByte != 0) && (lowByte != 0xFF))
                {
                    return false;
                }

                value >>= 8;
            }

            return true;
        }

        if (canEncodeByteShiftedImm(imm, elementSize, allowMsl: true))
        {
            return true;
        }

        var complemented = NOT_helper(imm, getBitWidth(elementSize));
        return canEncodeByteShiftedImm(complemented, elementSize, allowMsl: true);
    }

    public static bool emitIns_valid_imm_for_fmov(double immDbl)
    {
        if (canEncodeFloatImm8(immDbl))
        {
            return true;
        }

        return false;
    }

    public static bool emitIns_valid_imm_for_add(long imm, emitAttr size = EA_8BYTE)
    {
        if (unsigned_abs(imm) <= 0x0fff)
        {
            return true;
        }
        else if (canEncodeWithShiftImmBy12(imm))
        {
            return true;
        }

        return false;
    }

    public static bool emitIns_valid_imm_for_cmp(long imm, emitAttr size)
    {
        return emitIns_valid_imm_for_add(imm, size);
    }

    public static bool emitIns_valid_imm_for_alu(long imm, emitAttr size)
    {
        return canEncodeBitMaskImm(imm, size);
    }

    public static bool emitIns_valid_imm_for_ccmp(long imm)
    {
        return (imm & 0x1f) == imm;
    }

    private static ulong unsigned_abs(long imm)
    {
        return imm < 0 ? unchecked(0UL - (ulong)imm) : (ulong)imm;
    }

    private static bool canEncodeWithShiftImmBy12(long imm)
    {
        if (imm < 0)
        {
            imm = unchecked(-imm);
        }

        if (imm < 0)
        {
            return false;
        }

        if ((imm & 0x0fff) != 0)
        {
            return false;
        }

        imm >>= 12;

        return imm <= 0x0fff;
    }

    private static uint getBitWidth(emitAttr size)
    {
        assert(size <= EA_8BYTE);
        return (uint)size * BITS_PER_BYTE;
    }

    private static ulong normalizeImm64(long imm, emitAttr size)
    {
        var immWidth = getBitWidth(size);
        if (immWidth < 64)
        {
            var maxVal = 1L << (int)immWidth;
            var lowBitsMask = maxVal - 1;
            var hiBitsMask = ~lowBitsMask;
            var signBitsMask = hiBitsMask | (1L << (int)(immWidth - 1));
            assert((imm < maxVal) || ((imm & signBitsMask) == signBitsMask));

            return unchecked((ulong)(imm & lowBitsMask));
        }

        return unchecked((ulong)imm);
    }

    private static long NOT_helper(long value, uint width)
    {
        assert(width <= 64);

        var mask = ulong.MaxValue >> (int)(64 - width);
        return unchecked((long)(~unchecked((ulong)value) & mask));
    }

    private static bool canEncodeByteShiftedImm(long imm, emitAttr size, bool allowMsl, byteShiftedImm* wbBSI = null)
    {
        var canEncode = false;
        var onesShift = false;
        uint bySh = 0;
        uint imm8 = 0;
        var value = normalizeImm64(imm, size);
        if (size is EA_1BYTE or EA_8BYTE)
        {
            imm8 = unchecked((uint)value);
            assert(imm8 < 0x100);
            canEncode = true;
        }
        else
        {
            assert(size is EA_2BYTE or EA_4BYTE);
            var width = size is EA_4BYTE ? 32 : 16;
            var byteCount = width / BITS_PER_BYTE;
            var mask = uint.MaxValue >> (32 - width);
            for (bySh = 0; bySh < byteCount; bySh++)
            {
                var currentMask = 0xFFu << ((int)bySh * BITS_PER_BYTE);
                var otherBits = unchecked((int)(value & (mask & ~currentMask)));
                if (otherBits == 0)
                {
                    canEncode = true;
                }

                // MOVI/MVNI allow one-filled low bytes only in the 32-bit MSL forms.
                if (allowMsl && (size == EA_4BYTE))
                {
                    if ((bySh == 1) && (otherBits == 0xFF))
                    {
                        canEncode = true;
                        onesShift = true;
                    }
                    else if ((bySh == 2) && (otherBits == 0xFFFF))
                    {
                        canEncode = true;
                        onesShift = true;
                    }
                }
                if (canEncode)
                {
                    imm8 = (uint)((value & currentMask) >> ((int)bySh * BITS_PER_BYTE)) & 0xFF;
                    break;
                }
            }
        }

        if (canEncode)
        {
            if (wbBSI != null)
            {
                wbBSI->immOnes = onesShift ? 1u : 0u;
                wbBSI->immBY = bySh;
                wbBSI->immVal = imm8;
                assert(value == emitDecodeByteShiftedImm(*wbBSI, size));
            }

            return true;
        }

        return false;
    }

    private static bool canEncodeHalfwordImm(long imm, emitAttr size, halfwordImm* wbHWI = null)
    {
        assert((size == EA_4BYTE) || (size == EA_8BYTE));

        var immWidth = size == EA_8BYTE ? 64 : 32;
        var maxHW = size == EA_8BYTE ? 4 : 2;
        var immMask = ulong.MaxValue >> (64 - immWidth);
        var value = normalizeImm64(imm, size);

        for (var hw = 0; hw < maxHW; hw++)
        {
            var curMask = 0xffffUL << (hw * 16);
            var checkBits = immMask & ~curMask;

            if ((value & checkBits) == 0)
            {
                if (wbHWI != null)
                {
                    var val = ((value & curMask) >> (hw * 16)) & 0xFFFF;
                    wbHWI->immHW = (uint)hw;
                    wbHWI->immVal = (uint)val;
                    assert(value == unchecked((ulong)emitDecodeHalfwordImm(*wbHWI, size)));
                }

                return true;
            }
        }

        return false;
    }

    private static ulong ROR_helper(ulong value, int sh, int width)
    {
        assert(width <= 64);
        assert((width == 64) || (value < (1UL << width)));
        assert(sh < width);

        var result = (value >> sh) | (value << (width - sh));
        if (width < 64)
        {
            result &= (1UL << width) - 1;
        }

        return result;
    }

    private static bool canEncodeBitMaskImm(long imm, emitAttr size, bitMaskImm* wbBMI = null)
    {
        var immWidth = getBitWidth(size);
        var maxLen = size switch
        {
            EA_1BYTE => 3,
            EA_2BYTE => 4,
            EA_4BYTE => 5,
            EA_8BYTE => 6,
            _ => 0,
        };
        assert(maxLen != 0);

        var value = normalizeImm64(imm, size);
        for (var len = 1; len <= maxLen; len++)
        {
            var elemWidth = 1 << len;
            var elemMask = ulong.MaxValue >> (64 - elemWidth);
            var tempImm = value;
            var elemVal = tempImm & elemMask;

            if ((elemVal == 0) || (elemVal == elemMask))
            {
                continue;
            }

            var checkedBits = elemWidth;
            while (checkedBits < immWidth)
            {
                tempImm >>= elemWidth;
                var nextElem = tempImm & elemMask;
                if (nextElem != elemVal)
                {
                    break;
                }

                checkedBits += elemWidth;
            }

            if (checkedBits == immWidth)
            {
                // A rotated run of ones has exactly two transitions between zero and one.
                var elemRor = ROR_helper(elemVal, 1, elemWidth);
                var elemRorXor = elemVal ^ elemRor;
                var bitCount = 0;
                var oneBit = 1UL;
                var R = (uint)elemWidth;
                uint S = 0;
                var incr = -1;

                for (var bitNum = 0; bitNum < elemWidth; bitNum++)
                {
                    if (incr == -1)
                    {
                        R--;
                    }
                    if (bitCount == 1)
                    {
                        S = unchecked(S + (uint)incr);
                    }

                    if ((oneBit & elemRorXor) != 0)
                    {
                        bitCount++;
                        if (bitCount == 1)
                        {
                            var toZeros = (oneBit & elemVal) != 0;
                            if (toZeros)
                            {
                                S = (uint)elemWidth;
                                incr = -1;
                            }
                            else
                            {
                                S = 0;
                                incr = 1;
                            }
                        }
                        else
                        {
                            incr = 0;
                            if (bitCount > 2)
                            {
                                return false;
                            }
                        }
                    }

                    oneBit <<= 1;
                }

                assert(bitCount == 2);
                if (bitCount != 2)
                {
                    return false;
                }

                assert(S > 0);
                assert(S < elemWidth);
                assert(R < elemWidth);
                if (wbBMI != null)
                {
                    S--;
                    if (len == 6)
                    {
                        wbBMI->immN = 1;
                    }
                    else
                    {
                        wbBMI->immN = 0;
                        // Above S-1, complement the unused bits and leave the
                        // element-width marker clear (emitarm64.cpp N:R:S encoding).
                        var upperBitsOfS = (uint)(64 - (1 << (len + 1)));
                        S |= upperBitsOfS;
                    }
                    wbBMI->immR = R;
                    wbBMI->immS = S;
                    assert(value == unchecked((ulong)emitDecodeBitMaskImm(*wbBMI, size)));
                }

                return true;
            }
        }

        return false;
    }

    private static bool canEncodeFloatImm8(double immDbl)
    {
        var val = immDbl;
        if (val < 0.0)
        {
            val = -val;
        }

        var exp = 0;
        while ((val < 1.0) && (exp >= -4))
        {
            val *= 2.0;
            exp--;
        }

        while ((val >= 2.0) && (exp <= 5))
        {
            val *= 0.5;
            exp++;
        }

        exp += 3;
        val *= 16.0;
        var ival = unchecked((int)val);

        if ((exp >= 0) && (exp <= 7))
        {
            if (val == (double)ival)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
