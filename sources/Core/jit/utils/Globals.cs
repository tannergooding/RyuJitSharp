// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using static RyuJitSharp.ICorDebugInfo;

namespace RyuJitSharp;

public partial class Globals
{
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
    public static unsafe bool AreContiguous<TEnum>(TEnum value1, TEnum value2)
        where TEnum : unmanaged, Enum
    {
        if (sizeof(TEnum) == sizeof(byte))
        {
            return ((byte)(object)(value1) + 1) == (byte)(object)(value2);
        }
        else if (sizeof(TEnum) == sizeof(short))
        {
            return ((short)(object)(value1) + 1) == (short)(object)(value2);
        }
        else if (sizeof(TEnum) == sizeof(int))
        {
            return ((int)(object)(value1) + 1) == (int)(object)(value2);
        }
        else
        {
            return ((long)(object)(value1) + 1) == (long)(object)(value2);
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
        var approx = double.Log10(value);
        return (int)(double.Ceiling(approx)) + 1;
    }

    public static void dspRegMask(regMaskTP regMask, nint minSiz = 0)
    {
        var sep = "";
        jitprintf("[");

        sep = dspRegRange(regMask, ref minSiz, sep, REG_INT_FIRST, REG_INT_LAST);
        sep = dspRegRange(regMask, ref minSiz, sep, REG_FP_FIRST, REG_FP_LAST);
#if FEATURE_MASKED_HW_INTRINSICS
        sep = dspRegRange(regMask, ref minSiz, sep, REG_MASK_FIRST, REG_MASK_LAST);
#endif

        jitprintf("]");

        while (minSiz > 0)
        {
            jitprintf(" ");
            minSiz--;
        }
    }

    public static string dspRegRange(regMaskTP regMask, ref nint minSiz, string sep, regNumber regFirst, regNumber regLast)
    {
#if HAS_FIXED_REGISTER_SET
#if FEATURE_MASKED_HW_INTRINSICS
        assert(((regFirst is REG_INT_FIRST) && (regLast is REG_INT_LAST)) ||
               ((regFirst is REG_FP_FIRST) && (regLast is REG_FP_LAST)) ||
               ((regFirst is REG_MASK_FIRST) && (regLast is REG_MASK_LAST)));
#else
        assert(((regFirst is REG_INT_FIRST) && (regLast is REG_INT_LAST)) ||
               ((regFirst is REG_FP_FIRST) && (regLast is REG_FP_LAST)));
#endif

        if (sep.Length > 0)
        {
            // We've already printed something.
            sep = " ";
        }

        // When we start a range, remember the first register of the range, so we don't use range notation if the range contains just a single register.
        var inRegRange = false;
        var regPrev = REG_NA;
        var regHead = REG_NA;

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

#if TARGET_AMD64
                        // For AMD64, create ranges for int registers R8 through R15, but not the "old" registers.
                        if (regNum >= REG_R8)
                        {
                            regHead    = regNum;
                            inRegRange = true;
                            sep        = "-";
                        }
#elif TARGET_ARM64
                        // R17 and R28 can't be the start of a range, since the range would include TEB or FP
                        if ((regNum < REG_R17) || (regNum is >= REG_R19 and < REG_R28))
                        {
                            regHead    = regNum;
                            inRegRange = true;
                            sep        = "-";
                        }
#elif TARGET_ARM
                        if (regNum < REG_R12)
                        {
                            regHead    = regNum;
                            inRegRange = true;
                            sep        = "-";
                        }
#elif TARGET_X86
                        // No register ranges
#elif TARGET_LOONGARCH64
                        if (regNum is (>= REG_A0 and <= REG_T8))
                        {
                            regHead    = regNum;
                            inRegRange = true;
                            sep        = "-";
                        }
#elif TARGET_RISCV64
                        if (regNum is (>= REG_A0 and <= REG_A7) or REG_T0 or REG_T1 or (>= REG_T2 and <= REG_T6))
                        {
                            regHead    = regNum;
                            inRegRange = true;
                            sep        = "-";
                        }
#else
#error Unsupported or unset target architecture
#endif
                    }
                    else
                    {
                        regHead = regNum;
                        inRegRange = true;
                        sep = "-";
                    }
                }
#if TARGET_ARM64
                // R17: last register before TEB
                // R28: last register before FP
                else if ((regNum == regLast) || (regNum is REG_R17 or REG_R28))
#elif TARGET_LOONGARCH64
                else if ((regNum == regLast) || (regNum is REG_A7 or REG_T8))
#else
                else if (regNum == regLast)
#endif
                {
                    // We've already printed a register and hit the end of a range
                    var nam = regNum.Name;
                    jitprintf($"{sep}{nam}");
                    minSiz -= (sep.Length + nam.Length);

                    // No longer in the middle of a register range
                    regHead = REG_NA;
                    inRegRange = false;
                    sep = " ";
                }
            }
            else if (inRegRange)
            {
                assert(regHead != REG_NA);

                if (regPrev != regHead)
                {
                    // Close out the previous range, if it included more than one register.
                    var nam = regPrev.Name;
                    jitprintf($"{sep}{nam}");
                    minSiz -= (sep.Length + nam.Length);
                }

                regHead = REG_NA;
                inRegRange = false;
                sep = " ";
            }

            regPrev = regNum;
        }
#endif

        return sep;
    }

    public static bool FitsInI32(long value) => unchecked((int)(value)) == value;

    public static bool FitsInI32(nint value) => unchecked((int)(value)) == value;
}
