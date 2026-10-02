// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if DEBUG
using System;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FixedBitVectTests
{
    [TestCaseSource(nameof(SizeCases))]
    public static void InitializationIsEmptyAndRetainsTheRequestedSize(uint size)
    {
        var vector = Create(size);

        Assert.That(vector.bitVectGetSize(), Is.EqualTo(size));
        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(uint.MaxValue), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(0), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(size - 1), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(size), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(uint.MaxValue - 1), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNextAndClear(), Is.EqualTo(uint.MaxValue));

        for (uint bit = 0; bit < size; bit++)
        {
            Assert.That(vector.bitVectTest(bit), Is.False);
        }
    }

    [TestCaseSource(nameof(SizeCases))]
    public static void SetClearAndTestPreserveOtherBitsAcrossChunks(uint size)
    {
        var vector = Create(size);

        for (uint bit = 0; bit < size; bit += 2)
        {
            vector.bitVectSet(bit);
            vector.bitVectSet(bit);
        }

        for (uint bit = 0; bit < size; bit++)
        {
            Assert.That(vector.bitVectTest(bit), Is.EqualTo((bit % 2) == 0));

            if ((bit % 2) == 0)
            {
                vector.bitVectClear(bit);
                vector.bitVectClear(bit);
            }
            else
            {
                vector.bitVectSet(bit);
            }
        }

        for (uint bit = 0; bit < size; bit++)
        {
            Assert.That(vector.bitVectTest(bit), Is.EqualTo((bit % 2) != 0));
        }
    }

    [TestCase(0U)]
    [TestCase(1U)]
    [TestCase(31U)]
    [TestCase(32U)]
    [TestCase(63U)]
    [TestCase(64U)]
    [TestCase(95U)]
    [TestCase(96U)]
    public static void SingleBitScansSkipEmptyChunksAndClearTheExactPosition(uint selected)
    {
        var vector = Create(97);
        vector.bitVectSet(selected);

        for (uint bit = 0; bit < vector.bitVectGetSize(); bit++)
        {
            Assert.That(vector.bitVectTest(bit), Is.EqualTo(bit == selected));
        }

        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(selected));
        Assert.That(vector.bitVectGetNext(uint.MaxValue), Is.EqualTo(selected));
        Assert.That(vector.bitVectGetNext(selected), Is.EqualTo(uint.MaxValue));

        if (selected != 0)
        {
            Assert.That(vector.bitVectGetNext(selected - 1), Is.EqualTo(selected));
        }

        Assert.That(vector.bitVectGetNextAndClear(), Is.EqualTo(selected));
        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectTest(selected), Is.False);
    }

    [TestCaseSource(nameof(SizeCases))]
    public static void FullVectorEnumerationIsAscendingAndNonDestructive(uint size)
    {
        var vector = Create(size);

        for (uint bit = 0; bit < size; bit++)
        {
            vector.bitVectSet(bit);
        }

        for (var pass = 0; pass < 2; pass++)
        {
            var next = vector.bitVectGetFirst();

            for (uint bit = 0; bit < size; bit++)
            {
                Assert.That(next, Is.EqualTo(bit));
                Assert.That(vector.bitVectTest(bit), Is.True);
                next = vector.bitVectGetNext(next);
            }

            Assert.That(next, Is.EqualTo(uint.MaxValue));
        }

        Assert.That(vector.bitVectGetNext(size), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(uint.MaxValue - 1), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNext(uint.MaxValue), Is.EqualTo(0U));
    }

    [TestCase(uint.MaxValue, 0U)]
    [TestCase(0U, 1U)]
    [TestCase(1U, 31U)]
    [TestCase(30U, 31U)]
    [TestCase(31U, 32U)]
    [TestCase(32U, 63U)]
    [TestCase(62U, 63U)]
    [TestCase(63U, 64U)]
    [TestCase(64U, 95U)]
    [TestCase(94U, 95U)]
    [TestCase(95U, 96U)]
    [TestCase(96U, uint.MaxValue)]
    [TestCase(97U, uint.MaxValue)]
    [TestCase(127U, uint.MaxValue)]
    [TestCase(128U, uint.MaxValue)]
    [TestCase(uint.MaxValue - 1, uint.MaxValue)]
    public static void PopulatedEnumerationMasksThePreviousChunkAndResetsForFollowingChunks(uint previous, uint expected)
    {
        var vector = Create(97);
        uint[] bits = [0, 1, 31, 32, 63, 64, 95, 96];

        foreach (var bit in bits)
        {
            vector.bitVectSet(bit);
        }

        Assert.That(vector.bitVectGetNext(previous), Is.EqualTo(expected));

        foreach (var bit in bits)
        {
            Assert.That(vector.bitVectTest(bit), Is.True);
        }

        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(0U));
    }

    [TestCaseSource(nameof(DestructiveCases))]
    public static void GetNextAndClearRemovesOnlyTheLowestRemainingBit(uint size, bool full)
    {
        var vector = Create(size);

        for (uint bit = 0; bit < size; bit++)
        {
            if (full || IsPopulatedBit(bit, size))
            {
                vector.bitVectSet(bit);
            }
        }

        for (uint bit = 0; bit < size; bit++)
        {
            if (full || IsPopulatedBit(bit, size))
            {
                Assert.That(vector.bitVectGetFirst(), Is.EqualTo(bit));
                Assert.That(vector.bitVectGetNextAndClear(), Is.EqualTo(bit));
                Assert.That(vector.bitVectTest(bit), Is.False);
            }
        }

        Assert.That(vector.bitVectGetNextAndClear(), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNextAndClear(), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(uint.MaxValue));

        for (uint bit = 0; bit < size; bit++)
        {
            Assert.That(vector.bitVectTest(bit), Is.False);
        }
    }

    [TestCaseSource(nameof(SizeCases))]
    public static void SetAlgebraMutatesOnlyTheDestinationAndSupportsSelfAliasing(uint size)
    {
        var union = Create(size);
        var intersection = Create(size);
        var right = Create(size);

        for (uint bit = 0; bit < size; bit++)
        {
            if ((bit % 2) == 0)
            {
                union.bitVectSet(bit);
                intersection.bitVectSet(bit);
            }

            if ((bit % 3) == 0)
            {
                right.bitVectSet(bit);
            }
        }

        union.bitVectOr(right);
        intersection.bitVectAnd(right);
        union.bitVectOr(union);
        intersection.bitVectAnd(intersection);

        for (uint bit = 0; bit < size; bit++)
        {
            Assert.That(union.bitVectTest(bit), Is.EqualTo(((bit % 2) == 0) || ((bit % 3) == 0)));
            Assert.That(intersection.bitVectTest(bit), Is.EqualTo(((bit % 2) == 0) && ((bit % 3) == 0)));
            Assert.That(right.bitVectTest(bit), Is.EqualTo((bit % 3) == 0));
        }
    }

    [TestCase(1U)]
    [TestCase(31U)]
    [TestCase(33U)]
    [TestCase(63U)]
    [TestCase(65U)]
    [TestCase(95U)]
    [TestCase(97U)]
    public static void InclusiveSizeBoundaryIsRetainedWhenTheBitHasStorage(uint size)
    {
        // Non-multiples of 32 leave this native <= boundary inside the allocated final chunk.
        var vector = Create(size);
        vector.bitVectSet(size);

        Assert.That(vector.bitVectTest(size), Is.True);
        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(size));
        Assert.That(vector.bitVectGetNext(size), Is.EqualTo(uint.MaxValue));
        Assert.That(vector.bitVectGetNextAndClear(), Is.EqualTo(size));
        Assert.That(vector.bitVectTest(size), Is.False);

        vector.bitVectSet(size);
        vector.bitVectClear(size);
        Assert.That(vector.bitVectGetFirst(), Is.EqualTo(uint.MaxValue));
    }

    private static FixedBitVect Create(uint size)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        return FixedBitVect.bitVectInit(size, compiler);
    }

    private static bool IsPopulatedBit(uint bit, uint size)
    {
        return ((bit % 32) == 0) || ((bit % 32) == 31) || (bit == (size - 1));
    }

    private static readonly uint[] s_sizes = [1, 31, 32, 33, 63, 64, 65, 95, 96, 97];

    private static IEnumerable<TestCaseData> SizeCases()
    {
        foreach (var size in s_sizes)
        {
            yield return new TestCaseData(size);
        }
    }

    private static IEnumerable<TestCaseData> DestructiveCases()
    {
        foreach (var size in s_sizes)
        {
            yield return new TestCaseData(size, false);
            yield return new TestCaseData(size, true);
        }
    }

