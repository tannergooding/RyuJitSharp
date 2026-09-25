// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareConditionalSelectLoweringTests
{
    [Test]
    public static void MaskConversionSelectsBlendWithUnderlyingElementWidth()
    {
        WithCompiler(compiler => {
            simdmask_t bits = default;
            bits.u64[0] = 0x3;
            var mask = new GenTreeMskCon(bits);
            var conversion = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_ConvertMaskToVector,
                TYP_DOUBLE, 16, mask);
            var whenTrue = Local(compiler, 0);
            var whenFalse = Local(compiler, 1);
            var node = ConditionalSelect(conversion, whenTrue, whenFalse);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_SIMD16, node);
            var block = NewBlock(mask, conversion, whenTrue, whenFalse, node, consumer);

            var next = LowerHWIntrinsicCndSel(NewLowering(compiler, block), node);

            Assert.That(next, Is.SameAs(consumer));
            Assert.That(node.Next, Is.Null);
            Assert.That(conversion.Next, Is.Null);
            var result = consumer.Op1.AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AVX512_BlendVariableMask));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(result.GetOp(1), Is.SameAs(whenFalse));
            Assert.That(result.GetOp(2), Is.SameAs(whenTrue));
            Assert.That(result.GetOp(3), Is.SameAs(mask));
        });
    }

    [TestCase(false, GT_AND)]
    [TestCase(true, GT_AND_NOT)]
    public static void ZeroSelectionElidesUnusedOperand(bool trueIsZero, genTreeOps expectedOperation)
    {
        WithCompiler(compiler => {
            var condition = Local(compiler, 0);
            var vector = Local(compiler, 1);
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var node = ConditionalSelect(condition, trueIsZero ? zero : vector,
                trueIsZero ? vector : zero);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_SIMD16, node);
            var block = trueIsZero
                ? NewBlock(condition, zero, vector, node, consumer)
                : NewBlock(condition, vector, zero, node, consumer);

            _ = LowerHWIntrinsicCndSel(NewLowering(compiler, block), node);

            Assert.That(node.Next, Is.Null);
            Assert.That(zero.Next, Is.Null);
            var result = consumer.Op1.AsHWIntrinsic();
            Assert.That(result.GetOperForHWIntrinsicId(out _), Is.EqualTo(expectedOperation));
            Assert.That(result.GetOp(1), Is.SameAs(condition));
            Assert.That(result.GetOp(2), Is.SameAs(vector));
        });
    }

    [Test]
    public static void GeneralSelectOnAvx512UsesTernaryControl()
    {
        WithCompiler(compiler => {
            var condition = Local(compiler, 0);
            var whenTrue = Local(compiler, 1);
            var whenFalse = Local(compiler, 2);
            var node = ConditionalSelect(condition, whenTrue, whenFalse);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_SIMD16, node);
            var block = NewBlock(condition, whenTrue, whenFalse, node, consumer);

            _ = LowerHWIntrinsicCndSel(NewLowering(compiler, block), node);

            var result = consumer.Op1.AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AVX512_TernaryLogic));
            Assert.That(result.GetOp(1), Is.SameAs(condition));
            Assert.That(result.GetOp(2), Is.SameAs(whenTrue));
            Assert.That(result.GetOp(3), Is.SameAs(whenFalse));
            Assert.That(result.GetOp(4).AsIntConCommon().IconValue, Is.EqualTo((nint)0xCA));
        });
    }

    [Test]
    public static void GeneralSelectWithoutBlendExpandsAndClonesMask()
    {
        WithCompiler(compiler => {
            var condition = Local(compiler, 0);
            var whenTrue = Local(compiler, 1);
            var whenFalse = Local(compiler, 2);
            var node = ConditionalSelect(condition, whenTrue, whenFalse);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_SIMD16, node);
            var block = NewBlock(condition, whenTrue, whenFalse, node, consumer);

            _ = LowerHWIntrinsicCndSel(NewLowering(compiler, block), node);

            var result = consumer.Op1.AsHWIntrinsic();
            Assert.That(result.GetOperForHWIntrinsicId(out _), Is.EqualTo(GT_OR));
            var truePart = result.GetOp(1).AsHWIntrinsic();
            var falsePart = result.GetOp(2).AsHWIntrinsic();
            Assert.That(truePart.GetOperForHWIntrinsicId(out _), Is.EqualTo(GT_AND));
            Assert.That(falsePart.GetOperForHWIntrinsicId(out _), Is.EqualTo(GT_AND_NOT));
            Assert.That(truePart.GetOp(1), Is.SameAs(condition));
            Assert.That(falsePart.GetOp(1).Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(falsePart.GetOp(1), Is.Not.SameAs(condition));
        }, supportsAvx512: false);
    }

    [Test]
    public static void PerElementComparisonMaskSelectsVectorBlend()
    {
        WithCompiler(compiler => {
            var first = Local(compiler, 0);
            var second = Local(compiler, 1);
            var condition = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_CompareEqual,
                TYP_INT, 16, first, second);
            var whenTrue = Local(compiler, 2);
            var whenFalse = new GenTreeVecCon(TYP_SIMD16);
            whenFalse.SimdVal.i32[0] = 1;
            var node = ConditionalSelect(condition, whenTrue, whenFalse);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_SIMD16, node);
            var block = NewBlock(first, second, condition, whenTrue, whenFalse, node, consumer);

            _ = LowerHWIntrinsicCndSel(NewLowering(compiler, block), node);

            var result = consumer.Op1.AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_X86Base_BlendVariable));
            Assert.That(result.GetOp(1), Is.SameAs(whenFalse));
            Assert.That(result.GetOp(2), Is.SameAs(whenTrue));
            Assert.That(result.GetOp(3), Is.SameAs(condition));
        }, supportsAvx512: false);
    }

    private static GenTreeHWIntrinsic ConditionalSelect(GenTree condition, GenTree whenTrue, GenTree whenFalse)
        => new(TYP_SIMD16, NI_Vector_ConditionalSelect, TYP_INT, 16, condition, whenTrue, whenFalse);

    private static GenTreeLclVar Local(Compiler compiler, int number)
        => compiler.gtNewLclvNode(TYP_SIMD16, number);

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCndSel")]
    private static extern GenTree? LowerHWIntrinsicCndSel(Lowering lowering, GenTreeHWIntrinsic node);

    private static void WithCompiler(Action<Compiler> action, bool supportsAvx512 = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        if (supportsAvx512)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
        }
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
        for (var i = 0; i < compiler.lvaTable.Length; i++)
        {
            compiler.lvaTable[i].Type = TYP_SIMD16;
        }
        compiler.lvaCount = 3;
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
#endif
