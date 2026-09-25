// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LivenessBackwardLIRTests
{
    private readonly struct NoDce : ILivenessPolicy
    {
        public static bool IsLIR => true;
    }

    private readonly struct Dce : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool EliminateDeadCode => true;
        public static bool TrackAddressExposedLocals => true;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(65)]
    public static void EmptyBlockPreservesLifeAndDoesNotPublishPhaseState(int count)
    {
        WithCompiler(count, compiler => {
            var block = Block();
            var liveness = Prepare<NoDce>(compiler);
            var life = SetOps.MakeFull(compiler);
            liveness.ComputeLifeLIR(life, block, SetOps.MakeEmpty(compiler));
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(count));
            Assert.That(compiler.fgStmtRemoved, Is.False);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.False);
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, true)]
    public static void TrackedUsesAndDefinitionsRecomputeDeaths(bool partial, bool keepAlive, bool minopts)
    {
        WithCompiler(1, compiler => {
            var first = new GenTreeLclVar(TYP_INT, 0) { Flags = GTF_VAR_DEATH };
            var value = compiler.gtNewIconNode(TYP_INT, 1);
            var store = new GenTreeLclVar(TYP_INT, 0, value) { Flags = GTF_VAR_DEF | GTF_VAR_DEATH };
            if (partial)
            {
                store.Flags |= GTF_VAR_USEASG;
            }
            var last = new GenTreeLclVar(TYP_INT, 0);
            var block = Block(first, value, store, last);
            var liveness = Prepare<NoDce>(compiler);
            var keep = keepAlive ? SetOps.MakeFull(compiler) : SetOps.MakeEmpty(compiler);
            var life = SetOps.MakeCopy(compiler, keep);

            liveness.ComputeLifeLIR(life, block, keep);

            Assert.That((first.Flags & GTF_VAR_DEATH) != 0, Is.EqualTo(!partial && !keepAlive));
            Assert.That((store.Flags & GTF_VAR_DEATH) != 0, Is.False);
            Assert.That((last.Flags & GTF_VAR_DEATH) != 0, Is.EqualTo(!keepAlive));
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(1));
            liveness.ComputeLifeLIR(life, block, keep);
            Assert.That((last.Flags & GTF_VAR_DEATH) != 0, Is.False);
        }, minopts);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DeadStoreAndOperandsAreRemovedOnlyWithDce(bool eliminate)
    {
        WithCompiler(1, compiler => {
            var value = compiler.gtNewIconNode(TYP_INT, 42);
            var store = new GenTreeLclVar(TYP_INT, 0, value) { Flags = GTF_VAR_DEF };
            var block = Block(value, store);
            _ = Prepare<Dce>(compiler);
            var life = SetOps.MakeEmpty(compiler);
            if (eliminate)
            {
                new Liveness<Dce>(compiler).ComputeLifeLIR(life, block, life);
            }
            else
            {
                new Liveness<NoDce>(compiler).ComputeLifeLIR(life, block, life);
            }

            Assert.That(block.FirstNode is null, Is.EqualTo(eliminate));
            Assert.That(block.LastNode is null, Is.EqualTo(eliminate));
            Assert.That(compiler.fgStmtRemoved, Is.EqualTo(eliminate));
            Assert.That((store.Flags & GTF_VAR_DEATH) != 0, Is.True);
            Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.EqualTo(3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DeadAddressExposedDefinitionsAreNotEliminated(bool field)
    {
        WithCompiler(2, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            if (field)
            {
                compiler.lvaTable[1].lvIsStructField = true;
                compiler.lvaTable[1].lvParentLcl = 0;
            }
            var liveness = Prepare<Dce>(compiler);
            var local = field ? 1 : 0;
            var store = new GenTreeLclVar(TYP_INT, local, compiler.gtNewIconNode(TYP_INT, 1)) { Flags = GTF_VAR_DEF };
            Assert.That(liveness.ComputeLifeLocal(SetOps.MakeEmpty(compiler), SetOps.MakeEmpty(compiler), store), Is.False);
            Assert.That((store.Flags & GTF_VAR_DEATH) != 0, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PromotedFieldsHaveIndependentDeathsAndKeepalive(bool definition, bool untrackedField)
    {
        WithCompiler(4, compiler => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvDoNotEnregister = true;
            parent.lvFieldLclStart = 1;
            parent.lvFieldCnt = 3;
            for (var i = 1; i < 4; i++)
            {
                compiler.lvaTable[i].lvIsStructField = true;
                compiler.lvaTable[i].lvParentLcl = 0;
            }
            compiler.lvaTable[3].lvPinned = untrackedField;
            var liveness = Prepare<Dce>(compiler);
            var node = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            node.Flags |= GTF_VAR_DEATH_MASK;
            if (definition)
            {
                node.Flags |= GTF_VAR_DEF;
            }
            var keep = SetOps.MakeSingleton(compiler, compiler.lvaTable[1]._varIndex);
            var life = SetOps.MakeCopy(compiler, keep);

            Assert.That(liveness.ComputeLifeLocal(life, keep, node), Is.False);
            Assert.That(node.IsLastUse(0), Is.False);
            Assert.That(node.IsLastUse(1), Is.True);
            Assert.That(node.IsLastUse(2), Is.EqualTo(!untrackedField));
            Assert.That(SetOps.IsMember(compiler, life, compiler.lvaTable[1]._varIndex), Is.True);
            Assert.That(SetOps.IsMember(compiler, life, compiler.lvaTable[2]._varIndex), Is.EqualTo(!definition));
        });
    }

    [TestCase(false, 1, true)]
    [TestCase(false, 2, false)]
    [TestCase(true, 1, false)]
    public static void SingleReferenceUntrackedScalarRequiresAnUnpinnedLocal(bool pinned, int references, bool dead)
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].lvPinned = pinned;
            compiler.lvaTable[0].setLvRefCnt((ushort)references);
            MaxLocalsToTrack(ref JitConfig) = 0;
            var liveness = Prepare<Dce>(compiler);
            var store = new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 1)) { Flags = GTF_VAR_DEF };
            Assert.That(compiler.lvaTable[0].lvTracked, Is.False);
            Assert.That(liveness.ComputeLifeLocal([], [], store), Is.EqualTo(dead));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void AllDeadPromotedFieldsRespectUntrackedFieldsAndParentExposure(bool exposed, bool untrackedField)
    {
        WithCompiler(2, compiler => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvFieldLclStart = 1;
            parent.lvFieldCnt = 1;
            parent.SetAddressExposed(exposed, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            compiler.lvaTable[1].lvIsStructField = true;
            compiler.lvaTable[1].lvParentLcl = 0;
            compiler.lvaTable[1].lvPinned = untrackedField;
            var liveness = Prepare<Dce>(compiler);
            var definition = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            definition.Flags |= GTF_VAR_DEF;

            Assert.That(liveness.ComputeLifeLocal(SetOps.MakeEmpty(compiler), SetOps.MakeEmpty(compiler), definition),
                Is.EqualTo(!exposed && !untrackedField));
            Assert.That(definition.IsLastUse(0), Is.EqualTo(!untrackedField));
        });
    }

    [TestCase(false, false, 1)]
    [TestCase(false, true, 1)]
    [TestCase(true, false, 1)]
    [TestCase(true, true, 1)]
    [TestCase(true, true, 2)]
    public static void SingleReferencePromotionRemovalRequiresDependentParent(bool field, bool dependent, int parentReferences)
    {
        WithCompiler(2, compiler => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvDoNotEnregister = dependent;
            parent.lvFieldLclStart = 1;
            parent.lvFieldCnt = 1;
            parent.setLvRefCnt((ushort)parentReferences);
            compiler.lvaTable[1].lvIsStructField = true;
            compiler.lvaTable[1].lvParentLcl = 0;
            compiler.lvaTable[1].setLvRefCnt(1);
            MaxLocalsToTrack(ref JitConfig) = 0;
            var liveness = Prepare<Dce>(compiler);
            var definition = compiler.gtNewLclAddrNode(TYP_BYREF, field ? 1 : 0, 0);
            definition.Flags |= GTF_VAR_DEF;

            Assert.That(liveness.ComputeLifeLocal([], [], definition), Is.EqualTo(dependent && (parentReferences == 1)));
        });
    }

    [TestCase(64)]
    [TestCase(65)]
    [TestCase(129)]
    public static void TrackedTransfersPreserveOtherWords(int count)
    {
        WithCompiler(count, compiler => {
            var liveness = Prepare<NoDce>(compiler);
            var life = SetOps.MakeFull(compiler);
            var definition = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            definition.Flags |= GTF_VAR_DEF;
            Assert.That(compiler.lvaTable[0]._varIndex, Is.EqualTo(count - 1));
            Assert.That(liveness.ComputeLifeLocal(life, SetOps.MakeEmpty(compiler), definition), Is.False);
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(count - 1));
            var use = new GenTreeLclVar(TYP_INT, 0);
            Assert.That(liveness.ComputeLifeLocal(life, SetOps.MakeEmpty(compiler), use), Is.False);
            Assert.That((use.Flags & GTF_VAR_DEATH) != 0, Is.True);
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(count));
        });
    }

    [TestCase(GT_LCL_VAR, false)]
    [TestCase(GT_LCL_FLD, false)]
    [TestCase(GT_LCL_ADDR, false)]
    [TestCase(GT_LCL_VAR, true)]
    [TestCase(GT_LCL_FLD, true)]
    [TestCase(GT_LCL_ADDR, true)]
    public static void UnusedLocalInputsAreOnlyDeletedWithDce(genTreeOps oper, bool eliminate)
    {
        WithCompiler(1, compiler => {
            GenTree node = oper is GT_LCL_VAR
                ? new GenTreeLclVar(TYP_INT, 0)
                : new GenTreeLclFld(oper, oper is GT_LCL_ADDR ? TYP_BYREF : TYP_INT, 0, 0);
            node.IsUnusedValue = true;
            var block = Block(node);
            _ = Prepare<NoDce>(compiler);
            var life = SetOps.MakeEmpty(compiler);
            if (eliminate)
            {
                new Liveness<Dce>(compiler).ComputeLifeLIR(life, block, SetOps.MakeEmpty(compiler));
            }
            else
            {
                new Liveness<NoDce>(compiler).ComputeLifeLIR(life, block, SetOps.MakeEmpty(compiler));
            }
            Assert.That(block.FirstNode is null, Is.EqualTo(eliminate));
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(eliminate ? 0 : 1));
            Assert.That(compiler.fgStmtRemoved, Is.EqualTo(eliminate));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void DeadStoreIndirectionReplacementPreservesPendingReverseTraversal(bool indirectStore, bool nonFaulting)
    {
        WithCompiler(2, compiler => {
            compiler.lvaTable[1].Type = TYP_BYREF;
            var source = new GenTreeLclVar(TYP_BYREF, 1);
            var data = new GenTreeIndir(GT_IND, TYP_INT, source);
            if (nonFaulting)
            {
                data.Flags |= GTF_IND_NONFAULTING;
            }
            GenTree store;
            BasicBlock block;
            if (indirectStore)
            {
                var destination = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
                destination.Flags |= GTF_VAR_DEF;
                store = new GenTreeStoreInd(TYP_INT, destination, data);
                block = Block(source, data, destination, store);
            }
            else
            {
                store = new GenTreeLclVar(TYP_INT, 0, data) { Flags = GTF_VAR_DEF };
                block = Block(source, data, store);
            }
            var liveness = Prepare<Dce>(compiler);
            var life = SetOps.MakeEmpty(compiler);

            liveness.ComputeLifeLIR(life, block, SetOps.MakeEmpty(compiler));

            Assert.That(store.Prev, Is.Null);
            Assert.That(store.Next, Is.Null);
            Assert.That(data.Prev, Is.Null);
            Assert.That(data.Next, Is.Null);
            Assert.That(compiler.fgStmtRemoved, Is.True);
            if (nonFaulting)
            {
                Assert.That(block.FirstNode, Is.Null);
                Assert.That((int)SetOps.Count(compiler, life), Is.Zero);
            }
            else
            {
                Assert.That(block.FirstNode, Is.SameAs(source));
                var probe = block.LastNode;
                assert(probe is not null);
                Assert.That(probe.Oper, Is.EqualTo(GT_NULLCHECK));
                Assert.That(probe.Prev, Is.SameAs(source));
                Assert.That(source.Next, Is.SameAs(probe));
                Assert.That(probe.AsIndir().Addr, Is.SameAs(source));
                Assert.That((source.Flags & GTF_VAR_DEATH) != 0, Is.True);
                Assert.That(SetOps.IsMember(compiler, life, compiler.lvaTable[1]._varIndex), Is.True);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DeadIndirectStoreDeletesLocalDataOnEitherSideOfAddress(bool dataBeforeAddress)
    {
        WithCompiler(2, compiler => {
            var data = new GenTreeLclVar(TYP_INT, 1);
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            address.Flags |= GTF_VAR_DEF;
            var store = new GenTreeStoreInd(TYP_INT, address, data);
            var earlier = new GenTree(GT_NO_OP, TYP_VOID);
            var block = dataBeforeAddress ? Block(earlier, data, address, store) : Block(earlier, address, data, store);
            var liveness = Prepare<Dce>(compiler);
            liveness.ComputeLifeLIR(SetOps.MakeEmpty(compiler), block, SetOps.MakeEmpty(compiler));
            Assert.That(block.FirstNode, Is.SameAs(earlier));
            Assert.That(block.LastNode, Is.SameAs(earlier));
            Assert.That(earlier.Next, Is.Null);
            Assert.That(data.Next, Is.Null);
            Assert.That(data.Prev, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnusedBlockLoadsAreTransformedEvenWithoutDce(bool containedAddress)
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = new GenTreeLclVar(TYP_BYREF, 0);
            var load = new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(24)) { IsUnusedValue = true };
            address.IsContained = containedAddress;
            var block = Block(address, load);
            var liveness = Prepare<NoDce>(compiler);
            liveness.ComputeLifeLIR(SetOps.MakeEmpty(compiler), block, SetOps.MakeEmpty(compiler));
            var probe = block.LastNode;
            assert(probe is not null);
            Assert.That(probe.Oper, Is.EqualTo(containedAddress ? GT_IND : GT_NULLCHECK));
            Assert.That(probe.Type, Is.Not.EqualTo(TYP_STRUCT));
            Assert.That(probe.AsIndir().Addr, Is.SameAs(address));
            Assert.That((address.Flags & GTF_VAR_DEATH) != 0, Is.True);
            Assert.That(compiler.fgStmtRemoved, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PhysicalCallDefinitionsKillAtTheCallNotAtTheAddress(bool async, bool partial)
    {
        WithCompiler(1, compiler => {
#if DEBUG
            compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            address.Flags |= GTF_VAR_DEF | (partial ? GTF_VAR_USEASG : GTF_EMPTY);
            var intervening = new GenTreeLclVar(TYP_INT, 0);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            if (async)
            {
                call.SetIsAsync(default);
            }
            else
            {
                call._callMoreFlags |= GTF_CALL_M_RETBUFFARG_LCLOPT;
            }
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(
                async ? WellKnownArg.AsyncResumedDef : WellKnownArg.RetBuffer));
            var block = Block(address, intervening, call);
            var liveness = Prepare<NoDce>(compiler);
            var life = SetOps.MakeFull(compiler);

            liveness.ComputeLifeLIR(life, block, SetOps.MakeEmpty(compiler));

            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(1));
            Assert.That((intervening.Flags & GTF_VAR_DEATH) != 0, Is.EqualTo(!partial));
            Assert.That((address.Flags & GTF_VAR_DEATH) != 0, Is.False);
            Assert.That(liveness.ComputeLifeCall(life, SetOps.MakeEmpty(compiler), call),
                partial ? Is.SameAs(address) : Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PInvokeFrameRootDeathIsRecomputedAndRespectsSuppression(bool suppressed, bool helpers)
    {
        WithCompiler(1, compiler => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            compiler.info.compLvFrameListRoot = helpers ? BAD_VAR_NUM : 0;
            if (helpers)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            }
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_UNMANAGED;
            if (suppressed)
            {
                call._callMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
            }
            var liveness = Prepare<NoDce>(compiler);
            var life = SetOps.MakeEmpty(compiler);
            _ = liveness.ComputeLifeCall(life, SetOps.MakeEmpty(compiler), call);
            Assert.That((call._callMoreFlags & GTF_CALL_M_FRAME_VAR_DEATH) != 0, Is.EqualTo(!suppressed && !helpers));
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(!suppressed && !helpers ? 1 : 0));
            _ = liveness.ComputeLifeCall(life, SetOps.MakeEmpty(compiler), call);
            Assert.That((call._callMoreFlags & GTF_CALL_M_FRAME_VAR_DEATH) != 0, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ExplicitGcInitializationIsRetainedUnlessPartialOrSingleReference(bool partial, bool singleReference)
    {
        WithCompiler(1, compiler => {
            var builder = new ClassLayoutBuilder(compiler, 16);
            builder.SetGCPtrType(0, TYP_REF);
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.Type = TYP_STRUCT;
            descriptor.Layout = ClassLayout.Create(compiler, builder);
            descriptor.lvHasExplicitInit = true;
            descriptor.setLvRefCnt((ushort)(singleReference ? 1 : 3));
            var data = compiler.gtNewIconNode(TYP_INT, 0);
            var store = new GenTreeLclVar(TYP_STRUCT, 0, data) {
                Flags = GTF_VAR_DEF | (partial ? GTF_VAR_USEASG : GTF_EMPTY),
            };
            var block = Block(data, store);
            var liveness = Prepare<Dce>(compiler);
            Assert.That(liveness.TryRemoveDeadStoreLIR(store, store, block), Is.EqualTo(partial || singleReference));
            Assert.That(compiler.fgStmtRemoved, Is.EqualTo(partial || singleReference));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovingFlagsConsumerReleasesOnlyExplicitProducerRequirement(bool unused)
    {
        WithCompiler(0, compiler => {
            var left = compiler.gtNewIconNode(TYP_INT, 1);
            var right = compiler.gtNewIconNode(TYP_INT, 2);
            var producer = new GenTreeOp(GT_ADD, TYP_INT, left, right) {
                Flags = GTF_SET_FLAGS,
                IsUnusedValue = true,
            };
            var consumer = new GenTreeCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.EQ)) { IsUnusedValue = unused };
            var block = Block(left, right, producer, consumer);
            Prepare<Dce>(compiler).ComputeLifeLIR([], block, []);
            Assert.That(block.FirstNode is null, Is.EqualTo(unused));
            Assert.That((producer.Flags & GTF_SET_FLAGS) != 0, Is.EqualTo(!unused));
        });
    }

    [TestCase(GT_NO_OP)]
    [TestCase(GT_MEMORYBARRIER)]
    [TestCase(GT_START_NONGC)]
    [TestCase(GT_START_PREEMPTGC)]
    [TestCase(GT_GCPOLL)]
    [TestCase(GT_IL_OFFSET)]
    [TestCase(GT_PATCHPOINT)]
    [TestCase(GT_PATCHPOINT_FORCED)]
    [TestCase(GT_RECORD_ASYNC_RESUME)]
    public static void SideEffectingNodesAreRetained(genTreeOps oper)
    {
        WithCompiler(0, compiler => {
            var node = oper switch {
                GT_IL_OFFSET => new GenTreeILOffset(default),
                GT_RECORD_ASYNC_RESUME => new GenTreeVal(oper, TYP_VOID, 0),
                GT_PATCHPOINT => new GenTreeOp(oper, TYP_VOID,
                    compiler.gtNewIconNode(TYP_I_IMPL, 4096), compiler.gtNewIconNode(TYP_INT, 1)),
                GT_PATCHPOINT_FORCED => new GenTreeUnOp(oper, TYP_VOID, compiler.gtNewIconNode(TYP_INT, 1)),
                _ => new GenTree(oper, TYP_VOID),
            };
            var block = Block(node);
            foreach (var operand in node.Operands)
            {
                block.InsertBefore(node, operand);
            }
            Prepare<Dce>(compiler).ComputeLifeLIR([], block, []);
            Assert.That(block.LastNode, Is.SameAs(node));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PureCallsReleaseLateStackArgumentsButThrowingOrUsedCallsRemain(bool mayThrow, bool unused)
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].Type = TYP_LONG;
            var call = compiler.gtNewHelperCallNode(TYP_LONG, mayThrow
                ? CorInfoHelpFunc.CORINFO_HELP_LMUL_OVF : CorInfoHelpFunc.CORINFO_HELP_LMUL);
            call.IsUnusedValue = unused;
            var value = new GenTreeLclVar(TYP_LONG, 0);
            var stack = new GenTreePutArgStk(TYP_LONG, value, call, 0, TARGET_POINTER_SIZE, false);
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            arg.EarlyNode = null;
            arg.LateNode = stack;
            call.Args.PushLateBack(arg);
            var constant = compiler.gtNewLconNode(3);
            var register = new GenTreeUnOp(GT_PUTARG_REG, TYP_LONG, constant);
            arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(constant));
            arg.EarlyNode = null;
            arg.LateNode = register;
            call.Args.PushLateBack(arg);
            var block = Block(value, stack, constant, register, call);
            var liveness = Prepare<Dce>(compiler);
            var life = SetOps.MakeEmpty(compiler);

            liveness.ComputeLifeLIR(life, block, SetOps.MakeEmpty(compiler));

            var removed = !mayThrow && unused;
            Assert.That(block.FirstNode is null, Is.EqualTo(removed));
            Assert.That(block.LastNode is null, Is.EqualTo(removed));
            Assert.That(stack.Oper, Is.EqualTo(removed ? GT_NOP : GT_PUTARG_STK));
            Assert.That((int)SetOps.Count(compiler, life), Is.EqualTo(removed ? 0 : 1));
            Assert.That(compiler.fgStmtRemoved, Is.EqualTo(removed));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void EmbeddedMaskOperandsRemainContainedWhenTheyCanFault(bool embedded, bool nonFaulting)
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = new GenTreeLclVar(TYP_BYREF, 0);
            var memory = new GenTreeIndir(GT_IND, TYP_SIMD16, address) { IsContained = true };
            if (nonFaulting)
            {
                memory.Flags |= GTF_IND_NONFAULTING;
            }
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var operation = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_X86Base_Add, TYP_INT, 16, vector, memory) {
                IsContained = true,
            };
            if (embedded)
            {
                operation.Flags |= GTF_HW_EM_OP;
            }
            var parent = new GenTreeHWIntrinsic(TYP_INT, NamedIntrinsic.NI_Vector_ToScalar, TYP_INT, 16, operation) {
                IsUnusedValue = true,
            };
            var block = Block(address, vector, memory, operation, parent);
            var liveness = Prepare<Dce>(compiler);
            liveness.ComputeLifeLIR(SetOps.MakeEmpty(compiler), block, SetOps.MakeEmpty(compiler));

            var retained = embedded && !nonFaulting;
            Assert.That(operation.IsContained, Is.EqualTo(retained));
            Assert.That(block.LastNode == parent, Is.EqualTo(retained));
            Assert.That(block.FirstNode is null, Is.EqualTo(nonFaulting));
            if (!nonFaulting)
            {
                Assert.That(block.FirstNode, Is.SameAs(address));
            }
        });
    }

    [TestCase(NamedIntrinsic.NI_X86Base_StoreAligned)]
    [TestCase(NamedIntrinsic.NI_X86Base_MemoryFence)]
    public static void HardwareStoresAndSpecialEffectsAreNeverRemoved(NamedIntrinsic intrinsic)
    {
        WithCompiler(0, compiler => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 4096);
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var node = intrinsic is NamedIntrinsic.NI_X86Base_StoreAligned
                ? new GenTreeHWIntrinsic(TYP_VOID, intrinsic, TYP_INT, 16, address, vector)
                : new GenTreeHWIntrinsic(TYP_VOID, intrinsic, TYP_INT, 0);
            var block = intrinsic is NamedIntrinsic.NI_X86Base_StoreAligned
                ? Block(address, vector, node) : Block(node);
            Prepare<Dce>(compiler).ComputeLifeLIR([], block, []);
            Assert.That(block.LastNode, Is.SameAs(node));
        });
    }

#if DEBUG
    [Test]
    public static void DeadNodeDiagnosticsUseNativeTopOnlyTreeFormatting()
    {
        WithCompiler(0, compiler => {
            var node = compiler.gtNewIconNode(TYP_INT, 42);
            node.IsUnusedValue = true;
            var block = Block(node);
            var liveness = Prepare<Dce>(compiler);
            compiler.verbose = true;
            var expected = Capture(() => {
                jitprintf("Removing dead node:\n");
                compiler.gtDispTree(node, topOnly: true);
            });
            var actual = Capture(() => liveness.ComputeLifeLIR([], block, []));
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual, Does.Contain("Removing dead node:"));
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

    [TestCase(false)]
    [TestCase(true)]
    public static void OrderedNopsAreRetained(bool ordered)
    {
        WithCompiler(0, compiler => {
            var node = new GenTree(GT_NOP, TYP_VOID) { Flags = ordered ? GTF_ORDER_SIDEEFF : GTF_EMPTY };
            var block = Block(node);
            Prepare<Dce>(compiler).ComputeLifeLIR([], block, []);
            Assert.That(block.FirstNode is not null, Is.EqualTo(ordered));
        });
    }

    private static Liveness<TPolicy> Prepare<TPolicy>(Compiler compiler)
        where TPolicy : ILivenessPolicy
    {
        var liveness = new Liveness<TPolicy>(compiler);
        liveness.Init();

        return liveness;
    }

    private static BasicBlock Block(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null) { Kind = BBKinds.BBJ_RETURN };
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

    private static void WithCompiler(int count, Action<Compiler> action, bool minopts = false)
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
        compiler.info.compIsStatic = true;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.compHndBBtab = [];
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
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
