// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LocalsGenTreeListMutationTests
{
    [TestCase(1, 0)]
    [TestCase(3, 0)]
    [TestCase(3, 1)]
    [TestCase(3, 2)]
    public static void RemoveUpdatesListEdges(int count, int removeIndex)
    {
        CodeGenBinaryTests.WithCodeGen((_, _) =>
        {
            var nodes = CreateLocals(count);
            var statement = CreateStatement(nodes);

            new LocalsGenTreeList(statement).Remove(nodes[removeIndex]);

            var expected = new GenTreeLclVarCommon[count - 1];
            for (int sourceIndex = 0, targetIndex = 0; sourceIndex < count; sourceIndex++)
            {
                if (sourceIndex != removeIndex)
                {
                    expected[targetIndex++] = nodes[sourceIndex];
                }
            }

            AssertList(statement, expected);
        });
    }

    [TestCase(0, 0, true)]
    [TestCase(1, 2, false)]
    [TestCase(3, 3, false)]
    [TestCase(0, 3, false)]
    public static void ReplaceUpdatesListEdges(
        int firstIndex,
        int lastIndex,
        bool replaceWithSingleNode)
    {
        CodeGenBinaryTests.WithCodeGen((_, _) =>
        {
            var nodes = CreateLocals(4);
            var statement = CreateStatement(nodes);
            var replacementFirst = new GenTreeLclVar(TYP_INT, 4);
            var replacementLast = replaceWithSingleNode ? replacementFirst : new GenTreeLclVar(TYP_INT, 5);

            if (!replaceWithSingleNode)
            {
                replacementFirst.Next = replacementLast;
                replacementLast.Prev = replacementFirst;
            }

            new LocalsGenTreeList(statement).Replace(
                nodes[firstIndex],
                nodes[lastIndex],
                replacementFirst,
                replacementLast);

            var replacedCount = lastIndex - firstIndex + 1;
            var replacementCount = replaceWithSingleNode ? 1 : 2;
            var expected = new GenTreeLclVarCommon[4 - replacedCount + replacementCount];
            var targetIndex = 0;

            for (var sourceIndex = 0; sourceIndex < firstIndex; sourceIndex++)
            {
                expected[targetIndex++] = nodes[sourceIndex];
            }

            expected[targetIndex++] = replacementFirst;
            if (!replaceWithSingleNode)
            {
                expected[targetIndex++] = replacementLast;
            }

            for (var sourceIndex = lastIndex + 1; sourceIndex < nodes.Length; sourceIndex++)
            {
                expected[targetIndex++] = nodes[sourceIndex];
            }

            AssertList(statement, expected);
        });
    }

    private static GenTreeLclVar[] CreateLocals(int count)
    {
        var nodes = new GenTreeLclVar[count];

        for (var index = 0; index < count; index++)
        {
            nodes[index] = new GenTreeLclVar(TYP_INT, index);
            if (index > 0)
            {
                nodes[index - 1].Next = nodes[index];
                nodes[index].Prev = nodes[index - 1];
            }
        }

        return nodes;
    }

    private static Statement CreateStatement(GenTreeLclVar[] nodes)
    {
        var statement = new Statement(nodes[0], 0)
        {
            TreeListBegin = nodes[0],
            TreeListEnd = nodes[^1],
        };
        return statement;
    }

    private static void AssertList(Statement statement, GenTreeLclVarCommon[] nodes)
    {
        Assert.That(statement.TreeListBegin, Is.SameAs(nodes.Length == 0 ? null : nodes[0]));
        Assert.That(statement.TreeListEnd, Is.SameAs(nodes.Length == 0 ? null : nodes[^1]));

        for (var index = 0; index < nodes.Length; index++)
        {
            Assert.That(nodes[index].Prev, Is.SameAs(index == 0 ? null : nodes[index - 1]));
            Assert.That(nodes[index].Next, Is.SameAs(index == nodes.Length - 1 ? null : nodes[index + 1]));
        }
    }
}
