// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LirRangeEnumerationTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void OperandTreeRangesExcludeTheRootAndTrailingEffects(bool interiorGap, bool trailingGap)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var prefix = new GenTree(GT_MEMORYBARRIER, TYP_VOID) { Flags = GTF_ASG };
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var left = new GenTreeIndir(GT_IND, TYP_INT, address)
            {
                Flags = GTF_GLOB_REF | GTF_IND_NONFAULTING,
            };
            var right = compiler.gtNewIconNode(TYP_INT, 1);
            var root = new GenTreeOp(GT_ADD, TYP_INT, left, right)
            {
                Flags = GTF_OVERFLOW | GTF_EXCEPT,
            };
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(prefix);
            range.InsertAtEnd(address);
            range.InsertAtEnd(left);
            if (interiorGap)
            {
                range.InsertAtEnd(new GenTree(GT_MEMORYBARRIER, TYP_VOID) { Flags = GTF_ASG });
            }
            range.InsertAtEnd(right);
            if (trailingGap)
            {
                range.InsertAtEnd(new GenTree(GT_MEMORYBARRIER, TYP_VOID) { Flags = GTF_ASG });
            }
            range.InsertAtEnd(root);

            var operands = range.GetRangeOfOperandTrees(root, out var isClosed, out var sideEffects);

            Assert.That(operands.FirstNode, Is.SameAs(address));
            Assert.That(operands.LastNode, Is.SameAs(right));
            Assert.That(isClosed, Is.EqualTo(!interiorGap));
            Assert.That(sideEffects, Is.EqualTo(GTF_GLOB_REF | (interiorGap ? GTF_ASG : GTF_EMPTY)));

            var tree = range.GetTreeRange(root, out isClosed, out sideEffects);
            Assert.That(tree.FirstNode, Is.SameAs(address));
            Assert.That(tree.LastNode, Is.SameAs(root));
            Assert.That(isClosed, Is.EqualTo(!interiorGap && !trailingGap));
            Assert.That(sideEffects, Is.EqualTo(
                GTF_GLOB_REF | GTF_EXCEPT | ((interiorGap || trailingGap) ? GTF_ASG : GTF_EMPTY)));
            foreach (var node in range)
            {
                Assert.That(node._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
            }
        });
    }

    [Test]
    public static void LeafOperandRangeIsEmptyAndHasNoEffects()
    {
        CodeGenBinaryTests.WithCodeGen((_, _) =>
        {
            var root = new GenTree(GT_MEMORYBARRIER, TYP_VOID) { Flags = GTF_ASG };
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(root);

            var operands = range.GetRangeOfOperandTrees(root, out var isClosed, out var sideEffects);

            Assert.That(operands.IsEmpty, Is.True);
            Assert.That(isClosed, Is.True);
            Assert.That(sideEffects, Is.EqualTo(GTF_EMPTY));
            Assert.That(root.Flags, Is.EqualTo(GTF_ASG));
            Assert.That(root._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
        });
    }

    [Test]
    public static void UnaryOperandRangeExcludesTheRootException()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var root = new GenTreeIndir(GT_NULLCHECK, TYP_VOID, address) { Flags = GTF_EXCEPT };
            var range = new LIR.Range(null, null);
            range.InsertAtEnd(address);
            range.InsertAtEnd(root);

            var operands = range.GetRangeOfOperandTrees(root, out var isClosed, out var sideEffects);

            Assert.That(operands.ToArray(), Is.EqualTo(new GenTree[] { address }));
            Assert.That(isClosed, Is.True);
            Assert.That(sideEffects, Is.EqualTo(GTF_EMPTY));
            Assert.That(address._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
            Assert.That(root._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
        });
    }

    [TestCase(0, 4)]
    [TestCase(0, 2)]
    [TestCase(1, 2)]
    [TestCase(2, 2)]
    [TestCase(1, 1)]
    [TestCase(0, 0)]
    public static void EnumerationRespectsBothRangeBoundaries(int start, int count)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        try
        {
            var nodes = new GenTree[4];
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            for (var index = 0; index < nodes.Length; index++)
            {
                nodes[index] = new GenTree(GT_NOP, TYP_VOID);
                block.InsertAtEnd(nodes[index]);
            }
            var range = count == 0
                ? new LIR.ReadOnlyRange(null, null)
                : new LIR.ReadOnlyRange(nodes[start], nodes[start + count - 1]);
            var expected = nodes.Skip(start).Take(count).ToArray();
            Assert.That(range.ToArray(), Is.EqualTo(expected));

            var forward = range.GetEnumerator();
            for (var run = 0; run < 2; run++)
            {
                var actual = new List<GenTree>();
                while (forward.MoveNext())
                {
                    actual.Add(forward.Current);
                }
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(forward.MoveNext(), Is.False);
                forward.Reset();
            }

            var reverse = range.GetReverseEnumerator();
            for (var run = 0; run < 2; run++)
            {
                var actual = new List<GenTree>();
                while (reverse.MoveNext())
                {
                    actual.Add(reverse.Current);
                }
                Assert.That(actual, Is.EqualTo(expected.Reverse()));
                Assert.That(reverse.MoveNext(), Is.False);
                reverse.Reset();
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
