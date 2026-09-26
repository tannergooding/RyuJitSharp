// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
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
internal static unsafe class SsaLivenessTests
{
    private readonly struct UnsupportedPolicy : ILivenessPolicy
    {
    }

    private readonly struct EarlyPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
        public static bool EliminateDeadCode => true;
    }

    private readonly struct LirPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
    }

    [Test]
    public static void DeadStoresRepeatAcrossBlocksAndKeepTheNativePhaseOrder()
    {
        WithCompiler(2, compiler => {
            var first = AddBlock(compiler);
            var last = AddBlock(compiler);
            _ = AddStatement(compiler, first, Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1)));
            _ = AddStatement(compiler, last, Store(compiler, 1, new GenTreeLclVar(TYP_INT, 0)));
            SetDfs(compiler);
#if DEBUG
            compiler.verbose = true;
            var text = CodeGenLifeTransitionTests.Capture(compiler.fgSsaLiveness);
            Assert.That(Occurrences(text, "In Liveness::PerBlockLocalVarLiveness()"), Is.EqualTo(2));
            Assert.That(Occurrences(text, "IngInterBlockLocalVarLiveness()"), Is.EqualTo(2));
            Assert.That(Occurrences(text, "top level store"), Is.EqualTo(2));
            Assert.That(Occurrences(text, "removing stmt with no side effects"), Is.EqualTo(2));
            Assert.That(text, Does.Not.Contain("fgComputeLife modified tree:"));
#else
#if DEBUG
            compiler.verbose = true;
            var text = CodeGenLifeTransitionTests.Capture(compiler.fgSsaLiveness);
            Assert.That(Ordered(text, "Dead store has side effects...", "top level store",
                "Extracted side effects list...", "fgComputeLife modified tree:"), Is.True);
            Assert.That(Occurrences(text, "fgComputeLife modified tree:"), Is.EqualTo(1));
#else
            compiler.fgSsaLiveness();
#endif
#endif
            Assert.That(first.FirstStmt, Is.Null);
            Assert.That(last.FirstStmt, Is.Null);
            Assert.That(compiler.fgStmtRemoved, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_LCLVARLIVENESS_INTERBLOCK));
            Assert.That((int)SetOps.Count(compiler, first.bbLiveIn), Is.Zero);
        });
    }

    [Test]
    public static void TopLevelDeadStoreKeepsAndAnalyzesCallSideEffects()
    {
        WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            var call = IndirectCall(compiler);
            var statement = AddStatement(compiler, block, Store(compiler, 0, call));
            SetDfs(compiler);

            compiler.fgSsaLiveness();

            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(statement.RootNode, Is.SameAs(call));
            Assert.That(statement.TreeListBegin, Is.SameAs(call.ControlExpr));
            Assert.That(call.Next, Is.Null);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InteriorDeadStoreReplacesItsOwnerAndPreservesEffects(bool hasSideEffects)
    {
        WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            var call = IndirectCall(compiler);
            var store = Store(compiler, 0, hasSideEffects ? call : compiler.gtNewIconNode(TYP_INT, 1));
            var root = compiler.gtNewCommaNode(TYP_VOID, store, compiler.gtNewNothingNode());
            var statement = AddStatement(compiler, block, root);
            store.Flags |= GTF_DONT_CSE | GTF_REVERSE_OPS;
            store.AssertionInfo = new AssertionInfo(1);
            store._vnPair.SetBoth(73);
#if DEBUG
            var treeId = store.TreeId;
            compiler.verbose = true;
#endif
            SetDfs(compiler);

#if DEBUG
            var text = CodeGenLifeTransitionTests.Capture(compiler.fgSsaLiveness);
            if (hasSideEffects)
            {
                Assert.That(Ordered(text, "Dead store has side effects...",
                    "Extracted side effects list from condition...", "fgComputeLife modified tree:"), Is.True);
            }
            else
            {
                Assert.That(Ordered(text, $"Removing tree [{treeId:D6}]",
                    " as useless", "fgComputeLife modified tree:"), Is.True);
            }
            Assert.That(Occurrences(text, "fgComputeLife modified tree:"), Is.EqualTo(1));
#else
            compiler.fgSsaLiveness();
#endif

            Assert.That(block.FirstStmt, Is.SameAs(statement));
            var replacement = root.Op1;
            Assert.That(replacement.Oper, Is.EqualTo(hasSideEffects ? GT_COMMA : GT_NOP));
            Assert.That(replacement.Type, Is.EqualTo(TYP_VOID));
            Assert.That(replacement.Flags & GTF_DONT_CSE, Is.Not.Zero);
            Assert.That(replacement.AssertionInfo.AssertionIndex, Is.EqualTo((ushort)1));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(treeId));
#endif
            Assert.That(statement.RootNode, Is.SameAs(root));
            Assert.That(root.Next, Is.Null);
            Assert.That(statement.TreeListBegin, Is.Not.Null);
            if (hasSideEffects)
            {
                Assert.That(replacement, Is.Not.SameAs(store));
                Assert.That(replacement.AsOp().Op1, Is.SameAs(call));
                Assert.That(replacement.Flags & GTF_CALL, Is.Not.Zero);
                Assert.That(replacement.Flags & GTF_REVERSE_OPS, Is.Not.Zero);
                Assert.That(replacement.Flags & GTF_VAR_DEF, Is.Not.Zero);
            }
            else
            {
                Assert.That(replacement, Is.SameAs(store));
                Assert.That(replacement.Flags & (GTF_REVERSE_OPS | GTF_VAR_DEF | GTF_ALL_EFFECT),
                    Is.EqualTo(GTF_EMPTY));
            }
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
        });
    }

    [Test]
    public static void InteriorSideEffectReplacementUpdatesAllAliasedOwningEdges()
    {
        WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            var call = IndirectCall(compiler);
            var store = Store(compiler, 0, call);
            var firstOwner = compiler.gtNewCommaNode(TYP_VOID, store, compiler.gtNewNothingNode());
            var secondOwner = compiler.gtNewCommaNode(TYP_VOID, store, compiler.gtNewNothingNode());
            var root = compiler.gtNewCommaNode(TYP_VOID, firstOwner, secondOwner);
            var statement = AddStatement(compiler, block, root);
            SetDfs(compiler);
#if DEBUG
            var treeId = store.TreeId;
#endif

            compiler.fgSsaLiveness();

            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(firstOwner.Op1, Is.SameAs(secondOwner.Op1));
            Assert.That(firstOwner.Op1, Is.Not.SameAs(store));
            Assert.That(firstOwner.Op1.Oper, Is.EqualTo(GT_COMMA));
#if DEBUG
            Assert.That(firstOwner.Op1.TreeId, Is.EqualTo(treeId));
#endif
        });
    }

    [Test]
    public static void LivePartialDefinitionRemainsAUseAcrossBlocks()
    {
        WithCompiler(1, compiler => {
            var first = AddBlock(compiler);
            var last = AddBlock(compiler);
            var store = Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1));
            store.Flags |= GTF_VAR_USEASG;
            var statement = AddStatement(compiler, first, store);
            _ = AddStatement(compiler, last, new GenTreeLclVar(TYP_INT, 0));
            SetDfs(compiler);

            compiler.fgSsaLiveness();

            Assert.That(first.FirstStmt, Is.SameAs(statement));
            Assert.That(SetOps.IsMember(compiler, first.bbLiveIn, compiler.lvaTable[0]._varIndex), Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
        });
    }

    [Test]
    public static void AddressExposedLocalKeepsMemoryLivenessInsteadOfRemovingItsStore()
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = AddBlock(compiler);
            var statement = AddStatement(compiler, block, Store(compiler, 0, compiler.gtNewIconNode(TYP_INT, 1)));
            SetDfs(compiler);

            compiler.fgSsaLiveness();

            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(compiler.lvaTable[0].lvTracked, Is.False);
            Assert.That(block.bbMemoryDef & 1, Is.Not.Zero);
            Assert.That(compiler.byrefStatesMatchGcHeapStates, Is.False);
        });
    }

    [Test]
    public static void HandlerKeepAlivePreventsRemovingTryBlockStores()
    {
        WithCompiler(1, compiler => {
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
            var statement = AddStatement(compiler, body, Store(compiler, 0, compiler.gtNewIconNode(TYP_REF, 0)));
            _ = AddStatement(compiler, handler, new GenTreeLclVar(TYP_REF, 0));
            SetDfs(compiler);

            compiler.fgSsaLiveness();

            Assert.That(body.FirstStmt, Is.SameAs(statement));
            Assert.That(compiler.lvaTable[0]._lvLiveInOutOfHandler, Is.True);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.True);
            Assert.That((int)SetOps.Count(compiler, body.bbLiveOut), Is.EqualTo(1));
        });
    }

    [Test]
    public static void OtherPoliciesCannotEnterSsaTreeOrchestration()
    {
        WithCompiler(1, compiler => {
            _ = Assert.Throws<NotSupportedException>(new Liveness<UnsupportedPolicy>(compiler).RunSsa);
            _ = Assert.Throws<NotSupportedException>(new Liveness<EarlyPolicy>(compiler).RunSsa);
            _ = Assert.Throws<NotSupportedException>(new Liveness<LirPolicy>(compiler).RunSsa);
            Assert.That(compiler.fgBBVarSetsInited, Is.False);
        });
    }

#if DEBUG
    private static bool Ordered(string text, params string[] fragments)
    {
        var position = -1;
        foreach (var fragment in fragments)
        {
            position = text.IndexOf(fragment, position + 1, StringComparison.Ordinal);
            if (position < 0)
            {
                return false;
            }
        }
        return true;
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

    private static GenTreeLclVar Store(Compiler compiler, int local, GenTree value) =>
        compiler.gtNewStoreLclVarNode(local, value);

    private static GenTreeCall IndirectCall(Compiler compiler)
    {
        var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_INDIRECT, null);
        call._controlExpr = compiler.gtNewIconNode(TYP_I_IMPL, 1);
        return call;
    }

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
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        return statement;
    }

    private static void SetDfs(Compiler compiler)
    {
        compiler._dfsTree = compiler.fgComputeDfs();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

    private static void WithCompiler(int count, Action<Compiler> action)
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
        compiler.info.compFullName = nameof(SsaLivenessTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
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