#if DEBUG
    private static readonly List<string?> s_assertions = [];

    [TestCase("Set")]
    [TestCase("Clear")]
    [TestCase("Test")]
    [TestCase("GetNext")]
    [TestCase("GetNextAndClear")]
    public static void SizeAssertionTextIsRetainedWithSafeInChunkContinuation(string operation)
    {
        WithAssertionRecorder(() => {
            var vector = Create(33);

            // Bit 34 violates the size assertion but still resides in the allocated second chunk.
            if (operation is "GetNext" or "GetNextAndClear")
            {
                vector.bitVectSet(34);
                s_assertions.Clear();
            }

            switch (operation)
            {
                case "Set":
                {
                    vector.bitVectSet(34);
                    break;
                }

                case "Clear":
                {
                    vector.bitVectClear(34);
                    break;
                }

                case "Test":
                {
                    _ = vector.bitVectTest(34);
                    break;
                }

                case "GetNext":
                {
                    _ = vector.bitVectGetNext(uint.MaxValue);
                    break;
                }

                case "GetNextAndClear":
                {
                    _ = vector.bitVectGetNextAndClear();
                    break;
                }
            }

            Assert.That(s_assertions, Has.Count.EqualTo(1));
            Assert.That(s_assertions[0], Is.EqualTo("bitNum <= bitVectSize"));
        });
    }

    [TestCase(false, "bitVectSize == bv->bitVectSize")]
    [TestCase(true, "bitVectSize == bv.bitVectSize")]
    public static void SizeMismatchAssertionsRetainNativeTextWithEqualChunkCounts(bool intersect, string expected)
    {
        WithAssertionRecorder(() => {
            var left = Create(33);
            var right = Create(34);

            if (intersect)
            {
                left.bitVectAnd(right);
            }
            else
            {
                left.bitVectOr(right);
            }

            Assert.That(s_assertions, Has.Count.EqualTo(1));
            Assert.That(s_assertions[0], Is.EqualTo(expected));
        });
    }

    [TestCaseSource(nameof(SizeCases))]
    public static void ValidOperationsDoNotAssert(uint size)
    {
        WithAssertionRecorder(() => {
            var vector = Create(size);
            var other = Create(size);
            vector.bitVectSet(size - 1);
            _ = vector.bitVectTest(size - 1);
            vector.bitVectOr(other);
            vector.bitVectAnd(vector);
            _ = vector.bitVectGetFirst();
            _ = vector.bitVectGetNext(size - 1);
            _ = vector.bitVectGetNextAndClear();
            vector.bitVectClear(size - 1);

            if ((size % 32) != 0)
            {
                vector.bitVectSet(size);
                _ = vector.bitVectTest(size);
                _ = vector.bitVectGetNextAndClear();
            }

            Assert.That(s_assertions, Is.Empty);
        });
    }

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
        action();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }
#endif
}
