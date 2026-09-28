// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64ScalableValueNumTests
{
    [Test]
    public static void ZeroHasScalableConstantStorage()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = store.VNZeroForType(TYP_SIMD);

            Assert.That(store.IsVNConstant(vn), Is.True);
            Assert.That(store.TypeOfVN(vn), Is.EqualTo(TYP_SIMD));
            Assert.That(store.VNZeroForType(TYP_SIMD), Is.EqualTo(vn));
            Assert.That(store.GetConstantSimdScalable(vn), Is.EqualTo(simdscalable_t.Zero));
        });
    }

    [TestCase(SimdScalableRepeated)]
    [TestCase(SimdScalableSequence)]
    [TestCase(SimdScalableScalar)]
    public static void TreeConstantsInternBothValueNumbers(SimdScalableKind kind)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var first = new GenTreeVecCon(TYP_SIMD) {
                SimdScalableVal = Value(TYP_LONG, kind, ulong.MaxValue, 2),
            };
            var second = new GenTreeVecCon(TYP_SIMD) {
                SimdScalableVal = first.SimdScalableVal,
            };
            compiler.fgValueNumberTreeConst(first);
            compiler.fgValueNumberTreeConst(second);

            Assert.That(first._vnPair.BothEqual(), Is.True);
            Assert.That(first._vnPair, Is.EqualTo(second._vnPair));
            Assert.That(store.IsVNConstant(first._vnPair.Liberal), Is.True);
            Assert.That(store.TypeOfVN(first._vnPair.Liberal), Is.EqualTo(TYP_SIMD));
            Assert.That(store.GetConstantSimdScalable(first._vnPair.Liberal), Is.EqualTo(first.SimdScalableVal));
        });
    }

    [TestCase(SimdScalableRepeated, 9UL)]
    [TestCase(SimdScalableScalar, 9UL)]
    [TestCase(SimdScalableSequence, 0UL)]
    public static void ZeroEncodingsShareTheFirstInternedPayload(SimdScalableKind kind, ulong step)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var value = Value(TYP_DOUBLE, kind, 0, step);
            var vn = store.VNForSimdScalableCon(value);

            Assert.That(store.VNZeroForType(TYP_SIMD), Is.EqualTo(vn));
            var stored = store.GetConstantSimdScalable(vn);
            Assert.That(stored.BaseType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(stored.Kind, Is.EqualTo(kind));
            Assert.That(stored.Step.u64[0], Is.EqualTo(step));
        });
    }

    [TestCase(TYP_BYTE, SimdScalableRepeated, 255UL, 0UL)]
    [TestCase(TYP_UBYTE, SimdScalableScalar, 255UL, 0UL)]
    [TestCase(TYP_UBYTE, SimdScalableRepeated, 255UL, 1UL)]
    [TestCase(TYP_UBYTE, SimdScalableRepeated, 511UL, 0UL)]
    [TestCase(TYP_FLOAT, SimdScalableRepeated, 0x8000_0000UL, 0UL)]
    [TestCase(TYP_DOUBLE, SimdScalableRepeated, 0x8000_0000_0000_0000UL, 0UL)]
    [TestCase(TYP_FLOAT, SimdScalableSequence, 0x7FC0_1234UL, 0x7FC0_5678UL)]
    [TestCase(TYP_DOUBLE, SimdScalableSequence, 0x7FF8_0000_0000_1234UL, 0x7FF8_0000_0000_5678UL)]
    public static void NonzeroKeysPreserveTypeKindAndRawBits(
        var_types type, SimdScalableKind kind, ulong index, ulong step)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var baseline = store.VNForSimdScalableCon(Value(TYP_UBYTE, SimdScalableRepeated, 255, 0));
            var value = Value(type, kind, index, step);
            var vn = store.VNForSimdScalableCon(value);

            Assert.That(vn, Is.Not.EqualTo(baseline));
            Assert.That(vn, Is.Not.EqualTo(store.VNZeroForType(TYP_SIMD)));
            Assert.That(store.VNForSimdScalableCon(value), Is.EqualTo(vn));
            var stored = store.GetConstantSimdScalable(vn);
            Assert.That(stored.BaseType, Is.EqualTo(type));
            Assert.That(stored.Kind, Is.EqualTo(kind));
            Assert.That(stored.Index.u64[0], Is.EqualTo(index));
            Assert.That(stored.Step.u64[0], Is.EqualTo(step));

            value.Index.u64[0] ^= 1;
            Assert.That(store.VNForSimdScalableCon(value), Is.Not.EqualTo(vn));
        });
    }

    [Test]
    public static void StorageOwnsValueCopiesAcrossChunkBoundaries()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var values = new simdscalable_t[130];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = Value(TYP_LONG, SimdScalableSequence, (ulong)index + 1, 2);
            }
            var numbers = Array.ConvertAll(values, value => store.VNForSimdScalableCon(value));

            for (var index = 0; index < values.Length; index++)
            {
                var expected = values[index];
                values[index].Index.u64[0] = 0;
                var copy = store.GetConstantSimdScalable(numbers[index]);
                copy.Step.u64[0] = 0;

                Assert.That(store.GetConstantSimdScalable(numbers[index]), Is.EqualTo(expected));
                Assert.That(store.VNForSimdScalableCon(expected), Is.EqualTo(numbers[index]));
            }
        });
    }

    [Test]
    public static void FixedVectorAccessCannotReadScalableStorage()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var scalable = store.VNZeroForType(TYP_SIMD);
            var fixedVector = store.VNZeroForType(TYP_SIMD16);

            Assert.That(scalable, Is.Not.EqualTo(fixedVector));
            Assert.That(store.GetConstantSimd16(fixedVector), Is.EqualTo(simd16_t.Zero));
            _ = Assert.Throws<UnreachableException>(() => store.GetConstantSimd(scalable));
        });
    }

    [TestCase(VNF_OR, true)]
    [TestCase(VNF_AND, false)]
    public static void ComplementIdentitiesConstructScalableConstants(VNFunc operation, bool allBits)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var operand = store.VNForExpr(null, TYP_SIMD);
            var complement = store.VNForFunc(TYP_SIMD, VNF_NOT, operand);
            var result = store.VNForFunc(TYP_SIMD, operation, operand, complement);

            Assert.That(store.GetConstantSimdScalable(result),
                Is.EqualTo(allBits ? simdscalable_t.AllBitsSet : simdscalable_t.Zero));
        });
    }

