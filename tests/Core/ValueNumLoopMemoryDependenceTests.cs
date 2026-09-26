// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumLoopMemoryDependenceTests
{
    [Test]
    public static void DependenceTracksOnlyEnclosingLoopUpdatesAndKeepsTheClosestConstraint()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            var blocks = CreateLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var inside = store.VNForExpr(blocks[1], TYP_HEAP);
            var outside = store.VNForExpr(blocks[0], TYP_HEAP);
            var tree = compiler.gtNewIconNode(TYP_INT, 0);

            compiler.optRecordLoopMemoryDependence(tree, blocks[1], outside);
            Assert.That(compiler.NodeToLoopMemoryBlockMap.ContainsKey(tree), Is.False);

            compiler.optRecordLoopMemoryDependence(tree, blocks[1], inside);
            Assert.That(compiler.NodeToLoopMemoryBlockMap[tree], Is.SameAs(blocks[1]));

            compiler.NodeToLoopMemoryBlockMap[tree] = blocks[2];
            compiler.optRecordLoopMemoryDependence(tree, blocks[1], inside);
            Assert.That(compiler.NodeToLoopMemoryBlockMap[tree], Is.SameAs(blocks[2]));

            compiler.NodeToLoopMemoryBlockMap[tree] = blocks[0];
            compiler.optRecordLoopMemoryDependence(tree, blocks[1], inside);
            Assert.That(compiler.NodeToLoopMemoryBlockMap[tree], Is.SameAs(blocks[1]));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static BasicBlock[] CreateLoop(Compiler compiler)
    {
        BasicBlock[] blocks =
        [
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
        ];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgPredsComputed = true;
        blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
        blocks[1].SetCond(compiler.fgAddRefPred(blocks[2], blocks[1]),
            compiler.fgAddRefPred(blocks[3], blocks[1]));
        blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[2]));
        return blocks;
    }
}
