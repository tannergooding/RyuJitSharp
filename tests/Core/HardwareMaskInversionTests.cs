// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareMaskInversionTests
{
    [TestCase(TYP_INT, 16, 0x5UL, 0xAUL)]
    [TestCase(TYP_BYTE, 16, 0x55UL, 0xFFAAUL)]
    [TestCase(TYP_LONG, 64, 0x55UL, 0xAAUL)]
    public static void ConstantMaskInversionClearsUnusedHighBits(
        var_types baseType, byte size, ulong original, ulong expected)
    {
        WithCompiler(compiler => {
            simdmask_t bits = default;
            bits.u64[0] = original;
            var constant = new GenTreeMskCon(bits);
            var lowering = NewLowering(compiler, NewBlock(constant));

            Assert.That(TryInvertMask(lowering, constant, size, baseType), Is.True);
            Assert.That(constant.SimdMaskVal.u64[0], Is.EqualTo(expected));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void OrOfInvertedMaskUsesAndNotWithoutLeavingDetachedNot(bool invertedRight)
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewLclvNode(TYP_MASK, 0);
            var second = compiler.gtNewLclvNode(TYP_MASK, 1);
            var inverted = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_NotMask,
                TYP_INT, 16, invertedRight ? second : first);
            var operation = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_OrMask,
                TYP_INT, 16, invertedRight ? first : inverted, invertedRight ? inverted : second);
            var block = invertedRight
                ? NewBlock(first, second, inverted, operation)
                : NewBlock(first, inverted, second, operation);

            Assert.That(TryInvertMask(NewLowering(compiler, block), operation, 16, TYP_INT), Is.True);

            Assert.That(operation.HWIntrinsicId, Is.EqualTo(NI_AVX512_AndNotMask));
            Assert.That(operation.GetOp(1), Is.SameAs(invertedRight ? first : second));
            Assert.That(operation.GetOp(2), Is.SameAs(invertedRight ? second : first));
            Assert.That(inverted.Next, Is.Null);
            Assert.That(inverted.Prev, Is.Null);
        });
    }

    [Test]
    public static void ComparisonMaskInversionChangesThePredicate()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_SIMD16;
            var first = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var second = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var comparison = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_CompareEqualMask,
                TYP_INT, 16, first, second);
            var block = NewBlock(first, second, comparison);

            Assert.That(TryInvertMask(NewLowering(compiler, block), comparison, 16, TYP_INT), Is.True);
            Assert.That(comparison.HWIntrinsicId, Is.EqualTo(NI_AVX512_CompareNotEqualMask));
            Assert.That(comparison.GetOp(1), Is.SameAs(first));
            Assert.That(comparison.GetOp(2), Is.SameAs(second));
        });
    }

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryInvertMask")]
    private static extern bool TryInvertMask(Lowering lowering, GenTree mask, byte size, var_types baseType);

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
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaTable[0].Type = TYP_MASK;
        compiler.lvaTable[1].Type = TYP_MASK;
        compiler.lvaCount = 2;
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
