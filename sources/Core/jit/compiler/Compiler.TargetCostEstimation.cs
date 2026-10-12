// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

internal static class TargetCostEstimation
{
    internal const int RiscVAbsoluteMaxImmediateInstructionCount = 8;
    internal const int RiscVPreferredMaxImmediateInstructionCount = 5;

    public static bool IsArm32ModifiedImmediate(int value)
    {
        var unsignedValue = unchecked((uint)value);
        var imm8 = unsignedValue & 0xffu;
        if (imm8 == unsignedValue)
        {
            return true;
        }

        var imm32A = (imm8 << 16) | imm8;
        if (imm32A == unsignedValue)
        {
            return true;
        }

        var imm32B = imm32A << 8;
        if (imm32B == unsignedValue)
        {
            return true;
        }

        if ((imm32A | imm32B) == unsignedValue)
        {
            return true;
        }

        var mask32 = 0xffu;
        for (var encode = 31; encode >= 8; encode--)
        {
            mask32 <<= 1;
            if ((unsignedValue & ~mask32) == 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsArm32MoveImmediate(long value)
    {
        if ((value < int.MinValue) || (value > int.MaxValue))
        {
            return false;
        }

        var immediate = (int)value;
        return ((immediate & 0xffff) == immediate) ||
            IsArm32ModifiedImmediate(immediate) ||
            IsArm32ModifiedImmediate(~immediate);
    }

    public static bool IsArm32AddImmediate(long value)
    {
        if ((value < int.MinValue) || (value > int.MaxValue))
        {
            return false;
        }

        var immediate = (int)value;
        return (unchecked((uint)(immediate < 0 ? -(long)immediate : immediate)) <= 0x0fff) ||
            IsArm32ModifiedImmediate(immediate) ||
            IsArm32ModifiedImmediate(unchecked(-immediate));
    }

    public static bool IsArm64AddImmediate(long value)
    {
        if (UnsignedAbs(value) <= 0x0fff)
        {
            return true;
        }

        return CanEncodeArm64ShiftedAddImmediate(value);
    }

    public static bool IsArm64MoveImmediate(long value, emitAttr size)
    {
        if (TryEncodeArm64HalfwordImmediate(value, size, out _, out _))
        {
            return true;
        }

        var complemented = NotArm64Immediate(value, GetArm64BitWidth(size));
        if (TryEncodeArm64HalfwordImmediate(complemented, size, out _, out _))
        {
            return true;
        }

        return TryEncodeArm64BitMaskImmediate(value, size, out _, out _, out _);
    }

    public static bool IsArm64FloatImmediate(double value)
    {
        return TryEncodeArm64FloatImmediate(value, out _, out _, out _);
    }

    public static bool TryEncodeArm64HalfwordImmediate(
        long immediate,
        emitAttr size,
        out uint halfwordIndex,
        out uint immediateValue)
    {
        assert((size == EA_4BYTE) || (size == EA_8BYTE));

        halfwordIndex = 0;
        immediateValue = 0;

        var immediateWidth = size == EA_8BYTE ? 64 : 32;
        var maxHalfword = size == EA_8BYTE ? 4 : 2;
        var immediateMask = ulong.MaxValue >> (64 - immediateWidth);
        var value = NormalizeArm64Immediate(immediate, size);

        for (var halfword = 0; halfword < maxHalfword; halfword++)
        {
            var currentMask = 0xffffUL << (halfword * 16);
            var checkBits = immediateMask & ~currentMask;
            if ((value & checkBits) == 0)
            {
                halfwordIndex = (uint)halfword;
                immediateValue = (uint)((value & currentMask) >> (halfword * 16));
                return true;
            }
        }

        return false;
    }

    public static bool TryEncodeArm64BitMaskImmediate(
        long immediate,
        emitAttr size,
        out uint immediateN,
        out uint immediateR,
        out uint immediateS)
    {
        immediateN = 0;
        immediateR = 0;
        immediateS = 0;

        var immediateWidth = GetArm64BitWidth(size);
        var maxLength = size switch
        {
            EA_1BYTE => 3,
            EA_2BYTE => 4,
            EA_4BYTE => 5,
            EA_8BYTE => 6,
            _ => 0,
        };
        assert(maxLength != 0);

        var value = NormalizeArm64Immediate(immediate, size);
        for (var length = 1; length <= maxLength; length++)
        {
            var elementWidth = 1 << length;
            var elementMask = ulong.MaxValue >> (64 - elementWidth);
            var elementValue = value & elementMask;
            if ((elementValue == 0) || (elementValue == elementMask))
            {
                continue;
            }

            var checkedBits = elementWidth;
            var repeatedValue = value;
            while (checkedBits < immediateWidth)
            {
                repeatedValue >>= elementWidth;
                if ((repeatedValue & elementMask) != elementValue)
                {
                    break;
                }

                checkedBits += elementWidth;
            }

            if (checkedBits != immediateWidth)
            {
                continue;
            }

            var rotatedElement = RotateRightArm64(elementValue, 1, elementWidth);
            var transitions = elementValue ^ rotatedElement;
            var transitionCount = 0;
            var bit = 1UL;
            var rotation = (uint)elementWidth;
            uint sizeField = 0;
            var increment = -1;

            for (var bitNumber = 0; bitNumber < elementWidth; bitNumber++)
            {
                if (increment == -1)
                {
                    rotation--;
                }

                if (transitionCount == 1)
                {
                    sizeField = unchecked(sizeField + (uint)increment);
                }

                if ((bit & transitions) != 0)
                {
                    transitionCount++;
                    if (transitionCount == 1)
                    {
                        if ((bit & elementValue) != 0)
                        {
                            sizeField = (uint)elementWidth;
                            increment = -1;
                        }
                        else
                        {
                            sizeField = 0;
                            increment = 1;
                        }
                    }
                    else
                    {
                        increment = 0;
                        if (transitionCount > 2)
                        {
                            return false;
                        }
                    }
                }

                bit <<= 1;
            }

            assert(transitionCount == 2);
            if (transitionCount != 2)
            {
                return false;
            }

            assert(sizeField > 0);
            assert(sizeField < elementWidth);
            assert(rotation < elementWidth);

            sizeField--;
            if (length == 6)
            {
                immediateN = 1;
            }
            else
            {
                immediateN = 0;
                var upperBitsOfSize = (uint)(64 - (1 << (length + 1)));
                sizeField |= upperBitsOfSize;
            }

            immediateR = rotation;
            immediateS = sizeField;
            return true;
        }

        return false;
    }

    public static bool TryEncodeArm64FloatImmediate(
        double immediate,
        out uint sign,
        out uint exponent,
        out uint mantissa)
    {
        sign = 0;
        exponent = 0;
        mantissa = 0;

        var value = immediate;
        if (value < 0.0)
        {
            value = -value;
            sign = 1;
        }

        var encodedExponent = 0;
        while ((value < 1.0) && (encodedExponent >= -4))
        {
            value *= 2.0;
            encodedExponent--;
        }

        while ((value >= 2.0) && (encodedExponent <= 5))
        {
            value *= 0.5;
            encodedExponent++;
        }

        encodedExponent += 3;
        value *= 16.0;
        var integerValue = unchecked((int)value);

        if ((encodedExponent < 0) || (encodedExponent > 7) || (value != (double)integerValue))
        {
            return false;
        }

        integerValue -= 16;
        assert((integerValue >= 0) && (integerValue <= 15));
        exponent = (uint)(encodedExponent ^ 4);
        mantissa = (uint)integerValue;
        return true;
    }

    public static bool CanEncodeArm64ShiftedAddImmediate(long immediate)
    {
        if (immediate < 0)
        {
            immediate = unchecked(-immediate);
        }

        if ((immediate < 0) || ((immediate & 0x0fff) != 0))
        {
            return false;
        }

        immediate >>= 12;
        return immediate <= 0x0fff;
    }

    public static int GetRiscVLoadImmediateInstructionCount(emitAttr size, nint immediate)
    {
        assert(!EA_IS_RELOC(size));

        if ((immediate >= -2048) && (immediate <= 2047))
        {
            return 1;
        }

        assert(RiscVPreferredMaxImmediateInstructionCount <= RiscVAbsoluteMaxImmediateInstructionCount);

        var instructionCountLimit = RiscVPreferredMaxImmediateInstructionCount;
        if (JitTls.Compiler is Compiler compiler && compiler.codeGen is not null)
        {
            var emitter = compiler.GetEmitter();
            if (emitter.emitGeneratingPrologOrFuncletProlog() ||
                emitter.emitGeneratingEpilogOrFuncletEpilog())
            {
                instructionCountLimit = RiscVAbsoluteMaxImmediateInstructionCount;
            }
        }

        var immediateBits = unchecked((ulong)immediate);
        int x;
        int y;
        if ((immediateBits >> 63) != 0)
        {
            y = 63 - BitOperations.LeadingZeroCount(~immediateBits) + 1;
        }
        else
        {
            y = 63 - BitOperations.LeadingZeroCount(immediateBits) + 1;
        }

        if ((immediateBits & 1) != 0)
        {
            x = BitOperations.TrailingZeroCount(~immediateBits);
        }
        else
        {
            x = BitOperations.TrailingZeroCount(immediateBits);
        }

        var utilizeShiftRight = false;
        var cond1 = (y - x) > 31;
        if (((immediateBits >> 63) == 0) && cond1)
        {
            var shiftRightAmount = BitOperations.LeadingZeroCount(immediateBits);
            var tempImmediate = immediateBits << shiftRightAmount;
            var m = BitOperations.LeadingZeroCount(~tempImmediate);
            var b = 64 - m;
            var a = BitOperations.TrailingZeroCount(tempImmediate);
            var cond2 = (b - a) < 32;
            var cond3 = ((y - x) - (b - a)) >= 11;
            if (cond2 || cond3)
            {
                immediateBits = tempImmediate;
                y = b;
                x = a;
                utilizeShiftRight = true;
                instructionCountLimit--;
            }
        }

        assert(y >= x);
        assert((1 <= y) && (y <= 63));
        assert((1 <= x) && (x <= 63));

        if (y < 32)
        {
            y = 31;
            x = 0;
        }
        else if ((y - x) < 31)
        {
            y = x + 31;
            y = (y > 63) ? 63 : y;
        }
        else
        {
            x = y - 31;
        }

        var high32 = unchecked((uint)(unchecked((long)immediateBits) >> x)) & uint.MaxValue;
        var offset1 = immediateBits & Mask64(x);
        var offset2 = unchecked(~unchecked(offset1 - 1UL)) & Mask64(x);
        var offset = offset1;
        var isSubtractMode = false;

        if ((high32 == 0x7fffffff) && (y != 63))
        {
            var newX = x + 1;
            var newOffset1 = immediateBits & Mask64(newX);
            var newOffset2 = unchecked(~unchecked(newOffset1 - 1UL)) & Mask64(newX);
            if (newOffset2 < offset1)
            {
                x = newX;
                high32 = unchecked((uint)(unchecked((long)immediateBits) >> x)) & uint.MaxValue;
                offset2 = newOffset2;
                isSubtractMode = true;
            }
        }
        else if (offset2 < offset1)
        {
            isSubtractMode = true;
        }

        if (isSubtractMode)
        {
            offset = offset2;
            high32 = unchecked(high32 + 1) & uint.MaxValue;
        }

        var instructionCount = 0;
        var upper = (high32 >> 12) & 0x000fffff;
        var lower = high32 & 0x00000fff;
        var lowerMostSignificantBit = (lower >> 11) & 1;
        if (lowerMostSignificantBit == 1)
        {
            upper = unchecked(upper + 1) & 0x000fffff;
        }

        if (upper != 0)
        {
            instructionCount++;
        }

        if (lower != 0)
        {
            instructionCount++;
        }

        var chunkLsbPosition = (x < 11) ? 0 : (x - 11);
        var shift = (x < 11) ? x : 11;
        var chunkMask = (x < 11) ? Mask64(x) : Mask64(11);
        while (true)
        {
            var chunk = (offset >> chunkLsbPosition) & chunkMask;
            if (chunk != 0)
            {
                var leadingZerosOnChunk = 11 - (64 - BitOperations.LeadingZeroCount(chunk));
                if (leadingZerosOnChunk > 0)
                {
                    var maxAdditionalShift = (chunkLsbPosition < leadingZerosOnChunk)
                        ? chunkLsbPosition
                        : leadingZerosOnChunk;
                    chunkLsbPosition -= maxAdditionalShift;
                    shift += maxAdditionalShift;
                }

                instructionCount += 2;
                if (instructionCount > instructionCountLimit)
                {
                    break;
                }

                shift = 0;
            }

            if (chunkLsbPosition == 0)
            {
                break;
            }

            shift += (chunkLsbPosition < 11) ? chunkLsbPosition : 11;
            chunkMask = (chunkLsbPosition < 11)
                ? (chunkMask >> (11 - chunkLsbPosition))
                : Mask64(11);
            chunkLsbPosition -= (chunkLsbPosition < 11) ? chunkLsbPosition : 11;
        }

        if (shift > 0)
        {
            instructionCount++;
        }

        if (instructionCount <= instructionCountLimit)
        {
            if (utilizeShiftRight)
            {
                instructionCount++;
                assert(instructionCount < RiscVAbsoluteMaxImmediateInstructionCount);
            }

            return instructionCount;
        }

        if (EA_SIZE(size) == EA_PTRSIZE)
        {
            return -1;
        }

        assert(false, "If number of instructions exceeds MAX_NUM_OF_LOAD_IMM_INS, imm must be 8 bytes");
        return 0;
    }

    public static int SizeOfWasmSleb128(long value)
    {
        var signAdjustedValue = unchecked((ulong)(value ^ (value >> 63))) | 1UL;
        var significantBits = 1 + 6 + 64 - BitOperations.LeadingZeroCount(signAdjustedValue);
        return (significantBits * 37) >> 8;
    }

    private static ulong UnsignedAbs(long value)
    {
        return value < 0 ? unchecked(0UL - (ulong)value) : (ulong)value;
    }

    private static uint GetArm64BitWidth(emitAttr size)
    {
        assert(size <= EA_8BYTE);
        return (uint)size * BITS_PER_BYTE;
    }

    private static ulong NormalizeArm64Immediate(long immediate, emitAttr size)
    {
        var immediateWidth = GetArm64BitWidth(size);
        if (immediateWidth < 64)
        {
            var maxValue = 1L << (int)immediateWidth;
            var lowBitsMask = maxValue - 1;
            var highBitsMask = ~lowBitsMask;
            var signBitsMask = highBitsMask | (1L << (int)(immediateWidth - 1));
            assert((immediate < maxValue) || ((immediate & signBitsMask) == signBitsMask));
            return unchecked((ulong)(immediate & lowBitsMask));
        }

        return unchecked((ulong)immediate);
    }

    private static long NotArm64Immediate(long value, uint width)
    {
        assert(width <= 64);
        var mask = ulong.MaxValue >> (int)(64 - width);
        return unchecked((long)(~unchecked((ulong)value) & mask));
    }

    private static ulong RotateRightArm64(ulong value, int shift, int width)
    {
        assert(width <= 64);
        assert((width == 64) || (value < (1UL << width)));
        assert(shift < width);

        var result = (value >> shift) | (value << (width - shift));
        if (width < 64)
        {
            result &= (1UL << width) - 1;
        }

        return result;
    }

    private static ulong Mask64(int bits)
    {
        return bits == 64 ? ulong.MaxValue : ((1UL << bits) - 1);
    }
}
