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
internal static unsafe class HardwareTernaryLoweringTests
{
    [TestCase(0x00, 0)]
    [TestCase(0xFF, -1)]
    public static void ConstantControlsReplaceTheWholeIntrinsic(byte control, int expectedBits)
    {
        WithCompiler(compiler => {
            var a = new GenTreeVecCon(TYP_SIMD16);
            var b = new GenTreeVecCon(TYP_SIMD16);
            var c = new GenTreeVecCon(TYP_SIMD16);
            var immediate = compiler.gtNewIconNode(TYP_INT, control);
            var node = Ternary(a, b, c, immediate);
            var block = NewBlock(a, b, c, immediate, node);

            _ = LowerHWIntrinsicTernaryLogic(NewLowering(compiler, block), node);

            Assert.That(node.Next, Is.Null);
            Assert.That(node.Prev, Is.Null);
            Assert.That(immediate.Next, Is.Null);
            Assert.That(block.LastNode is GenTreeVecCon, Is.True);
            Assert.That(block.LastNode!.AsVecCon().SimdVal.i32[0], Is.EqualTo(expectedBits));
            Assert.That(a.IsUnusedValue && b.IsUnusedValue && c.IsUnusedValue, Is.True);
        });
    }

    [TestCase(0xF0, 1)]
    [TestCase(0xCC, 2)]
    [TestCase(0xAA, 3)]
    public static void SelectOnlyControlReusesItsLiveOperand(byte control, int index)
    {
        WithCompiler(compiler => {
            var a = new GenTreeVecCon(TYP_SIMD16);
            var b = new GenTreeVecCon(TYP_SIMD16);
            var c = new GenTreeVecCon(TYP_SIMD16);
            var immediate = compiler.gtNewIconNode(TYP_INT, control);
            var node = Ternary(a, b, c, immediate);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_SIMD16, node);
            var block = NewBlock(a, b, c, immediate, node, consumer);
            var original = new[] { a, b, c };

            _ = LowerHWIntrinsicTernaryLogic(NewLowering(compiler, block), node);

            Assert.That(consumer.Op1, Is.SameAs(original[index - 1]));
            Assert.That(node.Next, Is.Null);
            Assert.That(immediate.Next, Is.Null);
        });
    }

    [TestCase(0xFC)] // A | B; after normalization the first slot is unused.
    [TestCase(0xFA)] // A | C; likewise normalize the live inputs into slots two and three.
    public static void TwoInputControlsNormalizeTheDeadFirstOperand(byte control)
    {
        WithCompiler(compiler => {
            var a = new GenTreeVecCon(TYP_SIMD16);
            var b = new GenTreeVecCon(TYP_SIMD16);
            var c = new GenTreeVecCon(TYP_SIMD16);
            var immediate = compiler.gtNewIconNode(TYP_INT, control);
            var node = Ternary(a, b, c, immediate);
            var block = NewBlock(a, b, c, immediate, node);

            _ = LowerHWIntrinsicTernaryLogic(NewLowering(compiler, block), node);

            Assert.That(node.GetOp(1) is GenTreeVecCon, Is.True);
            Assert.That(TernaryLogicInfo.Lookup(unchecked((byte)immediate.IconValue)).GetAllUseFlags(),
                Is.EqualTo(TernaryLogicUseFlags.BC));
        });
    }

    [Test]
    public static void MaskConditionElidesConversionAndChoosesMatchingBlendWidth()
    {
        WithCompiler(compiler => {
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            var compare = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_CompareEqualMask,
                TYP_DOUBLE, 16, first, second);
            var conversion = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_ConvertMaskToVector,
                TYP_DOUBLE, 16, compare);
            var selectTrue = new GenTreeVecCon(TYP_SIMD16);
            var selectFalse = new GenTreeVecCon(TYP_SIMD16);
            var immediate = compiler.gtNewIconNode(TYP_INT, 0xCA);
            var node = Ternary(conversion, selectTrue, selectFalse, immediate);
            var block = NewBlock(first, second, compare, conversion, selectTrue, selectFalse, immediate, node);

            _ = LowerHWIntrinsicTernaryLogic(NewLowering(compiler, block), node);

            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_AVX512_BlendVariableMask));
            Assert.That(node.Operands.Length, Is.EqualTo(3));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(node.GetOp(1), Is.SameAs(selectFalse));
            Assert.That(node.GetOp(2), Is.SameAs(selectTrue));
            Assert.That(node.GetOp(3), Is.SameAs(compare));
            Assert.That(conversion.Next, Is.Null);
            Assert.That(immediate.Next, Is.Null);
        });
    }

    [TestCase(0xAC, 1, 3, 2)]
    [TestCase(0xB8, 2, 3, 1)]
    [TestCase(0xD8, 3, 2, 1)]
    [TestCase(0xCA, 1, 2, 3)]
    [TestCase(0xE2, 2, 1, 3)]
    [TestCase(0xE4, 3, 1, 2)]
    public static void ConditionalSelectControlsKeepMaskAndSelectionOrder(
        byte control, int conditionIndex, int trueIndex, int falseIndex)
    {
        WithCompiler(compiler => {
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            var mask = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_CompareEqualMask,
                TYP_DOUBLE, 16, first, second);
            var selections = new[] { new GenTreeVecCon(TYP_SIMD16), new GenTreeVecCon(TYP_SIMD16) };
            var operands = new GenTree[3];
            operands[conditionIndex - 1] = mask;
            operands[trueIndex - 1] = selections[0];
            operands[falseIndex - 1] = selections[1];
            var immediate = compiler.gtNewIconNode(TYP_INT, control);
            var node = Ternary(operands[0], operands[1], operands[2], immediate);
            var block = NewBlock(first, second, mask, selections[0], selections[1],
                immediate, node);

            _ = LowerHWIntrinsicTernaryLogic(NewLowering(compiler, block), node);

            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_AVX512_BlendVariableMask));
            Assert.That(node.GetOp(1), Is.SameAs(selections[1]));
            Assert.That(node.GetOp(2), Is.SameAs(selections[0]));
            Assert.That(node.GetOp(3), Is.SameAs(mask));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(immediate.Next, Is.Null);
        });
    }

    private static GenTreeHWIntrinsic Ternary(GenTree a, GenTree b, GenTree c, GenTree immediate)
        => new(TYP_SIMD16, NI_AVX512_TernaryLogic, TYP_INT, 16, a, b, c, immediate);

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicTernaryLogic")]
    private static extern GenTree? LowerHWIntrinsicTernaryLogic(Lowering lowering, GenTreeHWIntrinsic node);

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
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
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