#if DEBUG
    [TestCase(TYP_LONG, SimdScalableRepeated, ulong.MaxValue, 2UL,
        "long   0xffffffffffffffff, 0xffffffffffffffff, 0xffffffffffffffff...]")]
    [TestCase(TYP_LONG, SimdScalableSequence, ulong.MaxValue, 2UL,
        "long   0xffffffffffffffff, 0x0000000000000001, 0x0000000000000003...]")]
    [TestCase(TYP_BYTE, SimdScalableSequence, 255UL, 2UL,
        "byte   0x00000000000000ff, 0x0000000000000101, 0x0000000000000103...]")]
    [TestCase(TYP_FLOAT, SimdScalableScalar, 0x8000_0000UL, 2UL,
        "float  0x0000000080000000, 0x0, 0x0...]")]
    [TestCase(TYP_DOUBLE, SimdScalableSequence, 0x3FF0_0000_0000_0000UL, 1UL,
        "double 0x3ff0000000000000, 0x3ff0000000000001, 0x3ff0000000000002...]")]
    public static void DumpsUseRawUnsignedArithmetic(
        var_types type, SimdScalableKind kind, ulong index, ulong step, string expected)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var vn = store.VNForSimdScalableCon(Value(type, kind, index, step));
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            try
            {
                store.vnDump(compiler, vn);
                writer.Flush();
                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo($" {{SimdScalableCns[{expected}}}"));
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }
#endif

    private static simdscalable_t Value(var_types type, SimdScalableKind kind, ulong index, ulong step)
    {
        var value = new simdscalable_t { BaseType = type, Kind = kind };
        value.Index.u64[0] = index;
        value.Step.u64[0] = step;

        return value;
    }
}
#endif
