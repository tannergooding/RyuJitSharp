// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RegisterMaskNumericOperationsTests
{
    [TestCaseSource(nameof(BooleanConversionCases))]
    public static void BooleanConversionInspectsBothCompleteBanks(ulong lower, ulong upper, bool expected)
    {
        var mask = CreateMask(lower, upper);

        Assert.That((bool)mask, Is.EqualTo(expected));
        Assert.That(mask.IsEmpty, Is.EqualTo(!expected));
    }

    [TestCaseSource(nameof(UnsignedIntConversionCases))]
    public static void UnsignedIntConversionTruncatesTheLowBankWithoutAsserting(ulong lower, ulong upper, uint expected)
    {
        if (Configuration.REGMASK_BITS_32)
        {
            Assert.Ignore("Unsigned register-mask conversion cases require a register mask wider than 32 bits.");
        }

        var mask = CreateMask(lower, upper);
        if (TestConfiguration.IsDebug)
        {
            WithAssertionRecorder(() => Assert.That(checked((uint)mask), Is.EqualTo(expected)));
            Assert.That(s_assertions, Is.Zero);
        }
        else
        {
            Assert.That(checked((uint)mask), Is.EqualTo(expected));
        }
    }

    [TestCaseSource(nameof(IntegerFactoryCases))]
    public static void IntegerFactoryPreservesTheRawBankAndClearsTheUpperBank(ulong bits)
    {
        var lower = unchecked((regMask)bits);
        var mask = regMaskTP.FromIntRegSet(lower);

        Assert.That(mask.Lower, Is.EqualTo(lower));
        Assert.That(mask.IntRegSet, Is.EqualTo(lower));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(mask.Upper, Is.EqualTo(SRBM_NONE));
        }
    }

    [TestCaseSource(nameof(RightShiftCases))]
    public static void RightShiftIsLogicalAndOnlyReconstructsTheLowBank(
        ulong lower, ulong upper, int count, ulong expected)
    {
        var mask = CreateMask(lower, upper);
        var original = mask;
        var result = mask >> count;

        Assert.That(result, Is.EqualTo(new regMaskTP(unchecked((regMask)expected))));
        Assert.That(mask, Is.EqualTo(original));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(result.Upper, Is.EqualTo(SRBM_NONE));
        }
    }

    [TestCaseSource(nameof(RightShiftCounts))]
    public static void ShiftRightAssignReturnsAnAliasToTheUpdatedOwner(int count)
    {
        var destination = CreateMask(ulong.MaxValue, ulong.MaxValue);
        var expected = destination >> count;
        ref var alias = ref regMaskTP.ShiftRightAssign(ref destination, count);

        Assert.That(Unsafe.AreSame(ref destination, ref alias), Is.True);
        Assert.That(destination, Is.EqualTo(expected));
        Assert.That(alias, Is.EqualTo(expected));

        alias = new regMaskTP((regMask)0x55);
        Assert.That(destination, Is.EqualTo(new regMaskTP((regMask)0x55)));

        destination = new regMaskTP((regMask)0xAA);
        Assert.That(alias, Is.EqualTo(new regMaskTP((regMask)0xAA)));
    }

    [TestCaseSource(nameof(RightShiftCounts))]
    public static void CompoundRightShiftAssignsTheOrdinaryReadonlyValue(int count)
    {
        var destination = CreateMask(ulong.MaxValue, ulong.MaxValue);
        var original = destination;
        var expected = destination >> count;

        destination >>= count;

        Assert.That(destination, Is.EqualTo(expected));
        Assert.That(original, Is.EqualTo(CreateMask(ulong.MaxValue, ulong.MaxValue)));
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.That(destination.Upper, Is.EqualTo(SRBM_NONE));
        }
    }

    private static IEnumerable<TestCaseData> BooleanConversionCases()
    {
        yield return new TestCaseData(0UL, 0UL, false);
        yield return new TestCaseData(1UL, 0UL, true);
        yield return new TestCaseData(0x80000000UL, 0UL, true);

        if (Configuration.REGMASK_BITS_64)
        {
            yield return new TestCaseData(0x8000000000000000UL, 0UL, true);
            yield return new TestCaseData(ulong.MaxValue, 0UL, true);
        }

        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            yield return new TestCaseData(0UL, 1UL, true);
            yield return new TestCaseData(0UL, 0x8000000000000000UL, true);
            yield return new TestCaseData(0x8000000000000000UL, 0x8000000000000000UL, true);
        }
    }

    private static IEnumerable<TestCaseData> IntegerFactoryCases()
    {
        yield return new TestCaseData(0UL);
        yield return new TestCaseData(0x80000000UL);
        yield return new TestCaseData(0xAAAAAAAAUL);
        yield return new TestCaseData(0xFFFFFFFFUL);

        if (Configuration.REGMASK_BITS_64)
        {
            yield return new TestCaseData(0x8000000000000000UL);
            yield return new TestCaseData(0xAAAAAAAAAAAAAAAAUL);
            yield return new TestCaseData(ulong.MaxValue);
        }
    }

    private static IEnumerable<TestCaseData> UnsignedIntConversionCases()
    {
        yield return new TestCaseData(0UL, 0UL, 0U);
        yield return new TestCaseData(0UL, ulong.MaxValue, 0U);
        yield return new TestCaseData(0x80000000UL, 1UL, 0x80000000U);
        yield return new TestCaseData(0x8000000000000000UL, ulong.MaxValue, 0U);
        yield return new TestCaseData(0xAAAAAAAA55555555UL, ulong.MaxValue, 0x55555555U);
        yield return new TestCaseData(ulong.MaxValue, ulong.MaxValue, uint.MaxValue);
    }

    private static IEnumerable<TestCaseData> RightShiftCases()
    {
        yield return new TestCaseData(0UL, 0UL, 0, 0UL);
        yield return new TestCaseData(1UL, 0UL, 0, 1UL);
        yield return new TestCaseData(1UL, 0UL, 1, 0UL);
        yield return new TestCaseData(0x80000000UL, 0UL, 1, 0x40000000UL);
        yield return new TestCaseData(0xFFFFFFFFUL, 0UL, 31, 1UL);
        yield return new TestCaseData(0x80000001UL, 0UL, 31, 1UL);

        if (Configuration.REGMASK_BITS_64)
        {
            yield return new TestCaseData(0x8000000000000000UL, 0UL, 0, 0x8000000000000000UL);
            yield return new TestCaseData(0x8000000000000000UL, 0UL, 1, 0x4000000000000000UL);
            yield return new TestCaseData(ulong.MaxValue, 0UL, 32, 0xFFFFFFFFUL);
            yield return new TestCaseData(0x8000000000000000UL, 0UL, 63, 1UL);
            yield return new TestCaseData(ulong.MaxValue, 0UL, 63, 1UL);
        }

        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            yield return new TestCaseData(0UL, ulong.MaxValue, 0, 0UL);
            yield return new TestCaseData(0x8000000000000001UL, ulong.MaxValue, 0, 0x8000000000000001UL);
            yield return new TestCaseData(0x8000000000000001UL, 0x8000000000000000UL, 63, 1UL);
        }
    }

    private static IEnumerable<TestCaseData> RightShiftCounts()
    {
        yield return new TestCaseData(0);
        yield return new TestCaseData(1);
        yield return new TestCaseData(31);

        if (Configuration.REGMASK_BITS_64)
        {
            yield return new TestCaseData(63);
        }
    }

    private static regMaskTP CreateMask(ulong lower, ulong upper)
    {
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            return new regMaskTP(unchecked((regMask)lower), unchecked((regMask)upper));
        }

        return new regMaskTP(unchecked((regMask)lower));
    }

    private static int s_assertions;

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = TestConfiguration.CreateJitTls(&ee);
        var previous = JitTls.Compiler;
        JitTls.Compiler = null;
        s_assertions = 0;
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

        return 0;
    }
}
