// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64ScalableMaskTreeTests
{
    [Test]
    public static void ScalableOverlayFitsAndRetainsZeroedPadding()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var mask = compiler.gtNewMskConNode(TYP_MASK, TYP_LONG, true);

            Assert.That(Unsafe.SizeOf<simdmaskscalable_t>(), Is.EqualTo(2));
            Assert.That(Unsafe.SizeOf<simdmask_t>(), Is.EqualTo(8));
            Assert.That(mask.SimdMaskVal.u8[0], Is.EqualTo((byte)TYP_LONG));
            Assert.That(mask.SimdMaskVal.u8[1], Is.EqualTo(1));
            Assert.That(mask.SimdMaskVal.u64[0] >> 16, Is.Zero);
            mask.SimdMaskVal.u8[1] = 0;
            Assert.That(mask.SimdScalableMaskVal.Index, Is.Zero);
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void FixedFactoriesKeepTheirMaskPatterns(var_types type)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
#if DEBUG
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(false);
#endif
            var trueMask = compiler.gtNewSimdTrueMaskNode(type);
            var falseMask = compiler.gtNewSimdFalseMaskByteNode();
            simdmask_t expected = default;
            Assert.That(EvaluateSimdPatternToMask<simd16_t>(type, ref expected, SveMaskPattern.SveMaskPatternAll), Is.True);

            Assert.That(trueMask.SimdMaskVal, Is.EqualTo(expected));
            Assert.That(trueMask.IsTrue(type), Is.True);
            Assert.That(falseMask.SimdMaskVal, Is.EqualTo(simdmask_t.Zero));
            Assert.That(falseMask.IsZero, Is.True);
            Assert.That(falseMask.IsTrue(type), Is.False);
            Assert.That(GenTreeMskCon.Equals(trueMask, falseMask), Is.False);
        });
    }

#if DEBUG
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void TrueMaskFactoryUsesTheRequestedElementWidth(var_types type)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            var mask = compiler.gtNewSimdTrueMaskNode(type);

            Assert.That(mask.Type, Is.EqualTo(TYP_MASK));
            Assert.That(mask.IsZero, Is.False);
            Assert.That(mask.IsTrue(type), Is.True);
            Assert.That(mask.IsAllBitsSetForType(type), Is.True);
            Assert.That(mask.IsAllBitsSet, Is.EqualTo(type == TYP_BYTE));
        });
    }

    [TestCase(TYP_BYTE, TYP_LONG, true, false)]
    [TestCase(TYP_SHORT, TYP_INT, true, false)]
    [TestCase(TYP_LONG, TYP_BYTE, false, false)]
    [TestCase(TYP_INT, TYP_FLOAT, true, true)]
    public static void TrueMaskQueriesPermitWiderLanesButAllBitsRequiresEqualWidths(
        var_types storedType, var_types requestedType, bool trueMask, bool allBits)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            var mask = compiler.gtNewMskConNode(TYP_MASK, storedType, true);

            Assert.That(mask.IsTrue(requestedType), Is.EqualTo(trueMask));
            Assert.That(mask.IsAllBitsSetForType(requestedType), Is.EqualTo(allBits));
        });
    }

    [Test]
    public static void FalseMasksIgnoreBaseTypeButPreserveTheirEncodedBytes()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            var first = compiler.gtNewSimdFalseMaskByteNode();
            var second = compiler.gtNewMskConNode(TYP_MASK, TYP_DOUBLE, false);

            Assert.That(first.SimdScalableMaskVal.BaseType, Is.EqualTo(TYP_BYTE));
            Assert.That(first.IsZero && second.IsZero, Is.True);
            Assert.That(first.IsTrue(TYP_BYTE), Is.False);
            Assert.That(GenTreeMskCon.Equals(first, second), Is.True);
            Assert.That(first.SimdMaskVal, Is.Not.EqualTo(second.SimdMaskVal));
            var rawZero = compiler.gtNewZeroConNode(TYP_MASK).AsMskCon();
            Assert.That(rawZero.SimdMaskVal, Is.EqualTo(simdmask_t.Zero));
            Assert.That(GenTreeMskCon.Equals(first, rawZero), Is.True);
        });
    }

    [Test]
    public static void CloningAndHashingPreserveTheNativeOpaqueMaskPayload()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            var original = compiler.gtNewMskConNode(TYP_MASK, TYP_SHORT, true);
            var clone = compiler.gtCloneExpr(original).AsMskCon();

            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.SimdMaskVal, Is.EqualTo(original.SimdMaskVal));
            Assert.That(compiler.gtHashValue(clone), Is.EqualTo(compiler.gtHashValue(original)));
            clone.SimdScalableMaskVal.Index = 0;
            Assert.That(original.SimdScalableMaskVal.Index, Is.EqualTo(1));
            Assert.That(compiler.gtHashValue(clone), Is.Not.EqualTo(compiler.gtHashValue(original)));
        });
    }

    [TestCase(TYP_BYTE, true, "byte   <0x1, 0x1, 0x1...>")]
    [TestCase(TYP_LONG, false, "long   <0x0, 0x0, 0x0...>")]
    public static void ConstantDumpsPrintTheScalablePattern(var_types type, bool index, string expected)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            var mask = compiler.gtNewMskConNode(TYP_MASK, type, index);
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = s_jitstdout;
            s_jitstdout = writer;
            try
            {
                compiler.gtDispConst(mask);
                writer.Flush();
                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
            }
            finally
            {
                s_jitstdout = previous;
            }
        });
    }

    [TestCase(SimdScalableRepeated, 1UL, TYP_LONG, true)]
    [TestCase(SimdScalableRepeated, 1UL, TYP_INT, false)]
    [TestCase(SimdScalableSequence, 1UL, TYP_LONG, false)]
    [TestCase(SimdScalableScalar, 1UL, TYP_LONG, false)]
    [TestCase(SimdScalableScalar, 0UL, TYP_BYTE, true)]
    public static void VectorToMaskFoldingHonorsRepresentability(
        SimdScalableKind kind, ulong index, var_types requestedType, bool folded)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            var vector = compiler.gtNewSimdVconNode(TYP_SIMD, TYP_LONG, kind, index, 1);
            var intrinsic = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_ConvertVectorToMask, requestedType, 0, vector);
            var result = compiler.gtFoldExprConvertVecCnsToMask(intrinsic, vector);

            if (folded)
            {
                Assert.That(result.Oper.IsCnsMsk, Is.True);
                Assert.That(result.AsMskCon().SimdScalableMaskVal.BaseType, Is.EqualTo(requestedType));
                Assert.That(result.AsMskCon().SimdScalableMaskVal.Index, Is.EqualTo(index == 0 ? 0 : 1));
            }
            else
            {
                Assert.That(result, Is.SameAs(intrinsic));
            }
            Assert.That(intrinsic.GetOp(1), Is.SameAs(vector));
            Assert.That(vector.SimdScalableVal.Index.u64[0], Is.EqualTo(index));
        });
    }
#endif
}
#endif
