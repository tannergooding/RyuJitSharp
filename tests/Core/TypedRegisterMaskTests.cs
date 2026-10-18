// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class TypedRegisterMaskTests
{
    [SetUp]
    public static void RequireFixedRegisterSet()
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET, "This fixture requires a fixed register set.");
    }

    private static IEnumerable<TestCaseData> ScalarMaskCases()
    {
        var types = new List<var_types> { TYP_INT, TYP_FLOAT, TYP_STRUCT, TYP_VOID };
        if (!Configuration.TARGET_ARM)
        {
            types.Add(TYP_DOUBLE);
        }

        for (var number = 0; number <= (int)REG_STK; number++)
        {
            foreach (var type in types)
            {
                yield return new TestCaseData((regNumber)number, type);
            }
        }
    }

    [TestCaseSource(nameof(ScalarMaskCases))]
    public static void ScalarMasksUseRawBankLocalPositionsAndIgnoreUnnormalizedTypes(regNumber reg, var_types type)
    {
        var expected = reg == REG_STK ? SRBM_NONE : unchecked((regMask)(1UL << ((int)reg % 64)));
        if (Configuration.TARGET_ARM64 && (reg is REG_ARM64_SP or REG_ARM64_FFR))
        {
            expected = SRBM_NONE;
        }

        Assert.That(getRegForType(reg, type), Is.EqualTo(reg));
        Assert.That(getSingleTypeRegMask(reg, type), Is.EqualTo(expected));
        if (!Configuration.TARGET_ARM)
        {
            Assert.That(floatRegCanHoldType(reg, type), Is.True);
        }
    }

    private static IEnumerable<TestCaseData> NextRegisterCases()
    {
        var_types[] integerTypes = [TYP_INT, TYP_REF, TYP_BYREF, TYP_I_IMPL];
        foreach (var type in integerTypes)
        {
            for (var number = (int)TargetRuntimeMetadata.IntegerRegisterFirst;
                number <= (int)TargetRuntimeMetadata.IntegerRegisterLast; number++)
            {
                var expected = number == (int)TargetRuntimeMetadata.IntegerRegisterLast
                    ? REG_NA
                    : (regNumber)(number + 1);
                yield return new TestCaseData((regNumber)number, type, expected);
            }

            yield return new TestCaseData(REG_STK, type, REG_NA);
            yield return new TestCaseData(REG_NA, type, REG_NA);
        }

        var_types[] floatTypes = [TYP_FLOAT, TYP_DOUBLE];
        foreach (var type in floatTypes)
        {
            var step = Configuration.TARGET_ARM && type == TYP_DOUBLE ? 2 : 1;
            for (var number = (int)TargetRuntimeMetadata.FloatingRegisterFirst;
                number <= (int)TargetRuntimeMetadata.FloatingRegisterLast; number += step)
            {
                var expected = number + step > (int)TargetRuntimeMetadata.FloatingRegisterLast
                    ? REG_NA
                    : (regNumber)(number + step);
                yield return new TestCaseData((regNumber)number, type, expected);
            }

            if (Configuration.TARGET_ARM && type == TYP_DOUBLE)
            {
                continue;
            }

            yield return new TestCaseData(REG_STK, type, REG_NA);
            yield return new TestCaseData(REG_NA, type, REG_NA);
        }

        if (Configuration.TARGET_XARCH && Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            for (var number = (int)TargetRuntimeMetadata.MaskRegisterFirst;
                number <= (int)TargetRuntimeMetadata.MaskRegisterLast; number++)
            {
                var expected = number == (int)TargetRuntimeMetadata.MaskRegisterLast
                    ? REG_NA
                    : (regNumber)(number + 1);
                yield return new TestCaseData((regNumber)number, TYP_MASK, expected);
            }

            yield return new TestCaseData(REG_STK, TYP_MASK, REG_NA);
            yield return new TestCaseData(REG_NA, TYP_MASK, REG_NA);
        }
    }

    [TestCaseSource(nameof(NextRegisterCases))]
    public static void NextRegisterUsesTheTypeBankUpperBound(regNumber reg, var_types type, regNumber expected)
    {
        Assert.That(regNextOfType(reg, type), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> InvalidLowerBoundCases()
    {
        var integerFirst = TargetRuntimeMetadata.IntegerRegisterFirst;
        var floatingFirst = TargetRuntimeMetadata.FloatingRegisterFirst;
        yield return new TestCaseData((regNumber)255, TYP_INT, REG_NA);
        yield return new TestCaseData((regNumber)((int)floatingFirst - 1), TYP_FLOAT, floatingFirst);
        yield return new TestCaseData(integerFirst, TYP_FLOAT, (regNumber)((int)integerFirst + 1));
        yield return new TestCaseData(floatingFirst, TYP_INT, REG_NA);
        if (Configuration.TARGET_XARCH && Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            var maskFirst = TargetRuntimeMetadata.MaskRegisterFirst;
            yield return new TestCaseData((regNumber)((int)maskFirst - 1), TYP_MASK, maskFirst);
            yield return new TestCaseData(integerFirst, TYP_MASK, (regNumber)((int)integerFirst + 1));
        }
    }

    [TestCaseSource(nameof(InvalidLowerBoundCases))]
    public static void NextRegisterRetainsUncheckedArithmeticAndNoLowerBoundValidation(
        regNumber reg, var_types type, regNumber expected)
    {
        Assert.That(regNextOfType(reg, type), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> NonArmNormalizationCases()
    {
        yield return new TestCaseData(REG_NA, TYP_INT);
        yield return new TestCaseData(REG_NA, TYP_DOUBLE);
        yield return new TestCaseData(REG_STK, TYP_STRUCT);
        yield return new TestCaseData((regNumber)255, TYP_VOID);
        yield return new TestCaseData(TargetRuntimeMetadata.IntegerRegisterFirst, TYP_DOUBLE);
        yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterLast, TYP_INT);
    }

    [TestCaseSource(nameof(NonArmNormalizationCases))]
    public static void NonArmNormalizationAndFloatCapacityDoNotValidateRegisterOrType(regNumber reg, var_types type)
    {
        Assume.That(!Configuration.TARGET_ARM, "This test covers non-ARM register normalization.");

        Assert.That(getRegForType(reg, type), Is.EqualTo(reg));
        Assert.That(floatRegCanHoldType(reg, type), Is.True);
    }

    private static IEnumerable<TestCaseData> ArmFloatCapacityCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        for (var number = (int)TargetRuntimeMetadata.FloatingRegisterFirst;
            number <= (int)TargetRuntimeMetadata.FloatingRegisterLast; number++)
        {
            yield return new TestCaseData((regNumber)number, TYP_FLOAT, true);
            yield return new TestCaseData((regNumber)number, TYP_STRUCT, true);
            yield return new TestCaseData((regNumber)number, TYP_DOUBLE, ((number - (int)REG_F0) % 2) == 0);
        }
    }

    [TestCaseSource(nameof(ArmFloatCapacityCases))]
    public static void ArmDoubleCapacityReturnsAlignmentInsteadOfAssertingIt(regNumber reg, var_types type, bool expected)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM floating-point capacity.");

        WithAssertionRecorder(() => Assert.That(floatRegCanHoldType(reg, type), Is.EqualTo(expected)));
    }

    private static IEnumerable<TestCaseData> ArmDoubleMaskCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        for (var number = (int)TargetRuntimeMetadata.FloatingRegisterFirst;
            number <= (int)TargetRuntimeMetadata.FloatingRegisterLast; number++)
        {
            yield return new TestCaseData((regNumber)number, (regNumber)(number & ~1));
        }
    }

    [TestCaseSource(nameof(ArmDoubleMaskCases))]
    public static void ArmDoubleMasksNormalizeBothHalvesBeforeScalarLookupAndFeedTypedMaskOperations(
        regNumber reg, regNumber firstHalf)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM double-register masks.");

        var expected = unchecked((regMask)(3UL << (int)firstHalf));
        Assert.That(getRegForType(reg, TYP_DOUBLE), Is.EqualTo(firstHalf));
        Assert.That(getSingleTypeRegMask(reg, TYP_DOUBLE), Is.EqualTo(expected));

        var mask = new regMaskTP(SRBM_R0);
        regMaskTP.AddRegNum(ref mask, reg, TYP_DOUBLE);
        Assert.That(mask.Lower, Is.EqualTo(SRBM_R0 | expected));
        Assert.That(mask.IsRegNumPresent(firstHalf, TYP_DOUBLE), Is.True);
        Assert.That(mask.IsRegNumPresent(firstHalf + 1, TYP_DOUBLE), Is.True);
        regMaskTP.RemoveRegNum(ref mask, firstHalf + 1, TYP_DOUBLE);
        Assert.That(mask, Is.EqualTo(new regMaskTP(SRBM_R0)));
    }

    private static IEnumerable<TestCaseData> ArmDoubleNormalizationCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        yield return new TestCaseData(REG_R0, (regNumber)255);
        yield return new TestCaseData(REG_R1, REG_R0);
        yield return new TestCaseData(REG_STK, REG_F31);
        yield return new TestCaseData(REG_NA, (regNumber)((int)REG_NA - 1));
    }

    [TestCaseSource(nameof(ArmDoubleNormalizationCases))]
    public static void ArmDoubleNormalizationDecrementsEveryNonDoubleRegisterWithoutValidation(
        regNumber reg, regNumber expected)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM double-register normalization.");

        Assert.That(getRegForType(reg, TYP_DOUBLE), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> ArmOddDoubleIterationCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        yield return new TestCaseData(REG_F1, REG_F3);
        yield return new TestCaseData(REG_F29, REG_F31);
        yield return new TestCaseData(REG_F31, REG_NA);
    }

    [TestCaseSource(nameof(ArmOddDoubleIterationCases))]
    public static void ArmOddDoubleIterationAssertsThePreconditionButDoesNotNormalize(regNumber reg, regNumber expected)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM double-register iteration.");

        WithAssertionRecorder(() => Assert.That(regNextOfType(reg, TYP_DOUBLE), Is.EqualTo(expected)),
            "floatRegCanHoldType(reg, type)");
    }

    private static IEnumerable<TestCaseData> ArmCapacityCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        yield return new TestCaseData(REG_R0, TYP_FLOAT, "genIsValidFloatReg(reg)");
        yield return new TestCaseData(REG_R0, TYP_DOUBLE, "genIsValidFloatReg(reg)");
        yield return new TestCaseData(REG_F0, TYP_INT, "(type == TYP_FLOAT) || (type == TYP_STRUCT)");
    }

    [TestCaseSource(nameof(ArmCapacityCases))]
    public static void ArmCapacityPreservesNativeAssertions(regNumber reg, var_types type, string expectedAssertion)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM register-capacity assertions.");

        WithAssertionRecorder(() => Assert.That(floatRegCanHoldType(reg, type), Is.True), expectedAssertion);
    }

    [Test]
    public static void ArmCapacityPreservesRegisterThenTypeAssertionOrdering()
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM register-capacity assertion ordering.");

        WithAssertionRecorder(() => Assert.That(floatRegCanHoldType(REG_R0, TYP_INT), Is.True),
            "genIsValidFloatReg(reg)", "(type == TYP_FLOAT) || (type == TYP_STRUCT)");
    }

    private static IEnumerable<TestCaseData> ArmDoubleIterationNestedCapacityCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        yield return new TestCaseData(REG_R0, REG_R2, false);
        yield return new TestCaseData(REG_R1, REG_R3, true);
    }

    [TestCaseSource(nameof(ArmDoubleIterationNestedCapacityCases))]
    public static void ArmDoubleIterationPreservesNestedCapacityAssertions(
        regNumber reg, regNumber expected, bool odd)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM double-register capacity assertions.");

        string[] expectedAssertions = odd
            ? ["genIsValidFloatReg(reg)", "floatRegCanHoldType(reg, type)"]
            : ["genIsValidFloatReg(reg)"];

        WithAssertionRecorder(() => Assert.That(regNextOfType(reg, TYP_DOUBLE), Is.EqualTo(expected)),
            expectedAssertions);
    }

    private static IEnumerable<TestCaseData> ArmDoubleMaskAlignmentCases()
    {
        if (!Configuration.TARGET_ARM)
        {
            yield break;
        }

        yield return new TestCaseData(REG_R1, 3UL);
        yield return new TestCaseData(REG_STK, 3UL << (int)REG_F31);
        yield return new TestCaseData(REG_NA, 0UL);
    }

    [TestCaseSource(nameof(ArmDoubleMaskAlignmentCases))]
    public static void ArmDoubleMaskChecksAlignmentAfterNormalizationAndLookup(regNumber reg, ulong expected)
    {
        Assume.That(Configuration.TARGET_ARM, "This test covers ARM double-register mask alignment.");

        WithAssertionRecorder(
            () => Assert.That(getSingleTypeRegMask(reg, TYP_DOUBLE), Is.EqualTo(unchecked((regMask)expected))),
            "genIsValidDoubleReg(reg)");
    }

    [TestCaseSource(nameof(FloatingRegisterEndpoints))]
    public static void VoidTypeIterationUsesTheNativeIntegerClassification(regNumber reg)
    {
        Assert.That(varTypeUsesIntReg(TYP_VOID), Is.True);
        WithAssertionRecorder(() => Assert.That(regNextOfType(reg, TYP_VOID), Is.EqualTo(REG_NA)));
    }

    [TestCaseSource(nameof(MaskRegisterEndpoints))]
    public static void NonXarchMaskIterationRetainsTheNativeFloatBranch(regNumber reg)
    {
        Assume.That(
            Configuration.FEATURE_MASKED_HW_INTRINSICS && Configuration.TARGET_ARM64 &&
                !Configuration.TARGET_XARCH,
            "This test covers masked registers on non-xarch targets.");

        WithAssertionRecorder(() => Assert.That(regNextOfType(reg, TYP_MASK), Is.EqualTo(REG_NA)),
            "varTypeUsesFloatReg(type)");
    }

    private static IEnumerable<TestCaseData> FloatingRegisterEndpoints()
    {
        yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterFirst);
        yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterLast);
    }

    private static IEnumerable<TestCaseData> MaskRegisterEndpoints()
    {
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS || !Configuration.TARGET_ARM64)
        {
            yield break;
        }

        yield return new TestCaseData(TargetRuntimeMetadata.MaskRegisterFirst);
        yield return new TestCaseData(TargetRuntimeMetadata.MaskRegisterLast);
    }

    private static List<string?>? s_assertions;

    private static void WithAssertionRecorder(Action action, params string[] expected)
    {
        if (!TestConfiguration.IsDebug)
        {
            action();
            return;
        }

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = TestConfiguration.CreateJitTls(&ee);
        s_assertions = [];
        try
        {
            action();
            Assert.That(s_assertions, Is.EqualTo(expected));
        }
        finally
        {
            s_assertions = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        (s_assertions ?? throw new InvalidOperationException("Unexpected register assertion callback."))
            .Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }
}
