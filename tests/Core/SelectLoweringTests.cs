// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class SelectLoweringTests
{
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void SelectContainsBothIndependentMemoryOperandsWithoutChangingItsCondition(var_types type)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[1].Type = TYP_BYREF;
            compiler.lvaTable[3].Type = type;
            var condition = compiler.gtNewLclvNode(TYP_INT, 2);
            var firstAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var first = compiler.gtNewIndir(type, firstAddress);
            var secondAddress = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var second = compiler.gtNewIndir(type, secondAddress);
            var select = new GenTreeConditional(GT_SELECT, type, condition, first, second);
            select._vnPair.SetBoth(123);
            var owner = compiler.gtNewStoreLclVarNode(3, select);
            var block = NewBlock(condition, firstAddress, first, secondAddress, second, select, owner);

            ContainCheckSelect(NewLowering(compiler, block), select);

            Assert.That(first.IsContained && second.IsContained, Is.True);
            Assert.That(first.IsRegOptional || second.IsRegOptional, Is.False);
            Assert.That(select.Cond, Is.SameAs(condition));
            Assert.That(select.Op1, Is.SameAs(first));
            Assert.That(select.Op2, Is.SameAs(second));
            Assert.That(select._vnPair.Liberal, Is.EqualTo(123u));
            Assert.That(owner.Data, Is.SameAs(select));
        });
    }

    [TestCase(GenCondition.EQ, true)]
    [TestCase(GenCondition.FEQ, false)]
    [TestCase(GenCondition.FNEU, false)]
    public static void SelectccAvoidsMemoryAndOptionalRegistersForCompoundFloatConditions(
        GenCondition.CodeKind code, bool contained)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[1].Type = TYP_BYREF;
            var firstAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var first = compiler.gtNewIndir(TYP_INT, firstAddress);
            var secondAddress = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var second = compiler.gtNewIndir(TYP_INT, secondAddress);
            var condition = new GenCondition(code);
            var compareType = condition.IsFloat ? TYP_DOUBLE : TYP_INT;
            compiler.lvaTable[2].Type = compareType;
            var left = compiler.gtNewLclvNode(compareType, 2);
            GenTree right = condition.IsFloat
                ? compiler.gtNewDconNode(TYP_DOUBLE, 0.0)
                : compiler.gtNewIconNode(TYP_INT, 0);
            var compare = new GenTreeOp(GT_CMP, TYP_VOID, left, right) {
                Flags = GTF_SET_FLAGS,
            };
            var select = new GenTreeOpCC(GT_SELECTCC, TYP_INT, condition, first, second);
            var block = NewBlock(firstAddress, first, secondAddress, second, left, right, compare, select);

            ContainCheckSelect(NewLowering(compiler, block), select);

            Assert.That(first.IsContained, Is.EqualTo(contained));
            Assert.That(second.IsContained, Is.EqualTo(contained));
            Assert.That(first.IsRegOptional || second.IsRegOptional, Is.False);
            Assert.That(select.Condition.Code, Is.EqualTo(code));
        });
    }

    [Test]
    public static void SelectDoesNotContainOrMakeOptionalAnOperandRequiringExtension()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var condition = compiler.gtNewLclvNode(TYP_INT, 2);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var narrow = compiler.gtNewIndir(TYP_BYTE, address);
            var full = compiler.gtNewLclvNode(TYP_INT, 1);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, narrow, full);
            var block = NewBlock(condition, address, narrow, full, select);

            ContainCheckSelect(NewLowering(compiler, block), select);

            Assert.That(narrow.IsContained || narrow.IsRegOptional, Is.False);
            Assert.That(full.IsContained, Is.False);
            Assert.That(full.IsRegOptional, Is.True);
        });
    }

    [Test]
    public static void InterveningStorePreventsContainingTheEarlierLoadButAllowsItsSpilledValue()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[1].Type = TYP_BYREF;
            var condition = compiler.gtNewLclvNode(TYP_INT, 2);
            var firstAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var first = compiler.gtNewIndir(TYP_INT, firstAddress);
            var storeAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var store = compiler.gtNewStoreIndNode(TYP_INT, storeAddress, value);
            var secondAddress = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var second = compiler.gtNewIndir(TYP_INT, secondAddress);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            var block = NewBlock(condition, firstAddress, first, storeAddress, value, store,
                secondAddress, second, select);

            ContainCheckSelect(NewLowering(compiler, block), select);

            Assert.That(first.IsContained, Is.False);
            Assert.That(first.IsRegOptional, Is.True);
            Assert.That(second.IsContained, Is.True);
            Assert.That(first.Next, Is.SameAs(storeAddress));
            Assert.That(store.Next, Is.SameAs(secondAddress));
        });
    }

    [Test]
    public static void AddressExposedLocalCannotBecomeOptionalAcrossItsDefinition()
    {
        WithCompiler(compiler => {
            compiler.lvaSetVarAddrExposed(0, AddressExposedReason.ESCAPE_ADDRESS);
            var condition = compiler.gtNewLclvNode(TYP_INT, 2);
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            var second = compiler.gtNewLclvNode(TYP_INT, 1);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            var block = NewBlock(condition, first, value, store, second, select);

            ContainCheckSelect(NewLowering(compiler, block), select);

            Assert.That(first.IsContained || first.IsRegOptional, Is.False);
            Assert.That(second.IsContained, Is.False);
            Assert.That(second.IsRegOptional, Is.True);
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckSelect")]
    private static extern void ContainCheckSelect(Lowering lowering, GenTreeOp select);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 4;
        for (var index = 0; index < compiler.lvaCount; index++)
        {
            compiler.lvaTable[index].Type = TYP_INT;
        }
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
