// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if HAS_FIXED_REGISTER_SET
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
        var first = TargetRuntimeMetadata.FirstFloatingArgumentRegister;
        var last = TargetRuntimeMetadata.LastFloatingArgumentRegister;
        yield return new TestCaseData(first, first != REG_NA);
        yield return new TestCaseData(last, last != REG_NA);
        yield return new TestCaseData((regNumber)((int)first - 1), false);
        yield return new TestCaseData((regNumber)((int)last + 1), false);
        yield return new TestCaseData(REG_NA, false);
    }

    [TestCaseSource(nameof(FloatingArgumentCases))]
    public static void FloatingValidityPreservesInclusiveEndpointsAndRejectsNoRegister(
        regNumber reg, bool expected)
    {
        Assert.That(isValidFloatArgReg(reg), Is.EqualTo(expected));
    }

#if HAS_MORE_THAN_64_REGISTERS && FEATURE_MASKED_HW_INTRINSICS
    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.Swift)]
    public static void IntegerPredicatePreservesNativeBankLocalInput(CorInfoCallConvExtension callConv)
    {
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
#endif
}
#endif
