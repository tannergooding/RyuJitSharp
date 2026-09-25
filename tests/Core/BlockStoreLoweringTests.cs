// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockStoreLoweringTests
{
    [TestCase(1, 0, false, TYP_UBYTE, 0L)]
    [TestCase(2, 0x112, true, TYP_USHORT, 0x1212L)]
    [TestCase(8, 0x1FF, true, TYP_LONG, -1L)]
    public static void OptimizedInitReplacesTheStoreWithoutLosingItsSuccessor(
        int size, int value, bool wrapped, var_types expectedType, long expectedValue)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var fill = compiler.gtNewIconNode(TYP_INT, value);
            GenTree data = wrapped ? new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill) : fill;
            var original = new GenTreeBlk(TYP_STRUCT, address, data, new ClassLayout(size));
            original.Flags |= GTF_IND_NONFAULTING | GTF_IND_UNALIGNED;
            original._vnPair.SetBoth(123);
            var successor = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            var block = NewBlock(address, fill);
            if (wrapped)
            {
                block.InsertAtEnd(data);
            }
            block.InsertAtEnd(original);
            block.InsertAtEnd(successor);
            var lowering = NewLowering(compiler, block);

            Assert.That(LowerNode(lowering, original), Is.SameAs(successor));

            var store = successor.Prev!.AsStoreInd();
            Assert.That(store.Type, Is.EqualTo(expectedType));
            Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)expectedValue));
            Assert.That(store.Flags & GTF_IND_NONFAULTING, Is.EqualTo(GTF_IND_NONFAULTING));
            Assert.That(store.Flags & GTF_IND_UNALIGNED, Is.EqualTo(GTF_EMPTY));
            Assert.That(store._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(original.Next, Is.Null);
            Assert.That(original.Prev, Is.Null);
            Assert.That(fill.Next, Is.Null);
            Assert.That(fill.Prev, Is.Null);
#if DEBUG
            Assert.That(store.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts: false);
    }

    [Test]
    public static void OptimizedSmallCopyReplacesBothBlockNodesAndSuppressesExtension()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var sourceAddress = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var layout = new ClassLayout(2);
            var source = new GenTreeBlk(TYP_STRUCT, sourceAddress, layout);
            source._vnPair.SetBoth(123);
            var original = new GenTreeBlk(TYP_STRUCT, address, source, layout);
            var successor = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            var block = NewBlock(address, sourceAddress, source, original, successor);

            Assert.That(LowerNode(NewLowering(compiler, block), original), Is.SameAs(successor));

            var store = successor.Prev!.AsStoreInd();
            var load = store.Data.AsIndir();
            Assert.That(store.Type, Is.EqualTo(TYP_USHORT));
            Assert.That(load.Type, Is.EqualTo(TYP_USHORT));
            Assert.That(load.Oper, Is.EqualTo(GT_IND));
            Assert.That(load.Flags & GTF_DONT_EXTEND, Is.EqualTo(GTF_DONT_EXTEND));
            Assert.That(load._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(load.Addr, Is.SameAs(sourceAddress));
            Assert.That(source.Next, Is.Null);
            Assert.That(original.Next, Is.Null);
#if DEBUG
            Assert.That(load.TreeId, Is.EqualTo(source.TreeId));
            Assert.That(store.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts: false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SingleRegisterCallStoresBecomeScalarAndRetainIndirectionFlags(bool minOpts)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var call = new GenTreeCall(TYP_LONG);
            var original = new GenTreeBlk(TYP_STRUCT, address, call, new ClassLayout(8));
            original.Flags |= GTF_IND_VOLATILE | GTF_IND_UNALIGNED | GTF_IND_NONFAULTING;
            original._vnPair.SetBoth(123);
            var originalFlags = original.Flags;
            var successor = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            var block = NewBlock(address, call, original, successor);

            Assert.That(LowerNode(NewLowering(compiler, block), original), Is.SameAs(successor));

            var store = successor.Prev!.AsStoreInd();
            Assert.That(store.Type, Is.EqualTo(TYP_LONG));
            Assert.That(store.Data, Is.SameAs(call));
            Assert.That(store.Flags, Is.EqualTo(originalFlags));
            Assert.That(store._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(original.Next, Is.Null);
#if DEBUG
            Assert.That(store.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts);
    }

    [Test]
    public static void OptimizedBlockStoresRemoveOverwrittenBlocks()
    {
        WithCompiler(compiler => {
            var firstAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var firstZero = compiler.gtNewIconNode(TYP_INT, 0);
            var first = new GenTreeBlk(TYP_STRUCT, firstAddress, firstZero, new ClassLayout(24));
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var store = new GenTreeBlk(TYP_STRUCT, address, zero, new ClassLayout(24));
            var block = NewBlock(firstAddress, firstZero, first, address, zero, store);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, store);

            Assert.That(block.FirstNode, Is.SameAs(address));
            Assert.That(block.LastNode, Is.SameAs(store));
            Assert.That(first.Next, Is.Null);
            Assert.That(store.Oper, Is.EqualTo(GT_STORE_BLK));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts: false);
    }

    [TestCase(var_types.TYP_BYTE, 0, true)]
    [TestCase(var_types.TYP_USHORT, 0, true)]
    [TestCase(var_types.TYP_INT, 0, false)]
    [TestCase(var_types.TYP_LONG, 0, false)]
    [TestCase(var_types.TYP_INT, 17, true)]
    [TestCase(var_types.TYP_REF, 17, false)]
    public static void ScalarStoreContainmentPreservesZeroAndWriteBarrierRules(var_types type, int value, bool contained)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var valueType = type is var_types.TYP_LONG or var_types.TYP_REF ? type : var_types.TYP_INT;
            var source = compiler.gtNewIconNode(valueType, value);
            var store = new GenTreeStoreInd(type, address, source);
            var block = NewBlock(address, source, store);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, store);

            Assert.That(store.Data, Is.SameAs(source));
            Assert.That(source.IsContained, Is.EqualTo(contained));
            Assert.That(store.Type, Is.EqualTo(type));
        });
    }

    [TestCase(var_types.TYP_FLOAT, -0.0, var_types.TYP_INT, -2147483648L)]
    [TestCase(var_types.TYP_DOUBLE, -0.0, var_types.TYP_LONG, long.MinValue)]
    public static void ScalarFloatingStoreRetypingPreservesSignedZero(
        var_types type, double value, var_types expectedType, long expectedBits)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var source = compiler.gtNewDconNode(type, value);
            source._vnPair.SetBoth(123);
            var store = new GenTreeStoreInd(type, address, source);
            var block = NewBlock(address, source, store);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, store);

            Assert.That(store.Type, Is.EqualTo(expectedType));
            Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)expectedBits));
            Assert.That(store.Data._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.Prev, Is.SameAs(store.Data));
            Assert.That(address.Next, Is.SameAs(store.Data));
        });
    }

    [TestCase(var_types.TYP_BYTE, var_types.TYP_UBYTE)]
    [TestCase(var_types.TYP_SHORT, var_types.TYP_USHORT)]
    public static void TierZeroSmallScalarStoresContainVectorExtraction(var_types storeType, var_types baseType)
    {
        WithCompiler(compiler => {
            Assert.That(compiler.opts.Tier0OptimizationEnabled, Is.True);
            compiler.lvaTable[1].Type = var_types.TYP_SIMD16;
            var address = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var vector = compiler.gtNewLclvNode(var_types.TYP_SIMD16, 1);
            var scalar = compiler.gtNewSimdHWIntrinsicNode(var_types.TYP_INT,
                NamedIntrinsic.NI_Vector_ToScalar, baseType, 16, vector);
            var store = new GenTreeStoreInd(storeType, address, scalar);
            var block = NewBlock(address, vector, scalar, store);
            vector.IsRegOptional = true;
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, store);

            Assert.That(scalar.HWIntrinsicId, Is.EqualTo(NamedIntrinsic.NI_X86Base_Extract));
            Assert.That(scalar.IsContained, Is.True);
            Assert.That(scalar.GetOp(1), Is.SameAs(vector));
            Assert.That(scalar.GetOp(2).IsIntegralConst(0), Is.True);
            Assert.That(scalar.GetOp(2).IsContained, Is.True);
            Assert.That(scalar.Prev, Is.SameAs(scalar.GetOp(2)));
            Assert.That(vector.IsRegOptional, Is.False);
        });
    }

    [TestCase(4, 0x112, 0x12121212L, false, var_types.TYP_INT)]
    [TestCase(8, 0x112, 0x1212121212121212L, false, var_types.TYP_LONG)]
    [TestCase(8, 0x1FF, -1L, false, var_types.TYP_LONG)]
    [TestCase(16, 0x112, 0x12L, true, var_types.TYP_INT)]
    [TestCase(8, 0x100, 0L, false, var_types.TYP_INT)]
    public static void InitBlocksUseNativeFillWidths(
        int size, int value, long expected, bool contained, var_types type)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var fill = compiler.gtNewIconNode(var_types.TYP_INT, value);
            var init = new GenTreeUnOp(genTreeOps.GT_INIT_VAL, var_types.TYP_INT, fill);
            var store = new GenTreeBlk(var_types.TYP_STRUCT, address, init, new ClassLayout(size));
            var block = NewBlock(address, fill, init, store);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, store);

            Assert.That(store._kind, Is.EqualTo(GenTreeBlk.BlkOpKindUnroll));
            Assert.That(init.IsContained, Is.True);
            Assert.That(fill.IconValue, Is.EqualTo((nint)expected));
            Assert.That(fill.Type, Is.EqualTo(type));
            Assert.That(fill.IsContained, Is.EqualTo(contained));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LargeGcZeroingUsesAtomicLoopForHeapAndStack(bool stackDestination)
    {
        WithCompiler(compiler => {
            var layoutBuilder = new ClassLayoutBuilder(compiler, 1024);
            layoutBuilder.SetGCPtrType(0, var_types.TYP_REF);
            var layout = ClassLayout.Create(compiler, layoutBuilder);
            var address = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var zero = compiler.gtNewIconNode(var_types.TYP_INT, 0);
            var store = new GenTreeBlk(var_types.TYP_STRUCT, address, zero, layout);
            if (stackDestination)
            {
                store.Flags |= GenTreeFlags.GTF_IND_TGT_NOT_HEAP;
            }
            var block = NewBlock(address, zero, store);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, store);

            Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_STORE_BLK));
            Assert.That(store._kind, Is.EqualTo(GenTreeBlk.BlkOpKindLoop));
            Assert.That(store.Data, Is.SameAs(zero));
            Assert.That(zero.IsContained, Is.False);
        });
    }

    [Test]
    public static void SmallGcStackCopyIsNonInterruptibleAndReplacesBlockLoad()
    {
        WithCompiler(compiler => {
            var builder = new ClassLayoutBuilder(compiler, 24);
            builder.SetGCPtrType(0, var_types.TYP_REF);
            var layout = ClassLayout.Create(compiler, builder);
            var destination = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var sourceAddress = compiler.gtNewLclvNode(var_types.TYP_BYREF, 1);
            var source = new GenTreeBlk(var_types.TYP_STRUCT, sourceAddress, layout);
            source._vnPair.SetBoth(123);
            var store = new GenTreeBlk(var_types.TYP_STRUCT, destination, source, layout) {
                Flags = GenTreeFlags.GTF_ASG | GenTreeFlags.GTF_IND_TGT_NOT_HEAP,
            };
            var block = NewBlock(destination, sourceAddress, source, store);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, store);

            Assert.That(store._kind, Is.EqualTo(GenTreeBlk.BlkOpKindUnroll));
            Assert.That(store._gcUnsafe, Is.True);
            Assert.That(store.Data.Oper, Is.EqualTo(genTreeOps.GT_IND));
            Assert.That(store.Data.IsContained, Is.True);
            Assert.That(store.Data._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.Data.AsIndir().Addr, Is.SameAs(sourceAddress));
            Assert.That(store.Prev, Is.SameAs(store.Data));
            Assert.That(source.Next, Is.Null);
            Assert.That(source.Prev, Is.Null);
#if DEBUG
            Assert.That(store.Data.TreeId, Is.EqualTo(source.TreeId));
#endif
        });
    }

    [Test]
    public static void StructLocalStoreReplacesOwnerAndLowersBlockInitialization()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(24);
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var zero = compiler.gtNewIconNode(var_types.TYP_INT, 0);
            var original = compiler.gtNewStoreLclVarNode(0, zero);
            original.Type = var_types.TYP_STRUCT;
            var successor = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(zero, original, successor);
            var lowering = NewLowering(compiler, block);

            Assert.That(LowerNode(lowering, original), Is.SameAs(successor));

            var store = successor.Prev?.AsBlk() ?? throw new InvalidOperationException();
            Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_STORE_BLK));
            Assert.That(store._kind, Is.EqualTo(GenTreeBlk.BlkOpKindUnroll));
            Assert.That(store.Data, Is.SameAs(zero));
            Assert.That(store.Addr.Oper, Is.EqualTo(genTreeOps.GT_LCL_ADDR));
            Assert.That(store.Addr.IsContained, Is.True);
            Assert.That(store.Prev, Is.SameAs(store.Addr));
            Assert.That(original.Next, Is.Null);
            Assert.That(original.Prev, Is.Null);
#if DEBUG
            Assert.That(store.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    private static BasicBlock NewBlock(params ReadOnlySpan<GenTree> nodes)
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    private static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = var_types.TYP_BYREF;
        compiler.lvaTable[1].Type = var_types.TYP_BYREF;
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
