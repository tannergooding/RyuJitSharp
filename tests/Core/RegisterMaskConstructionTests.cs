// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if HAS_FIXED_REGISTER_SET
#if DEBUG
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RegisterMaskConstructionTests
{
    [TestCase(REG_STK)]
    [TestCase(REG_INT_FIRST)]
    [TestCase(REG_INT_LAST)]
    [TestCase(REG_FP_FIRST)]
    [TestCase(REG_FP_LAST)]
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(REG_MASK_FIRST)]
    [TestCase(REG_MASK_LAST)]
#endif
    public static void ConstructionUsesTheLookupBitAndTheFullRegisterBank(regNumber reg)
    {
        var expectedBit = reg == REG_STK ? SRBM_NONE : unchecked((regMask)(1UL << ((int)reg % 64)));
#if HAS_MORE_THAN_64_REGISTERS
        var expectedMask = (int)reg < 64
            ? new regMaskTP(expectedBit)
            : new regMaskTP(SRBM_NONE, expectedBit);
#else
        var expectedMask = new regMaskTP(expectedBit);
#endif
        var type = reg.IsFltReg ? TYP_FLOAT : TYP_INT;

        Assert.That(genSingleTypeRegMask(reg), Is.EqualTo(expectedBit));
        Assert.That(genSingleTypeRegMask(reg, type), Is.EqualTo(expectedBit));
        Assert.That(genRegMask(reg), Is.EqualTo(expectedMask));
        Assert.That(genRegMask(reg, type), Is.EqualTo(expectedMask));
    }

    [Test]
    public static void EachConstructionReturnsAnIndependentValue()
    {
        var first = genRegMask(REG_INT_FIRST);
        var original = first;
        var last = genRegMask(REG_INT_LAST, TYP_INT);
        var floating = genRegMask(REG_FP_FIRST, TYP_FLOAT);
        var empty = genRegMask(REG_STK);

        regMaskTP.AddRegNumInMask(ref first, REG_INT_LAST);
        Assert.That(original, Is.EqualTo(new regMaskTP(REG_INT_FIRST.SingleTypeMask)));
        Assert.That(last, Is.EqualTo(new regMaskTP(REG_INT_LAST.SingleTypeMask)));
        Assert.That(floating, Is.EqualTo(new regMaskTP(REG_FP_FIRST.SingleTypeMask)));
        Assert.That(empty, Is.EqualTo(RBM_NONE));
        Assert.That(genRegMask(REG_INT_FIRST), Is.EqualTo(original));
        Assert.That(first, Is.EqualTo(original | last));
    }

#if HAS_MORE_THAN_64_REGISTERS && FEATURE_MASKED_HW_INTRINSICS
    [Test]
    public static void ScalarMasksAreBankLocalWhileFullMasksKeepOverlappingBanksSeparate()
    {
        var integer = genSingleTypeRegMask(REG_INT_FIRST);
        var masked = genSingleTypeRegMask(REG_MASK_FIRST, TYP_MASK);

        Assert.That(integer, Is.EqualTo((regMask)1));
        Assert.That(masked, Is.EqualTo(integer));
        Assert.That(genRegMask(REG_INT_FIRST), Is.EqualTo(new regMaskTP(integer, SRBM_NONE)));
        Assert.That(genRegMask(REG_MASK_FIRST, TYP_MASK), Is.EqualTo(new regMaskTP(SRBM_NONE, masked)));
        Assert.That(genRegMask(REG_INT_FIRST), Is.Not.EqualTo(genRegMask(REG_MASK_FIRST)));
    }
#endif

#if !TARGET_ARM
    [TestCase(TYP_UNDEF)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_STRUCT)]
    public static void NonArmTypedConstructionIgnoresTheType(var_types type)
    {
        regNumber[] registers = [REG_STK, REG_INT_LAST, REG_FP_LAST
#if FEATURE_MASKED_HW_INTRINSICS
            , REG_MASK_LAST
#endif
        ];
        foreach (var reg in registers)
        {
            Assert.That(genSingleTypeRegMask(reg, type), Is.EqualTo(genSingleTypeRegMask(reg)));
            Assert.That(genRegMask(reg, type), Is.EqualTo(genRegMask(reg)));
        }
    }
