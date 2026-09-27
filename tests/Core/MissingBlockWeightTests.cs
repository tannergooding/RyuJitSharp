// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class MissingBlockWeightTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ProfileGatePreservesWeightsAndClearsGraphModificationFlag(bool profile)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1], []]);
            blocks[0].setBBProfileWeight(17);
            compiler.fgPgoHaveWeights = profile;
            compiler.fgModified = true;
            Assert.That(compiler.fgComputeBlockWeights(), Is.EqualTo(profile
                ? PhaseStatus.MODIFIED_NOTHING : PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(profile ? BB_UNITY_WEIGHT : 17));
            Assert.That(blocks[1].hasProfileWeight, Is.False);
            Assert.That(compiler.fgModified, Is.False);
        });
    }

    [Test]
    public static void SuccessorInferenceOverridesProfiledPredecessorWithoutRequiringProfiledSuccessor()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1], [2], []]);
            blocks[0].setBBProfileWeight(17);
            blocks[2].bbWeight = 29;
            Assert.That(compiler.fgComputeBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(29));
            Assert.That(blocks[0].bbWeight, Is.EqualTo(17));
            Assert.That(blocks[1].hasProfileWeight, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PredecessorInferenceRequiresAProfileWeight(bool profile)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1], []]);
            blocks[0].bbWeight = 17;
            if (profile)
            {
                blocks[0].setBBProfileWeight(17);
            }
            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.EqualTo(profile));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(profile ? 17 : BB_UNITY_WEIGHT));
        });
    }

    [Test]
    public static void ConditionalPredecessorAndMultiplyReferencedSuccessorDoNotInferWeights()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1, 2], [2], []]);
            blocks[0].setBBProfileWeight(17);
            blocks[1].bbWeight = 23;
            blocks[2].setBBProfileWeight(29);
            Assert.That(compiler.fgComputeBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(23));
        });
    }

    [Test]
    public static void MultipleIncomingEdgesDoNotInferFromTheFirstPredecessor()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1, 2], [3], [3], []]);
            blocks[1].setBBProfileWeight(17);
            blocks[2].setBBProfileWeight(29);
            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.False);
            Assert.That(blocks[3].bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
        });
    }

    [Test]
    public static void CallFinallyTailInheritsItsUniqueContinuationWeight()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1], [2], []]);
            blocks[1].SetKindAndTargetEdge(BBJ_CALLFINALLYRET, blocks[1].TargetEdge);
            blocks[2].setBBProfileWeight(29);
            Assert.That(compiler.fgComputeBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(29));
        });
    }

    [TestCase(BBJ_ALWAYS, false, 17)]
    [TestCase(BBJ_ALWAYS, true, 0)]
    [TestCase(BBJ_CALLFINALLY, false, 100)]
    [TestCase(BBJ_CALLFINALLY, true, 17)]
    public static void HandlerInferencePreservesSplittingAndCallFinallyQuirks(BBKinds predecessorKind,
        bool splitting, double expected)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[1], []]);
            blocks[0].SetKindAndTargetEdge(predecessorKind, blocks[0].TargetEdge);
            blocks[0].setBBProfileWeight(17);
            Handler(compiler, blocks, false);
            compiler.fgFirstColdBlock = splitting ? blocks[1] : null;
            _ = compiler.fgComputeBlockWeights();
            Assert.That(blocks[1].bbWeight, Is.EqualTo(expected));
            Assert.That(blocks[1].hasProfileWeight, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void UnreferencedHandlerAndFilterBecomeRareOnlyWhenSplitting(bool filter, bool splitting)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[], [], []]);
            Handler(compiler, blocks, filter);
            compiler.fgFirstColdBlock = splitting ? blocks[1] : null;
            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.EqualTo(splitting));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(splitting ? 0 : BB_UNITY_WEIGHT));
            if (filter)
            {
                Assert.That(blocks[2].bbWeight, Is.EqualTo(splitting ? 0 : BB_UNITY_WEIGHT));
            }
            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.False);
        });
    }

    [Test]
    public static void ProfiledHandlerWeightsAreNeverRewritten()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[], []]);
            Handler(compiler, blocks, false);
            blocks[1].setBBProfileWeight(17);
            compiler.fgFirstColdBlock = blocks[1];
            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.False);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(17));
        });
    }

    [Test]
    public static void BackwardPropagationStopsAfterExactlyTenPasses()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var successors = new int[14][];
            for (var index = 0; index < successors.Length - 1; index++)
            {
                successors[index] = [index + 1];
            }
            successors[^1] = [];
            var blocks = Graph(compiler, successors);
            blocks[^1].setBBProfileWeight(17);

            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.True);
            for (var index = 0; index < blocks.Length; index++)
            {
                Assert.That(blocks[index].bbWeight, Is.EqualTo(index < 3 ? BB_UNITY_WEIGHT : 17));
            }
        });
    }

    [Test]
    public static void UnreachableOscillatingCycleIsBounded()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var blocks = Graph(compiler, [[], [2], [3], [1]]);
            blocks[1].bbWeight = 1;
            blocks[2].bbWeight = 2;
            blocks[3].bbWeight = 3;
            Assert.That(compiler.fgComputeMissingBlockWeights(), Is.True);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(3));
            Assert.That(blocks[2].bbWeight, Is.EqualTo(2));
            Assert.That(blocks[3].bbWeight, Is.EqualTo(3));
        });
    }

    private static void Handler(Compiler compiler, BasicBlock[] blocks, bool filter)
    {
        var handler = filter ? blocks[2] : blocks[1];
        blocks[1].HndIndex = 0;
        handler.HndIndex = 0;
        compiler.compHndBBtab = [new() {
            ebdHandlerType = filter ? EHHandlerType.EH_HANDLER_FILTER : EHHandlerType.EH_HANDLER_CATCH,
            ebdTryBeg = blocks[0],
            ebdTryLast = blocks[0],
            ebdHndBeg = handler,
            ebdHndLast = handler,
            ebdFilter = filter ? blocks[1] : null,
            ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        }];
        compiler.compHndBBtabCount = 1;
    }

    private static BasicBlock[] Graph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].bbRefs = 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        blocks[0].bbRefs = 1;

        for (var index = 0; index < blocks.Length; index++)
        {
            var targets = successors[index];
            if (targets.Length == 1)
            {
                blocks[index].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[targets[0]], blocks[index]));
            }
            else if (targets.Length == 2)
            {
                blocks[index].SetCond(compiler.fgAddRefPred(blocks[targets[0]], blocks[index]),
                    compiler.fgAddRefPred(blocks[targets[1]], blocks[index]));
            }
        }

        return blocks;
    }
}
