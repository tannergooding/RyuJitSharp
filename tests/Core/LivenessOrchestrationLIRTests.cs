// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Phases;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LivenessOrchestrationLIRTests
{
    private readonly struct PostLowerPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool EliminateDeadCode => true;
    }

    private readonly struct AsyncPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool TrackAddressExposedLocals => true;
    }

    private readonly struct MemoryPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool ComputeMemoryLiveness => true;
    }

    private readonly struct HirPolicy : ILivenessPolicy
    {
    }

    private readonly struct EarlyPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
    }

    private readonly struct ContradictoryPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool IsEarly => true;
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 65)]
    public static void RunInitializesAndPublishesEvenWithEmptyLir(bool minopts, int count)
    {
        WithCompiler(count, minopts, compiler => {
            var blocks = Blocks(compiler, 2);
            Connect(blocks[0], blocks[1]);
            SetDfs(compiler);
            var epoch = compiler.CurLVEpoch;
            compiler.fgStmtRemoved = true;

            new Liveness<PostLowerPolicy>(compiler).RunLIR();

            Assert.That(compiler.fgBBVarSetsInited, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.CurLVEpoch == epoch, Is.EqualTo(count == 0));
            Assert.That(compiler.fgStmtRemoved, Is.False);
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(PHASE_LCLVARLIVENESS_INTERBLOCK));
            foreach (var block in blocks)
            {
                Assert.That((int)SetOps.Count(compiler, block.bbLiveIn), Is.Zero);
                Assert.That((int)SetOps.Count(compiler, block.bbLiveOut), Is.Zero);
            }
#if DEBUG
            Assert.That(compiler.compCurBB, Is.Null);
#else
            Assert.That(compiler.compCurBB, Is.SameAs(blocks[1]));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OneInterBlockPassRunsBackwardsAndShrinksOnlyTheFinalConsumersLiveIn(bool minopts)
    {
        WithCompiler(2, minopts, compiler => {
            var (blocks, firstStore, secondStore, lastUse) = DeadChain(compiler);
            var liveness = new Liveness<PostLowerPolicy>(compiler);
            liveness.Init();
            liveness.PerBlockLocalVarLiveness();
            var epoch = compiler.CurLVEpoch;
            var phase = compiler.mostRecentlyActivePhase;

            liveness.InterBlockLocalVarLivenessLIR();

            Assert.That(blocks[0].LastNode, Is.SameAs(firstStore));
            Assert.That(blocks[2].LastNode, Is.SameAs(secondStore));
            Assert.That(blocks[1].FirstNode, Is.Null);
            Assert.That(lastUse.Prev, Is.Null);
            Assert.That((int)SetOps.Count(compiler, blocks[2].bbLiveIn), Is.EqualTo(1));
            Assert.That((int)SetOps.Count(compiler, blocks[1].bbLiveIn), Is.Zero);
            Assert.That(compiler.fgStmtRemoved, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.CurLVEpoch, Is.EqualTo(epoch));
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(phase));
#if DEBUG
            Assert.That(compiler.compCurBB, Is.Null);
#else
            Assert.That(compiler.compCurBB, Is.SameAs(blocks[1]));
#endif
        });
    }

    [Test]
    public static void UnreachableBlocksAreInitializedButNotProcessedByTheBackwardPass()
    {
        WithCompiler(1, false, compiler => {
            var blocks = Blocks(compiler, 3);
            Connect(blocks[0], blocks[1]);
            var dead = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 42));
            Append(compiler, blocks[2], dead);
            SetDfs(compiler);

            new Liveness<PostLowerPolicy>(compiler).RunLIR();

            Assert.That(blocks[2].LastNode, Is.SameAs(dead));
            Assert.That((dead.Flags & GTF_VAR_DEATH) != 0, Is.False);
            Assert.That((int)SetOps.Count(compiler, blocks[2].bbVarDef), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, blocks[2].bbLiveIn), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, blocks[2].bbLiveOut), Is.Zero);
            Assert.That(compiler.fgStmtRemoved, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RunRepeatsGenerationAndBackwardPassUntilPredecessorStoresDisappear(bool minopts)
    {
        WithCompiler(2, minopts, compiler => {
            var (blocks, firstStore, secondStore, lastUse) = DeadChain(compiler);
            var liveness = new Liveness<PostLowerPolicy>(compiler);
            liveness.RunLIR();

            foreach (var block in blocks)
            {
                Assert.That(block.FirstNode, Is.Null);
                Assert.That(block.LastNode, Is.Null);
                Assert.That((int)SetOps.Count(compiler, block.bbLiveIn), Is.Zero);
                Assert.That((int)SetOps.Count(compiler, block.bbLiveOut), Is.Zero);
            }
            Assert.That(firstStore.Prev, Is.Null);
            Assert.That(secondStore.Prev, Is.Null);
            Assert.That(lastUse.Prev, Is.Null);
            Assert.That(compiler.fgStmtRemoved, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.EqualTo(3));
            Assert.That(compiler.lvaTable[1].lvRefCnt(), Is.EqualTo(3));

            liveness.RunLIR();
            Assert.That(compiler.fgStmtRemoved, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AsyncPolicyKeepsUnusedReferencesAndTracksAddressExposedLocals(bool minopts)
    {
        WithCompiler(1, minopts, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = Blocks(compiler, 1)[0];
            var use = new GenTreeLclVar(TYP_INT, 0) { IsUnusedValue = true };
            Append(compiler, block, use);
            SetDfs(compiler);

            new Liveness<AsyncPolicy>(compiler).RunLIR();

            Assert.That(compiler.lvaTable[0].lvTracked, Is.True);
            Assert.That(block.FirstNode, Is.SameAs(use));
            Assert.That((use.Flags & GTF_VAR_DEATH) != 0, Is.True);
            Assert.That((int)SetOps.Count(compiler, block.bbLiveIn), Is.EqualTo(1));
            Assert.That(compiler.fgStmtRemoved, Is.False);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void EhKeepaliveAndInitializationAreRecomputedAfterHandlerDce(bool minopts, bool unusedHandler)
    {
        WithCompiler(1, minopts, compiler => {
            compiler.lvaTable[0].Type = TYP_REF;
            var blocks = Blocks(compiler, 3);
            var body = blocks[0];
            var exit = blocks[1];
            var handler = blocks[2];
            Connect(body, exit);
            body.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = (bbCatchType)1;
            compiler.compHndBBtab = [Clause(EH_HANDLER_CATCH, body, handler)];
            compiler.compHndBBtabCount = 1;
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_REF, 0));
            Append(compiler, body, store);
            var use = new GenTreeLclVar(TYP_REF, 0) { IsUnusedValue = unusedHandler };
            Append(compiler, handler, unusedHandler ? use : new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, use));
            SetDfs(compiler);

            new Liveness<PostLowerPolicy>(compiler).RunLIR();

            Assert.That(body.LastNode, unusedHandler ? Is.Null : Is.SameAs(store));
            Assert.That((int)SetOps.Count(compiler, body.bbLiveIn), Is.EqualTo(unusedHandler ? 0 : 1));
            Assert.That((int)SetOps.Count(compiler, body.bbLiveOut), Is.EqualTo(unusedHandler ? 0 : 1));
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.EqualTo(!unusedHandler));
            Assert.That(compiler.lvaTable[0]._lvLiveInOutOfHandler, Is.EqualTo(!unusedHandler));
            Assert.That((store.Flags & GTF_VAR_DEATH) != 0, Is.EqualTo(unusedHandler));
            Assert.That(compiler.fgStmtRemoved, Is.EqualTo(unusedHandler));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FinallyExitGcVariablesRequireInitializationUnlessParameters(bool parameter)
    {
        WithCompiler(1, false, compiler => {
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[0].lvIsParam = parameter;
            compiler.info.compArgsCount = parameter ? 1 : 0;
            var blocks = Blocks(compiler, 4);
            var call = blocks[0];
            var tail = blocks[1];
            var handler = blocks[2];
            var exit = blocks[3];
            call.TryIndex = tail.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = BBCT_FINALLY;
            call.SetKindAndTargetEdge(BBJ_CALLFINALLY, new FlowEdge(call, handler, null));
            tail.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, new FlowEdge(tail, exit, null));
            handler.Kind = BBJ_EHFINALLYRET;
            handler.bbEhfTargets = new BBJumpTable([new FlowEdge(handler, tail, null)]);
            compiler.compHndBBtab = [Clause(EH_HANDLER_FINALLY, call, handler)];
            compiler.compHndBBtab[0].ebdTryLast = tail;
            compiler.compHndBBtabCount = 1;
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_REF, 0));
            Append(compiler, handler, store);
            Append(compiler, exit, new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, new GenTreeLclVar(TYP_REF, 0)));
            SetDfs(compiler);

            new Liveness<PostLowerPolicy>(compiler).RunLIR();

            Assert.That((int)SetOps.Count(compiler, call.bbLiveIn), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, handler.bbLiveOut), Is.EqualTo(1));
            Assert.That(handler.LastNode, Is.SameAs(store));
            Assert.That(compiler.lvaTable[0]._lvLiveInOutOfHandler, Is.True);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.EqualTo(!parameter));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MemoryPolicyConvergesAcrossCyclesAndPreservesLocalFlags(bool minopts)
    {
        WithCompiler(1, minopts, compiler => {
            var blocks = Blocks(compiler, 2);
            Connect(blocks[0], blocks[1]);
            Connect(blocks[1], blocks[0]);
            Append(compiler, blocks[0], new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, new GenTreeLclVar(TYP_INT, 0)));
            Append(compiler, blocks[1], new GenTreeIndir(GT_IND, TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 4096)));
            SetDfs(compiler);

            new Liveness<MemoryPolicy>(compiler).RunLIR();

            foreach (var block in blocks)
            {
                Assert.That((int)SetOps.Count(compiler, block.bbLiveIn), Is.EqualTo(1));
                Assert.That((int)SetOps.Count(compiler, block.bbLiveOut), Is.EqualTo(1));
                Assert.That(block.bbMemoryLiveIn, Is.EqualTo(3));
                Assert.That(block.bbMemoryLiveOut, Is.EqualTo(3));
            }
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.fgStmtRemoved, Is.False);
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public static void UnsupportedPoliciesRejectBeforeInitializationDiagnosticsOrPublication(int mode, bool run)
    {
        WithCompiler(1, false, compiler => {
            var blocks = Blocks(compiler, 1);
            compiler.compCurBB = blocks[0];
            compiler.fgStmtRemoved = true;
            var epoch = compiler.CurLVEpoch;
            var phase = compiler.mostRecentlyActivePhase;
#if DEBUG
            compiler.verbose = true;
            var output = Capture(Act);
            Assert.That(output, Is.Empty);
#else
            Act();
#endif
            Assert.That(compiler.fgBBVarSetsInited, Is.False);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.False);
            Assert.That(compiler.fgStmtRemoved, Is.True);
            Assert.That(compiler.lvaTrackedCount, Is.Zero);
            Assert.That(compiler.CurLVEpoch, Is.EqualTo(epoch));
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(phase));
            Assert.That(compiler.compCurBB, Is.SameAs(blocks[0]));

            void Act()
            {
                switch (mode)
                {
                    case 0:
                    {
                        Reject<HirPolicy>();
                        break;
                    }
                    case 1:
                    {
                        Reject<EarlyPolicy>();
                        break;
                    }
                    default:
                    {
                        Reject<ContradictoryPolicy>();
                        break;
                    }
                }
            }

            void Reject<TPolicy>()
                where TPolicy : ILivenessPolicy
            {
                var liveness = new Liveness<TPolicy>(compiler);
                if (run)
                {
                    Assert.Throws<NotSupportedException>(liveness.RunLIR);
                }
                else
                {
                    Assert.Throws<NotSupportedException>(liveness.InterBlockLocalVarLivenessLIR);
                }
            }
        });
    }

#if DEBUG
    [Test]
    public static void ChangedLiveInWithoutNodeRemovalDoesNotRepeat()
    {
        WithCompiler(1, true, compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            compiler.info.compLvFrameListRoot = 0;
            var block = Blocks(compiler, 1)[0];
            SetDfs(compiler);
            compiler.verbose = true;

            var text = Capture(new Liveness<PostLowerPolicy>(compiler).RunLIR);

            Assert.That((int)SetOps.Count(compiler, block.bbVarUse), Is.EqualTo(1));
            Assert.That((int)SetOps.Count(compiler, block.bbLiveIn), Is.Zero);
            Assert.That(Occurrences(text, "In Liveness::PerBlockLocalVarLiveness()"), Is.EqualTo(1));
            Assert.That(Occurrences(text, "IngInterBlockLocalVarLiveness()"), Is.EqualTo(1));
            Assert.That(compiler.fgStmtRemoved, Is.False);
        });
    }

    [Test]
    public static void NativeRepeatConditionRequiresBothRemovalAndChangedLiveIn()
    {
        WithCompiler(2, false, compiler => {
            _ = DeadChain(compiler);
            compiler.verbose = true;
            var text = Capture(new Liveness<PostLowerPolicy>(compiler).RunLIR);
            Assert.That(Occurrences(text, "In Liveness::Run()"), Is.EqualTo(1));
            Assert.That(Occurrences(text, "Initial local variable assignments"), Is.EqualTo(1));
            Assert.That(Occurrences(text, "In Liveness::Init"), Is.EqualTo(1));
            Assert.That(Occurrences(text, "In Liveness::PerBlockLocalVarLiveness()"), Is.EqualTo(3));
            Assert.That(Occurrences(text, "IngInterBlockLocalVarLiveness()"), Is.EqualTo(3));
            Assert.That(compiler.fgStmtRemoved, Is.True);
        });
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var position = text.IndexOf(value, StringComparison.Ordinal); position >= 0;
            position = text.IndexOf(value, position + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    private static (BasicBlock[] Blocks, GenTree FirstStore, GenTree SecondStore, GenTree LastUse) DeadChain(Compiler compiler)
    {
        var blocks = Blocks(compiler, 3);
        Connect(blocks[0], blocks[2]);
        Connect(blocks[2], blocks[1]);
        var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
        var second = compiler.gtNewStoreLclVarNode(1, new GenTreeLclVar(TYP_INT, 0));
        var last = new GenTreeLclVar(TYP_INT, 1) { IsUnusedValue = true };
        Append(compiler, blocks[0], first);
        Append(compiler, blocks[2], second);
        Append(compiler, blocks[1], last);
        SetDfs(compiler);

        return (blocks, first, second, last);
    }

    private static EHblkDsc Clause(EHHandlerType kind, BasicBlock body, BasicBlock handler) => new() {
        ebdHandlerType = kind,
        ebdTryBeg = body,
        ebdTryLast = body,
        ebdHndBeg = handler,
        ebdHndLast = handler,
        ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
    };

    private static void SetDfs(Compiler compiler)
    {
        compiler._dfsTree = compiler.fgComputeDfs(false);
    }

    private static void Connect(BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(source, target, null));
    }

    private static BasicBlock[] Blocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index] = block;
            block.MakeLir(null, null);
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

    private static void Append(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = new Statement(root, 0);
        compiler.fgSetStmtSeq(statement);
        var nodes = statement.TreeList.ToArray();
        foreach (var node in nodes)
        {
            node.Prev = null;
            node.Next = null;
        }
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
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
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.compHndBBtab = [];
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        MaxLocalsToTrack(ref JitConfig) = 1024;
        try
        {
            for (var local = 0; local < count; local++)
            {
                compiler.lvaTable[local].Type = TYP_INT;
                compiler.lvaTable[local].setLvRefCnt(3);
                compiler.lvaTable[local].setLvRefCntWtd(local + 1);
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
