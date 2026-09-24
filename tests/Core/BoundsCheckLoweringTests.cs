// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class BoundsCheckLoweringTests
{
    [TestCase(true)]
    [TestCase(false)]
    public static void ImmediateOperandIsContainedAndOtherOperandIsRegOptional(bool constantIndex)
    {
        WithCompiler(compiler => {
            GenTree index = constantIndex
                ? compiler.gtNewIconNode(TYP_INT, 5)
                : compiler.gtNewLclvNode(TYP_INT, 0);
            GenTree length = constantIndex
                ? compiler.gtNewLclvNode(TYP_INT, 1)
                : compiler.gtNewIconNode(TYP_INT, 9);
            var check = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            var block = NewBlock(index, length, check);

            ContainCheckBoundsChk(NewLowering(compiler, block), check);

            var constant = constantIndex ? index : length;
            var other = constantIndex ? length : index;
            Assert.That(constant.IsContained, Is.True);
            Assert.That(other.IsRegOptional, Is.True);
            Assert.That(other.IsContained, Is.False);
            Assert.That(index.Next, Is.SameAs(length));
            Assert.That(length.Next, Is.SameAs(check));
            Assert.That(check.Index, Is.SameAs(index));
            Assert.That(check.ArrayLength, Is.SameAs(length));
        });
    }

    [Test]
    public static void MemoryIndexIsPreferredWhenNeitherOperandIsImmediate()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            var length = compiler.gtNewLclvNode(TYP_INT, 1);
            var check = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            var block = NewBlock(index, length, check);

            ContainCheckBoundsChk(NewLowering(compiler, block), check);

            Assert.That(index.IsContained, Is.True);
            Assert.That(length.IsContained, Is.False);
            Assert.That(length.IsRegOptional, Is.False);
            Assert.That(index.Next, Is.SameAs(length));
            Assert.That(length.Next, Is.SameAs(check));
        });
    }

    [Test]
    public static void DifferentOperandTypesPreventSecondContainment()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].Type = TYP_LONG;
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            var length = compiler.gtNewLclvNode(TYP_LONG, 1);
            var check = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            var block = NewBlock(index, length, check);

            ContainCheckBoundsChk(NewLowering(compiler, block), check);

            Assert.That(index.IsContained, Is.True);
            Assert.That(length.IsContained, Is.False);
            Assert.That(length.IsRegOptional, Is.False);
        });
    }

    [Test]
    public static void UncontainableLargeIndexLeavesLengthRegOptional()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].Type = TYP_LONG;
            var index = compiler.gtNewIconNode(TYP_LONG, unchecked((nint)((long)int.MaxValue + 1)));
            var length = compiler.gtNewLclvNode(TYP_LONG, 1);
            var check = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            var block = NewBlock(index, length, check);

            ContainCheckBoundsChk(NewLowering(compiler, block), check);

            Assert.That(index.IsContained, Is.False);
            Assert.That(length.IsRegOptional, Is.True);
            Assert.That(index.Next, Is.SameAs(length));
            Assert.That(length.Next, Is.SameAs(check));
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBoundsChk")]
    private static extern void ContainCheckBoundsChk(Lowering lowering, GenTreeBoundsChk check);

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
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
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
