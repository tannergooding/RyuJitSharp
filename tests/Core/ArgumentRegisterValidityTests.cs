// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using System.Collections.Generic;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class ArgumentRegisterValidityTests
{
    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.C)]
    [TestCase(CorInfoCallConvExtension.Stdcall)]
    [TestCase(CorInfoCallConvExtension.Thiscall)]
    [TestCase(CorInfoCallConvExtension.Fastcall)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.FastcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.Swift)]
    public static void IntegerValidityUsesTheCompleteConventionMask(CorInfoCallConvExtension callConv)
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET);
        var arguments = fullIntArgRegMask(callConv);
        for (var reg = TargetRuntimeMetadata.IntegerRegisterFirst;
            reg <= TargetRuntimeMetadata.IntegerRegisterLast; reg++)
        {
            Assert.That(isValidIntArgReg(reg, callConv), Is.EqualTo(arguments.IsSet(reg)));
        }

        Assert.That(isValidIntArgReg(REG_ARG_0, callConv), Is.True);
        Assert.That(isValidIntArgReg(REG_ARG_1, callConv), Is.True);
        Assert.That(isValidIntArgReg(REG_STK, callConv), Is.False);
    }

    private static IEnumerable<TestCaseData> FloatingArgumentCases()
    {
        GetFloatingArgumentBounds(out var first, out var last);
        yield return new TestCaseData(first, first != REG_NA);
        yield return new TestCaseData(last, last != REG_NA);
        yield return new TestCaseData((regNumber)((int)first - 1), false);
        yield return new TestCaseData((regNumber)((int)last + 1), false);
        yield return new TestCaseData(REG_NA, false);
    }

    private static void GetFloatingArgumentBounds(out regNumber first, out regNumber last)
    {
        if (Configuration.TARGET_X86)
        {
            first = REG_X86_XMM0;
            last = REG_X86_XMM3;
            return;
        }

        var registers = FltArgRegs;
        if (registers.Length == 0)
        {
            first = REG_NA;
            last = REG_NA;
            return;
        }

        first = registers[0];
        last = registers[^1];
    }

    [TestCaseSource(nameof(FloatingArgumentCases))]
    public static void FloatingValidityPreservesInclusiveEndpointsAndRejectsNoRegister(
        regNumber reg, bool expected)
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET);
        Assert.That(isValidFloatArgReg(reg), Is.EqualTo(expected));
    }

    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.Swift)]
    public static void IntegerPredicatePreservesNativeBankLocalInput(CorInfoCallConvExtension callConv)
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET && Configuration.HAS_MORE_THAN_64_REGISTERS &&
            Configuration.FEATURE_MASKED_HW_INTRINSICS);
        var lowerArguments = fullIntArgRegMask(callConv).Lower;
        var sawOverlap = false;
        for (var reg = TargetRuntimeMetadata.MaskRegisterFirst;
            reg <= TargetRuntimeMetadata.MaskRegisterLast; reg++)
        {
            var overlaps = (reg.SingleTypeMask & lowerArguments) != 0;
            Assert.That(isValidIntArgReg(reg, callConv), Is.EqualTo(overlaps));
            sawOverlap |= overlaps;
        }

        Assert.That(sawOverlap, Is.True);
    }
}
