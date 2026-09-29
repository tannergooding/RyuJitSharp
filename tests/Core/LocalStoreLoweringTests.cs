// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LocalStoreLoweringTests
{
    [Test]
    public static void StoreConstructionPreservesNormalizationType(
        [Values(var_types.TYP_BYTE, var_types.TYP_UBYTE,
            var_types.TYP_SHORT, var_types.TYP_USHORT, var_types.TYP_INT)] var_types type,
        [Values] bool parameter)
    {
        WithCompiler(compiler =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.lvIsParam = parameter;
            var value = compiler.gtNewIconNode(var_types.TYP_INT, 257);

            var store = compiler.gtNewStoreLclVarNode(0, value);

            Assert.That(store.Type, Is.EqualTo(parameter ? type : var_types.TYP_INT));
            Assert.That(store.Data, Is.SameAs(value));
            Assert.That(store.LclNum, Is.Zero);
            Assert.That(store.Flags & (GenTreeFlags.GTF_VAR_DEF | GenTreeFlags.GTF_ASG),
                Is.EqualTo(GenTreeFlags.GTF_VAR_DEF | GenTreeFlags.GTF_ASG));
        });
    }

    [Test]
    public static void StoreCloningPreservesOriginalType(
        [Values(var_types.TYP_BYTE, var_types.TYP_UBYTE,
            var_types.TYP_SHORT, var_types.TYP_USHORT, var_types.TYP_INT)] var_types type,
        [Values] bool parameter, [Values] bool normalized)
    {
        WithCompiler(compiler =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.lvIsParam = parameter;
            var value = compiler.gtNewIconNode(var_types.TYP_INT, 257);
            var store = new GenTreeLclVar(normalized ? var_types.TYP_INT : type, 0, value);

            var copy = compiler.gtCloneExpr(store).AsLclVar();

            Assert.That(copy, Is.Not.SameAs(store));
            Assert.That(copy.Type, Is.EqualTo(store.Type));
            Assert.That(copy.LclNum, Is.EqualTo(store.LclNum));
            Assert.That(copy.Flags, Is.EqualTo(store.Flags));
            Assert.That(copy.Data, Is.Not.SameAs(value));
            Assert.That(copy.Data.AsIntCon().IconValue, Is.EqualTo(value.IconValue));
        });
    }

    [TestCase(var_types.TYP_DOUBLE, long.MinValue, false, false)]
    [TestCase(var_types.TYP_DOUBLE, 0x7FF8000000001234L, false, false)]
    [TestCase(var_types.TYP_FLOAT, long.MinValue, false, false)]
    [TestCase(var_types.TYP_DOUBLE, long.MinValue, false, true)]
    [TestCase(var_types.TYP_DOUBLE, long.MinValue, true, false)]
    public static void FloatingStoresPreserveBitsAndLocalIdentity(
        var_types type, long bits, bool enregister, bool fieldStore)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].lvDoNotEnregister = !enregister;
            var value = new GenTreeDblCon(type, BitConverter.Int64BitsToDouble(bits));
            value._vnPair.SetBoth(123);
            GenTreeLclVarCommon original = fieldStore
                ? new GenTreeLclFld(type, 0, 0, value, null)
                : compiler.gtNewStoreLclVarNode(0, value);
            original.SsaNum = 17;
            original._vnPair.SetBoth(456);
            var flags = original.Flags;
            var successor = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(value);
            block.InsertAtEnd(original);
            block.InsertAtEnd(successor);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(LowerNode(lowering, original), Is.SameAs(successor));

            var store = successor.Prev ?? throw new InvalidOperationException();
            var local = store.AsLclVarCommon();
            Assert.That(local.LclNum, Is.Zero);
            Assert.That(local.SsaNum, Is.EqualTo(17));
            Assert.That(local._vnPair.Conservative, Is.EqualTo(enregister || fieldStore ? 456 : ValueNumStore.NoVN));
            Assert.That(local.Flags, Is.EqualTo(flags));
            Assert.That(local.Op1, Is.SameAs(block.FirstNode));
            Assert.That(local.Op1._vnPair.Conservative, Is.EqualTo(enregister ? 123 : ValueNumStore.NoVN));
            if (enregister)
            {
                Assert.That(store, Is.SameAs(original));
                Assert.That(local.Op1, Is.SameAs(value));
                Assert.That(store.Type, Is.EqualTo(type));
            }
            else
            {
                var expectedBits = type is var_types.TYP_FLOAT
                    ? BitConverter.SingleToInt32Bits((float)value.DconVal)
                    : bits;
                Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_FLD));
                Assert.That(store.Type, Is.EqualTo(type is var_types.TYP_FLOAT ? var_types.TYP_INT : var_types.TYP_LONG));
                Assert.That(local.Op1.AsIntCon().IconValue, Is.EqualTo((nint)expectedBits));
                Assert.That(value.Prev, Is.Null);
                Assert.That(value.Next, Is.Null);
                if (!fieldStore)
                {
                    Assert.That(original.Prev, Is.Null);
                    Assert.That(original.Next, Is.Null);
                }
