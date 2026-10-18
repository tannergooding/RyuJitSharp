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
internal static unsafe class RegMaskOperationFamilyTests
{
    private sealed class MaskOwner
    {
        public regMaskTP Mask;
    }

    private static IEnumerable<TestCaseData> RegisterCases()
    {
        yield return new TestCaseData(TargetRuntimeMetadata.IntegerRegisterFirst);
        yield return new TestCaseData(TargetRuntimeMetadata.IntegerRegisterLast);
        yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterFirst);
        yield return new TestCaseData(TargetRuntimeMetadata.FloatingRegisterLast);
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            yield return new TestCaseData(TargetRuntimeMetadata.MaskRegisterFirst);
            yield return new TestCaseData(TargetRuntimeMetadata.MaskRegisterLast);
        }
    }

    [TestCaseSource(nameof(RegisterCases))]
    public static void RegisterOperationsUseTheNativeLookupAndBank(regNumber reg)
    {
        var expected = Configuration.HAS_MORE_THAN_64_REGISTERS && (int)reg >= 64
            ? new regMaskTP(SRBM_NONE, reg.SingleTypeMask)
            : new regMaskTP(reg.SingleTypeMask);
        var mask = default(regMaskTP);

        regMaskTP.AddRegNumInMask(ref mask, reg);
        Assert.That(mask, Is.EqualTo(expected));
        Assert.That(mask.IsRegNumInMask(reg), Is.True);
        Assert.That(mask.IsRegNumPresent(reg, TYP_FLOAT), Is.True);

        regMaskTP.RemoveRegNumFromMask(ref mask, reg);
        Assert.That(mask, Is.EqualTo(default(regMaskTP)));
        Assert.That(mask.IsRegNumInMask(reg), Is.False);
    }

    [Test]
    public static void StackPseudoRegisterDoesNotAddRemoveOrMatchAnyBit()
    {
        var mask = new regMaskTP((regMask)(-1));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            mask = new regMaskTP(mask.Lower, (regMask)(-1));
        }

        var original = mask;

        regMaskTP.AddRegNumInMask(ref mask, REG_STK);
        regMaskTP.RemoveRegNumFromMask(ref mask, REG_STK);
        Assert.That(mask, Is.EqualTo(original));
        Assert.That(mask.IsRegNumInMask(REG_STK), Is.False);
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_MASK)]
    public static void RegisterSetsSelectTheBankWithoutFilteringBits(var_types type)
    {
        Assume.That(type != TYP_SIMD16 || Configuration.FEATURE_SIMD);
        Assume.That(type != TYP_MASK || Configuration.FEATURE_MASKED_HW_INTRINSICS);

        var initial = new regMaskTP((regMask)1);
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            initial = new regMaskTP(initial.Lower, (regMask)4);
        }
        var mask = initial;
        var bits = (regMask)(-1);
        regMaskTP.AddRegsetForType(ref mask, bits, type);

        var expected = Configuration.HAS_MORE_THAN_64_REGISTERS
            ? varTypeIsMask(type)
                ? new regMaskTP(initial.Lower, bits)
                : new regMaskTP(bits, initial.Upper)
            : new regMaskTP(bits);
        Assert.That(mask, Is.EqualTo(expected));

        regMaskTP.RemoveRegsetForType(ref mask, bits, type);
        expected = Configuration.HAS_MORE_THAN_64_REGISTERS
            ? varTypeIsMask(type)
                ? new regMaskTP(initial.Lower, SRBM_NONE)
                : new regMaskTP(SRBM_NONE, initial.Upper)
            : default;
        Assert.That(mask, Is.EqualTo(expected));
    }

    [Test]
    public static void EmptyRegisterSetsLeaveBothBanksUnchanged()
    {
        var mask = new regMaskTP((regMask)(-1));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            mask = new regMaskTP(mask.Lower, (regMask)(-1));
        }
        var original = mask;

        regMaskTP.AddRegsetForType(ref mask, SRBM_NONE, TYP_FLOAT);
        regMaskTP.RemoveRegsetForType(ref mask, SRBM_NONE, TYP_FLOAT);
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            regMaskTP.AddRegsetForType(ref mask, SRBM_NONE, TYP_MASK);
            regMaskTP.RemoveRegsetForType(ref mask, SRBM_NONE, TYP_MASK);
        }
        Assert.That(mask, Is.EqualTo(original));
    }

    [Test]
    public static void MutatorsWriteBackToLocalsArrayEntriesAndFieldsWithoutChangingCopies()
    {
        var first = REG_INT_FIRST;
        var second = (regNumber)((int)first + 1);
        var local = regMaskTP.CreateFromRegNum(first, first.SingleTypeMask);
        var copy = local;
        regMaskTP.AddRegNum(ref local, second, TYP_INT);
        Assert.That(copy.IsRegNumInMask(second), Is.False);
        Assert.That(local.IsRegNumInMask(first), Is.True);
        Assert.That(local.IsRegNumInMask(second), Is.True);

        regMaskTP[] entries = [local, copy];
        regMaskTP.RemoveRegNum(ref entries[0], first, TYP_INT);
        Assert.That(entries[0].IsRegNumInMask(first), Is.False);
        Assert.That(entries[0].IsRegNumInMask(second), Is.True);
        Assert.That(entries[1], Is.EqualTo(copy));
        Assert.That(local.IsRegNumInMask(first), Is.True);

        var owner = new MaskOwner { Mask = entries[0] };
        AddGprRegs(ref owner.Mask, first.SingleTypeMask, copy);
        Assert.That(owner.Mask, Is.EqualTo(local));
        Assert.That(entries[0].IsRegNumInMask(first), Is.False);
        regMaskTP.RemoveRegNumFromMask(ref owner.Mask, second);
        Assert.That(owner.Mask, Is.EqualTo(copy));
    }

    [Test]
    public static void GprAdditionKeepsExistingFloatingAndUpperBankBits()
    {
        var first = REG_INT_FIRST.SingleTypeMask;
        var floating = REG_FP_FIRST.SingleTypeMask;
        var mask = new regMaskTP(floating);
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            mask = new regMaskTP(mask.Lower, (regMask)(-1));
        }
        var original = mask;

        AddGprRegs(ref mask, SRBM_NONE, default);
        Assert.That(mask, Is.EqualTo(original));
        AddGprRegs(ref mask, first, new regMaskTP(first));
        Assert.That(mask.Lower, Is.EqualTo(floating | first));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(mask.Upper, Is.EqualTo(original.Upper));
        }
    }

    [Test]
    public static void HighVectorBitAndMaskBankSurviveIndependentRegisterRemoval()
    {
        Assume.That(Configuration.TARGET_AMD64);

        var mask = new regMaskTP(SRBM_RAX | SRBM_XMM31, SRBM_K7);

        regMaskTP.RemoveRegNumFromMask(ref mask, REG_RAX);
        Assert.That(mask, Is.EqualTo(new regMaskTP(SRBM_XMM31, SRBM_K7)));
        Assert.That(mask.IsRegNumInMask(REG_XMM31), Is.True);
        Assert.That(mask.IsRegNumInMask(REG_K7), Is.True);

        regMaskTP.RemoveRegNum(ref mask, REG_XMM31, TYP_DOUBLE);
        Assert.That(mask, Is.EqualTo(new regMaskTP(SRBM_NONE, SRBM_K7)));
        regMaskTP.AddRegNum(ref mask, REG_XMM31, TYP_INT);
        regMaskTP.RemoveRegNumFromMask(ref mask, REG_K7);
        Assert.That(mask, Is.EqualTo(new regMaskTP(SRBM_XMM31)));
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void NonArmTypedRegisterOperationsIgnoreTheType(var_types type)
    {
        Assume.That(!Configuration.TARGET_ARM);

        var mask = default(regMaskTP);
        var reg = REG_INT_FIRST;

        regMaskTP.AddRegNum(ref mask, reg, type);
        Assert.That(mask, Is.EqualTo(new regMaskTP(reg.SingleTypeMask)));
        Assert.That(mask.IsRegNumPresent(reg, type), Is.True);
        regMaskTP.RemoveRegNum(ref mask, reg, type);
        Assert.That(mask, Is.EqualTo(default(regMaskTP)));
    }

    [TestCase(0)]
    [TestCase(30)]
    public static void ArmTypedLookupAddsAndRemovesBothDoubleHalves(int offset)
    {
        Assume.That(Configuration.TARGET_ARM);

        var first = (regNumber)((int)REG_F0 + offset);
        var second = first + 1;
        var pair = first.SingleTypeMask | second.SingleTypeMask;
        var mask = new regMaskTP(REG_INT_FIRST.SingleTypeMask);

        regMaskTP.AddRegNumInMask(ref mask, first, TYP_DOUBLE);
        Assert.That(mask.Lower, Is.EqualTo(REG_INT_FIRST.SingleTypeMask | pair));
        Assert.That(mask.IsRegNumInMask(first, TYP_DOUBLE), Is.True);
        Assert.That(mask.IsRegNumInMask(second), Is.True);
        regMaskTP.RemoveRegNumFromMask(ref mask, first, TYP_DOUBLE);
        Assert.That(mask, Is.EqualTo(new regMaskTP(REG_INT_FIRST.SingleTypeMask)));
    }

    [TestCase(0)]
    [TestCase(30)]
    public static void ArmNormalizedOperationsAcceptEitherDoubleHalf(int offset)
    {
        Assume.That(Configuration.TARGET_ARM);

        var first = (regNumber)((int)REG_F0 + offset);
        var second = first + 1;
        var pair = first.SingleTypeMask | second.SingleTypeMask;
        var mask = default(regMaskTP);

        regMaskTP.AddRegNum(ref mask, second, TYP_DOUBLE);
        Assert.That(mask.Lower, Is.EqualTo(pair));
        Assert.That(mask.IsRegNumPresent(second, TYP_DOUBLE), Is.True);
        regMaskTP.RemoveRegNum(ref mask, second, TYP_DOUBLE);
        Assert.That(mask.IsEmpty, Is.True);

        mask = new regMaskTP(second.SingleTypeMask);
        Assert.That(mask.IsRegNumPresent(first, TYP_DOUBLE), Is.True);
        Assert.That(mask.IsRegNumPresent(second, TYP_DOUBLE), Is.True);
        Assert.That(mask.IsRegNumInMask(first, TYP_DOUBLE), Is.True);
        Assert.That(mask.IsRegNumInMask(first), Is.False);
    }

    [TestCase(TYP_FLOAT)]
    public static void ArmNonDoubleTypedLookupKeepsTheSingleFloatHalf(var_types type)
    {
        Assume.That(Configuration.TARGET_ARM);

        var reg = REG_F1;
        var mask = default(regMaskTP);

        regMaskTP.AddRegNumInMask(ref mask, reg, type);
        Assert.That(mask, Is.EqualTo(new regMaskTP(reg.SingleTypeMask)));
        Assert.That(mask.IsRegNumInMask(reg, type), Is.True);
        regMaskTP.RemoveRegNumFromMask(ref mask, reg, type);
        Assert.That(mask.IsEmpty, Is.True);
    }

    [Test]
    public static void ArmIntegerTypedLookupDoesNotExpandTheRegister()
    {
        Assume.That(Configuration.TARGET_ARM);

        var mask = default(regMaskTP);

        regMaskTP.AddRegNumInMask(ref mask, REG_INT_FIRST, TYP_LONG);
        Assert.That(mask, Is.EqualTo(new regMaskTP(REG_INT_FIRST.SingleTypeMask)));
        Assert.That(mask.IsRegNumInMask(REG_INT_FIRST, TYP_LONG), Is.True);
        regMaskTP.RemoveRegNumFromMask(ref mask, REG_INT_FIRST, TYP_LONG);
        Assert.That(mask.IsEmpty, Is.True);
    }

    [TestCase(0U, 0, 0)]
    [TestCase(0x7FFFFFFFU, int.MaxValue, 0xFF)]
    [TestCase(0x80000000U, int.MinValue, 0)]
    [TestCase(uint.MaxValue, -1, 0xFF)]
    public static void ArmScalarConversionsPreserveTheLowRegisterMaskBits(
        uint bits, int expectedInt, byte expectedByte)
    {
        Assume.That(Configuration.TARGET_ARM);

        var mask = new regMaskTP((regMask)bits);

        Assert.That((int)mask, Is.EqualTo(expectedInt));
        Assert.That((byte)mask, Is.EqualTo(expectedByte));
    }

    [TestCase(0L, 1L, -1L)]
    [TestCase(1L, long.MinValue, long.MinValue + 1)]
    [TestCase(long.MinValue, 1L, long.MaxValue)]
    public static void ArmSubtractionWrapsAtTheRegisterMaskWidth(
        long first, long second, long expected)
    {
        Assume.That(Configuration.TARGET_ARM);

        var result = new regMaskTP((regMask)first) - new regMaskTP((regMask)second);

        Assert.That(result, Is.EqualTo(new regMaskTP((regMask)expected)));
    }

    [Test]
    public static void ArmMaskOrderingUsesUnsignedRegisterBits()
    {
        Assume.That(Configuration.TARGET_ARM);

        var belowSignBit = new regMaskTP((regMask)long.MaxValue);
        var aboveSignBit = new regMaskTP((regMask)long.MinValue);
        var allBits = new regMaskTP((regMask)(-1L));

        Assert.That(aboveSignBit > belowSignBit, Is.True);
        Assert.That(belowSignBit < aboveSignBit, Is.True);
        Assert.That(allBits > aboveSignBit, Is.True);
        Assert.That(aboveSignBit < allBits, Is.True);
    }

    [TestCase(0x4000000000000001L, 1, long.MinValue + 2)]
    [TestCase(1L, 63, long.MinValue)]
    [TestCase(long.MinValue, 1, 0L)]
    public static void ArmLeftShiftMatchesTheNativeRegisterWidth(long bits, int count, long expected)
    {
        Assume.That(Configuration.TARGET_ARM);

        var mask = new regMaskTP((regMask)bits);
        var result = mask << count;

        Assert.That(result, Is.EqualTo(new regMaskTP((regMask)expected)));

        mask <<= count;
        Assert.That(mask, Is.EqualTo(result));
    }

    private static int s_assertions;
    private static MaskOwner? s_assertionOwner;
    private static regMaskTP s_maskAtAssertion;
    private static string? s_assertionExpression;

    [TestCase(false)]
    [TestCase(true)]
    public static void GprAssertionRunsBeforeWritebackAndTestsIntersectionRatherThanSubset(bool intersects)
    {
        Assume.That(TestConfiguration.IsDebug);

        var first = REG_INT_FIRST.SingleTypeMask;
        var second = ((regNumber)((int)REG_INT_FIRST + 1)).SingleTypeMask;
        var third = ((regNumber)((int)REG_INT_FIRST + 2)).SingleTypeMask;
        var owner = new MaskOwner { Mask = new regMaskTP(third) };
        var requested = first | second;
        var available = new regMaskTP(intersects ? first : third);

        WithAssertionRecorder(owner, () => {
            AddGprRegs(ref owner.Mask, requested, available);
            Assert.That(s_assertions, Is.EqualTo(intersects ? 0 : 1));
            if (!intersects)
            {
                Assert.That(s_maskAtAssertion, Is.EqualTo(new regMaskTP(third)));
                Assert.That(s_assertionExpression,
                    Is.EqualTo("(gprRegs == RBM_NONE) || ((gprRegs & availableIntRegs) != RBM_NONE)"));
            }

            Assert.That(owner.Mask, Is.EqualTo(new regMaskTP(third | requested)));
        });
    }

    [Test]
    public static void GprAvailabilityDoesNotUseTheUpperBank()
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.HAS_MORE_THAN_64_REGISTERS);

        var bit = REG_INT_FIRST.SingleTypeMask;
        var owner = new MaskOwner { Mask = new regMaskTP(SRBM_NONE, (regMask)(-1)) };

        WithAssertionRecorder(owner, () => {
            var original = owner.Mask;
            AddGprRegs(ref owner.Mask, bit, new regMaskTP(SRBM_NONE, bit));
            Assert.That(s_assertions, Is.EqualTo(1));
            Assert.That(s_maskAtAssertion, Is.EqualTo(original));
            Assert.That(s_assertionExpression,
                Is.EqualTo("(gprRegs == RBM_NONE) || ((gprRegs & availableIntRegs) != RBM_NONE)"));
            Assert.That(owner.Mask, Is.EqualTo(new regMaskTP(bit, original.Upper)));
        });
    }

    [Test]
    public static void ArmStructTypedLookupRetainsTheNativeTypeAssertionAndContinuation()
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_ARM);

        var owner = new MaskOwner();

        WithAssertionRecorder(owner, () => {
            regMaskTP.AddRegNumInMask(ref owner.Mask, REG_F1, TYP_STRUCT);
            var expectedAssertions = varTypeUsesIntReg(TYP_STRUCT) || varTypeUsesFloatReg(TYP_STRUCT) ? 0 : 1;
            Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
            Assert.That(owner.Mask, Is.EqualTo(new regMaskTP(REG_F1.SingleTypeMask)));
        });
    }

    [Test]
    public static void ArmTypedLookupAssertsOnAnOddDoubleInsteadOfNormalizingIt()
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_ARM);

        var odd = REG_F1;
        var owner = new MaskOwner();

        WithAssertionRecorder(owner, () => {
            regMaskTP.AddRegNumInMask(ref owner.Mask, odd, TYP_DOUBLE);
            Assert.That(s_assertions, Is.EqualTo(1));
            Assert.That(s_maskAtAssertion, Is.EqualTo(default(regMaskTP)));
            Assert.That(owner.Mask.Lower, Is.EqualTo(odd.SingleTypeMask | (odd + 1).SingleTypeMask));
        });
    }

    private static void WithAssertionRecorder(MaskOwner owner, Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
        JitTls.Compiler = null;
        s_assertionOwner = owner;
        s_assertions = 0;
        s_maskAtAssertion = default;
        s_assertionExpression = null;
        try
        {
            action();
        }
        finally
        {
            s_assertionOwner = null;
            s_assertionExpression = null;
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        s_assertionExpression = Marshal.PtrToStringUTF8((nint)expression);
        if (s_assertionOwner is MaskOwner owner)
        {
            s_maskAtAssertion = owner.Mask;
        }

        return 0;
    }

    private static void AddGprRegs(ref regMaskTP mask, regMask registers, regMaskTP available)
    {
        regMaskTP.AddGprRegs(ref mask, registers, available);
    }
}
