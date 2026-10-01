// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#if TARGET_WASM
using System.Collections.Generic;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdComparisonConstructionCompletionTests
{
    [TestCase(GT_EQ, TYP_FLOAT)]
    [TestCase(GT_GE, TYP_DOUBLE)]
    [TestCase(GT_GT, TYP_FLOAT)]
    [TestCase(GT_LE, TYP_DOUBLE)]
    [TestCase(GT_LT, TYP_FLOAT)]
    [TestCase(GT_NE, TYP_DOUBLE)]
    public static void FloatingComparisonPreservesNaNPayloadsSignedZeroAndOperandOrder(
        genTreeOps operation, var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var left = compiler.gtNewVconNode(TYP_SIMD16);
            var right = compiler.gtNewVconNode(TYP_SIMD16);
            var nanBits = baseType == TYP_FLOAT ? 0x7FC012347FC05678UL : 0x7FF8000000001234UL;
            const ulong zeroBits = 0x8000000000000000;
            left.SimdVal.u64[0] = nanBits;
            right.SimdVal.u64[0] = zeroBits;
            var result = compiler.gtNewSimdCmpOpNode(operation, TYP_SIMD16, left, right, baseType, 16)
                .AsHWIntrinsic();
#if TARGET_XARCH
            var expected = operation switch
            {
                GT_EQ => NI_X86Base_CompareEqual,
                GT_GE => NI_X86Base_CompareGreaterThanOrEqual,
                GT_GT => NI_X86Base_CompareGreaterThan,
                GT_LE => NI_X86Base_CompareLessThanOrEqual,
                GT_LT => NI_X86Base_CompareLessThan,
                _ => NI_X86Base_CompareNotEqual,
            };
#elif TARGET_ARM64
            if (operation == GT_NE)
            {
                Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Not));
                result = result.GetOp(1).AsHWIntrinsic();
            }
            var expected = baseType == TYP_DOUBLE ? operation switch
            {
                GT_GE => NI_AdvSimd_Arm64_CompareGreaterThanOrEqual,
                GT_LE => NI_AdvSimd_Arm64_CompareLessThanOrEqual,
                _ => NI_AdvSimd_Arm64_CompareEqual,
            } : operation switch
            {
                GT_GT => NI_AdvSimd_CompareGreaterThan,
                GT_LT => NI_AdvSimd_CompareLessThan,
                _ => NI_AdvSimd_CompareEqual,
            };
#elif TARGET_WASM
            var expected = operation switch
            {
                GT_EQ => NI_PackedSimd_CompareEqual,
                GT_GE => NI_PackedSimd_CompareGreaterThanOrEqual,
                GT_GT => NI_PackedSimd_CompareGreaterThan,
                GT_LE => NI_PackedSimd_CompareLessThanOrEqual,
                GT_LT => NI_PackedSimd_CompareLessThan,
                _ => NI_PackedSimd_CompareNotEqual,
            };
#else
#error Unsupported SIMD comparison test target
#endif
            Assert.That(result.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(2), Is.SameAs(right));
            Assert.That(left.SimdVal.u64[0], Is.EqualTo(nanBits));
            Assert.That(right.SimdVal.u64[0], Is.EqualTo(zeroBits));
        });
    }

    [Test]
    public static void AllReductionUsesEqualityAndAnIntegralAllBitsSetMask(
        [Values(GT_EQ, GT_GE, GT_GT, GT_LE, GT_LT)] genTreeOps operation,
        [Values(TYP_FLOAT, TYP_DOUBLE, TYP_ULONG)] var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler);
            var right = Local(compiler);
            var result = compiler.gtNewSimdCmpOpAllNode(operation, TYP_INT, left, right, baseType, 16)
                .AsHWIntrinsic();
            Assert.That(result.Type, Is.EqualTo(TYP_INT));
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_op_Equality));
            Assert.That(result.SimdBaseType, Is.EqualTo(operation == GT_EQ ? baseType : IntegralMaskType(baseType)));
            if (operation == GT_EQ)
            {
                Assert.That(result.GetOp(1), Is.SameAs(left));
                Assert.That(result.GetOp(2), Is.SameAs(right));
            }
            else
            {
                Assert.That(result.GetOp(1).Type, Is.EqualTo(TYP_SIMD16));
                Assert.That(CountReferences(result.GetOp(1), left), Is.EqualTo(1));
                Assert.That(CountReferences(result.GetOp(1), right), Is.EqualTo(1));
                for (var index = 0; index < 16; index++)
                {
                    Assert.That(result.GetOp(2).AsVecCon().SimdVal.u8[index], Is.EqualTo(byte.MaxValue));
                }
            }
        });
    }

    [Test]
    public static void AnyReductionUsesInequalityAndAnIntegralZeroMask(
        [Values(GT_EQ, GT_GE, GT_GT, GT_LE, GT_LT, GT_NE)] genTreeOps operation,
        [Values(TYP_FLOAT, TYP_DOUBLE, TYP_ULONG)] var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler);
            var right = Local(compiler);
            var result = compiler.gtNewSimdCmpOpAnyNode(operation, TYP_INT, left, right, baseType, 16)
                .AsHWIntrinsic();
            Assert.That(result.Type, Is.EqualTo(TYP_INT));
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_op_Inequality));
            Assert.That(result.SimdBaseType, Is.EqualTo(operation == GT_NE ? baseType : IntegralMaskType(baseType)));
            if (operation == GT_NE)
            {
                Assert.That(result.GetOp(1), Is.SameAs(left));
                Assert.That(result.GetOp(2), Is.SameAs(right));
            }
            else
            {
                Assert.That(result.GetOp(1).Type, Is.EqualTo(TYP_SIMD16));
                Assert.That(CountReferences(result.GetOp(1), left), Is.EqualTo(1));
                Assert.That(CountReferences(result.GetOp(1), right), Is.EqualTo(1));
                Assert.That(result.GetOp(2).IsVectorZero, Is.True);
            }
        });
    }

