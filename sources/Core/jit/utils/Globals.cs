// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using static RyuJitSharp.ICorDebugInfo;

namespace RyuJitSharp;

public partial class Globals
{
    public static bool HasPreciseReciprocal(double value)
    {
        // Denormal inputs depend on the floating-point environment. Native
        // strength reduction admits normal powers of two, excluding +/-1.
        if (!double.IsNormal(value))
        {
            return false;
        }

        var bits = BitConverter.DoubleToUInt64Bits(value);
        var exponent = (bits >> 52) & 0x7FF;
        var mantissa = bits & 0xFFFFFFFFFFFFF;

        return (mantissa == 0) && (exponent != 0) && (exponent != 1023);
    }

    public static bool HasPreciseReciprocal(float value)
    {
        if (!float.IsNormal(value))
        {
            return false;
        }

        var bits = BitConverter.SingleToUInt32Bits(value);
        var exponent = (bits >> 23) & 0xFF;
        var mantissa = bits & 0x7FFFFF;

        return (mantissa == 0) && (exponent != 0) && (exponent != 127);
    }

    private static ReadOnlySpan<int> PowersOf10 => [
        1,
        10,
        100,
        1_000,
        10_000,
        100_000,
        1_000_000,
        10_000_000,
        100_000_000,
        1_000_000_000,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreContiguous<TEnum>(TEnum value1, TEnum value2)
        where TEnum : unmanaged, Enum
    {
        switch (Type.GetTypeCode(typeof(TEnum)))
        {
            case TypeCode.SByte:
                return (Unsafe.BitCast<TEnum, sbyte>(value1) + 1) == Unsafe.BitCast<TEnum, sbyte>(value2);
            case TypeCode.Byte:
                return (Unsafe.BitCast<TEnum, byte>(value1) + 1) == Unsafe.BitCast<TEnum, byte>(value2);
            case TypeCode.Int16:
                return (Unsafe.BitCast<TEnum, short>(value1) + 1) == Unsafe.BitCast<TEnum, short>(value2);
            case TypeCode.UInt16:
                return (Unsafe.BitCast<TEnum, ushort>(value1) + 1) == Unsafe.BitCast<TEnum, ushort>(value2);
            case TypeCode.Int32:
                return (Unsafe.BitCast<TEnum, int>(value1) + 1) == Unsafe.BitCast<TEnum, int>(value2);
            case TypeCode.UInt32:
                return unchecked(Unsafe.BitCast<TEnum, uint>(value1) + 1) == Unsafe.BitCast<TEnum, uint>(value2);
            case TypeCode.Int64:
                return (Unsafe.BitCast<TEnum, long>(value1) + 1) == Unsafe.BitCast<TEnum, long>(value2);
            case TypeCode.UInt64:
                return unchecked(Unsafe.BitCast<TEnum, ulong>(value1) + 1) == Unsafe.BitCast<TEnum, ulong>(value2);
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
        }
    }

    public static bool AreContiguous<TEnum>(params ReadOnlySpan<TEnum> values)
        where TEnum : unmanaged, Enum
    {
        var areContiguous = true;

        if (values.Length >= 2)
        {
            var previousValue = values[0];

            for (var i = 1; i < values.Length; i++)
            {
                var value = values[i];

                if (!AreContiguous(previousValue, value))
                {
                    areContiguous = false;
                    break;
                }

                previousValue = value;
            }
        }

        return areContiguous;
    }

    public static int CountDigits(int value)
    {
        // Use Log2 to get approximate Log10 via the relationship:
        // log10(x) ≈ (log2(x) + 1) * 1233 >> 12
        // Then correct with a powers-of-10 lookup table.
        // http://graphics.stanford.edu/~seander/bithacks.html#IntegerLog10

        value = (value < 0) ? -value : value | 1;
        value = (value < 0) ? int.MaxValue : value;

        var approx = ((int.Log2(value) + 1) * 1233) >>> 12;
        return (value < PowersOf10[approx]) ? approx : approx + 1;
    }

    public static int CountDigits(double value)
    {
        var count = 1;
        while (value >= 10)
        {
            value /= 10;
            count++;
        }

        return count;
    }

    public static void dspRegMask(regMaskTP regMask, nint minSiz = 0)
    {
        var sep = "";
        jitprintf("[");

        sep = dspRegRange(regMask, ref minSiz, sep,
            TargetRuntimeMetadata.IntegerRegisterFirst, TargetRuntimeMetadata.IntegerRegisterLast);
        sep = dspRegRange(regMask, ref minSiz, sep,
            TargetRuntimeMetadata.FloatingRegisterFirst, TargetRuntimeMetadata.FloatingRegisterLast);
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            _ = dspRegRange(regMask, ref minSiz, sep,
                TargetRuntimeMetadata.MaskRegisterFirst, TargetRuntimeMetadata.MaskRegisterLast);
        }

        jitprintf("]");

        while (minSiz > 0)
        {
            jitprintf(" ");
            minSiz--;
        }
    }

