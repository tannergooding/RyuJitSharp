// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompareNodeLoweringTests
{
    [TestCase(false, 200, true)]
    [TestCase(true, 200, false)]
    [TestCase(false, 256, false)]
    public static void SmallMemoryComparisonNarrowingHonorsOptimizationModeAndRange(
        bool minOpts, int constant, bool narrowed)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var load = compiler.gtNewIndir(TYP_UBYTE, address);
            var value = compiler.gtNewIconNode(TYP_INT, constant);
            var compare = new GenTreeOp(GT_LT, TYP_INT, load, value);
            var owner = compiler.gtNewStoreLclVarNode(2, compare);
            var block = NewBlock(address, load, value, compare, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(owner));
            Assert.That(value.Type, Is.EqualTo(narrowed ? TYP_UBYTE : TYP_INT));
            Assert.That(load.IsContained, Is.EqualTo(narrowed));
            Assert.That(value.IsContained, Is.True);
            Assert.That(compare.IsUnsigned, Is.EqualTo(narrowed));
            Assert.That(owner.Data, Is.SameAs(compare));
        }, minOpts);
    }

    [TestCase(GT_EQ, -64, true)]
    [TestCase(GT_LT, 0, false)]
    public static void SignedByteCastRemovalRetainsTheSignBitTestInvariant(
        genTreeOps oper, int constant, bool removed)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var cast = new GenTreeCast(TYP_INT, local, false, TYP_BYTE);
            var value = compiler.gtNewIconNode(TYP_INT, constant);
            var compare = new GenTreeOp(oper, TYP_INT, cast, value);
            var owner = compiler.gtNewStoreLclVarNode(2, compare);
            var block = NewBlock(local, cast, value, compare, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(owner));
            Assert.That(compare.Op1, Is.SameAs(removed ? (GenTree)local : cast));
            Assert.That(local.Type, Is.EqualTo(removed ? TYP_BYTE : TYP_INT));
            Assert.That(value.Type, Is.EqualTo(removed ? TYP_BYTE : TYP_INT));
            Assert.That(compare.IsUnsigned, Is.False);
            Assert.That(local.Next, Is.SameAs(removed ? (GenTree)value : cast));
        });
    }

    [Test]
    public static void ByteCastRemovalRecomputesContainmentForTheNarrowedProducer()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var load = compiler.gtNewIndir(TYP_INT, address);
            var mask = compiler.gtNewIconNode(TYP_INT, 1);
            var binary = new GenTreeOp(GT_OR, TYP_INT, load, mask);
            var cast = new GenTreeCast(TYP_INT, binary, false, TYP_UBYTE);
            var value = compiler.gtNewIconNode(TYP_INT, 200);
            var compare = new GenTreeOp(GT_EQ, TYP_INT, cast, value);
            var owner = compiler.gtNewStoreLclVarNode(2, compare);
            var block = NewBlock(address, load, mask, binary, cast, value, compare, owner);
            load.IsContained = true;

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(owner));
            Assert.That(compare.Op1, Is.SameAs(binary));
            Assert.That(binary.Type, Is.EqualTo(TYP_UBYTE));
            Assert.That(value.Type, Is.EqualTo(TYP_UBYTE));
            Assert.That(compare.IsUnsigned, Is.True);
            Assert.That(load.Type, Is.EqualTo(TYP_INT));
            Assert.That(load.IsContained, Is.False);
            Assert.That(mask.IsContained, Is.True);
            Assert.That(binary.Next, Is.SameAs(value));
        });
    }

    [TestCase(GT_EQ, 0, true)]
    [TestCase(GT_NE, 0, false)]
    [TestCase(GT_EQ, 1, false)]
    public static void BooleanLowBitComparisonsReplaceTheirOwningUse(
        genTreeOps oper, int constant, bool insertNot)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var and = new GenTreeOp(GT_AND, TYP_INT, local, one);
            var value = compiler.gtNewIconNode(TYP_INT, constant);
            var compare = new GenTreeOp(oper, TYP_INT, and, value);
            var owner = compiler.gtNewStoreLclVarNode(2, compare);
            var block = NewBlock(local, one, and, value, compare, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(owner));
            Assert.That(owner.Data, Is.SameAs(and));
            Assert.That(and.Next, Is.SameAs(owner));
            Assert.That(and.Op1.Oper, Is.EqualTo(insertNot ? GT_NOT : GT_LCL_VAR));
            if (insertNot)
            {
                Assert.That(local.Next, Is.SameAs(and.Op1));
                Assert.That(and.Op1.AsUnOp().Op1, Is.SameAs(local));
                Assert.That(and.Op1.Next, Is.SameAs(one));
            }
            Assert.That(compare.Next, Is.Null);
            Assert.That(value.Next, Is.Null);
        });
    }

    [TestCase(TYP_INT, 0x80, TYP_UBYTE)]
    [TestCase(TYP_SHORT, 0x1200, TYP_USHORT)]
    [TestCase(TYP_INT, 0x1200, TYP_INT)]
    public static void MemoryBitTestsNarrowOnlyToNativeProfitableWidths(
        var_types loadType, int bits, var_types expectedType)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var load = compiler.gtNewIndir(loadType, address);
            var mask = compiler.gtNewIconNode(TYP_INT, bits);
            var and = new GenTreeOp(GT_AND, TYP_INT, load, mask);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var compare = new GenTreeOp(GT_EQ, TYP_INT, and, zero);
            compare._vnPair.SetBoth(123);
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, compare);
            var block = NewBlock(address, load, mask, and, zero, compare, branch);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(branch));
            Assert.That(compare.Oper, Is.EqualTo(GT_TEST_EQ));
            Assert.That(compare.Op1, Is.SameAs(load));
            Assert.That(compare.Op2, Is.SameAs(mask));
            Assert.That(load.Type, Is.EqualTo(expectedType));
            Assert.That(mask.Type, Is.EqualTo(expectedType));
            Assert.That(load.IsContained && mask.IsContained, Is.True);
            Assert.That(compare._vnPair.Liberal, Is.EqualTo(123u));
            Assert.That(branch.Op1, Is.SameAs(compare));
        });
    }

    [Test]
    public static void BranchBitTestReductionRetainsTheRelopAndReleasesTheShiftOperands()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var load = compiler.gtNewIndir(TYP_INT, address);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_RSZ, TYP_INT, load, index);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var and = new GenTreeOp(GT_AND, TYP_INT, shift, one);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var compare = new GenTreeOp(GT_NE, TYP_INT, and, zero);
            compare._vnPair.SetBoth(123);
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, compare);
            var block = NewBlock(address, load, index, shift, one, and, zero, compare, branch);
            load.IsContained = true;

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(branch));
            Assert.That(compare.Oper, Is.EqualTo(GT_BITTEST_NE));
            Assert.That(compare.Op1, Is.SameAs(load));
            Assert.That(compare.Op2, Is.SameAs(index));
            Assert.That(load.IsContained || index.IsContained, Is.False);
            Assert.That(compare._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(load.Next, Is.SameAs(index));
            Assert.That(index.Next, Is.SameAs(compare));
            Assert.That(branch.Op1, Is.SameAs(compare));
        });
    }

    [Test]
    public static void MultiBitMaskEqualityReplacesTheZeroConstantOwnerAndPreservesItsPosition()
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var mask = compiler.gtNewIconNode(TYP_INT, 6);
            var and = new GenTreeOp(GT_AND, TYP_INT, local, mask);
            var value = compiler.gtNewIconNode(TYP_INT, 6);
            value._vnPair.SetBoth(123);
