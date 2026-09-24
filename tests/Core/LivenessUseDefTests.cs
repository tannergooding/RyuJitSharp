// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LivenessUseDefTests
{
    private readonly struct LirPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool ComputeMemoryLiveness => true;
    }

    private readonly struct SsaPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool SsaLiveness => true;
        public static bool ComputeMemoryLiveness => true;
    }

    private readonly struct HirPolicy : ILivenessPolicy
    {
        public static bool ComputeMemoryLiveness => true;
    }

    private readonly struct EarlyPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
    }

    private readonly struct ExposedPolicy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool TrackAddressExposedLocals => true;
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(65, false)]
    [TestCase(65, true)]
    public static void PerBlockUsesDfsOrderAndIndependentSetsWithoutSolvingDataflow(int count, bool minopts)
    {
        WithCompiler(count, minopts, compiler => {
            var first = Block(compiler);
            var second = Block(compiler);
            var third = Block(compiler);
            first.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(first, third, null));
            third.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(third, second, null));
            second.SetKindAndTargetEdge(BBJ_RETURN, null);
            Append(compiler, first, compiler.gtNewIconNode(TYP_INT, 123));
            for (var local = 0; local < count; local++)
            {
                Append(compiler, first, new GenTreeLclVar(TYP_INT, local));
                Append(compiler, second, Store(compiler, local));
                Append(compiler, second, new GenTreeLclVar(TYP_INT, local));
            }
            var liveness = Prepare<LirPolicy>(compiler);
            first.bbLiveOut = SetOps.MakeFull(compiler);
            var liveOut = first.bbLiveOut;
            first.bbLiveIn = SetOps.MakeFull(compiler);
            first.bbMemoryLiveIn = first.bbMemoryLiveOut = 3;
            var epoch = compiler.CurLVEpoch;

            liveness.PerBlockLocalVarLiveness();

            Assert.That((int)SetOps.Count(compiler, first.bbVarUse), Is.EqualTo(count));
            Assert.That((int)SetOps.Count(compiler, first.bbVarDef), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, second.bbVarUse), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, second.bbVarDef), Is.EqualTo(count));
            Assert.That((int)SetOps.Count(compiler, third.bbVarUse), Is.Zero);
            Assert.That((int)SetOps.Count(compiler, third.bbVarDef), Is.Zero);
            Assert.That(first.bbLiveIn, Has.All.EqualTo((nint)0));
            Assert.That(first.bbLiveOut, Is.SameAs(liveOut));
            Assert.That((int)SetOps.Count(compiler, first.bbLiveOut), Is.EqualTo(count));
            Assert.That(first.bbMemoryLiveIn, Is.Zero);
            Assert.That(first.bbMemoryLiveOut, Is.EqualTo(3));
            Assert.That(compiler.compCurBB, Is.SameAs(second));
            Assert.That(compiler.CurLVEpoch, Is.EqualTo(epoch));
            Assert.That(compiler.fgLocalVarLivenessDone, Is.False);
            Assert.That(compiler.fgStmtRemoved, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PartialDefinitionsHaveNativeSsaAndUseBeforeDefSemantics(bool ssa, bool priorDef)
    {
        WithCompiler(1, false, compiler => {
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var block = Block(compiler);
            if (priorDef)
            {
                Append(compiler, block, Store(compiler, 0));
            }
            var partial = new GenTreeLclFld(TYP_INT, 0, 0, compiler.gtNewIconNode(TYP_INT, 7), null);
            partial.Flags |= GTF_VAR_DEF | GTF_VAR_USEASG;
            Append(compiler, block, partial);
            if (ssa)
            {
                Prepare<SsaPolicy>(compiler).PerBlockLocalVarLiveness();
            }
            else
            {
                Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();
            }

            AssertLocal(compiler, block, 0, ssa && !priorDef, ssa || priorDef);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void AddressExposedLocalsTrackByrefMemoryOrPolicySelectedVariables(bool tracked, bool definition)
    {
        WithCompiler(1, false, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = Block(compiler);
            Append(compiler, block, definition ? Store(compiler, 0) : new GenTreeLclVar(TYP_INT, 0));
            if (tracked)
            {
                var liveness = Prepare<ExposedPolicy>(compiler);
                block.bbMemoryUse = 2;
                block.bbMemoryDef = 3;
                block.bbMemoryHavoc = 1;
                block.bbMemoryLiveIn = 3;
                liveness.PerBlockLocalVarLiveness();

                AssertLocal(compiler, block, 0, !definition, definition);
                Assert.That(block.bbMemoryUse, Is.EqualTo(2));
                Assert.That(block.bbMemoryDef, Is.EqualTo(3));
                Assert.That(block.bbMemoryHavoc, Is.EqualTo(1));
                Assert.That(block.bbMemoryLiveIn, Is.EqualTo(3));
                Assert.That(compiler.byrefStatesMatchGcHeapStates, Is.True);
            }
            else
            {
                Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

                Assert.That(compiler.lvaTable[0].lvTracked, Is.False);
                Assert.That(block.bbMemoryUse, Is.EqualTo(definition ? 0 : 1));
                Assert.That(block.bbMemoryDef, Is.EqualTo(definition ? 1 : 0));
                Assert.That(block.bbMemoryHavoc, Is.Zero);
                Assert.That(compiler.byrefStatesMatchGcHeapStates, Is.EqualTo(!definition));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PromotedParentReferencesPropagateToTrackedFields(bool dependent, bool definition)
    {
        WithCompiler(4, false, compiler => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvDoNotEnregister = dependent;
            parent.lvFieldLclStart = 1;
            parent.lvFieldCnt = 3;
            parent.setLvRefCnt(0);
            compiler.lvaTable[3].lvPinned = true;
            var block = Block(compiler);
            var reference = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            if (definition)
            {
                reference.Flags |= GTF_VAR_DEF;
            }
            Append(compiler, block, reference);

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            Assert.That(compiler.lvaTable[0].lvTracked, Is.False);
            Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.Zero);
            Assert.That(compiler.lvaTable[3].lvTracked, Is.False);
            AssertLocal(compiler, block, 1, !definition, definition);
            AssertLocal(compiler, block, 2, !definition, definition);
        });
    }

    [TestCase(GT_IND, false, 3, 0, 0)]
    [TestCase(GT_IND, true, 3, 3, 0)]
    [TestCase(GT_BLK, false, 3, 0, 0)]
    [TestCase(GT_BLK, true, 3, 3, 0)]
    [TestCase(GT_STOREIND, false, 0, 3, 0)]
    [TestCase(GT_STORE_BLK, false, 0, 3, 0)]
    [TestCase(GT_MEMORYBARRIER, false, 0, 3, 0)]
    [TestCase(GT_LOCKADD, false, 3, 3, 3)]
    [TestCase(GT_XORR, false, 3, 3, 3)]
    [TestCase(GT_XAND, false, 3, 3, 3)]
    [TestCase(GT_XADD, false, 3, 3, 3)]
    [TestCase(GT_XCHG, false, 3, 3, 3)]
    [TestCase(GT_CMPXCHG, false, 3, 3, 3)]
    public static void MemoryNodesPreserveNativeUseDefAndHavoc(genTreeOps oper, bool isVolatile, int use, int def, int havoc)
    {
        WithCompiler(0, true, compiler => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 4096);
            var data = compiler.gtNewIconNode(TYP_INT, 1);
            var node = oper switch {
                GT_BLK => new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(16)),
                GT_STORE_BLK => new GenTreeBlk(TYP_STRUCT, address, data, new ClassLayout(16)),
                GT_STOREIND => new GenTreeStoreInd(TYP_INT, address, data),
                GT_MEMORYBARRIER => new GenTree(GT_MEMORYBARRIER, TYP_VOID),
                GT_CMPXCHG => new GenTreeCmpXchg(TYP_INT, address, data, compiler.gtNewIconNode(TYP_INT, 0)),
                GT_IND => new GenTreeIndir(GT_IND, TYP_INT, address),
                _ => new GenTreeOp(oper, oper is GT_LOCKADD ? TYP_VOID : TYP_INT, address, data),
            };
            if (isVolatile)
            {
                node.Flags |= GTF_IND_VOLATILE;
            }
            var block = Block(compiler);
            Append(compiler, block, node);

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            Assert.That(block.bbMemoryUse, Is.EqualTo(use));
            Assert.That(block.bbMemoryDef, Is.EqualTo(def));
            Assert.That(block.bbMemoryHavoc, Is.EqualTo(havoc));
            Assert.That(compiler.byrefStatesMatchGcHeapStates, Is.True);
        });
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_UNDEF, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_LLSH, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_ASSIGN_REF, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_INITCLASS, true)]
    public static void CallsUseHelperHeapAndClassConstructorProperties(CorInfoHelpFunc helper, bool modifiesMemory)
    {
        WithCompiler(0, false, compiler => {
            var call = helper is CorInfoHelpFunc.CORINFO_HELP_UNDEF
                ? compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null)
                : compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_HELPER, Compiler.eeFindHelper(helper));
            var block = Block(compiler);
            Append(compiler, block, call);

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            Assert.That(block.bbMemoryUse, Is.EqualTo(modifiesMemory ? 3 : 0));
            Assert.That(block.bbMemoryDef, Is.EqualTo(modifiesMemory ? 3 : 0));
            Assert.That(block.bbMemoryHavoc, Is.EqualTo(modifiesMemory ? 3 : 0));
        });
    }

    [TestCase(NamedIntrinsic.NI_X86Base_LoadAlignedVector128, 3, 0)]
    [TestCase(NamedIntrinsic.NI_X86Base_StoreAligned, 0, 3)]
    [TestCase(NamedIntrinsic.NI_X86Base_MemoryFence, 0, 3)]
    [TestCase(NamedIntrinsic.NI_X86Base_Add, 0, 0)]
    public static void HardwareIntrinsicsKeepStoreBarrierLoadPriority(NamedIntrinsic id, int use, int def)
    {
        WithCompiler(0, false, compiler => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 4096);
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var node = id switch {
                NamedIntrinsic.NI_X86Base_LoadAlignedVector128 => new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, address),
                NamedIntrinsic.NI_X86Base_StoreAligned => new GenTreeHWIntrinsic(TYP_VOID, id, TYP_INT, 16, address, vector),
                NamedIntrinsic.NI_X86Base_MemoryFence => new GenTreeHWIntrinsic(TYP_VOID, id, TYP_INT, 0),
                _ => new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, vector, new GenTreeVecCon(TYP_SIMD16)),
            };
            var block = Block(compiler);
            Append(compiler, block, node);

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            Assert.That(block.bbMemoryUse, Is.EqualTo(use));
            Assert.That(block.bbMemoryDef, Is.EqualTo(def));
            Assert.That(block.bbMemoryHavoc, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PhysicalCallDefinitionsOccurAfterInterveningLocalUses(bool async)
    {
        WithCompiler(1, true, compiler => {
#if DEBUG
            compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            address.Flags |= GTF_VAR_DEF;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            if (async)
            {
                call.SetIsAsync(default);
            }
            else
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
            }
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(
                async ? WellKnownArg.AsyncResumedDef : WellKnownArg.RetBuffer));
            var block = Block(compiler);
            block.InsertAtEnd(address);
            block.InsertAtEnd(new GenTreeLclVar(TYP_INT, 0));
            block.InsertAtEnd(call);
            block.InsertAtEnd(new GenTreeLclVar(TYP_INT, 0));

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            AssertLocal(compiler, block, 0, true, true);
        });
    }

    [TestCase(false, false, false, false)]
    [TestCase(true, false, false, false)]
    [TestCase(true, true, false, false)]
    [TestCase(true, false, true, false)]
    [TestCase(true, false, false, true)]
    public static void FrameRootImplicitUsesRespectIlStubHelpersAndPriorDefinitions(bool ilStub, bool helpers, bool priorDef, bool untracked)
    {
        WithCompiler(1, true, compiler => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            compiler.info.compLvFrameListRoot = helpers ? BAD_VAR_NUM : 0;
            compiler.lvaTable[0].lvPinned = untracked;
            if (ilStub)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            }
            if (helpers)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            }
            var block = Block(compiler);
            Append(compiler, block, priorDef ? Store(compiler, 0) : compiler.gtNewIconNode(TYP_INT, 0));

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            Assert.That((int)SetOps.Count(compiler, block.bbVarUse), Is.EqualTo(ilStub && !helpers && !priorDef && !untracked ? 1 : 0));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    public static void CallFrameUsesRespectSuppressionAndX64TailHelperPolicy(bool tail, bool suppressed, bool priorDef)
    {
        WithCompiler(1, true, compiler => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            compiler.info.compLvFrameListRoot = 0;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            if (tail)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL | GenTreeCallFlags.GTF_CALL_M_TAILCALL_VIA_JIT_HELPER;
            }
            else
            {
                call.Flags |= GTF_CALL_UNMANAGED;
            }
            if (suppressed)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_SUPPRESS_GC_TRANSITION;
            }
            var block = Block(compiler);
            if (priorDef)
            {
                Append(compiler, block, Store(compiler, 0));
            }
            Append(compiler, block, call);

            Prepare<LirPolicy>(compiler).PerBlockLocalVarLiveness();

            AssertLocal(compiler, block, 0, !tail && !suppressed && !priorDef, priorDef);
        });
    }

    [Test]
    public static void FullyThreadedHirSkipsPhiStatementsAndIgnoresLocalAddresses()
    {
        WithCompiler(3, false, compiler => {
            var block = Block(compiler, lir: false);
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_INT)), early: false);
            _ = AddStatement(compiler, block, compiler.gtNewLclAddrNode(TYP_BYREF, 1, 0), early: false);
            var last = AddStatement(compiler, block, new GenTreeLclVar(TYP_INT, 2), early: false);

            Prepare<HirPolicy>(compiler).PerBlockLocalVarLiveness();

            AssertLocal(compiler, block, 0, false, false);
            AssertLocal(compiler, block, 1, false, false);
            AssertLocal(compiler, block, 2, true, false);
            Assert.That(compiler.compCurStmt, Is.SameAs(last));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void EarlyLocalThreadingIgnoresConditionalDefinitionsButKeepsDestination(bool qmarkUsed, bool assignment)
    {
        WithCompiler(3, false, compiler => {
            var block = Block(compiler, lir: false);
            compiler.fgNodeThreading = NodeThreading.AllLocals;
            compiler.compQmarkUsed = qmarkUsed;
            if (qmarkUsed)
            {
                var qmark = new GenTreeQmark(TYP_INT, new GenTreeLclVar(TYP_INT, 2),
                    new GenTreeColon(TYP_INT, Store(compiler, 0), new GenTreeLclVar(TYP_INT, 0)));
                _ = AddStatement(compiler, block, assignment ? compiler.gtNewStoreLclVarNode(1, qmark) : qmark, early: true);
            }
            else
            {
                _ = AddStatement(compiler, block, Store(compiler, 0), early: true);
            }
            _ = AddStatement(compiler, block, new GenTreeLclVar(TYP_INT, 0), early: true);

            Prepare<EarlyPolicy>(compiler).PerBlockLocalVarLiveness();

            AssertLocal(compiler, block, 0, qmarkUsed, !qmarkUsed);
            AssertLocal(compiler, block, 1, false, assignment);
            AssertLocal(compiler, block, 2, qmarkUsed, false);
        });
    }

    [Test]
    public static void RegenerationClearsScratchMemoryAndByrefDivergenceWithoutChangingLiveOut()
    {
        WithCompiler(1, false, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var first = Block(compiler);
            var second = Block(compiler);
            var store = Store(compiler, 0);
            Append(compiler, first, store);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            Append(compiler, first, call);
            Append(compiler, second, compiler.gtNewIconNode(TYP_INT, 0));
            var liveness = Prepare<LirPolicy>(compiler);
            first.bbMemoryLiveOut = 2;

            liveness.PerBlockLocalVarLiveness();

            Assert.That(compiler.byrefStatesMatchGcHeapStates, Is.False);
            Assert.That(first.bbMemoryHavoc, Is.EqualTo(3));
            Assert.That(second.bbMemoryUse | second.bbMemoryDef | second.bbMemoryHavoc, Is.Zero);

            first.Remove(store);
            first.Remove(call);
            liveness.PerBlockLocalVarLiveness();

            Assert.That(compiler.byrefStatesMatchGcHeapStates, Is.True);
            Assert.That(first.bbMemoryUse | first.bbMemoryDef | first.bbMemoryHavoc, Is.Zero);
            Assert.That(first.bbMemoryLiveOut, Is.EqualTo(2));
        });
    }

