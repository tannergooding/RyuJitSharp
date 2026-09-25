// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LirRangeEnumerationTests
{
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
