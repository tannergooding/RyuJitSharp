// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class NarrowingTests
{
    [TestCase(127L, TYP_BYTE, true)]
    [TestCase(128L, TYP_BYTE, false)]
    [TestCase(255L, TYP_UBYTE, true)]
    [TestCase(256L, TYP_UBYTE, false)]
    [TestCase(4294967295L, TYP_UINT, true)]
    [TestCase(-1L, TYP_INT, false)]
    public static void ConstantNarrowingPreservesNativeMasksAndProbePurity(long value, var_types target, bool accepted)
    {
        WithCompiler(compiler => {
            GenTree tree = compiler.gtNewLconNode(value);
            var original = tree;
            var numbers = new ValueNumPair(23, 24);
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, target, numbers, false), Is.EqualTo(accepted));
            Assert.That(tree, Is.SameAs(original));
            Assert.That(tree.Type, Is.EqualTo(TYP_LONG));
            Assert.That(tree.AsIntConCommon().IntegralValue, Is.EqualTo(value));
            if (accepted)
            {
                Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, target, numbers, true), Is.True);
                Assert.That(tree.Type, Is.EqualTo(TYP_INT));
                Assert.That(tree.AsIntCon().IconValue, Is.EqualTo((nint)unchecked((int)value)));
            }
        });
    }

    [Test]
    public static void FailedBinaryProbeDoesNotPartiallyNarrowItsFirstOperand()
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var constant = compiler.gtNewLconNode(-1);
            GenTree tree = compiler.gtNewBinaryNode(GT_OR, TYP_LONG, local, constant);
            tree._vnPair.SetBoth(42);
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_INT, new ValueNumPair(), false), Is.False);
            Assert.That(tree.Type, Is.EqualTo(TYP_LONG));
            Assert.That(local.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
            Assert.That(tree._vnPair.Liberal, Is.EqualTo(42));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AndNarrowsOneBoundingOperandAndCastsTheOther(bool constantFirst)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var constant = compiler.gtNewLconNode(255);
            GenTree tree = compiler.gtNewBinaryNode(GT_AND, TYP_LONG,
                constantFirst ? constant : local, constantFirst ? local : constant);
            var numbers = new ValueNumPair(23, 24);
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_UBYTE, numbers, false), Is.True);
            Assert.That(local.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_UBYTE, numbers, true), Is.True);

            var binary = tree.AsOp();
            var cast = (constantFirst ? binary.Op2 : binary.Op1).AsCast();
            Assert.That(cast.CastOp, Is.SameAs(local));
            Assert.That(cast.CastType, Is.EqualTo(TYP_INT));
            Assert.That(local.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.Type, Is.EqualTo(TYP_INT));
            Assert.That(tree.Type, Is.EqualTo(TYP_INT));
            Assert.That(tree._vnPair, Is.EqualTo(numbers));
        });
    }

    [TestCase(GT_ADD, false)]
    [TestCase(GT_ADD, true)]
    [TestCase(GT_MUL, false)]
    public static void ArithmeticNarrowingPreservesOverflowChecks(genTreeOps operation, bool overflow)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var constant = compiler.gtNewLconNode(1);
            GenTree tree = compiler.gtNewBinaryNode(operation, TYP_LONG, local, constant);
            if (overflow)
            {
                tree.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }

            var numbers = new ValueNumPair(23, 24);
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_INT, numbers, false), Is.EqualTo(!overflow));
            Assert.That(local.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
            if (!overflow)
            {
                Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_INT, numbers, true), Is.True);
                Assert.That(tree.Type, Is.EqualTo(TYP_INT));
                Assert.That(local.Type, Is.EqualTo(TYP_INT));
                Assert.That(constant.Type, Is.EqualTo(TYP_INT));
                Assert.That(tree._vnPair, Is.EqualTo(numbers));
                Assert.That(local._vnPair, Is.EqualTo(new ValueNumPair()));
            }
        });
    }

    [Test]
    public static void CommaNarrowingPreservesEarlierSideEffects()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            GenTree tree = compiler.gtNewCommaNode(TYP_LONG, call, local);
            var numbers = new ValueNumPair(23, 24);
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_INT, numbers, false), Is.True);
            Assert.That(compiler.optNarrowTree(ref tree, TYP_LONG, TYP_INT, numbers, true), Is.True);
            Assert.That(tree.AsOp().Op1, Is.SameAs(call));
            Assert.That(call.Type, Is.EqualTo(TYP_VOID));
            Assert.That(tree.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
            Assert.That(tree.Type, Is.EqualTo(TYP_INT));
            Assert.That(local._vnPair, Is.EqualTo(numbers));
        });
    }

    [Test]
    public static void UnsignedWideningMatchesByActualTypeAndCancelsWithNarrowing()
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 1);
            var inner = compiler.gtNewCastNode(TYP_LONG, value, true, TYP_ULONG);
            GenTree use = inner;
            Assert.That(compiler.optNarrowTree(ref use, TYP_LONG, TYP_INT, new ValueNumPair(), false), Is.True);
            Assert.That(inner.CastType, Is.EqualTo(TYP_ULONG));
            Assert.That(inner.Flags & GTF_UNSIGNED, Is.EqualTo(GTF_UNSIGNED));

            var outer = compiler.gtNewCastNode(TYP_INT, inner, false, TYP_INT);
            var result = compiler.fgOptimizeCast(outer);
            Assert.That(result, Is.SameAs(value));
            Assert.That(inner.CastType, Is.EqualTo(TYP_INT));
            Assert.That(inner.Type, Is.EqualTo(TYP_INT));
            Assert.That(inner.Flags & GTF_UNSIGNED, Is.EqualTo(GTF_EMPTY));
        });
    }

    [Test]
    public static void ProvenNonnegativeWideningDropsOverflowButRetainsSourceEffects()
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewIndir(TYP_UBYTE, compiler.gtNewIconNode(TYP_I_IMPL, 0x1234));
            var cast = compiler.gtNewCastNode(TYP_LONG, value, false, TYP_LONG);
            cast.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            Assert.That(compiler.fgOptimizeCast(cast), Is.SameAs(cast));
            Assert.That(cast.Flags & GTF_OVERFLOW, Is.EqualTo(GTF_EMPTY));
            Assert.That(cast.Flags & GTF_UNSIGNED, Is.EqualTo(GTF_UNSIGNED));
            Assert.That(cast.Flags & GTF_ALL_EFFECT, Is.EqualTo(value.Flags & GTF_ALL_EFFECT));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void SmallLocalStoreRequiresGuaranteedNormalizationAndUncheckedCast(bool exposed, bool overflow)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0] = new LclVarDsc { Type = TYP_BYTE, lvIsParam = true };
            if (exposed)
            {
                compiler.lvaSetVarAddrExposed(0, AddressExposedReason.DISPATCH_RET_BUF);
            }

            var value = compiler.gtNewLclvNode(TYP_INT, 1);
            var cast = compiler.gtNewCastNode(TYP_INT, value, false, TYP_UBYTE);
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }

            var store = compiler.gtNewStoreLclVarNode(0, cast);
            store.Type = TYP_BYTE;
            Assert.That(compiler.fgOptimizeCastOnStore(store), Is.SameAs(store));
            Assert.That(store.Data, Is.SameAs(exposed && !overflow ? value : cast));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compFlags |= CLFLG_TREETRANS;
        compiler.fgGlobalMorph = true;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_LONG }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 2;
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
