// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArithmeticLoweringTests
{
    [Test]
    public static void AddZeroReplacesTheOwningUseAndRemovesDeadNodes()
    {
        WithCompiler(true, (compiler, block, lowering) => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var add = new GenTreeOp(GT_ADD, TYP_INT, local, zero);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, add);
            block.InsertAtEnd(local);
            block.InsertAtEnd(zero);
            block.InsertAtEnd(add);
            block.InsertAtEnd(use);

            Assert.That(LowerAdd(lowering, add), Is.SameAs(use));
            Assert.That(use.Op1, Is.SameAs(local));
            Assert.That(local.Next, Is.SameAs(use));
            Assert.That(zero.Next, Is.Null);
            Assert.That(add.Next, Is.Null);
        });
    }

    [Test]
    public static void OptimizedAddFoldsAdjacentOffsetsWithoutChangingItsOwner()
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var first = compiler.gtNewIconNode(TYP_INT, 12);
            var inner = new GenTreeOp(GT_ADD, TYP_INT, local, first);
            var second = compiler.gtNewIconNode(TYP_INT, 30);
            var outer = new GenTreeOp(GT_ADD, TYP_INT, inner, second);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, outer);
            foreach (var node in new GenTree[] { local, first, inner, second, outer, use })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerAdd(lowering, outer), Is.Null);
            Assert.That(outer.Op1, Is.SameAs(local));
            Assert.That(outer.Op2, Is.SameAs(second));
            Assert.That(second.AsIntCon().IconValue, Is.EqualTo((nint)42));
            Assert.That(second.IsContained, Is.True);
            Assert.That(use.Op1, Is.SameAs(outer));
            Assert.That(inner.Prev, Is.Null);
        });
    }

    [Test]
    public static void AddReplacesNonIndirectionOwnerWithAddressMode()
    {
        WithCompiler(true, (compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_LONG;
            var baseAddress = compiler.gtNewLclvNode(TYP_LONG, 0);
            var index = compiler.gtNewLclvNode(TYP_LONG, 1);
            var scale = compiler.gtNewIconNode(TYP_INT, 2);
            var shift = new GenTreeOp(GT_LSH, TYP_LONG, index, scale);
            var add = new GenTreeOp(GT_ADD, TYP_LONG, baseAddress, shift);
            var displacement = compiler.gtNewIconNode(TYP_LONG, 16);
            var outer = new GenTreeOp(GT_ADD, TYP_LONG, add, displacement);
            var user = new GenTreeUnOp(GT_NEG, TYP_LONG, outer);
            foreach (var node in new GenTree[] { baseAddress, index, scale, shift, add, displacement, outer, user })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerAdd(lowering, outer), Is.SameAs(user));
            Assert.That(user.Op1.Oper, Is.EqualTo(GT_LEA));
            Assert.That(user.Op1.AsAddrMode().BaseAddress, Is.SameAs(baseAddress));
            Assert.That(user.Op1.AsAddrMode().Index, Is.SameAs(index));
            Assert.That(user.Op1.AsAddrMode().Scale, Is.EqualTo(4));
            Assert.That(user.Op1.AsAddrMode().Offset, Is.EqualTo(16));
            Assert.That(outer.Next, Is.Null);
        });
    }

    [Test]
    public static void OptimizedAddReplacesFrozenObjectHandleWithByrefConstant()
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var handle = compiler.gtNewIconHandleNode((nint)0x1000, GTF_ICON_OBJ_HDL);
            var offset = compiler.gtNewIconNode(TYP_I_IMPL, 24);
            var add = new GenTreeOp(GT_ADD, TYP_BYREF, handle, offset);
            var user = new GenTreeUnOp(GT_PUTARG_REG, TYP_BYREF, add);
            foreach (var node in new GenTree[] { handle, offset, add, user })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerAdd(lowering, add), Is.SameAs(user));
            Assert.That(user.Op1.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(user.Op1.AsIntCon().IconValue, Is.EqualTo((nint)0x1018));
            Assert.That(user.Op1.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(handle.Next, Is.Null);
            Assert.That(add.Next, Is.Null);
        });
    }

    [TestCase(true, GT_MUL, 8)]
    [TestCase(false, GT_LSH, 3)]
    public static void MulByPowerOfTwoDistinguishesMinOptsFromOptimization(bool minOpts, genTreeOps expected, int value)
    {
        WithCompiler(minOpts, (compiler, block, lowering) => {
            Assert.That(compiler.opts.Tier0OptimizationEnabled, Is.True);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 8);
            var mul = new GenTreeOp(GT_MUL, TYP_INT, local, constant);
            block.InsertAtEnd(local);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(mul);

            Assert.That(LowerMul(lowering, mul), Is.Null);
            Assert.That(mul.Oper, Is.EqualTo(expected));
            Assert.That(constant.AsIntCon().IconValue, Is.EqualTo((nint)value));
            Assert.That(constant.IsContained, Is.True);
        });
    }

    [TestCase(7, GT_SUB)]
    [TestCase(17, GT_ADD)]
    public static void MulNearPowerOfTwoBuildsShiftAndASeparateLocalRead(int multiplier, genTreeOps expected)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, multiplier);
            var mul = new GenTreeOp(GT_MUL, TYP_INT, local, constant);
            block.InsertAtEnd(local);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(mul);

            Assert.That(LowerMul(lowering, mul), Is.Null);
            Assert.That(mul.Oper, Is.EqualTo(expected));
            Assert.That(mul.Op1.Oper, Is.EqualTo(GT_LSH));
            Assert.That(mul.Op1.AsOp().Op1, Is.SameAs(local));
            Assert.That(mul.Op1.AsOp().Op2, Is.SameAs(constant));
            Assert.That(constant.IsContained, Is.True);
            Assert.That(mul.Op2, Is.Not.SameAs(local));
            Assert.That(mul.Op2.AsLclVar().LclNum, Is.EqualTo(0));
            Assert.That(mul.Op2.Next, Is.SameAs(constant));
        });
    }

    [TestCase(3, false)]
    [TestCase(11, true)]
    public static void MulImmediateContainmentPreservesLeaRegisterRequirement(int multiplier, bool memoryContained)
    {
        WithCompiler(true, (compiler, block, lowering) => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, multiplier);
            var mul = new GenTreeOp(GT_MUL, TYP_INT, local, constant);
            block.InsertAtEnd(local);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(mul);

            Assert.That(LowerMul(lowering, mul), Is.Null);
            Assert.That(constant.IsContained, Is.True);
            Assert.That(local.IsContained, Is.EqualTo(memoryContained));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerAdd")]
    private static extern GenTree? LowerAdd(Lowering lowering, GenTreeOp add);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerMul")]
    private static extern GenTree? LowerMul(Lowering lowering, GenTreeOp mul);

    private static void WithCompiler(bool minOpts, Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_LONG;
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