#if TARGET_XARCH
    [Test]
    public static void XarchUnsignedComparisonRetainsItsBiasAndSignedRelation(
        [Values(GT_GT, GT_LT)] genTreeOps operation,
        [Values(TYP_UBYTE, TYP_USHORT, TYP_UINT, TYP_ULONG)] var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler);
            var right = Local(compiler);
            var result = compiler.gtNewSimdCmpOpNode(operation, TYP_SIMD16, left, right, baseType, 16)
                .AsHWIntrinsic();
            AssertBiasedComparison(result, operation, left, right, baseType);
        });
    }
#endif

#if TARGET_WASM
    [Test]
    public static void WasmUnsignedComparisonSubtractsDistinctBiasesWithoutCapturingOrReorderingEffects(
        [Values(GT_GE, GT_GT, GT_LE, GT_LT)] genTreeOps operation, [Values(false, true)] bool effectful)
    {
        WithCompiler(compiler =>
        {
            GenTree left = Local(compiler);
            GenTree right = Local(compiler);
            var leftEffect = compiler.gtNewStoreLclVarNode(left.AsLclVarCommon().LclNum,
                compiler.gtNewVconNode(TYP_SIMD16));
            var rightEffect = compiler.gtNewStoreLclVarNode(right.AsLclVarCommon().LclNum,
                compiler.gtNewVconNode(TYP_SIMD16));
            if (effectful)
            {
                left = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD16, leftEffect, left);
                right = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD16, rightEffect, right);
            }
            var localCount = compiler.lvaCount;
            var result = compiler.gtNewSimdCmpOpNode(operation, TYP_SIMD16, left, right, TYP_ULONG, 16)
                .AsHWIntrinsic();
            AssertBiasedComparison(result, operation, left, right, TYP_ULONG);
            Assert.That(compiler.lvaCount, Is.EqualTo(localCount));
            Assert.That(CountReferences(result, leftEffect), Is.EqualTo(effectful ? 1 : 0));
            Assert.That(CountReferences(result, rightEffect), Is.EqualTo(effectful ? 1 : 0));
        });
    }

    [TestCaseSource(nameof(UnsignedBoundaryCases))]
    public static void WasmSignedBiasPreservesUnsignedOrderingAcrossTheSignBoundary(
        genTreeOps operation, ulong leftValue, ulong rightValue)
    {
        WithCompiler(compiler =>
        {
            var left = compiler.gtNewVconNode(TYP_SIMD16);
            var right = compiler.gtNewVconNode(TYP_SIMD16);
            left.SimdVal.u64[0] = leftValue;
            left.SimdVal.u64[1] = rightValue;
            right.SimdVal.u64[0] = rightValue;
            right.SimdVal.u64[1] = leftValue;
            var result = compiler.gtNewSimdCmpOpNode(operation, TYP_SIMD16, left, right, TYP_ULONG, 16)
                .AsHWIntrinsic();
            AssertBiasedComparison(result, operation, left, right, TYP_ULONG);
            for (var index = 0; index < 2; index++)
            {
                var bias1 = result.GetOp(1).AsHWIntrinsic().GetOp(2).AsVecCon().SimdVal.u64[index];
                var bias2 = result.GetOp(2).AsHWIntrinsic().GetOp(2).AsVecCon().SimdVal.u64[index];
                var biasedLeft = unchecked((long)(left.SimdVal.u64[index] - bias1));
                var biasedRight = unchecked((long)(right.SimdVal.u64[index] - bias2));
                var expected = operation switch
                {
                    GT_GE => left.SimdVal.u64[index] >= right.SimdVal.u64[index],
                    GT_GT => left.SimdVal.u64[index] > right.SimdVal.u64[index],
                    GT_LE => left.SimdVal.u64[index] <= right.SimdVal.u64[index],
                    _ => left.SimdVal.u64[index] < right.SimdVal.u64[index],
                };
                var actual = operation switch
                {
                    GT_GE => biasedLeft >= biasedRight,
                    GT_GT => biasedLeft > biasedRight,
                    GT_LE => biasedLeft <= biasedRight,
                    _ => biasedLeft < biasedRight,
                };
                Assert.That(actual ? ulong.MaxValue : 0UL, Is.EqualTo(expected ? ulong.MaxValue : 0UL));
            }
        });
    }

    [Test]
    public static void WasmWideVariableShuffleMasksOnlySafeOperationsAndEvaluatesIndicesOnce(
        [Values(TYP_LONG, TYP_DOUBLE)] var_types baseType, [Values(false, true)] bool native,
        [Values(false, true)] bool effectful)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler);
            var indexValue = Local(compiler);
            var effect = compiler.gtNewStoreLclVarNode(source.LclNum, compiler.gtNewVconNode(TYP_SIMD16));
            GenTree indices = effectful
                ? compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD16, effect, indexValue)
                : indexValue;
            var result = compiler.gtNewSimdShuffleVariableNode(TYP_SIMD16, source, indices, baseType, 16, native)
                .AsHWIntrinsic();
            var lookup = native ? result : result.GetOp(1).AsHWIntrinsic();
            Assert.That(lookup.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            Assert.That(lookup.GetOp(1), Is.SameAs(source));
            var expanded = lookup.GetOp(2).AsHWIntrinsic();
            Assert.That(expanded.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Or));
            var broadcast = expanded.GetOp(1).AsHWIntrinsic();
            Assert.That(broadcast.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            var shift = broadcast.GetOp(1).AsHWIntrinsic();
            Assert.That(shift.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_ShiftLeft));
            Assert.That(shift.SimdBaseType, Is.EqualTo(TYP_LONG));
            Assert.That(shift.GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)3));
            for (var index = 0; index < 16; index++)
            {
                Assert.That(broadcast.GetOp(2).AsVecCon().SimdVal.u8[index], Is.EqualTo((byte)((index / 8) * 8)));
                Assert.That(expanded.GetOp(2).AsVecCon().SimdVal.u8[index], Is.EqualTo((byte)(index & 7)));
            }
            if (native)
            {
                Assert.That(shift.GetOp(1), Is.SameAs(indices));
                Assert.That(CountReferences(result, indexValue), Is.EqualTo(1));
            }
            else
            {
                Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_And));
                var mask = result.GetOp(2).AsHWIntrinsic();
                var savedIndices = mask.GetOp(1).AsHWIntrinsic().GetOp(1);
                var comparand = mask.GetOp(2).AsHWIntrinsic().GetOp(1).AsVecCon();
                AssertBiasedComparison(mask, GT_LT, savedIndices, comparand, TYP_ULONG);
                Assert.That(comparand.SimdVal.u64[0], Is.EqualTo(2UL));
                Assert.That(comparand.SimdVal.u64[1], Is.EqualTo(2UL));
                Assert.That(savedIndices.Oper, Is.EqualTo(GT_LCL_VAR));
                if (effectful)
                {
                    var capture = shift.GetOp(1).AsOp();
                    Assert.That(capture.Oper, Is.EqualTo(GT_COMMA));
                    Assert.That(capture.Op1, Is.SameAs(indices));
                    var hoisted = capture.Op1.AsOp();
                    Assert.That(hoisted.Oper, Is.EqualTo(GT_COMMA));
                    Assert.That(hoisted.Op1, Is.SameAs(effect));
                    Assert.That(hoisted.Op2.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    var store = hoisted.Op2.AsLclVarCommon();
                    Assert.That(store.Data, Is.SameAs(indexValue));
                    Assert.That(capture.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
                    Assert.That(capture.Op2.AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
                    Assert.That(savedIndices.AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
                    Assert.That(savedIndices, Is.Not.SameAs(capture.Op2));
                    Assert.That(CountReferences(result, indexValue), Is.EqualTo(1));
                }
                else
                {
                    Assert.That(shift.GetOp(1), Is.SameAs(indexValue));
                    Assert.That(savedIndices.AsLclVarCommon().LclNum, Is.EqualTo(indexValue.LclNum));
                    Assert.That(savedIndices, Is.Not.SameAs(indexValue));
                }
            }
            Assert.That(CountReferences(result, effect), Is.EqualTo(effectful ? 1 : 0));
        });
    }

    private static IEnumerable<object[]> UnsignedBoundaryCases()
    {
        genTreeOps[] operations = [GT_GE, GT_GT, GT_LE, GT_LT];
        ulong[] values = [0, 1, 0x7FFFFFFFFFFFFFFF, 0x8000000000000000, ulong.MaxValue];
        foreach (var operation in operations)
        {
            foreach (var left in values)
            {
                foreach (var right in values)
                {
                    yield return [operation, left, right];
                }
            }
        }
    }
