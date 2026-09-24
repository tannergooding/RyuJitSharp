// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ComparisonLoweringTests
{
    [TestCase(GT_AND, false, false)]
    [TestCase(GT_TEST_EQ, true, false)]
    [TestCase(GT_TEST_NE, false, true)]
    public static void ShiftedOneReductionPreservesValueIndexAndOwner(
        genTreeOps oper, bool reverse, bool constantIndex)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            GenTree index = constantIndex
                ? compiler.gtNewIconNode(TYP_INT, 37)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            if (constantIndex)
            {
                index.IsContained = true;
            }
            var shift = new GenTreeOp(GT_LSH, TYP_INT, one, index);
            var test = new GenTreeOp(oper, TYP_INT, reverse ? shift : value, reverse ? value : shift);
            test._vnPair.SetBoth(123);
            var owner = compiler.gtNewStoreLclVarNode(2, test);
            var block = reverse
                ? NewBlock(one, index, shift, value, test, owner)
                : NewBlock(value, one, index, shift, test, owner);

            Assert.That(TryReduceSingleBitTestOps(NewLowering(compiler, block), test), Is.True);

            Assert.That(test.Op1, Is.SameAs(value));
            Assert.That(test.Op2, Is.SameAs(index));
            Assert.That(test.Oper, Is.EqualTo(oper));
            Assert.That(test._vnPair.Liberal, Is.EqualTo(123u));
            Assert.That(owner.Data, Is.SameAs(test));
            Assert.That(index.IsContained, Is.EqualTo(constantIndex));
            Assert.That(test.Next, Is.SameAs(owner));
            Assert.That(one.Prev is null && one.Next is null, Is.True);
            Assert.That(shift.Prev is null && shift.Next is null, Is.True);
            Assert.That(reverse ? value.Prev : index.Prev, Is.SameAs(reverse ? index : value));
        });
    }

    [TestCase(GT_RSH, false)]
    [TestCase(GT_RSZ, true)]
    public static void RightShiftReductionClearsContainedMemoryAndPreservesLirOrder(genTreeOps oper, bool reverse)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var value = compiler.gtNewIndir(TYP_INT, address);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(oper, TYP_INT, value, index);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var test = new GenTreeOp(GT_TEST_NE, TYP_INT, reverse ? one : shift, reverse ? shift : one);
            var owner = compiler.gtNewStoreLclVarNode(2, test);
            var block = reverse
                ? NewBlock(one, address, value, index, shift, test, owner)
                : NewBlock(address, value, index, shift, one, test, owner);
            value.IsContained = true;
            one.IsContained = true;
            var flags = value.Flags;

            Assert.That(TryReduceSingleBitTestOps(NewLowering(compiler, block), test), Is.True);

            Assert.That(test.Op1, Is.SameAs(value));
            Assert.That(test.Op2, Is.SameAs(index));
            Assert.That(test.Oper, Is.EqualTo(GT_TEST_NE));
            Assert.That(owner.Data, Is.SameAs(test));
            Assert.That(value.IsContained || value.IsRegOptional, Is.False);
            Assert.That(value.Flags, Is.EqualTo(flags & ~GTF_CONTAINED));
            Assert.That(address.Next, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(index));
            Assert.That(index.Next, Is.SameAs(test));
            Assert.That(test.Next, Is.SameAs(owner));
            Assert.That(one.Prev is null && one.Next is null, Is.True);
            Assert.That(shift.Prev is null && shift.Next is null, Is.True);
        });
    }

    [TestCase(GT_LSH, 2, false)]
    [TestCase(GT_RSH, 1, true)]
    [TestCase(GT_RSZ, 2, false)]
    public static void UnrecognizedBitTestsRetainOperandsFlagsAndLinks(genTreeOps oper, int mask, bool constantIndex)
    {
        WithCompiler(compiler => {
            GenTree value = oper is GT_LSH
                ? compiler.gtNewIconNode(TYP_INT, mask)
                : compiler.gtNewLclvNode(TYP_INT, 0);
            GenTree index = constantIndex
                ? compiler.gtNewIconNode(TYP_INT, 5)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(oper, TYP_INT, value, index);
            var other = compiler.gtNewIconNode(TYP_INT, mask);
            var test = new GenTreeOp(GT_AND, TYP_INT, shift, other);
            var block = NewBlock(value, index, shift, other, test);
            other.IsContained = true;

            Assert.That(TryReduceSingleBitTestOps(NewLowering(compiler, block), test), Is.False);

            Assert.That(test.Op1, Is.SameAs(shift));
            Assert.That(test.Op2, Is.SameAs(other));
            Assert.That(shift.Op1, Is.SameAs(value));
            Assert.That(shift.Op2, Is.SameAs(index));
            Assert.That(other.IsContained, Is.True);
            Assert.That(value.Next, Is.SameAs(index));
            Assert.That(index.Next, Is.SameAs(shift));
            Assert.That(shift.Next, Is.SameAs(other));
            Assert.That(other.Next, Is.SameAs(test));
        });
    }

    [TestCase(GT_LSH, false, false)]
    [TestCase(GT_LSH, true, true)]
    [TestCase(GT_RSH, false, false)]
    [TestCase(GT_RSZ, true, true)]
    [TestCase(GT_ROR, false, false)]
    [TestCase(GT_ROL, true, true)]
    [TestCase(GT_ADD, false, true)]
    public static void ZeroFlagProfitabilityIsSeparateFromInstructionFlagSupport(
        genTreeOps oper, bool constantCount, bool expected)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            GenTree count = constantCount
                ? compiler.gtNewIconNode(TYP_INT, 0)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            var operation = new GenTreeOp(oper, TYP_INT, value, count);
            var block = NewBlock(value, count, operation);
            var flags = operation.Flags;

            Assert.That(IsProfitableToSetZeroFlag(NewLowering(compiler, block), operation), Is.EqualTo(expected));
            Assert.That(operation.Flags, Is.EqualTo(flags));
            Assert.That(operation.Op1, Is.SameAs(value));
            Assert.That(operation.Op2, Is.SameAs(count));
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryReduceSingleBitTestOps")]
    private static extern bool TryReduceSingleBitTestOps(Lowering lowering, GenTreeOp test);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "IsProfitableToSetZeroFlag")]
    private static extern bool IsProfitableToSetZeroFlag(Lowering lowering, GenTree operation);

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
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
        compiler.lvaTable[2].Type = TYP_INT;
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
