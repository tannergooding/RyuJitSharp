// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.ValueNumStore.VN_RELATION_KIND;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumRelopRelationTests
{
    [TestCase(VNFunc.VNF_LT, VRK_Reverse, VNFunc.VNF_GE, false)]
    [TestCase(VNFunc.VNF_LE, VRK_Swap, VNFunc.VNF_GE, true)]
    [TestCase(VNFunc.VNF_GT, VRK_SwapReverse, VNFunc.VNF_GE, true)]
    [TestCase(VNFunc.VNF_EQ, VRK_Reverse, VNFunc.VNF_NE, false)]
    [TestCase(VNFunc.VNF_NE, VRK_Reverse, VNFunc.VNF_EQ, false)]
    [TestCase(VNFunc.VNF_LT_UN, VRK_Reverse, VNFunc.VNF_GE_UN, false)]
    [TestCase(VNFunc.VNF_LE_UN, VRK_Swap, VNFunc.VNF_GE_UN, true)]
    [TestCase(VNFunc.VNF_GT_UN, VRK_SwapReverse, VNFunc.VNF_GE_UN, true)]
    public static void RelatedIntegerComparisonsPreserveOperandOrderAndSignedness(
        VNFunc func, ValueNumStore.VN_RELATION_KIND relation, VNFunc expected, bool swapped)
    {
        WithStore(store =>
        {
            var left = store.VNForExpr(null, TYP_INT);
            var right = store.VNForExpr(null, TYP_INT);
            var original = store.VNForFunc(TYP_INT, func, left, right);
            var related = store.GetRelatedRelop(original, relation);

            Assert.That(related, Is.EqualTo(store.VNForFunc(TYP_INT, expected,
                swapped ? right : left, swapped ? left : right)));
            Assert.That(store.GetRelatedRelop(original, VRK_Same), Is.EqualTo(original));
            Assert.That(store.GetRelatedRelop(original, VRK_Inferred), Is.EqualTo(ValueNumStore.NoVN));
        });
    }

    [TestCase(VNFunc.VNF_LT_UN, true, genTreeOps.GT_LT)]
    [TestCase(VNFunc.VNF_GE_UN, true, genTreeOps.GT_GE)]
    [TestCase(VNFunc.VNF_GT, false, genTreeOps.GT_GT)]
    [TestCase(VNFunc.VNF_EQ, false, genTreeOps.GT_EQ)]
    [TestCase(VNFunc.VNF_NE, false, genTreeOps.GT_NE)]
    [TestCase(VNFunc.VNF_ADD, false, genTreeOps.GT_NONE)]
    public static void ConversionToTreeOperatorClassifiesUnsignedRelops(
        VNFunc func, bool expectedUnsigned, genTreeOps expected)
    {
        WithStore(store =>
        {
            Assert.That(store.VNRelopToGenTreeOp(func, out var isUnsigned), Is.EqualTo(expected));
            Assert.That(isUnsigned, Is.EqualTo(expectedUnsigned));
            Assert.That(ValueNumStore.VNFuncIsComparison(func), Is.EqualTo(expected is not genTreeOps.GT_NONE));
            Assert.That(ValueNumStore.VNFuncIsSignedComparison(func),
                Is.EqualTo((expected is not genTreeOps.GT_NONE) && !expectedUnsigned));
        });
    }

    [Test]
    public static void FloatingAndNonComparisonFunctionsHaveNoRelatedRelop()
    {
        WithStore(store =>
        {
            var left = store.VNForExpr(null, TYP_FLOAT);
            var right = store.VNForExpr(null, TYP_FLOAT);
            var ordered = store.VNForFunc(TYP_INT, VNFunc.VNF_LT, left, right);
            var unordered = store.VNForFunc(TYP_INT, VNFunc.VNF_LT_UN, left, right);
            var integer = store.VNForExpr(null, TYP_INT);
            var sum = store.VNForFunc(TYP_INT, VNFunc.VNF_ADD, integer, store.VNForIntCon(7));

            Assert.That(store.GetRelatedRelop(ordered, VRK_Reverse), Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.GetRelatedRelop(unordered, VRK_Swap), Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.GetRelatedRelop(sum, VRK_Reverse), Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.GetRelatedRelop(ValueNumStore.NoVN, VRK_Reverse), Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.GetRelatedRelop(sum, VRK_Same), Is.EqualTo(sum));
        });
    }

#if DEBUG
    [TestCase(VNFunc.VNF_LT, "LT")]
    [TestCase(VNFunc.VNF_LT_UN, "LT_UN")]
    public static void NativeComparisonNamesAreUsedForInferenceDiagnostics(VNFunc func, string expected)
    {
        Assert.That(ValueNumStore.VNFuncName(func), Is.EqualTo(expected));
    }
#endif

    private static void WithStore(Action<ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
