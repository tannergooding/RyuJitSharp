// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64ShiftRotateLoweringTests
{
    [TestCase(TYP_INT, 0, 32)]
    [TestCase(TYP_INT, -1, 33)]
    [TestCase(TYP_LONG, 63, 1)]
    public static void RotateLeftConstantPreservesNativeWidthBeforeContainment(
        var_types type, int leftCount, int rightCount)
    {
        WithLowering((compiler, lowering, block) => {
            var value = compiler.gtNewIconNode(type, 7);
            var count = compiler.gtNewIconNode(TYP_INT, leftCount);
            var rotate = new GenTreeOp(GT_ROL, type, value, count) { IsUnusedValue = true };
            block.InsertAtEnd(value);
            block.InsertAtEnd(count);
            block.InsertAtEnd(rotate);

            LowerRotate(lowering, rotate);

            Assert.That(rotate.Oper, Is.EqualTo(GT_ROR));
            Assert.That(count.IconValue, Is.EqualTo((nint)rightCount));
            Assert.That(count.IsContained, Is.True);
            Assert.That(count.Next, Is.SameAs(rotate));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void VariableRotateLeftInsertsNegationBeforeTheRotate()
    {
        WithLowering((compiler, lowering, block) => {
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var count = compiler.gtNewLclvNode(TYP_INT, 0);
            var rotate = new GenTreeOp(GT_ROL, TYP_INT, value, count) { IsUnusedValue = true };
            block.InsertAtEnd(value);
            block.InsertAtEnd(count);
            block.InsertAtEnd(rotate);

            LowerRotate(lowering, rotate);

            var negate = rotate.Op2.AsUnOp();
            Assert.That(rotate.Oper, Is.EqualTo(GT_ROR));
            Assert.That(negate.Oper, Is.EqualTo(GT_NEG));
            Assert.That(negate.Op1, Is.SameAs(count));
            Assert.That(count.Next, Is.SameAs(negate));
            Assert.That(negate.Next, Is.SameAs(rotate));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    private static void WithLowering(System.Action<Compiler, Lowering, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaCount = 1;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerRotate")]
    private static extern void LowerRotate(Lowering lowering, GenTree node);
}
#endif
