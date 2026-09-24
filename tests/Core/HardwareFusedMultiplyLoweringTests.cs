// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class HardwareFusedMultiplyLoweringTests
{
    [TestCase(NI_AVX2_MultiplyAddScalar, 0, NI_AVX2_MultiplyAddScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 1, NI_AVX2_MultiplyAddNegatedScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 2, NI_AVX2_MultiplyAddNegatedScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 3, NI_AVX2_MultiplyAddScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 4, NI_AVX2_MultiplySubtractScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 5, NI_AVX2_MultiplySubtractNegatedScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 6, NI_AVX2_MultiplySubtractNegatedScalar)]
    [TestCase(NI_AVX2_MultiplyAddScalar, 7, NI_AVX2_MultiplySubtractScalar)]
    [TestCase(NI_AVX2_MultiplySubtractNegatedScalar, 5, NI_AVX2_MultiplyAddScalar)]
    [TestCase(NI_AVX512_FusedMultiplySubtractNegatedScalar, 1, NI_AVX512_FusedMultiplySubtractScalar)]
    [TestCase(NI_AVX512_FusedMultiplyAddScalar, 5, NI_AVX512_FusedMultiplySubtractNegatedScalar)]
    public static void ScalarNegationsSelectTheNativeFusedVariant(
        NamedIntrinsic intrinsic, int negatedOperands, NamedIntrinsic expected)
    {
        WithCompiler(compiler => {
            var block = NewBlock();
            var values = new GenTree[3];
            var operands = new GenTree[3];
            for (var index = 0; index < 3; index++)
            {
                var value = compiler.gtNewLclvNode(TYP_FLOAT, index);
                values[index] = value;
                block.InsertAtEnd(value);
                if ((negatedOperands & (1 << index)) != 0)
                {
                    value.IsContained = true;
                    operands[index] = new GenTreeUnOp(GT_NEG, TYP_FLOAT, value);
                    block.InsertAtEnd(operands[index]);
                }
                else
                {
                    operands[index] = value;
                }
            }
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_FLOAT, intrinsic, TYP_FLOAT, 16, operands);
            node.IsUnusedValue = true;
            block.InsertAtEnd(node);

            LowerFusedMultiply(NewLowering(compiler, block), node);

            Assert.That(node.HWIntrinsicId, Is.EqualTo(expected));
            for (var index = 0; index < 3; index++)
            {
                Assert.That(node.GetOp(index + 1), Is.SameAs(values[index]));
                Assert.That(values[index].IsContained, Is.False);
                Assert.That(values[index].Next, Is.SameAs(index == 2 ? node : values[index + 1]));
                if (operands[index] != values[index])
                {
                    Assert.That(operands[index].Next is null && operands[index].Prev is null, Is.True);
                }
            }
            Assert.That(block.LastNode, Is.SameAs(node));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false, 1, true, true, TYP_FLOAT)]
    [TestCase(false, 2, true, true, TYP_FLOAT)]
    [TestCase(false, 3, true, true, TYP_DOUBLE)]
    [TestCase(true, 1, true, true, TYP_FLOAT)]
    [TestCase(true, 2, true, true, TYP_FLOAT)]
    [TestCase(false, 2, false, true, TYP_FLOAT)]
    [TestCase(false, 2, true, false, TYP_FLOAT)]
    public static void VectorNegationRespectsSignedZeroContainmentAndScalarUpperLanes(
        bool scalar, int operandIndex, bool contained, bool negativeZero, var_types baseType)
    {
        WithCompiler(compiler => {
            var block = NewBlock();
            var values = new GenTree[3];
            var operands = new GenTree[3];
            var mask = compiler.gtNewVconNode(TYP_SIMD16);
            if (negativeZero)
            {
                if (baseType is TYP_FLOAT)
                {
                    mask.SimdVal.AsSpan<uint>()[..4].Fill(0x80000000);
                }
                else
                {
                    mask.SimdVal.AsSpan<ulong>()[..2].Fill(0x8000000000000000);
                }
            }
            mask.IsContained = contained;
            GenTreeHWIntrinsic? xor = null;
            for (var index = 0; index < 3; index++)
            {
                compiler.lvaTable[index].Type = TYP_SIMD16;
                var value = compiler.gtNewLclvNode(TYP_SIMD16, index);
                values[index] = value;
                operands[index] = value;
                block.InsertAtEnd(value);
                if (index + 1 == operandIndex)
                {
                    block.InsertAtEnd(mask);
                    xor = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Xor, TYP_INT, 16, value, mask);
                    operands[index] = xor;
                    block.InsertAtEnd(xor);
                }
            }
            var intrinsic = scalar ? NI_AVX2_MultiplyAddScalar : NI_AVX2_MultiplyAdd;
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsic, baseType, 16, operands);
            node.IsUnusedValue = true;
            block.InsertAtEnd(node);

            LowerFusedMultiply(NewLowering(compiler, block), node);

            var folded = contained && negativeZero && (!scalar || (operandIndex != 1));
            var expected = intrinsic;
            if (folded)
            {
                expected = operandIndex == 3
                    ? (scalar ? NI_AVX2_MultiplySubtractScalar : NI_AVX2_MultiplySubtract)
                    : (scalar ? NI_AVX2_MultiplyAddNegatedScalar : NI_AVX2_MultiplyAddNegated);
            }
            Assert.That(node.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(node.GetOp(operandIndex), Is.SameAs(folded ? values[operandIndex - 1] : xor));
            Assert.That(xor, Is.Not.Null);
            Assert.That(xor?.Next is null && xor?.Prev is null, Is.EqualTo(folded));
            Assert.That(mask.Next is null && mask.Prev is null, Is.EqualTo(folded));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    private static BasicBlock NewBlock()
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        return block;
    }

    private static Lowering NewLowering(Compiler compiler, BasicBlock block)
    {
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;
        return lowering;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerFusedMultiplyOp")]
    private static extern void LowerFusedMultiply(Lowering lowering, GenTreeHWIntrinsic node);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = [new() { Type = TYP_FLOAT }, new() { Type = TYP_FLOAT }, new() { Type = TYP_FLOAT }];
        compiler.lvaCount = compiler.lvaTable.Length;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
