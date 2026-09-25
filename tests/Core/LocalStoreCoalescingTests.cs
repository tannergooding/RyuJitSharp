// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LocalStoreCoalescingTests
{
    [TestCase(0, 1, true, true, false, 0x2211)]
    [TestCase(1, 0, true, true, false, 0x1122)]
    [TestCase(0, 1, false, true, false, 0)]
    [TestCase(0, 1, true, false, false, 0)]
    [TestCase(1, 2, true, true, true, 0)]
    [TestCase(0, 1, true, true, true, 0x2211)]
    public static void AdjacentByteStoresObserveModeConfigAndAlignment(int firstOffset, int secondOffset,
        bool optimized, bool enabled, bool exposed, int expected)
    {
        WithCompiler(optimized, enabled, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_LONG;
            compiler.lvaTable[0].SetAddressExposed(exposed, AddressExposedReason.ESCAPE_ADDRESS);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_BYTE, 0x11);
            var first = new GenTreeLclFld(var_types.TYP_BYTE, 0, (ushort)firstOffset, firstValue, null);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_BYTE, 0x22);
            var second = new GenTreeLclFld(var_types.TYP_BYTE, 0, (ushort)secondOffset, secondValue, null);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstValue, first, secondValue, second, next);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(compiler.opts.OptimizationEnabled, Is.EqualTo(optimized));
            Assert.That(Globals.JitConfig.JitEnableStoreLclFldCoalescing, Is.EqualTo(enabled ? 1 : 0));
            Assert.That(LowerNode(lowering, first), Is.SameAs(secondValue));
            Assert.That(LowerNode(lowering, second), Is.SameAs(next));
            if (expected == 0)
            {
                Assert.That(first.Next, Is.SameAs(secondValue));
                Assert.That(second.Type, Is.EqualTo(var_types.TYP_BYTE));
            }
            else
            {
                Assert.That(block.FirstNode, Is.SameAs(secondValue));
                Assert.That(first.Next, Is.Null);
                Assert.That(second.Type, Is.EqualTo(var_types.TYP_USHORT));
                Assert.That(second.LclOffs, Is.EqualTo(Math.Min(firstOffset, secondOffset)));
                Assert.That(second.Data.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void OverlappingLocalStoreKeepsEarlierUnmodifiedBytes()
    {
        WithCompiler(optimized: true, enabled: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            var previousValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x11223344);
            var previous = compiler.gtNewStoreLclVarNode(0, previousValue);
            var currentValue = compiler.gtNewIconNode(var_types.TYP_BYTE, 0xAA);
            var current = new GenTreeLclFld(var_types.TYP_BYTE, 0, 1, currentValue, null);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(previousValue, previous, currentValue, current, next);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(LowerNode(lowering, previous), Is.SameAs(currentValue));
            Assert.That(LowerNode(lowering, current), Is.SameAs(next));
            Assert.That(block.FirstNode, Is.SameAs(currentValue));
            Assert.That(previous.Next, Is.Null);
            Assert.That(current.Type, Is.EqualTo(var_types.TYP_INT));
            Assert.That(current.LclOffs, Is.Zero);
            Assert.That(current.Data.AsIntCon().IconValue, Is.EqualTo((nint)0x1122AA44));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void FourAdjacentBytesCoalesceAcrossTwoWidths()
    {
        WithCompiler(optimized: true, enabled: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            var block = NewBlock();
            var stores = new GenTreeLclFld[4];
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            for (ushort index = 0; index < stores.Length; index++)
            {
                var value = compiler.gtNewIconNode(var_types.TYP_BYTE, 0x11 * (index + 1));
                stores[index] = new GenTreeLclFld(var_types.TYP_BYTE, 0, index, value, null);
                block.InsertAtEnd(value);
                block.InsertAtEnd(stores[index]);
            }
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            block.InsertAtEnd(next);
            foreach (var store in stores)
            {
                _ = LowerNode(lowering, store);
            }

            Assert.That(block.FirstNode, Is.SameAs(stores[3].Data));
            Assert.That(stores[3].Type, Is.EqualTo(var_types.TYP_INT));
            Assert.That(stores[3].Data.AsIntCon().IconValue, Is.EqualTo((nint)0x44332211));
            Assert.That(stores[3].Next, Is.SameAs(next));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(0, var_types.TYP_BYTE, 0x22)]
    [TestCase(1, var_types.TYP_INT, 0x22334455)]
    public static void CompleteOverwriteRemovesEarlierStore(int offset, var_types type, int value)
    {
        WithCompiler(optimized: true, enabled: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            var firstValue = compiler.gtNewIconNode(var_types.TYP_BYTE, 0x11);
            var first = new GenTreeLclFld(var_types.TYP_BYTE, 0, (ushort)offset, firstValue, null);
            var secondValue = compiler.gtNewIconNode(type, value);
            var second = new GenTreeLclFld(type, 0, 0, secondValue, null);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstValue, first, secondValue, second, next);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            _ = LowerNode(lowering, first);
            Assert.That(LowerNode(lowering, second), Is.SameAs(next));
            Assert.That(block.FirstNode, Is.SameAs(secondValue));
            Assert.That(second.Type, Is.EqualTo(type));
            Assert.That(second.Data.AsIntCon().IconValue, Is.EqualTo((nint)value));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void AdjacentLongConstantsBecomeOneVectorStore()
    {
        WithCompiler(optimized: true, enabled: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_LONG, unchecked((nint)0x1122334455667788L));
            var first = new GenTreeLclFld(var_types.TYP_LONG, 0, 0, firstValue, null);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_LONG, unchecked((nint)0x0102030405060708L));
            var second = new GenTreeLclFld(var_types.TYP_LONG, 0, 8, secondValue, null);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstValue, first, secondValue, second, next);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            _ = LowerNode(lowering, first);
            Assert.That(LowerNode(lowering, second), Is.SameAs(next));
            Assert.That(block.FirstNode, Is.SameAs(second.Data));
            Assert.That(second.Type, Is.EqualTo(var_types.TYP_SIMD16));
            Assert.That(second.LclOffs, Is.Zero);
            Assert.That(second.Data.AsVecCon().SimdVal.u64[0], Is.EqualTo(0x1122334455667788UL));
            Assert.That(second.Data.AsVecCon().SimdVal.u64[1], Is.EqualTo(0x0102030405060708UL));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void MatchingVectorsReuseEarlierValueWhenWiderVectorIsUnavailable()
    {
        WithCompiler(optimized: true, enabled: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(32);
            var firstValue = compiler.gtNewVconNode(var_types.TYP_SIMD16);
            firstValue.SimdVal.u64[0] = 0x1122334455667788;
            var first = new GenTreeLclFld(var_types.TYP_SIMD16, 0, 0, firstValue, null);
            var secondValue = compiler.gtNewVconNode(var_types.TYP_SIMD16);
            secondValue.SimdVal = firstValue.SimdVal;
            var second = new GenTreeLclFld(var_types.TYP_SIMD16, 0, 16, secondValue, null);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstValue, first, secondValue, second, next);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            _ = LowerNode(lowering, first);
            Assert.That(LowerNode(lowering, second), Is.SameAs(next));
            Assert.That(second.Type, Is.EqualTo(var_types.TYP_SIMD16));
            Assert.That(second.Data.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(first.Data.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(second.Data.AsLclVar().LclNum, Is.EqualTo(first.Data.AsLclVar().LclNum));
            Assert.That(secondValue.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    private static void WithCompiler(bool optimized, bool enabled, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(!optimized);
        compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 1;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        object config = previousConfig;
        var field = typeof(JitConfigValues).GetField("_jitEnableStoreLclFldCoalescing",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException();
        field.SetValue(config, enabled ? 1 : 0);
        Globals.JitConfig = (JitConfigValues)config;
        try
        {
            action(compiler);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }
}