#if DEBUG
                Assert.That(local.Op1.TreeId, Is.EqualTo(value.TreeId));
                Assert.That(store.TreeId, Is.EqualTo(original.TreeId));
#endif
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void FieldListSpillingLowersInsertedPrimitiveStores()
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var first = compiler.gtNewIconNode(var_types.TYP_INT, 3);
            var second = compiler.gtNewIconNode(var_types.TYP_INT, 5);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, var_types.TYP_INT);
            fields.AddFieldLIR(compiler, second, 4, var_types.TYP_INT);
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            block.InsertAtEnd(fields);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            var local = StoreFieldListToNewLocal(lowering, new ClassLayout(8), fields);

            Assert.That(compiler.lvaTable[local].lvDoNotEnregister, Is.True);
            var firstStore = first.Next ?? throw new InvalidOperationException();
            var secondStore = second.Next ?? throw new InvalidOperationException();
            Assert.That(firstStore.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_FLD));
            Assert.That(secondStore.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_FLD));
            Assert.That(firstStore.AsLclFld().LclNum, Is.EqualTo(local));
            Assert.That(secondStore.AsLclFld().LclNum, Is.EqualTo(local));
            Assert.That(firstStore.AsLclFld().LclOffs, Is.Zero);
            Assert.That(secondStore.AsLclFld().LclOffs, Is.EqualTo(4));
            Assert.That(first.IsContained && second.IsContained, Is.True);
            Assert.That(firstStore.Next, Is.SameAs(second));
            Assert.That(secondStore.Next, Is.SameAs(fields));
        });
    }

#if TARGET_XARCH
    [Test]
    public static void ScalarStructBlockLoadReplacesTheLocalStoreOperand()
    {
        WithCompiler(compiler => {
            var layout = new ClassLayout(4);
            compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[1].Type = var_types.TYP_BYREF;
            compiler.lvaCount = 2;

            var address = compiler.gtNewLclvNode(var_types.TYP_BYREF, 1);
            var original = new GenTreeBlk(var_types.TYP_STRUCT, address, layout);
            var store = compiler.gtNewStoreLclVarNode(0, original);
            store.Type = var_types.TYP_STRUCT;
            var successor = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(address);
            block.InsertAtEnd(original);
            block.InsertAtEnd(store);
            block.InsertAtEnd(successor);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(layout.RegisterType, Is.EqualTo(var_types.TYP_INT));
            Assert.That(LowerNode(lowering, store), Is.SameAs(successor));

            var replacement = store.Op1.AsIndir();
            Assert.That(store.Op1, Is.Not.SameAs(original));
            Assert.That(store.Op1.Oper, Is.EqualTo(genTreeOps.GT_IND));
            Assert.That(store.Op1.Type, Is.EqualTo(var_types.TYP_INT));
            Assert.That(replacement.Addr, Is.SameAs(address));
            Assert.That(address.Next, Is.SameAs(replacement));
            Assert.That(replacement.Next, Is.SameAs(store));
            Assert.That(block.TryGetUse(replacement, out var use), Is.True);
            Assert.That(use.User(), Is.SameAs(store));
            Assert.That(original.Prev, Is.Null);
            Assert.That(original.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "StoreFieldListToNewLocal")]
    private static extern int StoreFieldListToNewLocal(Lowering lowering, ClassLayout layout, GenTreeFieldList fields);

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
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = var_types.TYP_INT;
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
