// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class HardwareConditionLoweringTests
{
    [TestCase(NI_X86Base_COMIS, GenCondition.FLT, GenCondition.FGT, true)]
    [TestCase(NI_X86Base_UCOMIS, GenCondition.FGT, GenCondition.FGT, false)]
    [TestCase(NI_X86Base_COMIS, GenCondition.FEQ, GenCondition.FEQ, false)]
    [TestCase(NI_X86Base_PTEST, GenCondition.C, GenCondition.C, false)]
    [TestCase(NI_X86Base_PTEST, GenCondition.EQ, GenCondition.EQ, false)]
    [TestCase(NI_AVX512_KTEST, GenCondition.EQ, GenCondition.EQ, false)]
    public static void IntrinsicFlagsPreservePreferredFloatAndCarryConditions(
        NamedIntrinsic intrinsic, GenCondition.CodeKind condition, GenCondition.CodeKind expected, bool swap)
    {
        WithCompiler(compiler => {
            var type = intrinsic is NI_AVX512_KTEST ? TYP_MASK : TYP_SIMD16;
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            var left = compiler.gtNewLclvNode(type, 0);
            var right = compiler.gtNewLclvNode(type, 1);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_INT, intrinsic, TYP_FLOAT, 16, left, right);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, node) { IsUnusedValue = true };
            var block = NewBlock(left, right, node, user);

            LowerHWIntrinsicCC(NewLowering(compiler, block), node, intrinsic, new GenCondition(condition));

            var cc = user.Op1.AsCC();
            Assert.That(cc.Oper, Is.EqualTo(GT_SETCC));
            Assert.That(cc.Condition.Code, Is.EqualTo(expected));
            Assert.That(node.Type, Is.EqualTo(TYP_VOID));
            Assert.That(node.IsUnusedValue, Is.False);
            Assert.That(node.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
            Assert.That(node.GetOp(1), Is.SameAs(swap ? right : left));
            Assert.That(node.GetOp(2), Is.SameAs(swap ? left : right));
            Assert.That(node.Next, Is.SameAs(cc));
            Assert.That(cc.Next, Is.SameAs(user));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void UnusedIntrinsicDoesNotAcquireAFlagsConsumer()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var right = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_X86Base_COMIS, TYP_FLOAT, 16, left, right);
            node.IsUnusedValue = true;
            var block = NewBlock(left, right, node);

            LowerHWIntrinsicCC(NewLowering(compiler, block), node, NI_X86Base_COMIS, new GenCondition(GenCondition.FEQ));

            Assert.That(node.Type, Is.EqualTo(TYP_VOID));
            Assert.That(node.IsUnusedValue, Is.False);
            Assert.That(node.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_EMPTY));
            Assert.That(block.LastNode, Is.SameAs(node));
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCC")]
    private static extern void LowerHWIntrinsicCC(
        Lowering lowering, GenTreeHWIntrinsic node, NamedIntrinsic intrinsic, GenCondition condition);

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
        compiler.lvaTable = [new() { Type = TYP_SIMD16 }, new() { Type = TYP_SIMD16 }];
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
