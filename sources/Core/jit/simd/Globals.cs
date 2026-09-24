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
