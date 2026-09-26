// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private static T EvalOp<T>(VNFunc func, T value0, T value1) where T : unmanaged, INumber<T>
    {
        return EvalOpSpecialized(func, value0, value1);
    }

    private static T EvalOpSpecialized<T>(VNFunc func, T value0, T value1) where T : unmanaged, INumber<T>
    {
        if (typeof(T) == typeof(float))
        {
            var result = EvalFloatingOp(func, Unsafe.As<T, float>(ref value0), Unsafe.As<T, float>(ref value1));
            return Unsafe.As<float, T>(ref result);
        }

        if (typeof(T) == typeof(double))
        {
            var result = EvalFloatingOp(func, Unsafe.As<T, double>(ref value0), Unsafe.As<T, double>(ref value1));
            return Unsafe.As<double, T>(ref result);
        }

        if (typeof(T) == typeof(int))
        {
            var result = EvalIntegralBinaryOp(func, Unsafe.As<T, int>(ref value0), Unsafe.As<T, int>(ref value1));
            return Unsafe.As<int, T>(ref result);
        }

        if (typeof(T) == typeof(long))
        {
            var result = EvalIntegralBinaryOp(func, Unsafe.As<T, long>(ref value0), Unsafe.As<T, long>(ref value1));
            return Unsafe.As<long, T>(ref result);
        }

        if (typeof(T) == typeof(nuint))
        {
            var result = EvalIntegralBinaryOp(func, Unsafe.As<T, nuint>(ref value0), Unsafe.As<T, nuint>(ref value1));
            return Unsafe.As<nuint, T>(ref result);
        }

        throw new UnreachableException();
    }

    private static T EvalFloatingOp<T>(VNFunc func, T value0, T value1)
        where T : unmanaged, IFloatingPointIeee754<T>
    {
        if (func >= VNF_Boundary)
        {
            throw new UnreachableException();
        }

#if !TARGET_XARCH
        // The oracle's FpAdd/FpSub/FpMul/FpDiv use the target's default NaN for invalid infinities.
        if ((func == VNF_ADD) && T.IsInfinity(value0) && T.IsInfinity(value1) &&
            (T.IsNegative(value0) != T.IsNegative(value1)))
        {
            return TargetNaN<T>();
        }

        if ((func == VNF_SUB) && T.IsInfinity(value0) && T.IsInfinity(value1) &&
            (T.IsNegative(value0) == T.IsNegative(value1)))
        {
            return TargetNaN<T>();
        }

        if ((func == VNF_MUL) && ((T.IsZero(value0) && T.IsInfinity(value1)) ||
            (T.IsInfinity(value0) && T.IsZero(value1))))
        {
            return TargetNaN<T>();
        }

        if ((func == VNF_DIV) && ((T.IsZero(value0) && T.IsZero(value1)) ||
            (T.IsInfinity(value0) && T.IsInfinity(value1))))
        {
            return TargetNaN<T>();
        }
#endif

        switch (func)
        {
            case VNF_ADD:
            {
                return value0 + value1;
            }

            case VNF_SUB:
            {
                return value0 - value1;
            }

            case VNF_MUL:
            {
                return value0 * value1;
            }

            case VNF_DIV:
            {
                return value0 / value1;
            }

            case VNF_MOD:
            {
                // Native FpRem uses fmod(double, double) even for float arguments.
                if (T.IsZero(value1) || !T.IsFinite(value0))
                {
                    return TargetNaN<T>();
                }

                if (T.IsInfinity(value1))
                {
                    return value0;
                }

                return T.CreateTruncating(double.CreateTruncating(value0) % double.CreateTruncating(value1));
            }

            default:
            {
                throw new UnreachableException();
            }
        }
    }

    private static T TargetNaN<T>() where T : unmanaged, IFloatingPointIeee754<T>
    {
#if TARGET_XARCH
        const int floatBits = unchecked((int)0xFFC00000);
        const long doubleBits = unchecked((long)0xFFF8000000000000);
#else
        const int floatBits = 0x7FC00000;
        const long doubleBits = 0x7FF8000000000000;
#endif
        if (typeof(T) == typeof(float))
        {
            var result = BitConverter.Int32BitsToSingle(floatBits);
            return Unsafe.As<float, T>(ref result);
        }

        if (typeof(T) == typeof(double))
        {
            var result = BitConverter.Int64BitsToDouble(doubleBits);
            return Unsafe.As<double, T>(ref result);
        }

        throw new UnreachableException();
    }

    private static T EvalIntegralBinaryOp<T>(VNFunc func, T value0, T value1)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        assert(Unsafe.SizeOf<T>() is 4 or 8);

        if (func < VNF_Boundary)
        {
            var shift = int.CreateTruncating(value1);
            switch (func)
            {
                case VNF_ADD:
                {
                    return unchecked(value0 + value1);
                }

                case VNF_SUB:
                {
                    return unchecked(value0 - value1);
                }

                case VNF_MUL:
                {
                    return unchecked(value0 * value1);
                }

                case VNF_DIV:
                case VNF_MOD:
                {
                    assert(value1 != T.Zero);
                    assert(!T.IsNegative(T.MinValue) || (value0 != T.MinValue) ||
                        (value1 != T.CreateTruncating(-1)));
                    return func == VNF_DIV ? value0 / value1 : value0 % value1;
                }

                case VNF_UDIV:
                case VNF_UMOD:
                {
                    assert(value1 != T.Zero);
                    if (Unsafe.SizeOf<T>() == 4)
                    {
                        var left = uint.CreateTruncating(value0);
                        var right = uint.CreateTruncating(value1);
                        return T.CreateTruncating(func == VNF_UDIV ? left / right : left % right);
                    }

                    var left64 = ulong.CreateTruncating(value0);
                    var right64 = ulong.CreateTruncating(value1);
                    return T.CreateTruncating(func == VNF_UDIV ? left64 / right64 : left64 % right64);
                }

                case VNF_AND:
                {
                    return value0 & value1;
                }

                case VNF_OR:
                {
                    return value0 | value1;
                }

                case VNF_XOR:
                {
                    return value0 ^ value1;
                }

                case VNF_LSH:
                {
                    return value0 << shift;
                }

                case VNF_RSH:
                {
                    return value0 >> shift;
                }

                case VNF_RSZ:
                {
                    return Unsafe.SizeOf<T>() == 4
                        ? T.CreateTruncating(uint.CreateTruncating(value0) >> shift)
                        : T.CreateTruncating(ulong.CreateTruncating(value0) >> shift);
                }

                case VNF_ROL:
                {
                    // On Windows x64 the native shift pair uses the low five/six count bits;
                    // importer/morph also mask rotate counts before creating these nodes.
                    return Unsafe.SizeOf<T>() == 4
                        ? T.CreateTruncating(BitOperations.RotateLeft(uint.CreateTruncating(value0), shift))
                        : T.CreateTruncating(BitOperations.RotateLeft(ulong.CreateTruncating(value0), shift));
                }

                case VNF_ROR:
                {
                    return Unsafe.SizeOf<T>() == 4
                        ? T.CreateTruncating(BitOperations.RotateRight(uint.CreateTruncating(value0), shift))
                        : T.CreateTruncating(BitOperations.RotateRight(ulong.CreateTruncating(value0), shift));
                }
            }
        }
        else
        {
            bool valid;
            if (typeof(T) == typeof(int))
            {
                var left = Unsafe.As<T, int>(ref value0);
                var right = Unsafe.As<T, int>(ref value1);
                valid = func switch {
                    VNF_ADD_OVF => CheckedOps.TryAdd(left, right, out _),
                    VNF_ADD_UN_OVF => CheckedOps.TryAddUns(left, right, out _),
                    VNF_SUB_OVF => CheckedOps.TrySub(left, right, out _),
                    VNF_SUB_UN_OVF => CheckedOps.TrySubUns(left, right, out _),
                    VNF_MUL_OVF => CheckedOps.TryMul(left, right, out _),
                    VNF_MUL_UN_OVF => CheckedOps.TryMulUns(left, right, out _),
                    _ => throw new UnreachableException(),
                };
            }
            else if (typeof(T) == typeof(long))
            {
                var left = Unsafe.As<T, long>(ref value0);
                var right = Unsafe.As<T, long>(ref value1);
                valid = func switch {
                    VNF_ADD_OVF => CheckedOps.TryAdd(left, right, out _),
                    VNF_ADD_UN_OVF => CheckedOps.TryAddUns(left, right, out _),
                    VNF_SUB_OVF => CheckedOps.TrySub(left, right, out _),
                    VNF_SUB_UN_OVF => CheckedOps.TrySubUns(left, right, out _),
                    VNF_MUL_OVF => CheckedOps.TryMul(left, right, out _),
                    VNF_MUL_UN_OVF => CheckedOps.TryMulUns(left, right, out _),
                    _ => throw new UnreachableException(),
                };
            }
            else
            {
                throw new UnreachableException();
            }

            assert(valid);
            switch (func)
            {
                case VNF_ADD_OVF:
                case VNF_ADD_UN_OVF:
                {
                    return unchecked(value0 + value1);
                }

                case VNF_SUB_OVF:
                case VNF_SUB_UN_OVF:
                {
                    return unchecked(value0 - value1);
                }

                case VNF_MUL_OVF:
                case VNF_MUL_UN_OVF:
                {
                    return unchecked(value0 * value1);
                }
            }
        }

        throw new UnreachableException();
    }

    private static int EvalComparison<T>(VNFunc func, T value0, T value1) where T : unmanaged, INumber<T>
    {
        if (typeof(T) == typeof(float))
        {
            return EvalFloatingComparison(func, Unsafe.As<T, float>(ref value0), Unsafe.As<T, float>(ref value1));
        }

        if (typeof(T) == typeof(double))
        {
            return EvalFloatingComparison(func, Unsafe.As<T, double>(ref value0), Unsafe.As<T, double>(ref value1));
        }

        if (typeof(T) == typeof(int))
        {
            return EvalIntegralComparison(func, Unsafe.As<T, int>(ref value0), Unsafe.As<T, int>(ref value1));
        }

        if (typeof(T) == typeof(long))
        {
            return EvalIntegralComparison(func, Unsafe.As<T, long>(ref value0), Unsafe.As<T, long>(ref value1));
        }

        if (typeof(T) == typeof(nuint))
        {
            return EvalIntegralComparison(func, Unsafe.As<T, nuint>(ref value0), Unsafe.As<T, nuint>(ref value1));
        }

        throw new UnreachableException();
    }

    private static int EvalFloatingComparison<T>(VNFunc func, T value0, T value1)
        where T : unmanaged, IFloatingPointIeee754<T>
    {
        if (T.IsNaN(value0) || T.IsNaN(value1))
        {
            // Native treats every VNF_-range comparison as unordered.
            return func < VNF_Boundary ? (func == VNF_NE ? 1 : 0) : 1;
        }

        return EvalOrderedComparison(func, value0, value1);
    }

    private static int EvalIntegralComparison<T>(VNFunc func, T value0, T value1)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (func >= VNF_Boundary)
        {
            if (Unsafe.SizeOf<T>() == 4)
            {
                return EvalUnsignedComparison(func, uint.CreateTruncating(value0), uint.CreateTruncating(value1));
            }

            return EvalUnsignedComparison(func, ulong.CreateTruncating(value0), ulong.CreateTruncating(value1));
        }

        return EvalOrderedComparison(func, value0, value1);
    }

    private static int EvalUnsignedComparison<T>(VNFunc func, T value0, T value1) where T : INumber<T>
    {
        return func switch {
            VNF_GT_UN => value0 > value1 ? 1 : 0,
            VNF_GE_UN => value0 >= value1 ? 1 : 0,
            VNF_LT_UN => value0 < value1 ? 1 : 0,
            VNF_LE_UN => value0 <= value1 ? 1 : 0,
            _ => throw new UnreachableException(),
        };
    }

    private static int EvalOrderedComparison<T>(VNFunc func, T value0, T value1) where T : INumber<T>
    {
        return func switch {
            VNF_EQ => value0 == value1 ? 1 : 0,
            VNF_NE => value0 != value1 ? 1 : 0,
            VNF_GT or VNF_GT_UN => value0 > value1 ? 1 : 0,
            VNF_GE or VNF_GE_UN => value0 >= value1 ? 1 : 0,
            VNF_LT or VNF_LT_UN => value0 < value1 ? 1 : 0,
            VNF_LE or VNF_LE_UN => value0 <= value1 ? 1 : 0,
            _ => throw new UnreachableException(),
        };
    }
}