#endif

#if TARGET_XARCH || TARGET_WASM
    private static void AssertBiasedComparison(GenTreeHWIntrinsic result, genTreeOps operation,
        GenTree left, GenTree right, var_types unsignedType)
    {
#if TARGET_XARCH
        var comparison = operation == GT_GT ? NI_X86Base_CompareGreaterThan : NI_X86Base_CompareLessThan;
        var subtraction = NI_X86Base_Subtract;
#else
        var comparison = operation switch
        {
            GT_GE => NI_PackedSimd_CompareGreaterThanOrEqual,
            GT_GT => NI_PackedSimd_CompareGreaterThan,
            GT_LE => NI_PackedSimd_CompareLessThanOrEqual,
            _ => NI_PackedSimd_CompareLessThan,
        };
        var subtraction = NI_PackedSimd_Subtract;
#endif
        var signedType = unsignedType switch
        {
            TYP_UBYTE => TYP_BYTE,
            TYP_USHORT => TYP_SHORT,
            TYP_UINT => TYP_INT,
            _ => TYP_LONG,
        };
        Assert.That(result.HWIntrinsicId, Is.EqualTo(comparison));
        Assert.That(result.SimdBaseType, Is.EqualTo(signedType));
        var subtractLeft = result.GetOp(1).AsHWIntrinsic();
        var subtractRight = result.GetOp(2).AsHWIntrinsic();
        Assert.That(subtractLeft.HWIntrinsicId, Is.EqualTo(subtraction));
        Assert.That(subtractRight.HWIntrinsicId, Is.EqualTo(subtraction));
        Assert.That(subtractLeft.SimdBaseType, Is.EqualTo(unsignedType));
        Assert.That(subtractRight.SimdBaseType, Is.EqualTo(unsignedType));
        Assert.That(subtractLeft.GetOp(1), Is.SameAs(left));
        Assert.That(subtractRight.GetOp(1), Is.SameAs(right));
        Assert.That(subtractLeft.GetOp(2), Is.Not.SameAs(subtractRight.GetOp(2)));
        var expectedBias = 1UL << ((unsignedType.Size * 8) - 1);
        for (var index = 0; index < (16 / unsignedType.Size); index++)
        {
            Assert.That(subtractLeft.GetOp(2).GetIntegralVectorConstElement(index, unsignedType),
                Is.EqualTo(expectedBias));
            Assert.That(subtractRight.GetOp(2).GetIntegralVectorConstElement(index, unsignedType),
                Is.EqualTo(expectedBias));
        }
    }
#endif

    private static var_types IntegralMaskType(var_types baseType)
    {
        return baseType switch
        {
            TYP_FLOAT => TYP_INT,
            TYP_DOUBLE => TYP_LONG,
            _ => baseType,
        };
    }

    private static int CountReferences(GenTree tree, GenTree value)
    {
        var count = ReferenceEquals(tree, value) ? 1 : 0;
        foreach (var operand in tree.Operands)
        {
            count += CountReferences(operand, value);
        }

        return count;
    }

    private static GenTreeLclVar Local(Compiler compiler)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = TYP_SIMD16 };

        return compiler.gtNewLclvNode(TYP_SIMD16, index);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[128];
        compiler.lvaCount = 2;
        compiler.lvaTable[0] = new LclVarDsc { Type = TYP_SIMD16 };
        compiler.lvaTable[1] = new LclVarDsc { Type = TYP_INT };
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
