// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using System.Collections.Generic;
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
    [TestCase(TYP_SIMD_ARM64, true)]
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
    private static IEnumerable<TestCaseData> RegisterCases()
    {
        if (Configuration.TARGET_XARCH)
        {
            var byteRegisters = Configuration.TARGET_X86
                ? new[] { REG_X86_EAX, REG_X86_ECX, REG_X86_EDX, REG_X86_EBX }
                : new[] { REG_X64_RAX, REG_X64_RCX, REG_X64_RDX, REG_X64_RBX };
            foreach (var reg in byteRegisters)
            {
                yield return new TestCaseData(reg, true);
            }

            var nonByteRegisters = Configuration.TARGET_X86
                ? new[] { REG_X86_ESP, REG_X86_EBP, REG_X86_ESI, REG_X86_EDI }
                : new[] { REG_X64_RSP, REG_X64_RBP, REG_X64_RSI, REG_X64_RDI };
            foreach (var reg in nonByteRegisters)
            {
                yield return new TestCaseData(reg, false);
            }

            yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterFirst, false);
        }
        else
        {
            yield return new TestCaseData(TargetRuntimeMetadata.IntegerRegisterFirst, true);
            yield return new TestCaseData(TargetRuntimeMetadata.IntegerRegisterLast, true);
            yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterFirst, true);
            yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterLast, true);
        }

        yield return new TestCaseData(REG_STK, !Configuration.TARGET_XARCH);
        yield return new TestCaseData(REG_NA, !Configuration.TARGET_XARCH);
        yield return new TestCaseData(REG_COUNT, !Configuration.TARGET_XARCH);
        yield return new TestCaseData(unchecked((regNumber)255), !Configuration.TARGET_XARCH);
    }

    [TestCaseSource(nameof(RegisterCases))]
    public static void ByteRegisterClassificationUsesTheRuntimeTargetPolicy(regNumber reg, bool expected)
    {
        Assert.That(isByteReg(reg), Is.EqualTo(expected));
    }
#endif
}
