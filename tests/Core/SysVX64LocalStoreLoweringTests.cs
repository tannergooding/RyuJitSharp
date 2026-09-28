// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64LocalStoreLoweringTests
{
    [Test]
    public static void SmallConstantLocalStoreWidensToInt()
    {
        WithLowering((compiler, lowering, block) => {
            compiler.lvaTable[0].Type = TYP_SHORT;
            var value = compiler.gtNewIconNode(TYP_SHORT, 7);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            Append(block, value, store);

            Assert.That(LowerStoreLocCommon(lowering, store), Is.Null);
            Assert.That(store.Type, Is.EqualTo(TYP_INT));
            Assert.That(store.Data, Is.SameAs(value));
            Assert.That(value.IsContained, Is.True);
        });
    }

    [Test]
    public static void FloatingStackStoreRetypesConstantAndPreservesBits()
    {
        WithLowering((compiler, lowering, block) => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            compiler.lvaSetVarDoNotEnregister(0, DoNotEnregisterReason.LocalField);
            var value = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            Append(block, value, store);

            Assert.That(LowerStoreLocCommon(lowering, store), Is.Null);
            var lowered = block.LastNode!.AsLclVarCommon();
            Assert.That(lowered.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(lowered.Type, Is.EqualTo(TYP_LONG));
            Assert.That((long)lowered.Data.AsIntCon().IconValue, Is.EqualTo(long.MinValue));
            Assert.That(store.Next, Is.Null);
        });
    }

    [Test]
    public static void IndependentlyPromotedSingleFieldStoreUsesFieldLocal()
    {
        WithLowering((compiler, lowering, block) => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(4);
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[0].lvFieldCnt = 1;
            compiler.lvaTable[1].Type = TYP_INT;
            var value = compiler.gtNewIconNode(TYP_INT, 0);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            store.Type = TYP_STRUCT;
            Append(block, value, store);

            Assert.That(LowerStoreLocCommon(lowering, store), Is.Null);
            Assert.That(store.LclNum, Is.EqualTo(1));
            Assert.That(store.Type, Is.EqualTo(TYP_INT));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonEnregisterableStructStoreLowersAsBlock(bool copy)
    {
        WithLowering((compiler, lowering, block) => {
            var layout = new ClassLayout(24);
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaSetVarDoNotEnregister(0, DoNotEnregisterReason.LocalField);
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = layout;
            GenTree source = copy
                ? compiler.gtNewLclvNode(TYP_STRUCT, 1)
                : compiler.gtNewIconNode(TYP_INT, 0);
            var store = compiler.gtNewStoreLclVarNode(0, source);
            store.Type = TYP_STRUCT;
            var next = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            Append(block, source, store, next);

            Assert.That(LowerStoreLocCommon(lowering, store), Is.SameAs(next));
            var lowered = next.Prev!.AsBlk();
            Assert.That(lowered.Oper, Is.EqualTo(GT_STORE_BLK));
            Assert.That(lowered.Layout, Is.SameAs(layout));
            Assert.That(lowered.Addr.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(lowered.Data, Is.SameAs(source));
            Assert.That(lowered._kind, Is.EqualTo(GenTreeBlk.BlkOpKindUnroll));
            Assert.That(store.Next, Is.Null);
        });
    }

    private static void Append(BasicBlock block, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerStoreLocCommon")]
    private static extern GenTree? LowerStoreLocCommon(Lowering lowering, GenTreeLclVarCommon store);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action)
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
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 2;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(SysVX64LocalStoreLoweringTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            block.bbRefs = 1;
            block.MakeLir(null, null);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.compCurBB = block;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
