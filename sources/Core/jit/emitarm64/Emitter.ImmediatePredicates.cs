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
        return TargetCostEstimation.IsArm64MoveImmediate(imm, size);
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
        return TargetCostEstimation.IsArm64FloatImmediate(immDbl);
    }

    public static bool emitIns_valid_imm_for_add(long imm, emitAttr size = EA_8BYTE)
    {
        return TargetCostEstimation.IsArm64AddImmediate(imm);
    }

    public static bool emitIns_valid_imm_for_cmp(long imm, emitAttr size)
    {
        return emitIns_valid_imm_for_add(imm, size);
    }

    public static bool emitIns_valid_imm_for_alu(long imm, emitAttr size)
    {
        return canEncodeBitMaskImm(imm, size);
    }

    private static ulong unsigned_abs(long imm)
    {
        return imm < 0 ? unchecked(0UL - (ulong)imm) : (ulong)imm;
    }

    private static bool canEncodeWithShiftImmBy12(long imm)
    {
        return TargetCostEstimation.CanEncodeArm64ShiftedAddImmediate(imm);
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
        var canEncode = TargetCostEstimation.TryEncodeArm64HalfwordImmediate(
            imm,
            size,
            out var halfwordIndex,
            out var immediateValue);
        if (canEncode && (wbHWI != null))
        {
            wbHWI->immHW = halfwordIndex;
            wbHWI->immVal = immediateValue;
            assert(normalizeImm64(imm, size) == unchecked((ulong)emitDecodeHalfwordImm(*wbHWI, size)));
        }

        return canEncode;
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
        var canEncode = TargetCostEstimation.TryEncodeArm64BitMaskImmediate(
            imm,
            size,
            out var immediateN,
            out var immediateR,
            out var immediateS);
        if (canEncode && (wbBMI != null))
        {
            wbBMI->immN = immediateN;
            wbBMI->immR = immediateR;
            wbBMI->immS = immediateS;
            assert(normalizeImm64(imm, size) == unchecked((ulong)emitDecodeBitMaskImm(*wbBMI, size)));
        }

        return canEncode;
    }

    private static bool canEncodeFloatImm8(double immDbl, floatImm8* wbFPI = null)
    {
        var canEncode = TargetCostEstimation.TryEncodeArm64FloatImmediate(
            immDbl,
            out var sign,
            out var exponent,
            out var mantissa);
        if (canEncode && (wbFPI != null))
        {
            wbFPI->immSign = sign;
            wbFPI->immExp = exponent;
            wbFPI->immMant = mantissa;
            var imm8 = wbFPI->immFPIVal;
            assert((imm8 >= 0) && (imm8 <= 0xff));
        }

        return canEncode;
    }
}
#endif
