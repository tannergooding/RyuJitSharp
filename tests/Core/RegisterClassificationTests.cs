// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RegisterClassificationTests
{
    [TestCase(TYP_UNDEF, false)]
    [TestCase(TYP_VOID, false)]
    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_STRUCT, false)]
    [TestCase(TYP_REF, false)]
    [TestCase(TYP_BYREF, false)]
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_DOUBLE, true)]
#if FEATURE_SIMD
    [TestCase(TYP_SIMD8, true)]
    [TestCase(TYP_SIMD12, true)]
    [TestCase(TYP_SIMD16, true)]
#if TARGET_XARCH
    [TestCase(TYP_SIMD32, true)]
    [TestCase(TYP_SIMD64, true)]
#elif TARGET_ARM64
    [TestCase(TYP_SIMD, true)]
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK, false)]
#endif
#endif
    public static void FloatingRegisterTypeUsesTheNativeRegisterBank(var_types type, bool expected)
    {
        Assert.That(isFloatRegType(type), Is.EqualTo(expected));
    }

#if HAS_FIXED_REGISTER_SET
#if CPU_HAS_BYTE_REGS
    [TestCase(REG_EAX, true)]
    [TestCase(REG_ECX, true)]
    [TestCase(REG_EDX, true)]
    [TestCase(REG_EBX, true)]
    [TestCase(REG_ESP, false)]
    [TestCase(REG_EBP, false)]
    [TestCase(REG_ESI, false)]
    [TestCase(REG_EDI, false)]
    [TestCase(REG_FP_FIRST, false)]
    [TestCase(REG_STK, false)]
    [TestCase(REG_NA, false)]
    [TestCase(REG_COUNT, false)]
    [TestCase(unchecked((regNumber)255), false)]
    public static void ByteRegisterRestrictionEndsAtEbx(regNumber reg, bool expected)
    {
        Assert.That(isByteReg(reg), Is.EqualTo(expected));
    }
#else
    [TestCase(REG_INT_FIRST)]
    [TestCase(REG_INT_LAST)]
    [TestCase(REG_FP_FIRST)]
    [TestCase(REG_FP_LAST)]
    [TestCase(REG_STK)]
    [TestCase(REG_NA)]
    [TestCase(REG_COUNT)]
    [TestCase(unchecked((regNumber)255))]
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(REG_MASK_FIRST)]
    [TestCase(REG_MASK_LAST)]
#endif
    public static void UnrestrictedTargetsDoNotTurnThePredicateIntoRegisterValidation(regNumber reg)
    {
        Assert.That(isByteReg(reg), Is.True);
    }
#endif
#endif
}
