// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StackArgumentLoweringTests
{
    [TestCase(17)]
    [TestCase(23)]
    [TestCase(24)]
    public static void LoadSizeDecodesPaddingDelta(int loadSize)
    {
        WithCompiler(_ => {
            var value = new GenTreeIntCon(var_types.TYP_INT, 1);
            var argument = new GenTreePutArgStk(var_types.TYP_VOID, value, null, 0, 24, false) {
                ArgLoadSize = loadSize,
            };
            Assert.That(argument.ArgLoadSize, Is.EqualTo(loadSize));
        });
    }

    [TestCase(0L, false)]
    [TestCase(1L, true)]
    [TestCase(2147483648L, false)]
    public static void StackImmediatesRespectEncodingAndZeroingPolicy(long constant, bool contained)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewIconNode(var_types.TYP_LONG, (nint)constant);
            var argument = new GenTreePutArgStk(var_types.TYP_VOID, value, null, 0, 8, false);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(value);
            block.InsertAtEnd(argument);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            LowerPutArgStk(lowering, argument);

            Assert.That(value.IsContained, Is.EqualTo(contained));
            Assert.That(argument.Data, Is.SameAs(value));
        });
    }

    [TestCase(false, 18, false, GenTreePutArgStk.Kind.Unroll)]
    [TestCase(true, 18, false, GenTreePutArgStk.Kind.Unroll)]
    [TestCase(false, 1024, false, GenTreePutArgStk.Kind.RepInstr)]
    [TestCase(true, 24, true, GenTreePutArgStk.Kind.PartialRepInstr)]
    public static void StructCopiesSelectNativeLoadSizeAndCopyKind(
        bool localSource, int size, bool hasGcPointer, GenTreePutArgStk.Kind expectedKind)
    {
        WithCompiler(compiler => {
            var builder = new ClassLayoutBuilder(compiler, size);
            if (hasGcPointer)
            {
                builder.SetGCPtrType(0, var_types.TYP_REF);
            }
            var layout = ClassLayout.Create(compiler, builder);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            GenTree source;
            if (localSource)
            {
                compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
                compiler.lvaTable[0].Layout = layout;
                source = compiler.gtNewLclvNode(var_types.TYP_STRUCT, 0);
            }
            else
            {
                var address = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
                block.InsertAtEnd(address);
                source = new GenTreeBlk(var_types.TYP_STRUCT, address, layout);
            }
            var stackSize = (size + 7) & ~7;
            var argument = new GenTreePutArgStk(var_types.TYP_VOID, source, null, 0, stackSize, false);
            block.InsertAtEnd(source);
            block.InsertAtEnd(argument);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            LowerPutArgStk(lowering, argument);

            Assert.That(argument.ArgLoadSize, Is.EqualTo(localSource ? stackSize : size));
            Assert.That(argument._kind, Is.EqualTo(expectedKind));
            Assert.That(source.IsContained, Is.True);
            Assert.That(source.Type, Is.EqualTo(var_types.TYP_STRUCT));
            if (localSource)
            {
                Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SmallStructsWidenOnlyLocalLoadsAndReplaceBlockOwners(bool localSource)
    {
        WithCompiler(compiler => {
            var layout = new ClassLayout(2);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            GenTree source;
            if (localSource)
            {
                compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
                compiler.lvaTable[0].Layout = layout;
                source = compiler.gtNewLclvNode(var_types.TYP_STRUCT, 0);
            }
            else
            {
                var address = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
                block.InsertAtEnd(address);
                source = new GenTreeBlk(var_types.TYP_STRUCT, address, layout) {
                    Flags = GenTreeFlags.GTF_IND_NONFAULTING | GenTreeFlags.GTF_IND_INVARIANT,
                };
            }
            var originalFlags = source.Flags;
            source._vnPair.SetBoth(123);
            var argument = new GenTreePutArgStk(var_types.TYP_VOID, source, null, 0, 8, false);
            block.InsertAtEnd(source);
            block.InsertAtEnd(argument);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            LowerPutArgStk(lowering, argument);

            Assert.That(argument.Data.Type, Is.EqualTo(localSource ? var_types.TYP_INT : var_types.TYP_USHORT));
            if (localSource)
            {
                Assert.That(argument.Data, Is.SameAs(source));
            }
            else
            {
                Assert.That(argument.Data.Oper, Is.EqualTo(genTreeOps.GT_IND));
                Assert.That(argument.Data, Is.Not.SameAs(source));
                Assert.That(argument.Data.Flags, Is.EqualTo(originalFlags));
                Assert.That(argument.Data._vnPair.Conservative, Is.EqualTo(123));
                Assert.That(argument.Data.AsIndir().Addr, Is.SameAs(block.FirstNode));
                Assert.That(argument.Prev, Is.SameAs(argument.Data));
                Assert.That(argument.Data.Next, Is.SameAs(argument));
                Assert.That(source.Prev, Is.Null);
                Assert.That(source.Next, Is.Null);
#if DEBUG
                Assert.That(argument.Data.TreeId, Is.EqualTo(source.TreeId));
#endif
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerPutArgStk")]
    private static extern void LowerPutArgStk(Lowering lowering, GenTreePutArgStk argument);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = var_types.TYP_LONG;
        compiler.codeGen = new CodeGen(compiler);
        JitTls.Compiler = compiler;
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
