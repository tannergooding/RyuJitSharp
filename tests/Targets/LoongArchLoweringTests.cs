// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoongArchLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void IntegerJTrueLoweringReturnsNextLirNode(bool hasComparison)
    {
        WithLowering((compiler, block, lowering) =>
        {
            GenTree condition;
            if (hasComparison)
            {
                var left = compiler.gtNewIconNode(TYP_INT, 7);
                var right = compiler.gtNewIconNode(TYP_INT, 3);
                condition = new GenTreeOp(GT_NE, TYP_INT, left, right);
                block.InsertAtEnd(left);
                block.InsertAtEnd(right);
                block.InsertAtEnd(condition);
            }
            else
            {
                condition = compiler.gtNewIconNode(TYP_INT, 1);
                block.InsertAtEnd(condition);
            }

            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            var successor = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            block.InsertAtEnd(branch);
            block.InsertAtEnd(successor);

            Assert.That(LowerNode(lowering, branch), Is.SameAs(successor));
            Assert.That(successor.Prev?.Oper, Is.EqualTo(GT_JCMP));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    private static void WithLowering(Action<Compiler, BasicBlock, Lowering> action)
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
        compiler.lvaCount = 1;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);

        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, block, lowering);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
