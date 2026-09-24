// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BinaryLoweringTests
{
    [TestCase(genTreeOps.GT_ADD, false, false, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_ADD, true, false, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_SUB, true, false, var_types.TYP_INT, false)]
    [TestCase(genTreeOps.GT_ADD, false, true, var_types.TYP_INT, false)]
    [TestCase(genTreeOps.GT_LSH, false, false, var_types.TYP_SHORT, false)]
    [TestCase(genTreeOps.GT_NOT, false, false, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_NEG, false, false, var_types.TYP_INT, true)]
    public static void ReadModifyWriteRecognitionPreservesOperandAndOverflowRules(
        genTreeOps oper, bool readSecond, bool overflow, var_types memoryType, bool expected)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var target = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            var source = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            var load = new GenTreeIndir(genTreeOps.GT_IND, memoryType, source);
            var value = compiler.gtNewLclvNode(var_types.TYP_INT, 2);
            GenTree operation = oper.IsUnary
                ? new GenTreeUnOp(oper, var_types.TYP_INT, load)
                : new GenTreeOp(oper, var_types.TYP_INT, readSecond ? value : load, readSecond ? load : value);
            if (overflow)
            {
                operation.Flags |= GenTreeFlags.GTF_OVERFLOW | GenTreeFlags.GTF_EXCEPT;
            }
            var store = new GenTreeStoreInd(memoryType, target, operation);
            GenTree[] nodes = oper.IsUnary
                ? [target, source, load, operation, store]
                : [target, source, load, value, operation, store];
            foreach (var node in nodes)
            {
                block.InsertAtEnd(node);
            }
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(IsRMWMemOpRootedAtStoreInd(lowering, store, out var candidate, out var other), Is.EqualTo(expected));
            Assert.That(candidate, expected ? Is.SameAs(load) : Is.Null);
            Assert.That(other, expected ? Is.SameAs(oper.IsUnary ? load : value) : Is.Null);
            var cachedStatus = store.RmwStatus;
            Assert.That(cachedStatus, Is.Not.EqualTo(RmwStatus.STOREIND_RMW_STATUS_UNKNOWN));
            Assert.That(IsRMWMemOpRootedAtStoreInd(lowering, store, out candidate, out other), Is.EqualTo(expected));
            Assert.That(store.RmwStatus, Is.EqualTo(cachedStatus));
            Assert.That(candidate, expected ? Is.SameAs(load) : Is.Null);
            Assert.That(other, expected ? Is.SameAs(oper.IsUnary ? load : value) : Is.Null);

            if (expected && oper.IsBinary)
            {
                ContainCheckBinary(lowering, operation.AsOp());
                Assert.That(load.IsContained || value.IsContained || load.IsRegOptional || value.IsRegOptional, Is.False);
            }
            foreach (var node in nodes)
            {
                Assert.That(node._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReadModifyWriteChecksWholeAddressTreeAndClearsPendingMarks(bool modifyIndex)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var targetBase = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            var targetIndex = compiler.gtNewLclvNode(var_types.TYP_LONG, 1);
            var target = new GenTreeAddrMode(var_types.TYP_LONG, targetBase, targetIndex, 4, 16);
            var sourceBase = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            var sourceIndex = compiler.gtNewLclvNode(var_types.TYP_LONG, 1);
            var source = new GenTreeAddrMode(var_types.TYP_LONG, sourceBase, sourceIndex, 4, 16);
            var load = new GenTreeIndir(genTreeOps.GT_IND, var_types.TYP_INT, source);
            GenTree[] addressNodes = [targetBase, targetIndex, target, sourceBase, sourceIndex, source, load];
            foreach (var node in addressNodes)
            {
                block.InsertAtEnd(node);
            }
            if (modifyIndex)
            {
                var newIndex = compiler.gtNewIconNode(var_types.TYP_LONG, 8);
                block.InsertAtEnd(newIndex);
                block.InsertAtEnd(compiler.gtNewStoreLclVarNode(1, newIndex));
            }
            var value = compiler.gtNewIconNode(var_types.TYP_INT, 3);
            var operation = new GenTreeOp(genTreeOps.GT_ADD, var_types.TYP_INT, load, value);
            var store = new GenTreeStoreInd(var_types.TYP_INT, target, operation);
            block.InsertAtEnd(value);
            block.InsertAtEnd(operation);
            block.InsertAtEnd(store);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(IsRMWMemOpRootedAtStoreInd(lowering, store, out _, out _), Is.EqualTo(!modifyIndex));

            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                Assert.That(node._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
            }
        });
    }

    [TestCase(0L, false)]
    [TestCase(long.MinValue, true)]
    [TestCase(0x3FF0000000000000L, true)]
    public static void FloatingBinaryContainmentPreservesSignedZero(long bits, bool contained)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = var_types.TYP_DOUBLE;
            var local = compiler.gtNewLclvNode(var_types.TYP_DOUBLE, 2);
            var constant = new GenTreeDblCon(var_types.TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits));
            var operation = new GenTreeOp(genTreeOps.GT_ADD, var_types.TYP_DOUBLE, local, constant);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(local);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(operation);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            ContainCheckBinary(lowering, operation);

            Assert.That(constant.IsContained, Is.EqualTo(contained));
            Assert.That(local.IsContained, Is.EqualTo(!contained));
            Assert.That(constant.IsBitwiseEqual(bits), Is.True);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "IsRMWMemOpRootedAtStoreInd")]
    private static extern bool IsRMWMemOpRootedAtStoreInd(
        Lowering lowering, GenTreeStoreInd store, out GenTree? candidate, out GenTree? source);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBinary")]
    private static extern void ContainCheckBinary(Lowering lowering, GenTreeOp operation);

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
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = var_types.TYP_LONG;
        compiler.lvaTable[1].Type = var_types.TYP_LONG;
        compiler.lvaTable[2].Type = var_types.TYP_INT;
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
