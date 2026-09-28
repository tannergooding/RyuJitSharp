// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64ScalableMaskValueNumTests
{
    [TestCase(0UL)]
    [TestCase(0x8000_0000_0000_0000UL)]
    [TestCase(ulong.MaxValue)]
    public static void FixedMasksRoundTripOnArm64(ulong bits)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            simdmask_t value = default;
            value.u64[0] = bits;
            var vn = store.VNForSimdMaskCon(value);

            Assert.That(store.TypeOfVN(vn), Is.EqualTo(TYP_MASK));
            Assert.That(store.GetConstantSimdMask(vn).u64[0], Is.EqualTo(bits));
            Assert.That(store.VNForSimdMaskCon(value), Is.EqualTo(vn));
            Assert.That(store.GetConstantSimdMaskValue(vn).IsScalable, Is.False);
            var tree = new GenTreeMskCon(value);
            compiler.fgValueNumberTreeConst(tree);
            Assert.That(tree._vnPair.BothEqual(), Is.True);
            Assert.That(tree._vnPair.Liberal, Is.EqualTo(vn));
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ScalableZeroCanonicalizesWithoutMergingFixedZero(var_types type)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var value = new simdmaskscalable_t { BaseType = type };
            var vn = store.VNForSimdMaskScalableCon(value);
            var zero = store.VNForSimdMaskScalableCon(new simdmaskscalable_t { BaseType = TYP_BYTE });

            Assert.That(vn, Is.EqualTo(zero));
            Assert.That(vn, Is.Not.EqualTo(store.VNZeroForType(TYP_MASK)));
            Assert.That(store.GetConstantSimdMaskValue(vn).IsScalable, Is.True);
            Assert.That(store.GetConstantSimdMaskScalable(vn).BaseType, Is.EqualTo(type));
            Assert.That(store.GetConstantSimdMaskScalable(vn).Index, Is.Zero);
        });
    }

    [Test]
    public static void BothPayloadKindsShareChunksWithoutLosingBits()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var values = new simdmask_t[130];
            for (var index = 0; index < values.Length; index++)
            {
                values[index].u64[0] = (ulong)index;
            }
            var scalableValue = simdmaskscalable_t.AllBitsSet;
            var scalable = store.VNForSimdMaskScalableCon(scalableValue);
            var numbers = Array.ConvertAll(values, value => store.VNForSimdMaskCon(value));
            var anotherScalable = store.VNForSimdMaskScalableCon(
                new simdmaskscalable_t { BaseType = TYP_LONG, Index = 1 });
            scalableValue.Index = 0;

            Assert.That(anotherScalable, Is.Not.EqualTo(scalable));
            Assert.That(store.GetConstantSimdMaskScalable(scalable).Index, Is.EqualTo(1));
            for (var index = 0; index < values.Length; index++)
            {
                values[index].u64[0] = ulong.MaxValue;
                var copy = store.GetConstantSimdMask(numbers[index]);
                copy.u64[0] = ulong.MaxValue;
                Assert.That(store.GetConstantSimdMask(numbers[index]).u64[0], Is.EqualTo((ulong)index));
                Assert.That(numbers[index], Is.Not.EqualTo(scalable));
                Assert.That(numbers[index], Is.Not.EqualTo(anotherScalable));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TypedAccessorsEnforceTheStoredKind(bool scalable)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var vn = scalable
                ? store.VNForSimdMaskScalableCon(simdmaskscalable_t.AllBitsSet)
                : store.VNZeroForType(TYP_MASK);

            if (scalable)
            {
                _ = Assert.Throws<FatalJitException>(() => store.GetConstantSimdMask(vn));
            }
            else
            {
                _ = Assert.Throws<FatalJitException>(() => store.GetConstantSimdMaskScalable(vn));
            }
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void AllBitsUsesConfigurationButZeroRemainsFixed(bool scalable)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            SetScalableConfiguration(scalable);
            var store = new ValueNumStore(compiler);
            var operand = store.VNForExpr(null, TYP_MASK);
            var complement = store.VNForFunc(TYP_MASK, VNF_NOT, operand);
            var allBits = store.VNForFunc(TYP_MASK, VNF_OR, operand, complement);
            var storage = store.GetConstantSimdMaskValue(allBits);

            Assert.That(storage.IsScalable, Is.EqualTo(scalable));
            Assert.That(store.GetConstantSimdMaskValue(store.VNZeroForType(TYP_MASK)).IsScalable, Is.False);
            if (scalable)
            {
                Assert.That(storage.Scalable, Is.EqualTo(simdmaskscalable_t.AllBitsSet));
            }
            else
            {
                Assert.That(storage.Fixed, Is.EqualTo(simdmask_t.AllBitsSet(1)));
            }
        });
    }

    [Test]
    public static void ScalableTreeNumberingKeepsItsUnportedRepresentationGate()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            SetScalableConfiguration(true);
            compiler.vnStore = new ValueNumStore(compiler);
            var tree = new GenTreeMskCon(simdmask_t.Zero);

            _ = Assert.Throws<NotImplementedException>(() => compiler.fgValueNumberTreeConst(tree));
        });
    }

    [TestCase(false, " {SimdMaskCns[0x76543210, 0xfedcba98]}")]
    [TestCase(true, " {SimdMaskScalableCns[base:long idx:1]}")]
    public static void DiagnosticsUseStoredTagRatherThanCurrentConfiguration(bool scalable, string expected)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            SetScalableConfiguration(!scalable);
            var store = new ValueNumStore(compiler);
            simdmask_t fixedMask = default;
            fixedMask.u64[0] = 0xFEDC_BA98_7654_3210;
            var vn = scalable
                ? store.VNForSimdMaskScalableCon(new simdmaskscalable_t { BaseType = TYP_LONG, Index = 1 })
                : store.VNForSimdMaskCon(fixedMask);
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = s_jitstdout;
            s_jitstdout = writer;
            try
            {
                store.vnDump(compiler, vn);
                writer.Flush();
                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
            }
            finally
            {
                s_jitstdout = previous;
            }
        });
    }

    private static void SetScalableConfiguration(bool enabled)
    {
        object config = JitConfig;
        var field = typeof(JitConfigValues).GetField("_jitUseScalableVectorT", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected scalable-vector configuration field.");
        field.SetValue(config, enabled ? 1 : 0);
        JitConfig = (JitConfigValues)config;
    }
#endif
}
#endif