    public static string dspRegRange(regMaskTP regMask, ref nint minSiz, string sep, regNumber regFirst, regNumber regLast)
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            return sep;
        }

        var isIntegerRegisterRange = (regFirst == TargetRuntimeMetadata.IntegerRegisterFirst) &&
            (regLast == TargetRuntimeMetadata.IntegerRegisterLast);
        var isFloatingRegisterRange = (regFirst == TargetRuntimeMetadata.FloatingRegisterFirst) &&
            (regLast == TargetRuntimeMetadata.FloatingRegisterLast);
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            var isMaskRegisterRange = (regFirst == TargetRuntimeMetadata.MaskRegisterFirst) &&
                (regLast == TargetRuntimeMetadata.MaskRegisterLast);
            assert(isIntegerRegisterRange || isFloatingRegisterRange || isMaskRegisterRange);
        }
        else
        {
            assert(isIntegerRegisterRange || isFloatingRegisterRange);
        }

        if (sep.Length > 0)
        {
            // We've already printed something.
            sep = " ";
        }

        // When we start a range, remember the first register of the range, so we don't use range notation if the range contains just a single register.
        const regNumber invalidRegister = unchecked((regNumber)uint.MaxValue);
        var inRegRange = false;
        var regPrev = invalidRegister;
        var regHead = invalidRegister;

        for (var regNum = regFirst; regNum <= regLast; regNum = REG_NEXT(regNum))
        {
            if (regMask.IsSet(regNum))
            {
                // We have a register to display. It gets displayed now if:
                // 1. This is the first register to display of a new range of registers (possibly because
                //    no register has ever been displayed).
                // 2. This is the last register of an acceptable range (either the last register of a type,
                //    or the last of a range that is displayed with range notation).
                if (!inRegRange)
                {
                    // It's the first register of a potential range.
                    var nam = regNum.Name;
                    jitprintf($"{sep}{nam}");
                    minSiz -= (sep.Length + nam.Length);

                    // What kind of separator should we use for this range (if it is indeed going to be a range)?
                    if (genIsValidIntReg(regNum))
                    {
                        // By default, we're not starting a potential register range.
                        sep = " ";

                        if (Configuration.TARGET_AMD64)
                        {
                            // For AMD64, create ranges for int registers R8 through R15, but not the "old" registers.
                            if (regNum >= REG_X64_R8)
                            {
                                regHead = regNum;
                                inRegRange = true;
                                sep = "-";
                            }
                        }
                        else if (Configuration.TARGET_ARM64)
                        {
                            // R17 and R28 can't be the start of a range, since the range would include TEB or FP.
                            if ((regNum < REG_ARM64_R17) || (regNum is >= REG_ARM64_R19 and < REG_ARM64_R28))
                            {
                                regHead = regNum;
                                inRegRange = true;
                                sep = "-";
                            }
                        }
                        else if (Configuration.TARGET_ARM)
                        {
                            if (regNum < REG_ARM32_R12)
                            {
                                regHead = regNum;
                                inRegRange = true;
                                sep = "-";
                            }
                        }
                        else if (Configuration.TARGET_LOONGARCH64)
                        {
                            if (regNum is (>= REG_LOONGARCH64_A0 and <= REG_LOONGARCH64_T8))
                            {
                                regHead = regNum;
                                inRegRange = true;
                                sep = "-";
                            }
                        }
                        else if (Configuration.TARGET_RISCV64)
                        {
                            if (regNum is (>= REG_RISCV64_A0 and <= REG_RISCV64_A7) or REG_RISCV64_T0 or
                                REG_RISCV64_T1 or (>= REG_RISCV64_T2 and <= REG_RISCV64_T6))
                            {
                                regHead = regNum;
                                inRegRange = true;
                                sep = "-";
                            }
                        }
                        else if (!Configuration.TARGET_X86)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterRangeFormattingIsNotSupportedForThisTarget);
                            return default!;
                        }
                    }
                    else
                    {
                        regHead = regNum;
                        inRegRange = true;
                        sep = "-";
                    }
                }
                else if ((regNum == regLast) ||
                    (Configuration.TARGET_ARM64 && (regNum is REG_ARM64_R17 or REG_ARM64_R28)) ||
                    (Configuration.TARGET_LOONGARCH64 &&
                        (regNum is REG_LOONGARCH64_A7 or REG_LOONGARCH64_T8)))
                {
                    // We've already printed a register and hit the end of a range
                    var nam = regNum.Name;
                    jitprintf($"{sep}{nam}");
                    minSiz -= (sep.Length + nam.Length);

                    // No longer in the middle of a register range
                    regHead = invalidRegister;
                    inRegRange = false;
                    sep = " ";
                }
            }
            else if (inRegRange)
            {
                assert(regHead != invalidRegister);

                if (regPrev != regHead)
                {
                    // Close out the previous range, if it included more than one register.
                    var nam = regPrev.Name;
                    jitprintf($"{sep}{nam}");
                    minSiz -= (sep.Length + nam.Length);
                }

                regHead = invalidRegister;
                inRegRange = false;
                sep = " ";
            }

            regPrev = regNum;
        }
        return sep;
    }

    public static bool FitsIn<T>(var_types type, T value)
        where T : IBinaryInteger<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)) ||
               (typeof(T) == typeof(nint)) || (typeof(T) == typeof(nuint)) ||
               (typeof(T) == typeof(uint)) || (typeof(T) == typeof(ulong)));

        // Saturating the destination bounds to T intersects the two types' ranges.
        switch (type)
        {
            case TYP_BYTE:
                return (value >= T.CreateSaturating(sbyte.MinValue)) && (value <= T.CreateSaturating(sbyte.MaxValue));
            case TYP_UBYTE:
                return (value >= T.Zero) && (value <= T.CreateSaturating(byte.MaxValue));
            case TYP_SHORT:
                return (value >= T.CreateSaturating(short.MinValue)) && (value <= T.CreateSaturating(short.MaxValue));
            case TYP_USHORT:
                return (value >= T.Zero) && (value <= T.CreateSaturating(ushort.MaxValue));
            case TYP_INT:
                return (value >= T.CreateSaturating(int.MinValue)) && (value <= T.CreateSaturating(int.MaxValue));
            case TYP_UINT:
                return (value >= T.Zero) && (value <= T.CreateSaturating(uint.MaxValue));
            case TYP_LONG:
                return (value >= T.CreateSaturating(long.MinValue)) && (value <= T.CreateSaturating(long.MaxValue));
            case TYP_ULONG:
                return (value >= T.Zero) && (value <= T.CreateSaturating(ulong.MaxValue));
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
        }
    }

    public static bool FitsInI8(long value) => unchecked((sbyte)(value)) == value;

    public static bool FitsInI8(nint value) => unchecked((sbyte)(value)) == value;

    public static bool FitsInI32(long value) => unchecked((int)(value)) == value;

    public static bool FitsInI32(nint value) => unchecked((int)(value)) == value;
}
