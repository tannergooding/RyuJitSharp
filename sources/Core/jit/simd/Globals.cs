// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public static partial class Globals
{
#if FEATURE_SIMD
    public static bool IsUnaryBitwiseOperation(genTreeOps oper) => oper is GT_LZCNT or GT_NOT;

    public static T EvaluateUnaryScalar<T>(genTreeOps oper, T value) where T : unmanaged, IBinaryInteger<T>
    {
        switch (oper)
        {
            case GT_NEG:
            {
                return unchecked(-value);
            }

            case GT_NOT:
            {
                return ~value;
            }

            case GT_LZCNT:
            {
                if (Unsafe.SizeOf<T>() == sizeof(uint))
                {
                    return T.CreateTruncating(BitOperations.LeadingZeroCount(uint.CreateTruncating(value)));
                }

                if (Unsafe.SizeOf<T>() == sizeof(ulong))
                {
                    return T.CreateTruncating(BitOperations.LeadingZeroCount(ulong.CreateTruncating(value)));
                }

                break;
            }
        }

        unreached();
        return T.Zero;
    }

    public static float EvaluateUnaryScalar(genTreeOps oper, float value)
    {
        if (oper == GT_NEG)
        {
            return -value;
        }

        unreached();
        return 0;
    }

    public static double EvaluateUnaryScalar(genTreeOps oper, double value)
    {
        if (oper == GT_NEG)
        {
            return -value;
        }

        unreached();
        return 0;
    }

    public static void EvaluateUnarySimd(genTreeOps oper, bool scalar, var_types baseType,
        Span<byte> result, ReadOnlySpan<byte> arg0)
    {
        assert(result.Length == arg0.Length);
        if (scalar)
        {
#if TARGET_XARCH
            arg0.CopyTo(result);
#elif TARGET_ARM64
            result.Clear();
#endif
        }

        // Do not load signaling NaNs as floating point for bitwise operations.
        if (IsUnaryBitwiseOperation(oper))
        {
            baseType = baseType switch {
                TYP_FLOAT => TYP_INT,
                TYP_DOUBLE => TYP_LONG,
                _ => baseType,
            };
        }

        switch (baseType)
        {
            case TYP_FLOAT:
            {
                var input = MemoryMarshal.Cast<byte, float>(arg0);
                var output = MemoryMarshal.Cast<byte, float>(result);
                var count = scalar ? 1 : input.Length;
                for (var index = 0; index < count; index++)
                {
                    output[index] = EvaluateUnaryScalar(oper, input[index]);
                }

                break;
            }

            case TYP_DOUBLE:
            {
                var input = MemoryMarshal.Cast<byte, double>(arg0);
                var output = MemoryMarshal.Cast<byte, double>(result);
                var count = scalar ? 1 : input.Length;
                for (var index = 0; index < count; index++)
                {
                    output[index] = EvaluateUnaryScalar(oper, input[index]);
                }

                break;
            }

            case TYP_BYTE:
            {
                EvaluateUnaryIntegralSimd<sbyte>(oper, scalar, result, arg0);
                break;
            }

            case TYP_SHORT:
            {
                EvaluateUnaryIntegralSimd<short>(oper, scalar, result, arg0);
                break;
            }

            case TYP_INT:
            {
                EvaluateUnaryIntegralSimd<int>(oper, scalar, result, arg0);
                break;
            }

            case TYP_LONG:
            {
                EvaluateUnaryIntegralSimd<long>(oper, scalar, result, arg0);
                break;
            }

            case TYP_UBYTE:
            {
                EvaluateUnaryIntegralSimd<byte>(oper, scalar, result, arg0);
                break;
            }

            case TYP_USHORT:
            {
                EvaluateUnaryIntegralSimd<ushort>(oper, scalar, result, arg0);
                break;
            }

            case TYP_UINT:
            {
                EvaluateUnaryIntegralSimd<uint>(oper, scalar, result, arg0);
                break;
            }

            case TYP_ULONG:
            {
                EvaluateUnaryIntegralSimd<ulong>(oper, scalar, result, arg0);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    private static void EvaluateUnaryIntegralSimd<T>(genTreeOps oper, bool scalar,
        Span<byte> result, ReadOnlySpan<byte> arg0) where T : unmanaged, IBinaryInteger<T>
    {
        var input = MemoryMarshal.Cast<byte, T>(arg0);
        var output = MemoryMarshal.Cast<byte, T>(result);
        var count = scalar ? 1 : input.Length;
        for (var index = 0; index < count; index++)
        {
            output[index] = EvaluateUnaryScalar(oper, input[index]);
        }
    }
#endif

#if FEATURE_SIMD
    public static bool IsBinaryBitwiseOperation(genTreeOps oper)
    {
        return oper is GT_AND or GT_AND_NOT or GT_LSH or GT_OR or GT_OR_NOT or
            GT_ROL or GT_ROR or GT_RSH or GT_RSZ or GT_XOR or GT_XOR_NOT;
    }

    public static T EvaluateBinaryScalarRSZ<T>(T arg0, T arg1) where T : unmanaged, IBinaryInteger<T>
    {
        var width = Unsafe.SizeOf<T>() * 8;
#if TARGET_XARCH || TARGET_ARM64
        // SIMD overshifts zero the lane rather than masking the count like scalar shifts.
        if ((arg1 < T.Zero) || (arg1 >= T.CreateTruncating(width)))
        {
            return T.Zero;
        }
#else
        arg1 &= T.CreateTruncating(width - 1);
#endif

        return arg0 >>> int.CreateTruncating(arg1);
    }

    public static T EvaluateBinaryScalar<T>(genTreeOps oper, T arg0, T arg1) where T : unmanaged, IBinaryInteger<T>
    {
        var width = Unsafe.SizeOf<T>() * 8;
        switch (oper)
        {
            case GT_ADD:
            {
                return unchecked(arg0 + arg1);
            }

            case GT_DIV:
            {
                return unchecked(arg0 / arg1);
            }

            case GT_MUL:
            {
                return unchecked(arg0 * arg1);
            }

            case GT_SUB:
            {
                return unchecked(arg0 - arg1);
            }

            case GT_AND:
            {
                return arg0 & arg1;
            }

            case GT_AND_NOT:
            {
                return arg0 & ~arg1;
            }

            case GT_EQ:
            {
                return arg0 == arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_GT:
            {
                return arg0 > arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_GE:
            {
                return arg0 >= arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_LSH:
            {
#if TARGET_XARCH || TARGET_ARM64
                if ((arg1 < T.Zero) || (arg1 >= T.CreateTruncating(width)))
                {
                    return T.Zero;
                }
#else
                arg1 &= T.CreateTruncating(width - 1);
#endif

                return arg0 << int.CreateTruncating(arg1);
            }

            case GT_LT:
            {
                return arg0 < arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_LE:
            {
                return arg0 <= arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_NE:
            {
                return arg0 != arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_OR:
            {
                return arg0 | arg1;
            }

            case GT_OR_NOT:
            {
                return arg0 | ~arg1;
            }

            case GT_ROL:
            {
                // Normalize before using helpers that zero an over-wide shift.
                arg1 &= T.CreateTruncating(width - 1);
                return EvaluateBinaryScalar(GT_LSH, arg0, arg1) |
                    EvaluateBinaryScalarRSZ(arg0, unchecked(T.CreateTruncating(width) - arg1));
            }

            case GT_ROR:
            {
                arg1 &= T.CreateTruncating(width - 1);
                return EvaluateBinaryScalarRSZ(arg0, arg1) |
                    EvaluateBinaryScalar(GT_LSH, arg0, unchecked(T.CreateTruncating(width) - arg1));
            }

            case GT_RSH:
            {
#if TARGET_XARCH || TARGET_ARM64
                if ((arg1 < T.Zero) || (arg1 >= T.CreateTruncating(width)))
                {
                    // Two shifts give sign fill for signed lanes and zero for unsigned lanes.
                    arg0 >>= width - 1;
                    arg1 = T.One;
                }
#else
                arg1 &= T.CreateTruncating(width - 1);
#endif

                return arg0 >> int.CreateTruncating(arg1);
            }

            case GT_RSZ:
            {
                return EvaluateBinaryScalarRSZ(arg0, arg1);
            }

            case GT_XOR:
            {
                return arg0 ^ arg1;
            }

            case GT_XOR_NOT:
            {
                return arg0 ^ ~arg1;
            }
        }

        unreached();
        return T.Zero;
    }

    public static float EvaluateBinaryScalar(genTreeOps oper, float arg0, float arg1)
        => EvaluateBinaryFloatingScalar(oper, arg0, arg1);

    public static double EvaluateBinaryScalar(genTreeOps oper, double arg0, double arg1)
        => EvaluateBinaryFloatingScalar(oper, arg0, arg1);

    private static T EvaluateBinaryFloatingScalar<T>(genTreeOps oper, T arg0, T arg1)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>
    {
        switch (oper)
        {
            case GT_ADD:
            {
                return arg0 + arg1;
            }

            case GT_DIV:
            {
                return arg0 / arg1;
            }

            case GT_MUL:
            {
                return arg0 * arg1;
            }

            case GT_SUB:
            {
                return arg0 - arg1;
            }

            case GT_EQ:
            {
                return arg0 == arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_GT:
            {
                return arg0 > arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_GE:
            {
                return arg0 >= arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_LT:
            {
                return arg0 < arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_LE:
            {
                return arg0 <= arg1 ? T.AllBitsSet : T.Zero;
            }

            case GT_NE:
            {
                return arg0 != arg1 ? T.AllBitsSet : T.Zero;
            }
        }

        unreached();
        return T.Zero;
    }

    public static void EvaluateBinarySimd(genTreeOps oper, bool scalar, var_types baseType,
        Span<byte> result, ReadOnlySpan<byte> arg0, ReadOnlySpan<byte> arg1, int simdSize)
    {
        assert((result.Length == arg0.Length) && (result.Length == arg1.Length));
        assert((simdSize >= 0) && (simdSize <= result.Length) && ((simdSize % baseType.Size) == 0));
        if (scalar)
        {
#if TARGET_XARCH
            arg0.CopyTo(result);
#elif TARGET_ARM64
            result.Clear();
#endif
        }

        if (IsBinaryBitwiseOperation(oper))
        {
            baseType = baseType switch {
                TYP_FLOAT => TYP_INT,
                TYP_DOUBLE => TYP_LONG,
                _ => baseType,
            };
        }

        switch (baseType)
        {
            case TYP_FLOAT:
            {
                EvaluateBinaryFloatingSimd<float>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_DOUBLE:
            {
                EvaluateBinaryFloatingSimd<double>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_BYTE:
            {
                EvaluateBinaryIntegralSimd<sbyte>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_SHORT:
            {
                EvaluateBinaryIntegralSimd<short>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_INT:
            {
                EvaluateBinaryIntegralSimd<int>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_LONG:
            {
                EvaluateBinaryIntegralSimd<long>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_UBYTE:
            {
                EvaluateBinaryIntegralSimd<byte>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_USHORT:
            {
                EvaluateBinaryIntegralSimd<ushort>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_UINT:
            {
                EvaluateBinaryIntegralSimd<uint>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            case TYP_ULONG:
            {
                EvaluateBinaryIntegralSimd<ulong>(oper, scalar, result, arg0, arg1, simdSize);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    private static void EvaluateBinaryIntegralSimd<T>(genTreeOps oper, bool scalar,
        Span<byte> result, ReadOnlySpan<byte> arg0, ReadOnlySpan<byte> arg1, int simdSize)
        where T : unmanaged, IBinaryInteger<T>
    {
        var input0 = MemoryMarshal.Cast<byte, T>(arg0);
        var input1 = MemoryMarshal.Cast<byte, T>(arg1);
        var output = MemoryMarshal.Cast<byte, T>(result);
        var count = scalar ? 1 : simdSize / Unsafe.SizeOf<T>();
        for (var index = 0; index < count; index++)
        {
            output[index] = EvaluateBinaryScalar(oper, input0[index], input1[index]);
        }
    }

    private static void EvaluateBinaryFloatingSimd<T>(genTreeOps oper, bool scalar,
        Span<byte> result, ReadOnlySpan<byte> arg0, ReadOnlySpan<byte> arg1, int simdSize)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>
    {
        var input0 = MemoryMarshal.Cast<byte, T>(arg0);
        var input1 = MemoryMarshal.Cast<byte, T>(arg1);
        var output = MemoryMarshal.Cast<byte, T>(result);
        var count = scalar ? 1 : simdSize / Unsafe.SizeOf<T>();
        for (var index = 0; index < count; index++)
        {
            output[index] = EvaluateBinaryFloatingScalar(oper, input0[index], input1[index]);
        }
    }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
    public static void EvaluateUnaryMask(genTreeOps oper, bool scalar, var_types baseType, int simdSize,
        ref simdmask_t result, in simdmask_t arg0)
    {
        var bitMask = GetMaskEvaluationBitMask(baseType, simdSize);
        var arg0Value = (ulong)arg0.RawBits & bitMask;
        ulong resultValue = 0;
        switch (oper)
        {
            case GT_NOT:
            {
                resultValue = ~arg0Value;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        // Native mask evaluation ignores scalar and normalizes all-true to all 64 bits.
        resultValue &= bitMask;
        result.u64[0] = resultValue == bitMask ? ulong.MaxValue : resultValue;
    }

    public static void EvaluateBinaryMask(genTreeOps oper, bool scalar, var_types baseType, int simdSize,
        ref simdmask_t result, in simdmask_t arg0, in simdmask_t arg1)
    {
        var bitMask = GetMaskEvaluationBitMask(baseType, simdSize);
        var arg0Value = (ulong)arg0.RawBits & bitMask;
        var arg1Value = (ulong)arg1.RawBits & bitMask;
        ulong resultValue = 0;
        switch (oper)
        {
            case GT_AND_NOT:
            {
                resultValue = arg0Value & ~arg1Value;
                break;
            }

            case GT_AND:
            {
                resultValue = arg0Value & arg1Value;
                break;
            }

            case GT_OR:
            {
                resultValue = arg0Value | arg1Value;
                break;
            }

            case GT_XOR:
            {
                resultValue = arg0Value ^ arg1Value;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        resultValue &= bitMask;
        result.u64[0] = resultValue == bitMask ? ulong.MaxValue : resultValue;
    }

    public static void EvaluateSimdCvtMaskToVector(var_types baseType, Span<byte> result, in simdmask_t arg0)
    {
        var elementSize = GetMaskElementSize(baseType);
        assert((result.Length <= Unsafe.SizeOf<simd_t>()) && ((result.Length % elementSize) == 0));
        var count = result.Length / elementSize;
        var mask = (ulong)arg0.RawBits;
        for (var index = 0; index < count; index++)
        {
#if TARGET_XARCH
            // Xarch packs one bit per lane; ARM64 uses one bit per byte of lane storage.
            var bit = index;
#elif TARGET_ARM64
            var bit = index * elementSize;
#else
#error Unsupported platform
#endif
            var isSet = ((mask >> bit) & 1) != 0;
            result.Slice(index * elementSize, elementSize).Fill(isSet ? byte.MaxValue : (byte)0);
        }
    }

    public static void EvaluateSimdCvtVectorToMask(var_types baseType, ref simdmask_t result, ReadOnlySpan<byte> arg0)
    {
        var elementSize = GetMaskElementSize(baseType);
        assert((arg0.Length <= Unsafe.SizeOf<simd_t>()) && ((arg0.Length % elementSize) == 0));
        var count = arg0.Length / elementSize;
        ulong mask = 0;
        for (var index = 0; index < count; index++)
        {
#if TARGET_XARCH
            // Supported targets are little-endian; read the sign bit without loading floating data.
            var isSet = (arg0[((index + 1) * elementSize) - 1] & 0x80) != 0;
            var bit = index;
#elif TARGET_ARM64
            var isSet = arg0.Slice(index * elementSize, elementSize).IndexOfAnyExcept((byte)0) >= 0;
            var bit = index * elementSize;
#else
#error Unsupported platform
#endif
            if (isSet)
            {
                mask |= 1UL << bit;
            }
        }

        result.u64[0] = mask;
    }

    private static int GetMaskElementSize(var_types baseType)
    {
        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                return 1;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                return 2;
            }

            case TYP_INT:
            case TYP_UINT:
            case TYP_FLOAT:
            {
                return 4;
            }

            case TYP_LONG:
            case TYP_ULONG:
            case TYP_DOUBLE:
            {
                return 8;
            }
        }

        unreached();
        return 0;
    }

    private static ulong GetMaskEvaluationBitMask(var_types baseType, int simdSize)
    {
        var elementSize = GetMaskElementSize(baseType);
#if TARGET_XARCH
        // Mask instructions have an eight-bit minimum even for vectors with fewer lanes.
        var count = Math.Max(simdSize / elementSize, 8);
        assert(count is 8 or 16 or 32 or 64);
        return (ulong)simdmask_t.GetBitMask(count);
#elif TARGET_ARM64
        // Predicate bits are spaced by the element width, across the entire mask storage.
        switch (elementSize)
        {
            case 1:
            {
                return 0xFFFFFFFFFFFFFFFF;
            }

            case 2:
            {
                return 0x5555555555555555;
            }

            case 4:
            {
                return 0x1111111111111111;
            }

            case 8:
            {
                return 0x0101010101010101;
            }
        }

        unreached();
        return 0;
#else
#error Unsupported platform
#endif
    }
#endif

#if TARGET_XARCH
    // SSE2 Shuffle control byte to shuffle vector <W, Z, Y, X>
    // These correspond to shuffle immediate byte in shufps SSE2 instruction.
    public const byte SHUFFLE_XXXX = 0x00; // 00 00 00 00
    public const byte SHUFFLE_XXZX = 0x08; // 00 00 10 00
    public const byte SHUFFLE_XXWW = 0x0F; // 00 00 11 11
    public const byte SHUFFLE_XYZW = 0x1B; // 00 01 10 11
    public const byte SHUFFLE_YXYX = 0x44; // 01 00 01 00
    public const byte SHUFFLE_YWXZ = 0x72; // 01 11 00 10
    public const byte SHUFFLE_YWXW = 0x73; // 01 11 00 11
    public const byte SHUFFLE_YYZZ = 0x5A; // 01 01 10 10
    public const byte SHUFFLE_ZXXX = 0x80; // 10 00 00 00
    public const byte SHUFFLE_ZXXY = 0x81; // 10 00 00 01
    public const byte SHUFFLE_ZZXX = 0xA0; // 10 10 00 00
    public const byte SHUFFLE_ZWXY = 0xB1; // 10 11 00 01
    public const byte SHUFFLE_WYZX = 0xD8; // 11 01 10 00
    public const byte SHUFFLE_WWYY = 0xF5; // 11 11 01 01
#endif
}
