// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FlowGraphEdgeSplitTests
{
    [Test]
    public static void SplittingPreservesEdgesProfileAndIndependentLiveSets(
        [Values(BBJ_ALWAYS, BBJ_COND, BBJ_SWITCH)] BBKinds kind, [Values] bool adjacent)
    {
        WithCompiler(compiler => {
            var source = AddBlock(compiler, kind);
            var first = AddBlock(compiler, BBJ_RETURN);
            var second = AddBlock(compiler, BBJ_RETURN);
            var target = adjacent ? first : second;
            var other = adjacent ? second : first;
            var edge = compiler.fgAddRefPred(target, source);
            var duplicates = kind is BBJ_SWITCH ? 2 : 1;
            if (kind is BBJ_ALWAYS)
            {
                source.TargetEdge = edge;
            }
            else
            {
                var otherEdge = compiler.fgAddRefPred(other, source);
                edge.Likelihood = 0.625;
                otherEdge.Likelihood = 0.375;
                if (kind is BBJ_COND)
                {
                    source.SetCond(edge, otherEdge);
                }
                else
                {
                    var descriptor = new BBswtDesc([edge, otherEdge], [0, 0, 1], hasDefault: true);
                    descriptor.Cases[0] = edge;
                    descriptor.Cases[1] = compiler.fgAddRefPred(target, source);
                    descriptor.Cases[2] = otherEdge;
                    source.SwitchTargets = descriptor;
                }
            }

            source.setBBProfileWeight(80);
            target.setBBProfileWeight(100);
            source.SetFlags(BBF_BACKWARD_JUMP | BBF_ASYNC_RESUMPTION);
            target.SetFlags(BBF_BACKWARD_JUMP);
            compiler.fgLocalVarLivenessDone = true;
            compiler.lvaCurEpoch = 1;
            compiler.lvaTrackedCount = 70;
            compiler.lvaTrackedCountInSizeTUnits = 2;
            target.bbLiveIn = SetOps.MakeSingleton(compiler, 69);
            target.bbLiveOut = SetOps.MakeSingleton(compiler, 0);
            var expectedWeight = edge.LikelyWeight;

            var split = compiler.fgSplitEdge(source, target);

            Assert.That(split.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(split.IsEmpty, Is.True);
            Assert.That(split.Target, Is.SameAs(target));
            Assert.That(split.TargetEdge.DupCount, Is.EqualTo(1));
            Assert.That(split.TargetEdge.Likelihood, Is.EqualTo(1));
            Assert.That(split.bbRefs, Is.EqualTo(duplicates));
            Assert.That(split.bbWeight, Is.EqualTo(expectedWeight));
            Assert.That(split.hasProfileWeight, Is.True);
            Assert.That(split.HasFlag(BBF_BACKWARD_JUMP), Is.True);
            Assert.That(split.HasFlag(BBF_ASYNC_RESUMPTION), Is.True);
            Assert.That(target.bbWeight, Is.EqualTo(100));
            Assert.That(target.bbRefs, Is.EqualTo(1));
            Assert.That(compiler.fgGetPredForBlock(target, source), Is.Null);
            Assert.That(compiler.fgGetPredForBlock(split, source)?.DupCount, Is.EqualTo(duplicates));
            Assert.That(split.bbTryIndex, Is.EqualTo(source.bbTryIndex));
            Assert.That(split.bbHndIndex, Is.EqualTo(source.bbHndIndex));
            if (adjacent)
            {
                Assert.That(source.Next, Is.SameAs(split));
                Assert.That(split.Next, Is.SameAs(target));
            }
            if (kind is BBJ_SWITCH)
            {
                Assert.That(source.SwitchTargets.Cases[0].DestinationBlock, Is.SameAs(split));
                Assert.That(source.SwitchTargets.Cases[1], Is.SameAs(source.SwitchTargets.Cases[0]));
                Assert.That(source.SwitchTargets.DefaultCase.DestinationBlock, Is.SameAs(other));
            }

            Assert.That(SetOps.Equal(compiler, split.bbLiveIn, target.bbLiveIn), Is.True);
            Assert.That(SetOps.Equal(compiler, split.bbLiveOut, target.bbLiveIn), Is.True);
            SetOps.RemoveElemD(compiler, split.bbLiveIn, 69);
            Assert.That(SetOps.IsMember(compiler, split.bbLiveOut, 69), Is.True);
            Assert.That(SetOps.IsMember(compiler, target.bbLiveIn, 69), Is.True);
        });
    }

    [Test]
    public static void AdjacentSplitExtendsTryEndAndMarksZeroWeightRare()
    {
        WithCompiler(compiler => {
            var source = AddBlock(compiler, BBJ_ALWAYS);
            var target = AddBlock(compiler, BBJ_RETURN);
            var handler = AddBlock(compiler, BBJ_EHFAULTRET);
            source.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = source, ebdTryLast = source, ebdHndBeg = handler, ebdHndLast = handler },
            ];
            compiler.compHndBBtabCount = 1;
            source.TargetEdge = compiler.fgAddRefPred(target, source);
            source.setBBProfileWeight(0);
            source.SetFlags(BBF_BACKWARD_JUMP);

            var split = compiler.fgSplitEdge(source, target);

            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(split));
            Assert.That(split.TryIndex, Is.EqualTo(source.TryIndex));
            Assert.That(split.isRunRarely, Is.True);
            Assert.That(split.bbWeight, Is.Zero);
            Assert.That(split.HasFlag(BBF_BACKWARD_JUMP), Is.False);
            Assert.That(split.HasFlag(BBF_ASYNC_RESUMPTION), Is.False);
        });
    }

    [Test]
    public static void CallFinallyReturnSplitPreservesThePair()
    {
        WithCompiler(compiler => {
            var call = AddBlock(compiler, BBJ_CALLFINALLY);
            var continuation = AddBlock(compiler, BBJ_CALLFINALLYRET);
            var target = AddBlock(compiler, BBJ_RETURN);
            var handler = AddBlock(compiler, BBJ_EHFINALLYRET);
            call.TargetEdge = compiler.fgAddRefPred(handler, call);
            continuation.TargetEdge = compiler.fgAddRefPred(target, continuation);

            var split = compiler.fgSplitEdge(continuation, target);

            Assert.That(call.Next, Is.SameAs(continuation));
            Assert.That(call.isBBCallFinallyPair, Is.True);
            Assert.That(continuation.Kind, Is.EqualTo(BBJ_CALLFINALLYRET));
            Assert.That(continuation.Target, Is.SameAs(split));
            Assert.That(split.Target, Is.SameAs(target));
        });
    }

    private static BasicBlock AddBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;

        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            action(compiler);
        });
    }
}