#if DEBUG
            var constantId = value.TreeId;
#endif
            var intervening = compiler.gtNewIconNode(TYP_INT, 19);
            var compare = new GenTreeOp(GT_EQ, TYP_INT, and, value);
            var block = NewBlock(local, mask, and, value, intervening, compare);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.Null);
            Assert.That(and.Op1.Oper, Is.EqualTo(GT_NOT));
            Assert.That(and.Op1.AsUnOp().Op1, Is.SameAs(local));
            Assert.That(local.Next, Is.SameAs(and.Op1));
            Assert.That(compare.Op2, Is.Not.SameAs(value));
            Assert.That(compare.Op2.IsIntegralConst(0), Is.True);
            Assert.That(compare.Op2._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(and.Next, Is.SameAs(compare.Op2));
            Assert.That(compare.Op2.Next, Is.SameAs(intervening));
            Assert.That(value.Prev is null && value.Next is null, Is.True);
#if DEBUG
            Assert.That(compare.Op2.TreeId, Is.EqualTo(constantId));
#endif
        });
    }

    [Test]
    public static void ZeroComparisonCapturesProducerFlagsBeforeInterveningNodes()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = TYP_LONG;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var increment = compiler.gtNewIconNode(TYP_INT, 7);
            var add = new GenTreeOp(GT_ADD, TYP_INT, local, increment);
            var intervening = compiler.gtNewIconNode(TYP_INT, 19);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var compare = new GenTreeOp(GT_EQ, TYP_LONG, add, zero);
            var owner = compiler.gtNewStoreLclVarNode(2, compare);
            var block = NewBlock(local, increment, add, intervening, zero, compare, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(owner));
            Assert.That(owner.Data.Oper, Is.EqualTo(GT_SETCC));
            Assert.That(owner.Data.AsCC().Condition.Code, Is.EqualTo(GenCondition.EQ));
            Assert.That(owner.Data.Type, Is.EqualTo(TYP_LONG));
            Assert.That(add.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
            Assert.That(add.IsUnusedValue, Is.True);
            Assert.That(add.Next, Is.SameAs(owner.Data));
            Assert.That(owner.Data.Next, Is.SameAs(intervening));
            Assert.That(intervening.Next, Is.SameAs(owner));
            Assert.That(compare.Next, Is.Null);
        });
    }

    [Test]
    public static void NegatedFloatingRelopPreservesNanSemanticsAndRetypesTheOwnerValue()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            compiler.lvaTable[1].Type = TYP_DOUBLE;
            compiler.lvaTable[2].Type = TYP_LONG;
            var first = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var second = compiler.gtNewLclvNode(TYP_DOUBLE, 1);
            var inner = new GenTreeOp(GT_LT, TYP_INT, first, second);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var outer = new GenTreeOp(GT_EQ, TYP_LONG, inner, zero);
            var owner = compiler.gtNewStoreLclVarNode(2, outer);
            var block = NewBlock(first, second, inner, zero, outer, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), outer), Is.SameAs(owner));
            Assert.That(owner.Data, Is.SameAs(inner));
            Assert.That(inner.Type, Is.EqualTo(TYP_LONG));
            Assert.That(GenCondition.FromFloatRelop(inner).Code, Is.EqualTo(GenCondition.FGEU));
            Assert.That(inner.Next, Is.SameAs(owner));
        });
    }

    [Test]
    public static void FloatingComparisonUsesConditionSelectedContainmentWithoutChangingNanFlags()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            var first = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            var second = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var compare = new GenTreeOp(GT_GT, TYP_INT, first, second) {
                Flags = GTF_RELOP_NAN_UN,
            };
            var owner = compiler.gtNewStoreLclVarNode(2, compare);
            var block = NewBlock(first, second, compare, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), compare), Is.SameAs(owner));
            Assert.That(first.IsContained, Is.True);
            Assert.That(second.IsContained, Is.False);
            Assert.That(compare.Op1, Is.SameAs(first));
            Assert.That(compare.Op2, Is.SameAs(second));
            Assert.That(GenCondition.FromFloatRelop(compare).Code, Is.EqualTo(GenCondition.FGTU));
        });
    }

    [Test]
    public static void NegatedSetccUsesTheCanonicalConditionReversal()
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var second = compiler.gtNewIconNode(TYP_INT, 3);
            var producer = new GenTreeOp(GT_CMP, TYP_VOID, first, second) {
                Flags = GTF_SET_FLAGS,
            };
            var inner = compiler.gtNewCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.P));
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var outer = new GenTreeOp(GT_EQ, TYP_INT, inner, zero);
            var owner = compiler.gtNewStoreLclVarNode(2, outer);
            var block = NewBlock(first, second, producer, inner, zero, outer, owner);

            Assert.That(LowerCompare(NewLowering(compiler, block), outer), Is.SameAs(owner));
            Assert.That(owner.Data, Is.SameAs(inner));
            Assert.That(inner.Condition.Code, Is.EqualTo(GenCondition.NP));
            Assert.That(producer.Next, Is.SameAs(inner));
            Assert.That(inner.Next, Is.SameAs(owner));
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCompare")]
    private static extern GenTree? LowerCompare(Lowering lowering, GenTree compare);

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
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