#if DEBUG
    [TestCase(0, "{        V00}")]
    [TestCase(1, "{    V01    }")]
    [TestCase(2, "{V02        }")]
    public static void VariableSetDumpsPreserveTrackedIndexOrderAndAlignment(int local, string aligned)
    {
        WithCompiler(3, false, compiler => {
            _ = Block(compiler);
            _ = Prepare<LirPolicy>(compiler);
            var set = SetOps.MakeSingleton(compiler, compiler.lvaTable[local]._varIndex);
            var all = SetOps.MakeFull(compiler);

            Assert.That(Capture(() => compiler.lvaDispVarSet(set)), Is.EqualTo($"{{V{local:D2}}}"));
            Assert.That(Capture(() => compiler.lvaDispVarSet(set, all)), Is.EqualTo(aligned));
        });
    }

    [Test]
    public static void VerboseBlockDumpIncludesAlignedLocalsMemoryAndHavoc()
    {
        WithCompiler(1, false, compiler => {
            var block = Block(compiler);
            Append(compiler, block, new GenTreeLclVar(TYP_INT, 0));
            Append(compiler, block, compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null));
            var liveness = Prepare<LirPolicy>(compiler);
            compiler.verbose = true;

            var text = Capture(liveness.PerBlockLocalVarLiveness);

            Assert.That(text, Is.EqualTo(("*************** In Liveness::PerBlockLocalVarLiveness()\n" +
                $"{FMT_BB(block.bbNum)} USE(1)={{V00}} + ByrefExposed + GcHeap\n" +
                "     DEF(0)={   } + ByrefExposed* + GcHeap*\n\n" +
                "** Memory liveness computed, GcHeap states and ByrefExposed states match\n").ReplaceLineEndings(Environment.NewLine)));
        });
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

    private static GenTreeLclVar Store(Compiler compiler, int local)
    {
        var store = new GenTreeLclVar(TYP_INT, local, compiler.gtNewIconNode(TYP_INT, 42));
        store.Flags |= GTF_VAR_DEF;

        return store;
    }

    private static void AssertLocal(Compiler compiler, BasicBlock block, int local, bool use, bool def)
    {
        Assert.That(compiler.lvaTable[local].lvTracked, Is.True);
        var index = compiler.lvaTable[local]._varIndex;
        Assert.That(SetOps.IsMember(compiler, block.bbVarUse, index), Is.EqualTo(use), $"V{local} use");
        Assert.That(SetOps.IsMember(compiler, block.bbVarDef, index), Is.EqualTo(def), $"V{local} def");
    }

    private static Liveness<TPolicy> Prepare<TPolicy>(Compiler compiler)
        where TPolicy : ILivenessPolicy
    {
        compiler._dfsTree = compiler.fgComputeDfs(false);
        var liveness = new Liveness<TPolicy>(compiler);
        liveness.Init();

        return liveness;
    }

    private static BasicBlock Block(Compiler compiler, bool lir = true)
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
        if (lir)
        {
            block.MakeLir(null, null);
        }

        return block;
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

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root, bool early)
    {
        var statement = new Statement(root, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        if (early)
        {
            var sequencer = new LocalSequencer(compiler);
            sequencer.Start(statement);
            _ = sequencer.WalkTree(ref root, null);
            sequencer.Finish(statement);
        }
        else
        {
            compiler.fgSetStmtSeq(statement);
        }

        return statement;
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
        compiler.compHndBBtab = [];
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitTls.Compiler = compiler;
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
