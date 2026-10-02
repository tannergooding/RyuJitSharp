// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
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

    [TestCase(REG_INT_FIRST)]
    [TestCase(REG_INT_LAST)]
    [TestCase(REG_FP_FIRST)]
    [TestCase(REG_FP_LAST)]
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(REG_MASK_FIRST)]
    [TestCase(REG_MASK_LAST)]
#endif
    public static void RegisterOperationsUseTheNativeLookupAndBank(regNumber reg)
    {
#if HAS_MORE_THAN_64_REGISTERS
        var expected = (int)reg < 64
            ? new regMaskTP(reg.SingleTypeMask)
            : new regMaskTP(SRBM_NONE, reg.SingleTypeMask);
#else
        var expected = new regMaskTP(reg.SingleTypeMask);
#endif
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
#if HAS_MORE_THAN_64_REGISTERS
        mask = new regMaskTP(mask.Lower, (regMask)(-1));
#endif
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
#if FEATURE_SIMD
    [TestCase(TYP_SIMD16)]
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK)]
#endif
    public static void RegisterSetsSelectTheBankWithoutFilteringBits(var_types type)
    {
        var initial = new regMaskTP((regMask)1);
#if HAS_MORE_THAN_64_REGISTERS
        initial = new regMaskTP(initial.Lower, (regMask)4);
#endif
        var mask = initial;
        var bits = (regMask)(-1);
        regMaskTP.AddRegsetForType(ref mask, bits, type);

#if HAS_MORE_THAN_64_REGISTERS
        var expected = varTypeIsMask(type)
            ? new regMaskTP(initial.Lower, bits)
            : new regMaskTP(bits, initial.Upper);
#else
        var expected = new regMaskTP(bits);
#endif
        Assert.That(mask, Is.EqualTo(expected));

        regMaskTP.RemoveRegsetForType(ref mask, bits, type);
#if HAS_MORE_THAN_64_REGISTERS
        expected = varTypeIsMask(type)
            ? new regMaskTP(initial.Lower, SRBM_NONE)
            : new regMaskTP(SRBM_NONE, initial.Upper);
#else
        expected = default;
#endif
        Assert.That(mask, Is.EqualTo(expected));
    }

    [Test]
    public static void EmptyRegisterSetsLeaveBothBanksUnchanged()
    {
        var mask = new regMaskTP((regMask)(-1));
#if HAS_MORE_THAN_64_REGISTERS
        mask = new regMaskTP(mask.Lower, (regMask)(-1));
#endif
        var original = mask;

        regMaskTP.AddRegsetForType(ref mask, SRBM_NONE, TYP_FLOAT);
        regMaskTP.RemoveRegsetForType(ref mask, SRBM_NONE, TYP_FLOAT);
#if FEATURE_MASKED_HW_INTRINSICS
        regMaskTP.AddRegsetForType(ref mask, SRBM_NONE, TYP_MASK);
        regMaskTP.RemoveRegsetForType(ref mask, SRBM_NONE, TYP_MASK);
#endif
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
#if HAS_MORE_THAN_64_REGISTERS
        mask = new regMaskTP(mask.Lower, (regMask)(-1));
#endif
        var original = mask;

        AddGprRegs(ref mask, SRBM_NONE, default);
        Assert.That(mask, Is.EqualTo(original));
        AddGprRegs(ref mask, first, new regMaskTP(first));
        Assert.That(mask.Lower, Is.EqualTo(floating | first));
#if HAS_MORE_THAN_64_REGISTERS
        Assert.That(mask.Upper, Is.EqualTo(original.Upper));
#endif
    }

#if TARGET_AMD64
    [Test]
    public static void HighVectorBitAndMaskBankSurviveIndependentRegisterRemoval()
    {
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
#endif

#if !TARGET_ARM
    [TestCase(TYP_INT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void NonArmTypedRegisterOperationsIgnoreTheType(var_types type)
    {
        var mask = default(regMaskTP);
        var reg = REG_INT_FIRST;

        regMaskTP.AddRegNum(ref mask, reg, type);
        Assert.That(mask, Is.EqualTo(new regMaskTP(reg.SingleTypeMask)));
        Assert.That(mask.IsRegNumPresent(reg, type), Is.True);
        regMaskTP.RemoveRegNum(ref mask, reg, type);
        Assert.That(mask, Is.EqualTo(default(regMaskTP)));
    }
#endif

#if TARGET_ARM
    [TestCase(0)]
    [TestCase(30)]
    public static void ArmTypedLookupAddsAndRemovesBothDoubleHalves(int offset)
    {
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
        var mask = default(regMaskTP);

        regMaskTP.AddRegNumInMask(ref mask, REG_INT_FIRST, TYP_LONG);
        Assert.That(mask, Is.EqualTo(new regMaskTP(REG_INT_FIRST.SingleTypeMask)));
        Assert.That(mask.IsRegNumInMask(REG_INT_FIRST, TYP_LONG), Is.True);
        regMaskTP.RemoveRegNumFromMask(ref mask, REG_INT_FIRST, TYP_LONG);
        Assert.That(mask.IsEmpty, Is.True);
    }
#endif

#if DEBUG
    private static int s_assertions;
    private static MaskOwner? s_assertionOwner;
    private static regMaskTP s_maskAtAssertion;
    private static string? s_assertionExpression;

    [TestCase(false)]
    [TestCase(true)]
    public static void GprAssertionRunsBeforeWritebackAndTestsIntersectionRatherThanSubset(bool intersects)
    {
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

#if HAS_MORE_THAN_64_REGISTERS
    [Test]
    public static void GprAvailabilityDoesNotUseTheUpperBank()
    {
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
#endif

#if TARGET_ARM
    [Test]
    public static void ArmStructTypedLookupRetainsTheNativeTypeAssertionAndContinuation()
    {
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
        var odd = REG_F1;
        var owner = new MaskOwner();

        WithAssertionRecorder(owner, () => {
            regMaskTP.AddRegNumInMask(ref owner.Mask, odd, TYP_DOUBLE);
            Assert.That(s_assertions, Is.EqualTo(1));
            Assert.That(s_maskAtAssertion, Is.EqualTo(default(regMaskTP)));
            Assert.That(owner.Mask.Lower, Is.EqualTo(odd.SingleTypeMask | (odd + 1).SingleTypeMask));
        });
    }
#endif

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
#endif

    private static void AddGprRegs(ref regMaskTP mask, regMask registers, regMaskTP available)
    {
        regMaskTP.AddGprRegs(ref mask, registers
#if DEBUG
            , available
#endif
        );
    }
}
