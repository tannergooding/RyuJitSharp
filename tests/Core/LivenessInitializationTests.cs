// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;
using TrackedSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LivenessInitializationTests
{
    private readonly struct LatePolicy : ILivenessPolicy
    {
    }

    private readonly struct EarlyPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
    }

    private readonly struct AsyncPolicy : ILivenessPolicy
    {
        public static bool TrackAddressExposedLocals => true;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(65)]
    public static void InitSelectsTheNewEpochBeforeReplacingEveryBlocksVariableSets(int count)
    {
        WithCompiler(count, false, compiler => {
            var blocks = MakeBlocks(compiler, 3);
            compiler.lvaCurEpoch = 17;
            compiler.lvaTrackedCount = 9;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.fgStmtRemoved = true;
            compiler.fgLocalVarLivenessDone = true;
            nint[] oldBits = [-1];
            foreach (var block in blocks)
            {
                block.bbVarUse = block.bbVarDef = block.bbLiveIn = block.bbLiveOut = oldBits;
                block.bbMemoryUse = block.bbMemoryDef = block.bbMemoryLiveIn = block.bbMemoryLiveOut = 1;
                block.bbMemoryHavoc = 1;
                block.bbMemorySsaNumIn[0] = 7;
                block.bbMemorySsaNumOut[1] = 11;
                block.SetFlags(BBF_IMPORTED | BBF_HAS_CALL);
                block.MakeLir(null, null);
                block.InsertAtEnd(compiler.gtNewIconNode(TYP_INT, block.bbNum));
            }
            var firstNode = blocks[0].FirstNode;
            var flags = blocks[0].FlagsRaw;

            new Liveness<LatePolicy>(compiler).Init();

            Assert.That(compiler.fgBBVarSetsInited, Is.True);
            Assert.That(compiler.lvaTrackedCount, Is.EqualTo(count));
            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(count == 0 ? 17 : 18));
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.fgStmtRemoved, Is.True);
            foreach (var block in blocks)
            {
                AssertEmptySets(block, (count + 63) / 64);
                Assert.That(block.bbVarUse, Is.Not.SameAs(oldBits));
                Assert.That(block.bbMemoryUse, Is.Zero);
                Assert.That(block.bbMemoryDef, Is.Zero);
                Assert.That(block.bbMemoryLiveIn, Is.Zero);
                Assert.That(block.bbMemoryLiveOut, Is.Zero);
                Assert.That(block.bbMemoryHavoc, Is.EqualTo(1));
                Assert.That(block.bbMemorySsaNumIn[0], Is.EqualTo(7));
                Assert.That(block.bbMemorySsaNumOut[1], Is.EqualTo(11));
            }
            Assert.That(blocks[0].FirstNode, Is.SameAs(firstNode));
            Assert.That(blocks[0].FlagsRaw, Is.EqualTo(flags));
            Assert.That(blocks[0].Next, Is.SameAs(blocks[1]));
            Assert.That(blocks[1].Next, Is.SameAs(blocks[2]));
            Assert.That(oldBits[0], Is.EqualTo((nint)(-1)));
            if (count != 0)
            {
                Assert.That(compiler.lvaTable[0].lvSingleDef, Is.True);
                Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.EqualTo(1));
            }
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    public static void InitRetainsPolicyDifferencesAndUsesExistingMinoptsAccounting(int policy, bool minopts)
    {
        WithCompiler(3, minopts, compiler => {
            var block = MakeBlocks(compiler, 1)[0];
            compiler.lvaTable[1].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            if (minopts)
            {
                compiler.lvaComputeRefCounts(false, false);
            }

            switch (policy)
            {
                case 0:
                {
                    new Liveness<LatePolicy>(compiler).Init();
                    break;
                }

                case 1:
                {
                    new Liveness<EarlyPolicy>(compiler).Init();
                    break;
                }

                case 2:
                {
                    new Liveness<AsyncPolicy>(compiler).Init();
                    break;
                }
            }

            int[] expected = policy == 2 ? [2, 1, 0] : policy == 1 || minopts ? [0, 2] : [2, 0];
            Assert.That(compiler.lvaTrackedCount, Is.EqualTo(expected.Length));
            Assert.That(compiler.lvaTrackedToVarNum.AsSpan(0, expected.Length).ToArray(), Is.EqualTo(expected));
            AssertEmptySets(block, 1);
            Assert.That(compiler.fgBBVarSetsInited, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.False);
        });
    }

    [Test]
    public static void ReinitializationUsesFreshIndependentSetsAtTheNewTrackingSize()
    {
        WithCompiler(65, false, compiler => {
            var blocks = MakeBlocks(compiler, 2);
            var liveness = new Liveness<LatePolicy>(compiler);
            liveness.Init();
            var oldUse = blocks[0].bbVarUse;
            TrackedSetOps.AddElemD(compiler, oldUse, 64);
            Assert.That(blocks[0].bbVarDef[1], Is.EqualTo((nint)0));
            Assert.That(blocks[0].bbLiveIn[1], Is.EqualTo((nint)0));
            Assert.That(blocks[0].bbLiveOut[1], Is.EqualTo((nint)0));
            Assert.That(blocks[1].bbVarUse[1], Is.EqualTo((nint)0));
            MaxLocalsToTrack(ref JitConfig) = 1;

            liveness.Init();

            Assert.That(compiler.lvaTrackedCount, Is.EqualTo(1));
            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(2));
            Assert.That(compiler.fgBBVarSetsInited, Is.True);
            foreach (var block in blocks)
            {
                AssertEmptySets(block, 1);
            }
            Assert.That(blocks[0].bbVarUse, Is.Not.SameAs(oldUse));
            Assert.That(oldUse[1], Is.EqualTo((nint)1));
        });
    }

    [Test]
    public static void InitPublishesInitializationWithoutPretendingToRunDataflowForAnEmptyGraph()
    {
        WithCompiler(0, false, compiler => {
            new Liveness<LatePolicy>(compiler).Init();

            Assert.That(compiler.fgBBVarSetsInited, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.False);
            Assert.That(compiler.fgStmtRemoved, Is.False);
            Assert.That(compiler.lvaCurEpoch, Is.Zero);
        });
    }

    private static void AssertEmptySets(BasicBlock block, int wordCount)
    {
        Assert.That(block.bbVarUse, Has.Length.EqualTo(wordCount).And.All.EqualTo((nint)0));
        Assert.That(block.bbVarDef, Has.Length.EqualTo(wordCount).And.All.EqualTo((nint)0));
        Assert.That(block.bbLiveIn, Has.Length.EqualTo(wordCount).And.All.EqualTo((nint)0));
        Assert.That(block.bbLiveOut, Has.Length.EqualTo(wordCount).And.All.EqualTo((nint)0));
    }

    private static BasicBlock[] MakeBlocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN, bbNum = index + 1 };
            blocks[index] = block;
            if (index != 0)
            {
                blocks[index - 1].Next = block;
                block.Prev = blocks[index - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        return blocks;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

    private static void WithCompiler(int count, bool minopts, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.lvaRefCountState = RCS_NORMAL;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
        JitTls.Compiler = compiler;
        MaxLocalsToTrack(ref JitConfig) = 1024;
        try
        {
            for (var index = 0; index < count; index++)
            {
                ref var local = ref compiler.lvaTable[index];
                local.Type = TYP_INT;
                local.lvSingleDef = true;
                local.setLvRefCnt(1);
                local.setLvRefCntWtd(index + 1);
            }
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
