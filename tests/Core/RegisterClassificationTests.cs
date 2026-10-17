// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using System.Collections.Generic;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RegisterClassificationTests
{
    private static IEnumerable<TestCaseData> FloatingRegisterTypes()
    {
        yield return new TestCaseData(TYP_UNDEF, false);
        yield return new TestCaseData(TYP_VOID, false);
        yield return new TestCaseData(TYP_BYTE, false);
        yield return new TestCaseData(TYP_INT, false);
        yield return new TestCaseData(TYP_LONG, false);
        yield return new TestCaseData(TYP_STRUCT, false);
        yield return new TestCaseData(TYP_REF, false);
        yield return new TestCaseData(TYP_BYREF, false);
        yield return new TestCaseData(TYP_FLOAT, true);
        yield return new TestCaseData(TYP_DOUBLE, true);

        if (Configuration.FEATURE_SIMD)
        {
            yield return new TestCaseData(TYP_SIMD8, true);
            yield return new TestCaseData(TYP_SIMD12, true);
            yield return new TestCaseData(TYP_SIMD16, true);

            if (Configuration.TARGET_XARCH)
            {
                yield return new TestCaseData(TYP_SIMD32, true);
                yield return new TestCaseData(TYP_SIMD64, true);
            }
            else if (Configuration.TARGET_ARM64)
            {
                yield return new TestCaseData(TYP_SIMD_ARM64, true);
            }

            if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                yield return new TestCaseData(TYP_MASK, false);
            }
        }
    }

    [TestCaseSource(nameof(FloatingRegisterTypes))]
    public static void FloatingRegisterTypeUsesTheNativeRegisterBank(var_types type, bool expected)
    {
        Assert.That(isFloatRegType(type), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> RegisterCases()
    {
        if (Configuration.TARGET_XARCH)
        {
            regNumber[] byteRegisters = Configuration.TARGET_X86
                ? [REG_X86_EAX, REG_X86_ECX, REG_X86_EDX, REG_X86_EBX]
                : [REG_X64_RAX, REG_X64_RCX, REG_X64_RDX, REG_X64_RBX];
            foreach (var reg in byteRegisters)
            {
                yield return new TestCaseData(reg, true);
            }

            regNumber[] nonByteRegisters = Configuration.TARGET_X86
                ? [REG_X86_ESP, REG_X86_EBP, REG_X86_ESI, REG_X86_EDI]
                : [REG_X64_RSP, REG_X64_RBP, REG_X64_RSI, REG_X64_RDI];
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
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET);
        Assert.That(isByteReg(reg), Is.EqualTo(expected));
    }
}
