// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class MemoryCallLoweringTests
{
    [TestCase(0, false)]
    [TestCase(-1, false)]
    [TestCase(16, true)]
    [TestCase(4096, false)]
    public static void MemmoveUnrollsOnlyPositiveSizesWithinTheNativeThreshold(int size, bool unrolled)
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, destination, source, length) = NewMemoryCall(compiler,
                CorInfoHelpFunc.CORINFO_HELP_MEMCPY, size);
            var cell = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(cell)
                .WithWellKnownArg(WellKnownArg.VirtualStubCell));
            foreach (var node in new GenTree[] { destination, source, length, cell, call })
            {
                block.InsertAtEnd(node);
            }

            var result = LowerCallMemmove(lowering, call, out var next);

            Assert.That(result, Is.EqualTo(unrolled));
            if (!unrolled)
            {
                Assert.That(next, Is.Null);
                Assert.That(call.Prev, Is.SameAs(cell));
                return;
            }
            var sourceBlock = cell.Next?.AsIndir() ?? throw new InvalidOperationException();
            var store = sourceBlock.Next?.AsBlk() ?? throw new InvalidOperationException();
            Assert.That(store._kind, Is.EqualTo(GenTreeBlk.BlkOpKind.BlkOpKindUnrollMemmove));
            Assert.That(store.Layout.Size, Is.EqualTo(size));
            Assert.That(store.Addr, Is.SameAs(destination));
            Assert.That(store.Data, Is.SameAs(sourceBlock));
            Assert.That(sourceBlock.Addr, Is.SameAs(source));
            Assert.That(sourceBlock.IsContained, Is.True);
            Assert.That(store.Flags & (GTF_IND_UNALIGNED | GTF_ASG | GTF_EXCEPT | GTF_GLOB_REF),
                Is.EqualTo(GTF_IND_UNALIGNED | GTF_ASG | GTF_EXCEPT | GTF_GLOB_REF));
            Assert.That(store.Flags & GTF_IND_VOLATILE, Is.EqualTo(GTF_EMPTY));
            Assert.That(next, Is.SameAs(store.Next));
            Assert.That(length.Next, Is.Null);
            Assert.That(cell.IsUnusedValue, Is.True);
            Assert.That(call.Next, Is.Null);
        });
    }

    [TestCase(0, false)]
    [TestCase(-1, false)]
    [TestCase(8, true)]
    [TestCase(4096, false)]
    public static void MemsetPreservesZeroLengthAndThresholdPolicy(int size, bool unrolled)
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, destination, value, length) = NewMemoryCall(compiler,
                CorInfoHelpFunc.CORINFO_HELP_MEMSET, size, 0);
            foreach (var node in new GenTree[] { destination, value, length, call })
            {
                block.InsertAtEnd(node);
            }

            var result = LowerCallMemset(lowering, call, out var store);

            Assert.That(result, Is.EqualTo(unrolled));
            if (!unrolled)
            {
                Assert.That(store, Is.Null);
                Assert.That(length.Next, Is.SameAs(call));
                return;
            }
            var unrolledStore = store ?? throw new InvalidOperationException();
            Assert.That(unrolledStore._kind, Is.EqualTo(GenTreeBlk.BlkOpKind.BlkOpKindUnroll));
            Assert.That(unrolledStore.Layout.Size, Is.EqualTo(size));
            Assert.That(unrolledStore.Data, Is.SameAs(value));
            Assert.That(unrolledStore.Addr, Is.SameAs(destination));
            Assert.That(unrolledStore.Flags & GTF_IND_UNALIGNED, Is.EqualTo(GTF_IND_UNALIGNED));
            Assert.That(unrolledStore.Flags & GTF_IND_VOLATILE, Is.EqualTo(GTF_EMPTY));
            Assert.That(value.IsUnusedValue, Is.False);
            Assert.That(destination.IsUnusedValue, Is.False);
            Assert.That(length.IsUnusedValue, Is.True);
            Assert.That(call.Next, Is.Null);
        });
    }

    [Test]
    public static void NonzeroByteMemsetWrapsValueInInitVal()
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, destination, value, length) = NewMemoryCall(compiler,
                CorInfoHelpFunc.CORINFO_HELP_MEMSET, 8, 0xAB);
            foreach (var node in new GenTree[] { destination, value, length, call })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemset(lowering, call, out var store), Is.True);
            Assert.That(store?.Data.Oper, Is.EqualTo(genTreeOps.GT_INIT_VAL));
            Assert.That(store?.Data.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(store?.Data));
            Assert.That(value.IsUnusedValue, Is.False);
        });
    }

    [Test]
    public static void NonconstantLengthAndReturnAddressRequirementRetainTheCall()
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, destination, source, _) = NewMemoryCall(compiler,
                CorInfoHelpFunc.CORINFO_HELP_MEMCPY, 8);
            var length = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var lengthArg = call.Args.GetUserArgByIndex(2) ?? throw new InvalidOperationException();
            lengthArg.EarlyNode = length;
            block.InsertAtEnd(destination);
            block.InsertAtEnd(source);
            block.InsertAtEnd(length);
            block.InsertAtEnd(call);
            Assert.That(LowerCallMemmove(lowering, call, out var next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(length.Next, Is.SameAs(call));

            compiler.info.compHasNextCallRetAddr = true;
            Assert.That(LowerCallMemmove(lowering, call, out next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(call.Prev, Is.SameAs(length));
        });
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMSET)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMCPY)]
    public static void NextCallReturnAddressRetainsEligibleMemoryCalls(CorInfoHelpFunc helper)
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, destination, source, length) = NewMemoryCall(compiler, helper, 8);
            foreach (var node in new GenTree[] { destination, source, length, call })
            {
                block.InsertAtEnd(node);
            }
            compiler.info.compHasNextCallRetAddr = true;

            if (helper is CorInfoHelpFunc.CORINFO_HELP_MEMSET)
            {
                Assert.That(LowerCallMemset(lowering, call, out var store), Is.False);
                Assert.That(store, Is.Null);
            }
            else
            {
                Assert.That(LowerCallMemmove(lowering, call, out var next), Is.False);
                Assert.That(next, Is.Null);
            }
            Assert.That(length.Next, Is.SameAs(call));
        });
    }

    private static (GenTreeCall Call, GenTreeLclVar Destination, GenTree Source, GenTree Length) NewMemoryCall(
        Compiler compiler, CorInfoHelpFunc helper, int length, int value = 0)
    {
        var destination = compiler.gtNewLclvNode(TYP_BYREF, 0);
        GenTree source = helper is CorInfoHelpFunc.CORINFO_HELP_MEMSET
            ? compiler.gtNewIconNode(TYP_INT, value)
            : compiler.gtNewLclvNode(TYP_BYREF, 1);
        var size = compiler.gtNewIconNode(TYP_I_IMPL, length);
        var call = new GenTreeCall(TYP_VOID) {
            _callType = gtCallTypes.CT_HELPER,
            _callMethHnd = Compiler.eeFindHelper(helper),
        };
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(destination));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(source));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(size));
        return (call, destination, source, size);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCallMemmove")]
    private static extern bool LowerCallMemmove(Lowering lowering, GenTreeCall call, out GenTree? next);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCallMemset")]
    private static extern bool LowerCallMemset(Lowering lowering, GenTreeCall call, out GenTreeBlk? next);

    private static void WithCompiler(Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_BYREF;
        compiler.lvaTable[1].Type = TYP_BYREF;
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
