// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64ScalableConstantTests
{
    [TestCase(TYP_BYTE, 0xFFUL, -1L, ulong.MaxValue)]
    [TestCase(TYP_SHORT, 0xFFFFUL, -1L, ulong.MaxValue)]
    [TestCase(TYP_INT, 0xFFFF_FFFFUL, -1L, ulong.MaxValue)]
    [TestCase(TYP_LONG, ulong.MaxValue, -1L, ulong.MaxValue)]
    [TestCase(TYP_UBYTE, 0xFFUL, 255L, 255UL)]
    [TestCase(TYP_USHORT, 0xFFFFUL, 65535L, 65535UL)]
    [TestCase(TYP_UINT, 0xFFFF_FFFFUL, 4294967295L, 4294967295UL)]
    [TestCase(TYP_ULONG, 0x7FFF_FFFF_FFFF_FFFFUL, long.MaxValue, 0x7FFF_FFFF_FFFF_FFFFUL)]
    public static void DecodePreservesSignedExtensionAndUnsignedMagnitude(
        var_types type, ulong bits, long immediate, ulong registerValue)
    {
        var value = Value(type, SimdScalableSequence, bits, bits);
        var info = Arm64SimdScalableConstInfo.Decode(value);

        Assert.Multiple(() => {
            Assert.That(info.baseType, Is.EqualTo(type));
            Assert.That(info.indexImm, Is.EqualTo(immediate));
            Assert.That(info.stepImm, Is.EqualTo(immediate));
            Assert.That(info.indexVal, Is.EqualTo(registerValue));
            Assert.That(info.stepVal, Is.EqualTo(registerValue));
            Assert.That(info.indexHasImm && info.stepHasImm, Is.True);
            Assert.That(info.Has64BitElements(), Is.EqualTo(type is TYP_LONG or TYP_ULONG));
        });
    }

    [TestCase(0x8000_0000_0000_0000UL, 15UL, false, true)]
    [TestCase(15UL, ulong.MaxValue, true, false)]
    [TestCase(ulong.MaxValue, ulong.MaxValue, false, false)]
    public static void UnsignedLongOverflowDisablesEachImmediateIndependently(
        ulong index, ulong step, bool indexImmediate, bool stepImmediate)
    {
        var info = Arm64SimdScalableConstInfo.Decode(Value(TYP_ULONG, SimdScalableSequence, index, step));

        Assert.Multiple(() => {
            Assert.That(info.indexVal, Is.EqualTo(index));
            Assert.That(info.stepVal, Is.EqualTo(step));
            Assert.That(info.indexHasImm, Is.EqualTo(indexImmediate));
            Assert.That(info.stepHasImm, Is.EqualTo(stepImmediate));
            Assert.That(info.indexImm, Is.EqualTo(indexImmediate ? (long)index : -1L));
            Assert.That(info.stepImm, Is.EqualTo(stepImmediate ? (long)step : -1L));
            Assert.That(info.IndexNeedsSequenceReg(), Is.EqualTo(!indexImmediate));
            Assert.That(info.StepNeedsSequenceReg(), Is.EqualTo(!stepImmediate));
        });
    }

    [TestCase(TYP_FLOAT, 0x8000_0000UL, 0x7FC0_1234UL)]
    [TestCase(TYP_DOUBLE, 0x8000_0000_0000_0000UL, 0x7FF8_0000_0000_1234UL)]
    public static void FloatingDecodePreservesSignedZeroAndNaNPayloadBits(
        var_types type, ulong index, ulong step)
    {
        var info = Arm64SimdScalableConstInfo.Decode(Value(type, SimdScalableSequence, index, step));

        Assert.Multiple(() => {
            Assert.That(info.indexVal, Is.EqualTo(index));
            Assert.That(info.stepVal, Is.EqualTo(step));
            Assert.That(info.indexHasImm || info.stepHasImm, Is.False);
            Assert.That(info.indexImm, Is.EqualTo(-1));
            Assert.That(info.stepImm, Is.EqualTo(-1));
            Assert.That(info.Has64BitElements(), Is.EqualTo(type == TYP_DOUBLE));
        });
    }

    [TestCase(-32769L, false)]
    [TestCase(-32768L, true)]
    [TestCase(-256L, true)]
    [TestCase(-129L, false)]
    [TestCase(-128L, true)]
    [TestCase(127L, true)]
    [TestCase(128L, false)]
    [TestCase(256L, true)]
    [TestCase(32512L, true)]
    [TestCase(32768L, false)]
    [TestCase(long.MinValue, false)]
    [TestCase(long.MaxValue, false)]
    public static void RepeatedIntegerUsesSignedByteOrShiftedSignedByte(long immediate, bool encodable)
    {
        var value = Value(TYP_LONG, SimdScalableRepeated, unchecked((ulong)immediate), 0);
        var info = Arm64SimdScalableConstInfo.Decode(value);

        Assert.That(info.CanEncodeRepeated(value), Is.EqualTo(encodable));
        Assert.That(info.CanEncodeScalar(value, emitAttr.EA_8BYTE), Is.False);
    }

    [TestCase(-17L, 0L, true, false)]
    [TestCase(-16L, 15L, false, false)]
    [TestCase(15L, -16L, false, false)]
    [TestCase(16L, -17L, true, true)]
    [TestCase(0L, 16L, false, true)]
    public static void SequenceOperandsUseIndependentSignedFiveBitImmediates(
        long index, long step, bool indexRegister, bool stepRegister)
    {
        var info = Arm64SimdScalableConstInfo.Decode(
            Value(TYP_LONG, SimdScalableSequence, unchecked((ulong)index), unchecked((ulong)step)));

        Assert.Multiple(() => {
            Assert.That(info.IndexNeedsSequenceReg(), Is.EqualTo(indexRegister));
            Assert.That(info.StepNeedsSequenceReg(), Is.EqualTo(stepRegister));
            Assert.That(info.CanEncodeSequence(), Is.EqualTo(!indexRegister && !stepRegister));
        });
    }

    [TestCase(TYP_FLOAT, 0x3F80_0000UL, true)]
    [TestCase(TYP_FLOAT, 0x8000_0000UL, false)]
    [TestCase(TYP_FLOAT, 0x7FC0_1234UL, false)]
    [TestCase(TYP_DOUBLE, 0x3FF0_0000_0000_0000UL, true)]
    [TestCase(TYP_DOUBLE, 0x8000_0000_0000_0000UL, false)]
    [TestCase(TYP_DOUBLE, 0x7FF8_0000_0000_1234UL, false)]
    public static void FloatingFormsReuseArm64ImmediateEncoding(var_types type, ulong bits, bool encodable)
    {
        var value = Value(type, SimdScalableScalar, bits, 0);
        var info = Arm64SimdScalableConstInfo.Decode(value);

        Assert.That(info.CanEncodeRepeated(value), Is.EqualTo(encodable));
        Assert.That(info.CanEncodeScalar(value, type.EmitActualSize), Is.EqualTo(encodable));
    }

    [TestCase(TYP_BYTE, 0xFFUL)]
    [TestCase(TYP_SHORT, 0xFFFFUL)]
    [TestCase(TYP_INT, 0xFFFF_FFFFUL)]
    [TestCase(TYP_ULONG, ulong.MaxValue)]
    [TestCase(TYP_FLOAT, 0xFFFF_FFFFUL)]
    [TestCase(TYP_DOUBLE, ulong.MaxValue)]
    public static void AllBitsSetRequiresRepeatedValuesOfExactlyTheElementWidth(var_types type, ulong bits)
    {
        Assert.That(Value(type, SimdScalableRepeated, bits, 0).IsAllBitsSet, Is.True);
        Assert.That(Value(type, SimdScalableSequence, bits, 0).IsAllBitsSet, Is.False);
        Assert.That(Value(type, SimdScalableScalar, bits, 0).IsAllBitsSet, Is.False);
        Assert.That(Value(type, SimdScalableRepeated, bits - 1, 0).IsAllBitsSet, Is.False);
    }

    [TestCase(SimdScalableRepeated, 9UL, true)]
    [TestCase(SimdScalableScalar, 9UL, true)]
    [TestCase(SimdScalableSequence, 0UL, true)]
    [TestCase(SimdScalableSequence, 1UL, false)]
    public static void ZeroEqualityIgnoresBaseTypeAndOnlyInspectsSequenceSteps(
        SimdScalableKind kind, ulong step, bool zero)
    {
        var value = Value(TYP_DOUBLE, kind, 0, step);

        Assert.That(value.IsZero, Is.EqualTo(zero));
        Assert.That(value == simdscalable_t.Zero, Is.EqualTo(zero));
        if (zero)
        {
            Assert.That(value.GetHashCode(), Is.EqualTo(simdscalable_t.Zero.GetHashCode()));
        }
        Assert.That(simdscalable_t.AllBitsSet.IsAllBitsSet, Is.True);
        Assert.That(simdscalable_t.AllBitsSet.IsZero, Is.False);
    }

    [Test]
    public static void NonzeroEqualityPreservesTypeKindAndBothRawOperands()
    {
        var value = Value(TYP_UBYTE, SimdScalableRepeated, 255, 0);

        Assert.That(value, Is.EqualTo(Value(TYP_UBYTE, SimdScalableRepeated, 255, 0)));
        Assert.That(value != Value(TYP_BYTE, SimdScalableRepeated, 255, 0), Is.True);
        Assert.That(value != Value(TYP_UBYTE, SimdScalableScalar, 255, 0), Is.True);
        Assert.That(value != Value(TYP_UBYTE, SimdScalableRepeated, 255, 1), Is.True);
        Assert.That(value != Value(TYP_UBYTE, SimdScalableRepeated, 0x1FF, 0), Is.True);
        Assert.That(Value(TYP_UBYTE, SimdScalableRepeated, 0x1FF, 0).IsAllBitsSet, Is.False);
    }

    [TestCase(TYP_FLOAT, 0x8000_0000UL)]
    [TestCase(TYP_DOUBLE, 0x8000_0000_0000_0000UL)]
    public static void NegativeZeroDoesNotEqualTheCanonicalZero(var_types type, ulong bits)
    {
        var value = Value(type, SimdScalableRepeated, bits, 0);

        Assert.That(value.IsZero, Is.False);
        Assert.That(value == simdscalable_t.Zero, Is.False);
    }

    private static simdscalable_t Value(var_types type, SimdScalableKind kind, ulong index, ulong step)
    {
        var value = new simdscalable_t { BaseType = type, Kind = kind };
        value.Index.u64[0] = index;
        value.Step.u64[0] = step;

        return value;
    }
}
#endif
