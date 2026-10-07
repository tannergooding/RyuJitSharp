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
    [TestCase(GT_ADD, 4096, false, true)]
    [TestCase(GT_ADD, 4097, false, false)]
    [TestCase(GT_ADD, 0x1_00000FFFL, false, true)]
    [TestCase(GT_SUB, -4095, false, true)]
    [TestCase(GT_AND, -16777216, false, true)]
    [TestCase(GT_AND, 0x00FF00FF, false, true)]
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
    public static void Arm32NoOpContainmentLeavesOperandsUncontained()
    {
        WithLowering((compiler, block, lowering) =>
        {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            var division = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor);
            block.InsertAtEnd(dividend);
            block.InsertAtEnd(divisor);
            block.InsertAtEnd(division);

            ContainCheckDivOrMod(lowering, division);

            Assert.That(dividend.IsContained || dividend.IsRegOptional, Is.False);
            Assert.That(divisor.IsContained || divisor.IsRegOptional, Is.False);

            var target = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var nonLocalJump = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, target);
            block.InsertAtEnd(target);
            block.InsertAtEnd(nonLocalJump);

            ContainCheckNonLocalJmp(lowering, nonLocalJump);

            Assert.That(target.IsContained || target.IsRegOptional, Is.False);

            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            ContainCheckCallOperands(lowering, call);
        });
    }

    [TestCase(GT_LSH)]
    [TestCase(GT_RSH)]
    public static void ShiftContainmentContainsTheImmediateCount(genTreeOps operation)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            var shiftBy = compiler.gtNewIconNode(TYP_INT, 3);
            var shift = new GenTreeOp(operation, TYP_INT, source, shiftBy);
            block.InsertAtEnd(source);
            block.InsertAtEnd(shiftBy);
            block.InsertAtEnd(shift);

            ContainCheckShiftRotate(lowering, shift);

            Assert.That(source.IsContained, Is.False);
            Assert.That(shiftBy.IsContained, Is.True);
        });
    }

    [TestCase(GT_LSH_HI)]
    [TestCase(GT_RSH_LO)]
    public static void LongWordShiftContainmentContainsTheLongSource(genTreeOps operation)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var low = compiler.gtNewLclvNode(TYP_INT, 0);
            var high = compiler.gtNewLclvNode(TYP_INT, 1);
            var source = new GenTreeOp(GT_LONG, TYP_LONG, low, high);
            var shiftBy = compiler.gtNewIconNode(TYP_INT, 3);
            var shift = new GenTreeOp(operation, TYP_INT, source, shiftBy);
            block.InsertAtEnd(low);
            block.InsertAtEnd(high);
            block.InsertAtEnd(source);
            block.InsertAtEnd(shiftBy);
            block.InsertAtEnd(shift);

            ContainCheckShiftRotate(lowering, shift);

            Assert.That(source.IsContained, Is.True);
            Assert.That(shiftBy.IsContained, Is.True);
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

    [TestCase(-256, 4u, false)]
    [TestCase(-255, 4u, true)]
    [TestCase(255, 1u, true)]
    [TestCase(255, 2u, false)]
    [TestCase(256, 1u, false)]
    public static void BlockStoreAddressContainmentUsesArmImmediateBounds(int offset, uint size, bool expectedContained)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var offsetNode = compiler.gtNewIconNode(TYP_INT, (nint)offset);
            var address = new GenTreeOp(GT_ADD, TYP_BYREF, baseAddress, offsetNode);
            address._vnPair.SetBoth(123);
            var data = compiler.gtNewIconNode(TYP_INT, 0);
            var store = new GenTreeBlk(TYP_STRUCT, address, data, new ClassLayout(size))
            {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };
            block.InsertAtEnd(baseAddress);
            block.InsertAtEnd(offsetNode);
            block.InsertAtEnd(address);
            block.InsertAtEnd(data);
            block.InsertAtEnd(store);

            ContainBlockStoreAddress(lowering, store, size, address, null);

            Assert.That(store.Addr.IsContained, Is.EqualTo(expectedContained));
            if (expectedContained)
            {
                var mode = store.Addr.AsAddrMode();
                Assert.That(mode.BaseAddress, Is.SameAs(baseAddress));
                Assert.That(mode.Index, Is.Null);
                Assert.That(mode.Scale, Is.Zero);
                Assert.That(mode.Offset, Is.EqualTo(offset));
                Assert.That(address.Next, Is.Null);
                Assert.That(offsetNode.Next, Is.Null);
            }
            else
            {
                Assert.That(store.Addr, Is.SameAs(address));
                Assert.That(address.AsOp().Op2, Is.SameAs(offsetNode));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBinaryArithmetic")]
    private static extern GenTree? LowerBinaryArithmetic(Lowering lowering, GenTreeOp binary);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainBlockStoreAddress")]
    private static extern void ContainBlockStoreAddress(
        Lowering lowering, GenTreeBlk block, uint size, GenTree address, GenTree? addressParent);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBinary")]
    private static extern void ContainCheckBinary(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerMul")]
    private static extern GenTree? LowerMul(Lowering lowering, GenTreeOp multiply);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckDivOrMod")]
    private static extern void ContainCheckDivOrMod(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckNonLocalJmp")]
    private static extern void ContainCheckNonLocalJmp(Lowering lowering, GenTreeUnOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckCallOperands")]
    private static extern void ContainCheckCallOperands(Lowering lowering, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckShiftRotate")]
    private static extern void ContainCheckShiftRotate(Lowering lowering, GenTreeOp node);

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
