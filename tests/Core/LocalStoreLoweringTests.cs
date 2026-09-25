// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LocalStoreLoweringTests
{
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
