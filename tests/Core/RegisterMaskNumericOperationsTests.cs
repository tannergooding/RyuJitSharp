// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
#if DEBUG && !REGMASK_BITS_32
using System;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RegisterMaskNumericOperationsTests
{
    [TestCase(0UL, 0UL, false)]
    [TestCase(1UL, 0UL, true)]
    [TestCase(0x80000000UL, 0UL, true)]
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL, 0UL, true)]
    [TestCase(ulong.MaxValue, 0UL, true)]
#endif
#if HAS_MORE_THAN_64_REGISTERS
    [TestCase(0UL, 1UL, true)]
    [TestCase(0UL, 0x8000000000000000UL, true)]
    [TestCase(0x8000000000000000UL, 0x8000000000000000UL, true)]
#endif
    public static void BooleanConversionInspectsBothCompleteBanks(ulong lower, ulong upper, bool expected)
    {
        var mask = CreateMask(lower, upper);

        Assert.That((bool)mask, Is.EqualTo(expected));
        Assert.That(mask.IsEmpty, Is.EqualTo(!expected));
    }

#if !REGMASK_BITS_32
    [TestCase(0UL, 0UL, 0U)]
    [TestCase(0UL, ulong.MaxValue, 0U)]
    [TestCase(0x80000000UL, 1UL, 0x80000000U)]
    [TestCase(0x8000000000000000UL, ulong.MaxValue, 0U)]
    [TestCase(0xAAAAAAAA55555555UL, ulong.MaxValue, 0x55555555U)]
    [TestCase(ulong.MaxValue, ulong.MaxValue, uint.MaxValue)]
    public static void UnsignedIntConversionTruncatesTheLowBankWithoutAsserting(ulong lower, ulong upper, uint expected)
    {
        var mask = CreateMask(lower, upper);
#if DEBUG
        WithAssertionRecorder(() => Assert.That(checked((uint)mask), Is.EqualTo(expected)));
        Assert.That(s_assertions, Is.Zero);
#else
        Assert.That(checked((uint)mask), Is.EqualTo(expected));
#endif
    }
#endif

    [TestCase(0UL)]
    [TestCase(0x80000000UL)]
    [TestCase(0xAAAAAAAAUL)]
    [TestCase(0xFFFFFFFFUL)]
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL)]
    [TestCase(0xAAAAAAAAAAAAAAAAUL)]
    [TestCase(ulong.MaxValue)]
#endif
    public static void IntegerFactoryPreservesTheRawBankAndClearsTheUpperBank(ulong bits)
    {
        var lower = unchecked((regMask)bits);
        var mask = regMaskTP.FromIntRegSet(lower);

        Assert.That(mask.Lower, Is.EqualTo(lower));
        Assert.That(mask.IntRegSet, Is.EqualTo(lower));
#if HAS_MORE_THAN_64_REGISTERS
        Assert.That(mask.Upper, Is.EqualTo(SRBM_NONE));
#endif
    }

    [TestCase(0UL, 0UL, 0, 0UL)]
    [TestCase(1UL, 0UL, 0, 1UL)]
    [TestCase(1UL, 0UL, 1, 0UL)]
    [TestCase(0x80000000UL, 0UL, 1, 0x40000000UL)]
    [TestCase(0xFFFFFFFFUL, 0UL, 31, 1UL)]
    [TestCase(0x80000001UL, 0UL, 31, 1UL)]
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL, 0UL, 0, 0x8000000000000000UL)]
    [TestCase(0x8000000000000000UL, 0UL, 1, 0x4000000000000000UL)]
    [TestCase(ulong.MaxValue, 0UL, 32, 0xFFFFFFFFUL)]
    [TestCase(0x8000000000000000UL, 0UL, 63, 1UL)]
    [TestCase(ulong.MaxValue, 0UL, 63, 1UL)]
#endif
#if HAS_MORE_THAN_64_REGISTERS
    [TestCase(0UL, ulong.MaxValue, 0, 0UL)]
    [TestCase(0x8000000000000001UL, ulong.MaxValue, 0, 0x8000000000000001UL)]
    [TestCase(0x8000000000000001UL, 0x8000000000000000UL, 63, 1UL)]
#endif
    public static void RightShiftIsLogicalAndOnlyReconstructsTheLowBank(
        ulong lower, ulong upper, int count, ulong expected)
    {
        var mask = CreateMask(lower, upper);
        var original = mask;
        var result = mask >> count;

        Assert.That(result, Is.EqualTo(new regMaskTP(unchecked((regMask)expected))));
        Assert.That(mask, Is.EqualTo(original));
#if HAS_MORE_THAN_64_REGISTERS
        Assert.That(result.Upper, Is.EqualTo(SRBM_NONE));
#endif
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
#if REGMASK_BITS_64
    [TestCase(63)]
#endif
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

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
#if REGMASK_BITS_64
    [TestCase(63)]
#endif
    public static void CompoundRightShiftAssignsTheOrdinaryReadonlyValue(int count)
    {
        var destination = CreateMask(ulong.MaxValue, ulong.MaxValue);
        var original = destination;
        var expected = destination >> count;

        destination >>= count;

        Assert.That(destination, Is.EqualTo(expected));
        Assert.That(original, Is.EqualTo(CreateMask(ulong.MaxValue, ulong.MaxValue)));
#if HAS_MORE_THAN_64_REGISTERS
        Assert.That(destination.Upper, Is.EqualTo(SRBM_NONE));
#endif
    }

    private static regMaskTP CreateMask(ulong lower, ulong upper)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(unchecked((regMask)lower), unchecked((regMask)upper));
#else
        return new regMaskTP(unchecked((regMask)lower));
#endif
    }

#if DEBUG && !REGMASK_BITS_32
    private static int s_assertions;

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
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
#endif
}