#else
    [TestCase(0)]
    [TestCase(30)]
    public static void ArmDoubleConstructionPairsFromTheFirstHalfWithoutNormalizing(int offset)
    {
        var first = (regNumber)((int)REG_F0 + offset);
        var second = first + 1;
        var pair = first.SingleTypeMask | second.SingleTypeMask;

        Assert.That(genSingleTypeRegMask(first), Is.EqualTo(first.SingleTypeMask));
        Assert.That(genRegMask(first), Is.EqualTo(new regMaskTP(first.SingleTypeMask)));
        Assert.That(genSingleTypeRegMask(first, TYP_DOUBLE), Is.EqualTo(pair));
        Assert.That(genRegMask(first, TYP_DOUBLE), Is.EqualTo(new regMaskTP(pair)));
        Assert.That(getSingleTypeRegMask(second, TYP_DOUBLE), Is.EqualTo(pair));
        Assert.That(genSingleTypeRegMask(second, TYP_FLOAT), Is.EqualTo(second.SingleTypeMask));
        Assert.That(genRegMask(second, TYP_FLOAT), Is.EqualTo(new regMaskTP(second.SingleTypeMask)));
    }

    [Test]
    public static void ArmLongConstructionDoesNotIncludeTheNextIntegerRegister()
    {
        var reg = REG_INT_FIRST;

        Assert.That(genSingleTypeRegMask(reg, TYP_LONG), Is.EqualTo(reg.SingleTypeMask));
        Assert.That(genRegMask(reg, TYP_LONG), Is.EqualTo(new regMaskTP(reg.SingleTypeMask)));
    }
#endif

#if DEBUG
    private static int s_assertions;
    private static string? s_lastAssertion;
#if TARGET_ARM
    private static bool s_returned;
    private static bool s_returnedAtAssertion;
#endif

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InvalidLookupAssertsBeforeRetainingTheArrayIndexFailure(bool fullMask, bool typed)
    {
        regNumber[] invalidRegisters = [unchecked((regNumber)(-1)), REG_COUNT];
        foreach (var reg in invalidRegisters)
        {
            WithAssertionRecorder(() => {
                if (fullMask)
                {
                    _ = Assert.Throws<IndexOutOfRangeException>(
                        () => _ = typed ? genRegMask(reg, TYP_INT) : genRegMask(reg));
                }
                else
                {
                    _ = Assert.Throws<IndexOutOfRangeException>(
                        () => _ = typed ? genSingleTypeRegMask(reg, TYP_INT) : genSingleTypeRegMask(reg));
                }

                Assert.That(s_assertions, Is.EqualTo(1));
                Assert.That(s_lastAssertion, Is.EqualTo("(uint)regNum < (uint)s_masks.Length"));
            });
        }
    }

#if TARGET_ARM
    [TestCase(false)]
    [TestCase(true)]
    public static void ArmOddDoubleAssertsBeforeReturningTheUnnormalizedPair(bool fullMask)
    {
        var odd = REG_F1;
        var expected = odd.SingleTypeMask | (odd + 1).SingleTypeMask;

        WithAssertionRecorder(() => {
            var result = fullMask ? genRegMask(odd, TYP_DOUBLE).Lower : genSingleTypeRegMask(odd, TYP_DOUBLE);
            s_returned = true;

            Assert.That(s_assertions, Is.EqualTo(1));
            Assert.That(s_lastAssertion,
                Is.EqualTo("type is TYP_DOUBLE ? ((regNum - REG_F0) % 2) == 0 : type is TYP_FLOAT or TYP_STRUCT"));
            Assert.That(s_returnedAtAssertion, Is.False);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result, Is.Not.EqualTo(getSingleTypeRegMask(odd, TYP_DOUBLE)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ArmStructRetainsTheTypeAssertionBeforeReturningItsSingleHalf(bool fullMask)
    {
        WithAssertionRecorder(() => {
            var result = fullMask ? genRegMask(REG_F1, TYP_STRUCT).Lower : genSingleTypeRegMask(REG_F1, TYP_STRUCT);
            s_returned = true;
            var expectedAssertions = varTypeUsesIntReg(TYP_STRUCT) || varTypeUsesFloatReg(TYP_STRUCT) ? 0 : 1;

            Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
            if (expectedAssertions != 0)
            {
                Assert.That(s_lastAssertion, Is.EqualTo("varTypeUsesFloatReg(type)"));
                Assert.That(s_returnedAtAssertion, Is.False);
            }

            Assert.That(result, Is.EqualTo(REG_F1.SingleTypeMask));
        });
    }
#endif

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
        JitTls.Compiler = null;
        s_assertions = 0;
        s_lastAssertion = null;
#if TARGET_ARM
        s_returned = false;
        s_returnedAtAssertion = false;
#endif
        try
        {
            action();
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        s_lastAssertion = Marshal.PtrToStringUTF8((nint)expression);
#if TARGET_ARM
        s_returnedAtAssertion = s_returned;
#endif

        return 0;
    }
#endif
}
#endif
