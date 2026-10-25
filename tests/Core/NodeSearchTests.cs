// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class NodeSearchTests
{
    [TestCase(GTF_EMPTY, 3, true)]
    [TestCase(GTF_EXCEPT, 0, true)]
    [TestCase(GTF_EXCEPT, 1, true)]
    [TestCase(GTF_EXCEPT, 2, true)]
    [TestCase(GTF_EXCEPT, 3, false)]
    [TestCase(GTF_EXCEPT | GTF_ORDER_SIDEEFF, 1, true)]
    [TestCase(GTF_EXCEPT | GTF_ORDER_SIDEEFF, 2, false)]
    [TestCase(GTF_CALL, 0, false)]
    public static void SearchAppliesEveryRequiredFlagBeforeCallingThePredicate(GenTreeFlags required, int targetIndex, bool found)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            var address = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8);
            var left = compiler.gtNewIndir(var_types.TYP_INT, address, GTF_IND_VOLATILE);
            var right = compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 16));
            var root = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, var_types.TYP_INT, left, right);
            GenTree[] nodes = [root, left, right, address];
            var target = nodes[targetIndex];
            List<GenTree> visited = [];

            var result = compiler.gtFindNodeInTree(root, node => {
                visited.Add(node);
                return node == target;
            }, required);
            var constrainedVisited = new List<GenTree>();
            var predicate = new RecordingPredicate(target, constrainedVisited);
            var constrainedResult = compiler.gtFindNodeInTree(root, ref predicate, required);

            Assert.That(result, Is.SameAs(found ? target : null));
            Assert.That(constrainedResult, Is.SameAs(found ? target : null));
            Assert.That(constrainedVisited, Is.EqualTo(visited));
            Assert.That(predicate.Count, Is.EqualTo(constrainedVisited.Count));
            foreach (var node in visited)
            {
                Assert.That(node.Flags & required, Is.EqualTo(required));
            }

            if (required == GTF_CALL)
            {
                Assert.That(visited, Is.Empty);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct RecordingPredicate(GenTree target, List<GenTree> visited)
        : IGenTreePredicate<RecordingPredicate>
    {
        public int Count;

        public bool IsMatch(GenTree node)
        {
            Count++;
            visited.Add(node);
            return node == target;
        }
    }
}
