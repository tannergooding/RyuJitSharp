// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ShiftRotateLoweringTests
{
    [TestCase(TYP_INT, 5, 31, true)]
    [TestCase(TYP_INT, 5, 30, false)]
    [TestCase(TYP_LONG, 32, 31, false)]
    [TestCase(TYP_LONG, 32, 63, true)]
    [TestCase(TYP_ULONG, 32, 31, false)]
    [TestCase(TYP_ULONG, 32, 63, true)]
    public static void CountMaskRemovalUsesOperandWidthAndUnlinksRemovedNodes(
        var_types type, int shiftCount, int maskValue, bool remove)
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
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var value = compiler.gtNewIconNode(type, 7);
            var amount = compiler.gtNewIconNode(TYP_INT, shiftCount);
            var mask = compiler.gtNewIconNode(TYP_INT, maskValue);
            var and = new GenTreeOp(GT_AND, TYP_INT, amount, mask);
            var shift = new GenTreeOp(GT_LSH, type, value, and) { IsUnusedValue = true };
            block.InsertAtEnd(value);
            block.InsertAtEnd(amount);
            block.InsertAtEnd(mask);
            block.InsertAtEnd(and);
            block.InsertAtEnd(shift);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            amount.IsContained = true;

            TryRemoveShiftRotateMask(lowering, shift);

            Assert.That(shift.Op2, Is.SameAs(remove ? amount : and));
            Assert.That(amount.IsContained, Is.EqualTo(!remove));
            Assert.That(amount.Next, Is.SameAs(remove ? shift : mask));
            Assert.That(mask.Next, remove ? Is.Null : Is.SameAs(and));
            Assert.That(and.Next, remove ? Is.Null : Is.SameAs(shift));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryRemoveShiftRotateMask")]
    private static extern void TryRemoveShiftRotateMask(Lowering lowering, GenTreeOp shift);
}
