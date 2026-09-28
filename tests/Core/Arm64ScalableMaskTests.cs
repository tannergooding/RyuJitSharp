// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64ScalableMaskTests
{
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_UBYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_ULONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ZeroEqualityAndHashingIgnoreBaseType(var_types type)
    {
        var value = new simdmaskscalable_t { BaseType = type };
        var zero = new simdmaskscalable_t { BaseType = TYP_BYTE };
        var wrapped = simdmaskvalue_t.FromScalable(value);

        Assert.That(value.IsZero, Is.True);
        Assert.That(value, Is.EqualTo(zero));
        Assert.That(value.GetHashCode(), Is.EqualTo(zero.GetHashCode()));
        Assert.That(wrapped, Is.EqualTo(simdmaskvalue_t.FromScalable(zero)));
        Assert.That(wrapped.GetHashCode(), Is.EqualTo(1));
        Assert.That(wrapped, Is.Not.EqualTo(simdmaskvalue_t.FromFixed(simdmask_t.Zero)));
    }

    [TestCase(TYP_BYTE, TYP_UBYTE, true)]
    [TestCase(TYP_SHORT, TYP_USHORT, true)]
    [TestCase(TYP_INT, TYP_FLOAT, true)]
    [TestCase(TYP_LONG, TYP_DOUBLE, true)]
    [TestCase(TYP_BYTE, TYP_LONG, false)]
    [TestCase(TYP_LONG, TYP_BYTE, false)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, false)]
    public static void TrueMasksRequireMatchingElementWidths(var_types storedType, var_types requestedType, bool allBits)
    {
        var value = new simdmaskscalable_t { BaseType = storedType, Index = 1 };

        Assert.That(value.IsAllBitsSet(requestedType), Is.EqualTo(allBits));
        Assert.That(value.IsZero, Is.False);
        Assert.That(value, Is.Not.EqualTo(new simdmaskscalable_t { BaseType = requestedType, Index = 1 }));
        value.Index = 0;
        Assert.That(value.IsAllBitsSet(requestedType), Is.False);
        Assert.That(simdmaskscalable_t.AllBitsSet.IsAllBitsSet(TYP_BYTE), Is.True);
        Assert.That(simdmaskscalable_t.AllBitsSet.IsAllBitsSet(TYP_LONG), Is.False);
    }

    [Test]
    public static void TaggedStorageSeparatesKindsAndOwnsCopies()
    {
        var scalable = simdmaskscalable_t.AllBitsSet;
        var fixedMask = simdmask_t.AllBitsSet(64);
        var scalableValue = simdmaskvalue_t.FromScalable(scalable);
        var fixedValue = simdmaskvalue_t.FromFixed(fixedMask);
        scalable.Index = 0;
        fixedMask.u64[0] = 0;

        Assert.That(scalableValue.IsScalable, Is.True);
        Assert.That(scalableValue.Scalable.Index, Is.EqualTo(1));
        Assert.That(scalableValue.Fixed.IsZero, Is.True);
        Assert.That(fixedValue.IsScalable, Is.False);
        Assert.That(fixedValue.Fixed.u64[0], Is.EqualTo(ulong.MaxValue));

        var values = new HashSet<simdmaskvalue_t> {
            fixedValue, scalableValue,
            simdmaskvalue_t.FromScalable(new simdmaskscalable_t { BaseType = TYP_BYTE }),
            simdmaskvalue_t.FromScalable(new simdmaskscalable_t { BaseType = TYP_DOUBLE }),
            simdmaskvalue_t.FromFixed(simdmask_t.Zero),
        };
        Assert.That(values, Has.Count.EqualTo(4));
    }

    [TestCase(TYP_BYTE, 0xFFUL)]
    [TestCase(TYP_UBYTE, 0xFFUL)]
    [TestCase(TYP_SHORT, 0xFFFFUL)]
    [TestCase(TYP_USHORT, 0xFFFFUL)]
    [TestCase(TYP_INT, 0xFFFF_FFFFUL)]
    [TestCase(TYP_UINT, 0xFFFF_FFFFUL)]
    [TestCase(TYP_LONG, ulong.MaxValue)]
    [TestCase(TYP_ULONG, ulong.MaxValue)]
    [TestCase(TYP_FLOAT, 0xFFFF_FFFFUL)]
    [TestCase(TYP_DOUBLE, ulong.MaxValue)]
    public static void TrueMasksBecomeRepeatedAllBitsVectors(var_types type, ulong expected)
    {
        var mask = new simdmaskscalable_t { BaseType = type, Index = 1 };
        var result = Vector(TYP_BYTE, SimdScalableSequence, 12, 13);

        Assert.That(EvaluateSimdCvtScalableMaskToVector(type, ref result, mask), Is.True);
        Assert.That(result.BaseType, Is.EqualTo(type));
        Assert.That(result.Kind, Is.EqualTo(SimdScalableRepeated));
        Assert.That(result.Index.u64[0], Is.EqualTo(expected));
        Assert.That(result.Step.u64[0], Is.Zero);
    }

    [TestCase(TYP_BYTE, TYP_UBYTE, 1UL)]
    [TestCase(TYP_INT, TYP_FLOAT, 0x8000_0000UL)]
    [TestCase(TYP_FLOAT, TYP_FLOAT, 0x1_0000_0000UL)]
    [TestCase(TYP_DOUBLE, TYP_LONG, 0x7FF8_0000_0000_1234UL)]
    public static void RepeatedVectorConversionUsesAnyNonzeroRawBits(
        var_types storedType, var_types requestedType, ulong bits)
    {
        var value = Vector(storedType, SimdScalableRepeated, bits, 99);
        simdmaskscalable_t result = default;

        Assert.That(EvaluateSimdCvtScalableVectorToMask(requestedType, ref result, value), Is.True);
        Assert.That(result.BaseType, Is.EqualTo(requestedType));
        Assert.That(result.Index, Is.EqualTo(1));
    }

    [TestCase(SimdScalableRepeated, 9UL)]
    [TestCase(SimdScalableScalar, 9UL)]
    [TestCase(SimdScalableSequence, 0UL)]
    public static void ZeroConversionsIgnoreSourceWidth(SimdScalableKind kind, ulong step)
    {
        var vector = Vector(TYP_DOUBLE, kind, 0, step);
        var mask = simdmaskscalable_t.AllBitsSet;

        Assert.That(EvaluateSimdCvtScalableVectorToMask(TYP_BYTE, ref mask, vector), Is.True);
        Assert.That(mask.BaseType, Is.EqualTo(TYP_BYTE));
        Assert.That(mask.Index, Is.Zero);
        Assert.That(EvaluateSimdCvtScalableMaskToVector(TYP_FLOAT, ref vector, mask), Is.True);
        Assert.That(vector.BaseType, Is.EqualTo(TYP_FLOAT));
        Assert.That(vector.Kind, Is.EqualTo(SimdScalableRepeated));
        Assert.That(vector.Index.u64[0], Is.Zero);
        Assert.That(vector.Step.u64[0], Is.Zero);
    }

    [TestCase(SimdScalableRepeated, TYP_BYTE)]
    [TestCase(SimdScalableSequence, TYP_LONG)]
    [TestCase(SimdScalableScalar, TYP_LONG)]
    public static void UnrepresentableVectorsLeaveMaskOutputUnchanged(SimdScalableKind kind, var_types type)
    {
        var vector = Vector(TYP_LONG, kind, 1, 1);
        var result = simdmaskscalable_t.AllBitsSet;
        var expected = result;

        Assert.That(EvaluateSimdCvtScalableVectorToMask(type, ref result, vector), Is.False);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public static void MismatchedMaskWidthLeavesVectorOutputUnchanged()
    {
        var mask = simdmaskscalable_t.AllBitsSet;
        var result = Vector(TYP_DOUBLE, SimdScalableSequence, 3, 5);
        var expected = result;

        Assert.That(EvaluateSimdCvtScalableMaskToVector(TYP_LONG, ref result, mask), Is.False);
        Assert.That(result, Is.EqualTo(expected));
    }

    private static simdscalable_t Vector(var_types type, SimdScalableKind kind, ulong index, ulong step)
    {
        var value = new simdscalable_t { BaseType = type, Kind = kind };
        value.Index.u64[0] = index;
        value.Step.u64[0] = step;

        return value;
    }
}
#endif
