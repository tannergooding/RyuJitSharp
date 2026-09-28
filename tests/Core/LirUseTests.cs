// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LirUseTests
{
    [Test]
    public static void DefaultUseIsNeitherInitializedNorDummy()
    {
        LIR.Use use = default;

        Assert.That(use.IsInitialized(), Is.False);
        Assert.That(use.IsDummyUse(), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DummyCopiesOwnTheirDefinitions(bool passByValue)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            var third = compiler.gtNewIconNode(TYP_INT, 3);
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(first);
            range.InsertAtEnd(second);
            range.InsertAtEnd(third);
            LIR.Use.MakeDummyUse(range, first, out var original);

            var copy = passByValue ? Copy(original) : original;

            Assert.That(copy.IsInitialized(), Is.True);
            Assert.That(copy.IsDummyUse(), Is.True);
            Assert.That(copy.Def(), Is.SameAs(first));
            copy.ReplaceWith(second);
            Assert.That(copy.Def(), Is.SameAs(second));
            Assert.That(original.Def(), Is.SameAs(first));

            original.ReplaceWith(third);
            Assert.That(original.Def(), Is.SameAs(third));
            Assert.That(copy.Def(), Is.SameAs(second));
            Assert.That(original.IsDummyUse(), Is.True);
            Assert.That(copy.IsDummyUse(), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RealCopiesShareTheOperandEdge(bool passByValue)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var replacement = compiler.gtNewIconNode(TYP_INT, 2);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, first);
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(first);
            range.InsertAtEnd(replacement);
            range.InsertAtEnd(user);
            Assert.That(range.TryGetUse(first, out var original), Is.True);

            var copy = passByValue ? Copy(original) : original;
            Assert.That(copy.IsDummyUse(), Is.False);
            Assert.That(copy.User(), Is.SameAs(user));
            copy.ReplaceWith(replacement);

            Assert.That(original.Def(), Is.SameAs(replacement));
            Assert.That(copy.Def(), Is.SameAs(replacement));
            Assert.That(user.Op1, Is.SameAs(replacement));
        });
    }

    [Test]
    public static void DummyCopyCanBeSpilledWithoutChangingTheOriginalUse()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var value = compiler.gtNewIconNode(TYP_INT, 42);
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(value);
            LIR.Use.MakeDummyUse(range, value, out var original);
            var copy = Copy(original);
            Assert.That(copy.IsDummyUse(), Is.True);

            var local = copy.ReplaceWithLclVar(compiler, out var store);

            Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(store.AsLclVar().LclNum, Is.EqualTo(local));
            Assert.That(store.AsLclVar().Op1, Is.SameAs(value));
            Assert.That(copy.Def().Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(copy.Def().AsLclVar().LclNum, Is.EqualTo(local));
            Assert.That(original.Def(), Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(store));
            Assert.That(store.Next, Is.SameAs(copy.Def()));
        });
    }

    private static LIR.Use Copy(LIR.Use use) => use;
}
