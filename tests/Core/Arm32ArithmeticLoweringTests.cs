// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32ArithmeticLoweringTests
{
    [TestCase(GT_ADD, 4095, false, true)]
    [TestCase(GT_ADD, 4095, true, false)]
    [TestCase(GT_ADD, 0x1_00000FFFL, false, true)]
    [TestCase(GT_SUB, -4095, false, true)]
    [TestCase(GT_AND, -16777216, false, true)]
    [TestCase(GT_XOR, 0x12345678, false, false)]
    [TestCase(GT_MUL, 1, false, false)]
    public static void ImmediateClassificationMatchesArmInstructionForms(
        genTreeOps operation, long value, bool setsFlags, bool expected)
    {
        WithLowering((compiler, _, lowering) =>
        {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, (nint)value);
            var node = new GenTreeOp(operation, TYP_INT, left, constant);
            if (setsFlags)
            {
                node.Flags |= GTF_SET_FLAGS;
            }

            Assert.That(lowering.IsContainableImmed(node, constant), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void CommutativeContainmentSwapsAnEncodableFirstOperand()
    {
        WithLowering((compiler, block, lowering) =>
        {
            var constant = compiler.gtNewIconNode(TYP_INT, -16777216);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var add = new GenTreeOp(GT_ADD, TYP_INT, constant, local);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(local);
            block.InsertAtEnd(add);

            ContainCheckBinary(lowering, add);

            Assert.That(constant.IsContained, Is.True);
            Assert.That(add.Op1, Is.SameAs(local));
            Assert.That(add.Op2, Is.SameAs(constant));
        });
    }

    [TestCase(GT_MUL)]
    [TestCase(GT_MULHI)]
    public static void MultiplyLoweringPreservesTheUncontainedOperands(genTreeOps operation)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            var multiply = new GenTreeOp(operation, TYP_INT, left, right);
            var next = compiler.gtNewIconNode(TYP_INT, 7);
            block.InsertAtEnd(left);
            block.InsertAtEnd(right);
            block.InsertAtEnd(multiply);
            block.InsertAtEnd(next);

            Assert.That(LowerMul(lowering, multiply), Is.SameAs(next));
            Assert.That(multiply.Op1, Is.SameAs(left));
            Assert.That(multiply.Op2, Is.SameAs(right));
            Assert.That(left.IsContained, Is.False);
            Assert.That(right.IsContained, Is.False);
        });
    }

    [Test]
    public static void AndWithNotLowersToAndNotOnlyWhenOptimizing()
    {
        foreach (var minOpts in new[] { false, true })
        {
            foreach (var notFirst in new[] { false, true })
            {
                WithLowering((compiler, block, lowering) =>
                {
                    var negatedOperand = compiler.gtNewLclvNode(TYP_INT, 0);
                    var otherOperand = compiler.gtNewLclvNode(TYP_INT, 1);
                    var not = new GenTreeUnOp(GT_NOT, TYP_INT, negatedOperand);
                    var and = new GenTreeOp(
                        GT_AND,
                        TYP_INT,
                        notFirst ? not : otherOperand,
                        notFirst ? otherOperand : not)
                    {
                        Flags = GTF_UNSIGNED | GTF_DONT_CSE,
                        IsUnusedValue = true,
                    };
                    var next = compiler.gtNewIconNode(TYP_INT, 7);
                    and._vnPair.SetBoth(123);

                    if (notFirst)
                    {
                        block.InsertAtEnd(negatedOperand);
                        block.InsertAtEnd(not);
                        block.InsertAtEnd(otherOperand);
                    }
                    else
                    {
                        block.InsertAtEnd(otherOperand);
                        block.InsertAtEnd(negatedOperand);
                        block.InsertAtEnd(not);
                    }

                    block.InsertAtEnd(and);
                    block.InsertAtEnd(next);

                    Assert.That(LowerBinaryArithmetic(lowering, and), Is.SameAs(next));
                    Assert.That(and.Oper, Is.EqualTo(minOpts ? GT_AND : GT_AND_NOT));
                    Assert.That(and.Flags, Is.EqualTo(GTF_UNSIGNED | GTF_DONT_CSE));
                    Assert.That(and._vnPair.Liberal, Is.EqualTo(minOpts ? 123u : ValueNumStore.NoVN));
                    if (minOpts)
                    {
                        Assert.That(and.Op1, Is.SameAs(notFirst ? not : otherOperand));
                        Assert.That(and.Op2, Is.SameAs(notFirst ? otherOperand : not));
                        Assert.That(not.Next, Is.SameAs(notFirst ? otherOperand : and));
                    }
                    else
                    {
                        Assert.That(and.Op1, Is.SameAs(otherOperand));
                        Assert.That(and.Op2, Is.SameAs(negatedOperand));
                        Assert.That(not.Prev, Is.Null);
                        Assert.That(not.Next, Is.Null);
                    }
                }, minOpts);
            }
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBinaryArithmetic")]
    private static extern GenTree? LowerBinaryArithmetic(Lowering lowering, GenTreeOp binary);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBinary")]
    private static extern void ContainCheckBinary(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerMul")]
    private static extern GenTree? LowerMul(Lowering lowering, GenTreeOp multiply);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    private static void WithLowering(Action<Compiler, BasicBlock, Lowering> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compFlags = minOpts ? 0 : CLFLG_REGVAR;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
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
