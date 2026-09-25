// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LocalHeapLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroHeapReplacesOwnedUseWithNull(bool initMem)
    {
        WithCompiler(initMem, compiler => {
            var size = compiler.gtNewIconNode(TYP_INT, 0);
            var heap = NewHeap(size);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, heap);
            var block = NewBlock(size, heap, user);
            var originalFlags = heap.Flags;
            var next = LowerHeap(NewLowering(compiler, block), heap);
            var zero = user.Op1.AsIntCon();

            Assert.That(zero.IconValue, Is.EqualTo((nint)0));
            Assert.That(zero.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(zero.Flags & GTF_NODE_MASK, Is.EqualTo(originalFlags & GTF_NODE_MASK));
            Assert.That(zero._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(next, Is.SameAs(user));
            Assert.That(block.FirstNode, Is.SameAs(zero));
            Assert.That(size.Next, Is.Null);
            Assert.That(heap.Next, Is.Null);
        });
    }

    [TestCase(1L, 16L)]
    [TestCase(16L, 16L)]
    [TestCase(17L, 32L)]
    [TestCase(31L, 32L)]
    public static void InitMemAlignsAndZeroesTheAllocatedBlock(long sizeValue, long aligned)
    {
        WithCompiler(true, compiler => {
            var size = compiler.gtNewIconNode(TYP_I_IMPL, (nint)sizeValue);
            var heap = NewHeap(size);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, heap);
            var block = NewBlock(size, heap, user);

            var next = LowerHeap(NewLowering(compiler, block), heap);
            Assert.That(size.IconValue, Is.EqualTo((nint)aligned));
            Assert.That(size.IsContained, Is.True);
            Assert.That(user.Op1.Oper, Is.EqualTo(GT_LCL_VAR));

            var tempRead = user.Op1.AsLclVar();
            var store = tempRead.Next!.Next!.Next!.AsBlk();
            var heapRead = store.Addr.AsLclVar();
            Assert.That(store.Oper, Is.EqualTo(GT_STORE_BLK));
            Assert.That(store.Size, Is.EqualTo((int)aligned));
            Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
            Assert.That(store.Flags & (GTF_IND_UNALIGNED | GTF_ASG | GTF_EXCEPT | GTF_GLOB_REF),
                Is.EqualTo(GTF_IND_UNALIGNED | GTF_ASG | GTF_EXCEPT | GTF_GLOB_REF));
            Assert.That(heapRead.LclNum, Is.EqualTo(tempRead.LclNum));
            Assert.That(next!.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(next.Next!.Next, Is.SameAs(heapRead));
            Assert.That(tempRead.Next, Is.SameAs(heapRead));
            Assert.That(store.Next, Is.SameAs(user));
        });
    }

    [Test]
    public static void InitMemWithUnusedHeapLeavesSizeUncontained()
    {
        WithCompiler(true, compiler => {
            var size = compiler.gtNewIconNode(TYP_INT, 17);
            var heap = NewHeap(size);
            var block = NewBlock(size, heap);

            Assert.That(LowerHeap(NewLowering(compiler, block), heap), Is.Null);
            Assert.That(size.IconValue, Is.EqualTo((nint)17));
            Assert.That(size.IsContained, Is.False);
            Assert.That(size.Next, Is.SameAs(heap));
        });
    }

    [TestCase(0xFFFF_FFF1L)]
    [TestCase(0xFFFF_FFFFL)]
    [TestCase(0x1_0000_0000L)]
    public static void OutOfRangeInitMemSizeDoesNotContainConstant(long sizeValue)
    {
        WithCompiler(true, compiler => {
            var size = compiler.gtNewIconNode(TYP_I_IMPL, (nint)sizeValue);
            var heap = NewHeap(size);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, heap);
            var block = NewBlock(size, heap, user);

            Assert.That(LowerHeap(NewLowering(compiler, block), heap), Is.SameAs(user));
            Assert.That(size.IconValue, Is.EqualTo((nint)sizeValue));
            Assert.That(size.IsContained, Is.False);
            Assert.That(user.Op1, Is.SameAs(heap));
        });
    }

    [TestCase(0x7fff_fff0L, 0x7fff_fff0u)]
    [TestCase(0x8000_0000L, 0x8000_0000u)]
    [TestCase(0xffff_fff0L, 0xffff_fff0u)]
    [TestCase(-16L, 0xffff_fff0u)]
    public static void InitMemPreservesFullUnsignedBlockSize(long input, uint expectedSize)
    {
        WithCompiler(true, compiler => {
            var size = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)input));
            var heap = NewHeap(size);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, heap);
            var block = NewBlock(size, heap, user);
            var next = LowerHeap(NewLowering(compiler, block), heap);
            var store = user.Prev!.AsBlk();

            Assert.That(next?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            var alignedSignedSize = unchecked((long)(((ulong)input + 15) & ~15UL));
            Assert.That(size.IconValue, Is.EqualTo((nint)alignedSignedSize));
            Assert.That(size.IsContained, Is.True);
            Assert.That(store.Layout.Size, Is.EqualTo(expectedSize));
            Assert.That(store.Size, Is.EqualTo(expectedSize));
            Assert.That(store.ValueSize.ExactSize, Is.EqualTo(expectedSize));
            Assert.That(store.Layout.HasGCPtr, Is.False);
            Assert.That(store.Layout._gcPtrs, Is.Null);
            Assert.That(store.Data.IsIntegralConst(0), Is.True);
            Assert.That(user.Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            var helperSize = NewBlockHelperSize(null, compiler, store);
            Assert.That(helperSize.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(helperSize.IconValue, Is.EqualTo((nint)expectedSize));
        });
    }

    [Test]
    public static void NegativeSizeAligningToZeroRetainsNativeZeroLayoutAssertion()
    {
        WithCompiler(true, compiler => {
            var size = compiler.gtNewIconNode(TYP_I_IMPL, -1);
            var heap = NewHeap(size);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, heap);
            var block = NewBlock(size, heap, user);
#if DEBUG
            Assert.That(() => _ = LowerHeap(NewLowering(compiler, block), heap), Throws.Exception);
#else
            _ = LowerHeap(NewLowering(compiler, block), heap);
            Assert.That(user.Prev!.AsBlk().Size, Is.EqualTo(0u));
#endif
            Assert.That(size.IconValue, Is.EqualTo((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VariableSizeRemainsRegisterInput(bool initMem)
    {
        WithCompiler(initMem, compiler => {
            var size = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var heap = NewHeap(size);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, heap);
            var block = NewBlock(size, heap, user);
            Assert.That(LowerHeap(NewLowering(compiler, block), heap), Is.SameAs(user));
            Assert.That(size.IsContained, Is.False);
            Assert.That(user.Op1, Is.SameAs(heap));
        });
    }

    [Test]
    public static void WithoutInitMemConstantCanBeContained()
    {
        WithCompiler(false, compiler => {
            var size = compiler.gtNewIconNode(TYP_INT, 17);
            var heap = NewHeap(size);
            var block = NewBlock(size, heap);

            Assert.That(LowerHeap(NewLowering(compiler, block), heap), Is.Null);
            Assert.That(size.IconValue, Is.EqualTo((nint)17));
            Assert.That(size.IsContained, Is.True);
        });
    }

    [Test]
    public static void NonLocalJumpMarksSafeLocalOptional()
    {
        WithCompiler(false, compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var jump = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, address);
            var block = NewBlock(address, jump);
            ContainJump(NewLowering(compiler, block), jump);
            Assert.That(address.IsContained || address.IsRegOptional, Is.True);
        });
    }

    [Test]
    public static void NonLocalJumpContainsSafeMemorySource()
    {
        WithCompiler(false, compiler => {
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var jump = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, address);
            var block = NewBlock(address, jump);
            ContainJump(NewLowering(compiler, block), jump);
            Assert.That(address.IsContained, Is.True);
            Assert.That(address.IsRegOptional, Is.False);
        });
    }

    private static GenTreeUnOp NewHeap(GenTree size) => new(GT_LCLHEAP, TYP_I_IMPL, size);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "NewBlockHelperSize")]
    private static extern GenTreeIntCon NewBlockHelperSize(Lowering? _, Compiler compiler, GenTreeBlk block);

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerHeap(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckNonLocalJmp")]
    private static extern void ContainJump(Lowering lowering, GenTreeUnOp node);

    private static void WithCompiler(bool initMem, Action<Compiler> action)
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
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        compiler.info.compInitMem = initMem;
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
