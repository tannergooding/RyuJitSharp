// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Reflection;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EarlyLivenessTests
{
    private readonly struct UnsupportedTreePolicy : ILivenessPolicy
    {
    }

    private readonly struct UnsupportedSsaPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
        public static bool SsaLiveness => true;
        public static bool EliminateDeadCode => true;
    }

    [Test]
    public static void MinOptsDoesNotMutateTheGraphOrPublishEarlyLiveness()
    {
        WithCompiler(1, minOpts: true, compiler => {
            var block = AddBlock(compiler);
            var statement = AddStatement(compiler, block, Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1)));

            Assert.That(compiler.fgEarlyLiveness(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(compiler.fgDidEarlyLiveness, Is.False);
            Assert.That(compiler.fgBBVarSetsInited, Is.False);
        });
    }

    [Test]
    public static void DeadStoresAcrossBlocksRepeatUntilPredecessorStoresDisappear()
    {
        WithCompiler(2, minOpts: false, compiler => {
            var first = AddBlock(compiler);
            var last = AddBlock(compiler);
            var firstStore = AddStatement(compiler, first, Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1)));
            var lastStore = AddStatement(compiler, last, Store(compiler, 1, new GenTreeLclVar(TYP_INT, 0)));
            SetDfs(compiler);

            Assert.That(compiler.fgEarlyLiveness(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(first.FirstStmt, Is.Null);
            Assert.That(last.FirstStmt, Is.Null);
            Assert.That(firstStore.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(lastStore.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(compiler.fgStmtRemoved, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.fgDidEarlyLiveness, Is.True);
            Assert.That(compiler.compCurStmt, Is.Null);
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_LCLVARLIVENESS_INTERBLOCK));
            Assert.That((int)SetOps.Count(compiler, first.bbLiveIn), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, last.bbLiveIn), Is.Zero);
        });
    }

    [Test]
    public static void DeadStoreRetainsItsSideEffectsAndResequencesLocals()
    {
        WithCompiler(1, minOpts: false, compiler => {
            var block = AddBlock(compiler);
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var statement = AddStatement(compiler, block, Store(compiler, 0, call));
            SetDfs(compiler);

            Assert.That(compiler.fgEarlyLiveness(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(statement.RootNode, Is.SameAs(call));
            Assert.That(statement.TreeListEnd, Is.Null);
            Assert.That(compiler.compCurStmt, Is.Null);
            Assert.That(compiler.lvaTable[0].lvTracked, Is.True);
            Assert.That(compiler.fgDidEarlyLiveness, Is.True);
        });
    }

    [Test]
    public static void ConditionalQmarkDefinitionsDoNotKillUnconditionalUses()
    {
        WithCompiler(3, minOpts: false, compiler => {
            var block = AddBlock(compiler);
            compiler.compQmarkUsed = true;
            var conditionalDef = Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1));
            var elseUse = new GenTreeLclVar(TYP_INT, 0);
            var conditional = new GenTreeQmark(TYP_INT, new GenTreeLclVar(TYP_INT, 2),
                new GenTreeColon(TYP_INT, conditionalDef, elseUse));
            var root = Store(compiler, 1, conditional);
            var statement = AddStatement(compiler, block, root);
            _ = AddStatement(compiler, block, new GenTreeLclVar(TYP_INT, 1));
            SetDfs(compiler);

            Assert.That(compiler.fgEarlyLiveness(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(statement.RootNode, Is.SameAs(root));
            Assert.That(elseUse.Flags & GTF_VAR_DEATH, Is.Not.EqualTo(0));
            Assert.That(SetOps.IsMember(compiler, block.bbLiveIn, compiler.lvaTable[0]._varIndex), Is.True);
            Assert.That(SetOps.IsMember(compiler, block.bbLiveIn, compiler.lvaTable[2]._varIndex), Is.True);
        });
    }

    [Test]
    public static void HandlerUsesKeepStoresInTheTryBlockAlive()
    {
        WithCompiler(1, minOpts: false, compiler => {
            compiler.lvaTable[0].Type = TYP_REF;
            var body = AddBlock(compiler);
            var exit = AddBlock(compiler);
            var handler = AddBlock(compiler);
            exit.SetKindAndTargetEdge(BBJ_RETURN, null);
            body.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = (bbCatchType)1;
            compiler.compHndBBtab = [new EHblkDsc {
                ebdHandlerType = EH_HANDLER_CATCH,
                ebdTryBeg = body,
                ebdTryLast = body,
                ebdHndBeg = handler,
                ebdHndLast = handler,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            var store = AddStatement(compiler, body, Store(compiler, 0, compiler.gtNewIconNode(TYP_REF, 0)));
            _ = AddStatement(compiler, handler, new GenTreeLclVar(TYP_REF, 0));
            SetDfs(compiler);

            Assert.That(compiler.fgEarlyLiveness(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(body.FirstStmt, Is.SameAs(store));
            Assert.That(compiler.lvaTable[0]._lvLiveInOutOfHandler, Is.True);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.True);
            Assert.That((int)SetOps.Count(compiler, body.bbLiveOut), Is.EqualTo(1));
        });
    }

#if DEBUG
    [Test]
    public static void DebugMethodRangeCanExcludeOptimizedEarlyLiveness()
    {
        WithCompiler(1, minOpts: false, compiler => {
            var block = AddBlock(compiler);
            var statement = AddStatement(compiler, block, Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1)));
            var field = typeof(Compiler).GetField("s_jitEnableEarlyLivenessRange",
                BindingFlags.NonPublic | BindingFlags.Static) ??
                throw new InvalidOperationException("Early liveness method range is missing.");
            var previous = field.GetValue(null);
            var excludedHash = unchecked((uint)(compiler.info.compMethodHash() ^ 1));
            var encodedRange = Encoding.ASCII.GetBytes($"{excludedHash:x8}\0");
            var range = new ConfigMethodRange();
            fixed (byte* text = encodedRange)
            {
                range.EnsureInit(text);
            }

            try
            {
                field.SetValue(null, range);
                Assert.That(compiler.fgEarlyLiveness(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
                Assert.That(block.FirstStmt, Is.SameAs(statement));
                Assert.That(compiler.fgDidEarlyLiveness, Is.False);
                Assert.That(compiler.fgBBVarSetsInited, Is.False);
            }
            finally
            {
                field.SetValue(null, previous);
            }
        });
    }

    [Test]
    public static void DeadStorePropagationRepeatsTheNativePhaseSequence()
    {
        WithCompiler(2, minOpts: false, compiler => {
            var first = AddBlock(compiler);
            var last = AddBlock(compiler);
            _ = AddStatement(compiler, first, Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1)));
            _ = AddStatement(compiler, last, Store(compiler, 1, new GenTreeLclVar(TYP_INT, 0)));
            SetDfs(compiler);
            compiler.verbose = true;

            var text = CodeGenLifeTransitionTests.Capture(() => compiler.fgEarlyLiveness());

            Assert.That(Occurrences(text, "In Liveness::Run()"), Is.EqualTo(1));
            Assert.That(Occurrences(text, "In Liveness::Init"), Is.EqualTo(1));
            Assert.That(Occurrences(text, "In Liveness::PerBlockLocalVarLiveness()"), Is.EqualTo(2));
            Assert.That(Occurrences(text, "IngInterBlockLocalVarLiveness()"), Is.EqualTo(2));
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

#endif

    [Test]
    public static void EarlyTreeDriverRejectsOtherTreeAndSsaPoliciesBeforeMutation()
    {
        WithCompiler(1, minOpts: false, compiler => {
            _ = Assert.Throws<NotSupportedException>(new Liveness<UnsupportedTreePolicy>(compiler).RunEarly);
            _ = Assert.Throws<NotSupportedException>(new Liveness<UnsupportedSsaPolicy>(compiler).RunEarly);
            Assert.That(compiler.fgBBVarSetsInited, Is.False);
        });
    }

    private static GenTreeLclVar Store(Compiler compiler, int local, GenTree value) =>
        compiler.gtNewStoreLclVarNode(local, value);

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
            block.Prev = previous;
            previous.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(previous, block, null));
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;
        return block;
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = new Statement(root, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.fgSequenceLocals(statement);
        return statement;
    }

    private static void SetDfs(Compiler compiler)
    {
        compiler._dfsTree = compiler.fgComputeDfs();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

    private static void WithCompiler(int count, bool minOpts, Action<Compiler> action)
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
#if DEBUG
        compiler.info.compFullName = nameof(EarlyLivenessTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.AllLocals;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        JitConfig = new JitConfigValues();
        MaxLocalsToTrack(ref JitConfig) = 1024;
        JitTls.Compiler = compiler;
        try
        {
            for (var local = 0; local < count; local++)
            {
                compiler.lvaTable[local].Type = TYP_INT;
                compiler.lvaTable[local].setLvRefCnt(3);
                compiler.lvaTable[local].setLvRefCntWtd(3);
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
