// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LocalReferenceAccountingTests
{
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    public static void PhaseInitializesCountsSlotsAndGenericContext(bool minopts, bool keepContext, bool scopeInfo)
    {
        WithCompiler(minopts, compiler => {
            CORINFO_METHOD_INFO method = default;
            method.options = CorInfoOptions.CORINFO_GENERICS_CTXT_FROM_METHODDESC;
            if (keepContext)
            {
                method.options |= CorInfoOptions.CORINFO_GENERICS_CTXT_KEEP_ALIVE;
            }
            compiler.info.compMethodInfo = &method;
            compiler.info.compIsStatic = true;
            compiler.info.compTypeCtxtArg = 0;
            compiler.info.compVarScopesCount = 1;
            compiler.opts.compScopeInfo = scopeInfo;
            compiler.lvaRefCountState = RCS_EARLY;
            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                local.lvSlotNum = 99;
            }
            var block = TreeBlock(compiler, BB_UNITY_WEIGHT, compiler.gtNewLclvNode(TYP_INT, 1));
            compiler.fgFirstBB = compiler.fgLastBB = block;

            var result = compiler.lvaMarkLocalVars();

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaRefCountState, Is.EqualTo(RCS_NORMAL));
            AssertCounts(compiler, 1, 1, BB_UNITY_WEIGHT);
            Assert.That(compiler.lvaTable[0].lvImplicitlyReferenced, Is.EqualTo(minopts || keepContext));
#if DEBUG
            Assert.That(compiler.lvaTable[1].lvSlotNum, Is.EqualTo(1));
#else
            Assert.That(compiler.lvaTable[1].lvSlotNum, Is.EqualTo(scopeInfo ? 1 : 99));
#endif
        });
    }

#if DEBUG
    [Test]
    public static void DisqualificationReasonHasNativeDefaultAndValueCopySemantics()
    {
        LclVarDsc zeroed = default;
        var constructed = new LclVarDsc();
        var locals = new LclVarDsc[2];
        Assert.That(zeroed.lvSingleDefDisqualifyReason, Is.EqualTo((byte)'H'));
        Assert.That(constructed.lvSingleDefDisqualifyReason, Is.EqualTo((byte)'H'));
        Assert.That(locals[0].lvSingleDefDisqualifyReason, Is.EqualTo((byte)'H'));

        locals[0].lvSingleDefDisqualifyReason = (byte)'Z';
        var copy = locals[0];
        locals[0].lvSingleDefDisqualifyReason = (byte)'M';
        Assert.That(copy.lvSingleDefDisqualifyReason, Is.EqualTo((byte)'Z'));
        Assert.That(locals[1].lvSingleDefDisqualifyReason, Is.EqualTo((byte)'H'));
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            copy.lvSingleDefDisqualifyReason = (byte)value;
            Assert.That(copy.lvSingleDefDisqualifyReason, Is.EqualTo((byte)value));
        }
        Array.Clear(locals);
        Assert.That(locals[0].lvSingleDefDisqualifyReason, Is.EqualTo((byte)'H'));
    }
#endif

    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(false, true, true)]
    public static void ImpreciseFirstComputeInitializesLocalsButRecomputeDoesNothing(bool minopts, bool debugCode,
        bool slots)
    {
        WithCompiler(minopts, compiler => {
            compiler.info.compIsVarArgs = true;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaCurEpoch = 10;
            compiler.lvaTrackedCount = 3;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                local.setLvRefCnt(9);
                local.setLvRefCntWtd(77);
                local.lvTracked = true;
                local.lvSlotNum = 99;
            }

            compiler.lvaComputeRefCounts(false, slots);

            for (var index = 0; index < compiler.lvaCount; index++)
            {
                ref var local = ref compiler.lvaTable[index];
                Assert.That(local.lvImplicitlyReferenced, Is.True);
                Assert.That(local.lvTracked, Is.False);
                Assert.That(local.lvRefCnt(), Is.EqualTo(1));
                Assert.That(local.lvRefCntWtd(), Is.EqualTo(BB_UNITY_WEIGHT));
                Assert.That(local.lvSlotNum, Is.EqualTo(slots ? index : 99));
            }
            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(11));
            Assert.That(compiler.lvaTrackedCount, Is.Zero);
            Assert.That(compiler.lvaTrackedCountInSizeTUnits, Is.Zero);
            compiler.lvaTable[0].setLvRefCnt(4);
            compiler.lvaTable[0].setLvRefCntWtd(17);
            compiler.lvaTable[0].lvSlotNum = 77;

            compiler.lvaComputeRefCounts(true, true);

            Assert.That(compiler.lvaCurEpoch, Is.EqualTo(11));
            Assert.That(compiler.lvaTable[0].lvRefCnt(), Is.EqualTo(4));
            Assert.That(compiler.lvaTable[0].lvRefCntWtd(), Is.EqualTo(17));
            Assert.That(compiler.lvaTable[0].lvSlotNum, Is.EqualTo(77));
        }, debugCode);
    }

    [Test]
    public static void TreeWalkSkipsPhisAndCountsNormalizedReferencesAndParameterPrologUses()
    {
        WithCompiler(false, compiler => {
            compiler.fgCalledCount = 10;
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaTable[2].lvIsTemp = true;
            compiler.lvaTable[3].lvIsParam = true;
            compiler.lvaTable[4].lvIsParamRegTarget = true;
            compiler.compJmpOpUsed = true;
            compiler.lvaGenericsContextInUse = true;
            var context = compiler.gtNewLclvNode(TYP_INT, 5);
            context.Flags |= GTF_VAR_CONTEXT;
            var block = TreeBlock(compiler, 30,
                compiler.gtNewStoreLclVarNode(6, new GenTreePhi(TYP_INT)),
                compiler.gtNewStoreLclVarNode(1, new GenTreeOp(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 2))),
                context, compiler.gtNewLclvNode(TYP_INT, 4));
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(false, true);

            AssertCounts(compiler, 0, 3, 5 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 1, 1, 3 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 2, 1, 6 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 3, 1, BB_UNITY_WEIGHT);
            AssertCounts(compiler, 4, 3, 5 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 5, 1, 3 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 6, 0, 0);
            Assert.That(compiler.lvaTable[0].lvSingleDef, Is.True);
            Assert.That(compiler.lvaTable[4].lvSingleDef, Is.True);
            Assert.That(compiler.lvaTable[1].lvSingleDefRegCandidate, Is.True);
            Assert.That(compiler.lvaTable[1].lvSingleDef, Is.False);
            Assert.That(compiler.lvaGenericsContextInUse, Is.True);
            Assert.That(compiler.lvaRefCountState, Is.EqualTo(RCS_NORMAL));
            Assert.That(compiler.lvaTable[7].lvSlotNum, Is.EqualTo(7));

            compiler.lvaTable[1].lvSingleDef = true;
            compiler.lvaTable[1].lvSlotNum = 55;
            context.Flags &= ~GTF_VAR_CONTEXT;

            compiler.lvaComputeRefCounts(true, false);

            AssertCounts(compiler, 0, 3, 5 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 1, 1, 3 * BB_UNITY_WEIGHT);
            Assert.That(compiler.lvaTable[1].lvSingleDef, Is.True);
            Assert.That(compiler.lvaTable[1].lvSlotNum, Is.EqualTo(55));
            Assert.That(compiler.lvaGenericsContextInUse, Is.False);
            // Native still runs the tree walk on HIR recomputation, without
            // reinitializing candidate state before visiting the definition.
            Assert.That(compiler.lvaTable[1].lvDisqualifySingleDefRegCandidate, Is.True);
        });
    }

    [TestCase(0.0)]
    [TestCase(3.0)]
    public static void LocalAddressesCountAsReferencesEvenInZeroWeightBlocks(double weight)
    {
        WithCompiler(false, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = TreeBlock(compiler, weight * BB_UNITY_WEIGHT, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0));
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(false, false);

            AssertCounts(compiler, 0, 1, weight * BB_UNITY_WEIGHT);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LirRecomputePreservesAnalysisAndZerosOnlyEligibleEhDefinitionWeights(bool doNotEnregister)
    {
        WithCompiler(false, compiler => {
            ref var local = ref compiler.lvaTable[0];
            local.lvTracked = true;
            local.lvDoNotEnregister = doNotEnregister;
            SetLiveInOutOfHandler(ref local, true);
            local.lvSingleDef = true;
            local.lvSingleDefRegCandidate = true;
            local.lvAllDefsAreNoGc = false;
            local.lvSlotNum = 53;
            compiler.lvaGenericsContextInUse = true;
            var constant = compiler.gtNewIconNode(TYP_INT, 1);
            var store = compiler.gtNewStoreLclVarNode(0, constant);
            var read = compiler.gtNewLclvNode(TYP_INT, 0);
            var block = LirBlock(4 * BB_UNITY_WEIGHT, constant, store, read);
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(true, false);

            AssertCounts(compiler, 0, 2, (doNotEnregister ? 8 : 4) * BB_UNITY_WEIGHT);
            Assert.That(local.lvSingleDef, Is.True);
            Assert.That(local.lvSingleDefRegCandidate, Is.True);
            Assert.That(local.lvAllDefsAreNoGc, Is.False);
            Assert.That(local.lvSlotNum, Is.EqualTo(53));
            Assert.That(compiler.lvaGenericsContextInUse, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PromotedStructAccountingUsesCanonicalParentFieldPropagation(bool dependent)
    {
        WithCompiler(false, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(8);
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[0].lvFieldCnt = 2;
            compiler.lvaTable[0].lvDoNotEnregister = dependent;
            for (var index = 1; index <= 2; index++)
            {
                compiler.lvaTable[index].lvIsStructField = true;
                compiler.lvaTable[index].lvParentLcl = 0;
                compiler.lvaTable[index].lvFldOffset = (byte)((index - 1) * 4);
            }
            compiler.lvaTable[2].lvIsRegArg = true;
            var block = LirBlock(2 * BB_UNITY_WEIGHT, compiler.gtNewLclvNode(TYP_STRUCT, 0),
                compiler.gtNewLclvNode(TYP_INT, 1));
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(true, false);

            AssertCounts(compiler, 0, dependent ? 3 : 0, dependent ? 5 * BB_UNITY_WEIGHT : 0);
            AssertCounts(compiler, 1, 2, 4 * BB_UNITY_WEIGHT);
            AssertCounts(compiler, 2, 2, 3 * BB_UNITY_WEIGHT);
        });
    }

    [TestCase(1, false, false)]
    [TestCase(2, false, false)]
    [TestCase(1, true, false)]
    [TestCase(1, false, true)]
    public static void TreeDefinitionsTrackSingleDefCandidatesAndDisqualificationReasons(int definitions,
        bool explicitInitialization, bool doNotEnregister)
    {
        WithCompiler(false, compiler => {
            compiler.lvaTable[0].lvHasExplicitInit = explicitInitialization;
            compiler.lvaTable[0].lvDoNotEnregister = doNotEnregister;
            var block = TreeBlock(compiler, BB_UNITY_WEIGHT);
            for (var index = 0; index < definitions; index++)
            {
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(
                    compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, index))));
            }
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(false, false);

            var disqualified = explicitInitialization || (definitions > 1);
            Assert.That(compiler.lvaTable[0].lvDisqualifySingleDefRegCandidate, Is.EqualTo(disqualified));
            Assert.That(compiler.lvaTable[0].lvSingleDefRegCandidate, Is.EqualTo(!disqualified && !doNotEnregister));
#if DEBUG
            var reason = explicitInitialization ? 'Z' : definitions > 1 ? 'M' : 'H';
            Assert.That(compiler.lvaTable[0].lvSingleDefDisqualifyReason, Is.EqualTo((byte)reason));
#endif
        });
    }

    [TestCase(TYP_SIMD16, true)]
    [TestCase(TYP_SIMD32, false)]
    [TestCase(TYP_SIMD64, false)]
    public static void WindowsPartialSimdCalleeSavesExcludeSingleDefCandidates(var_types type, bool candidate)
    {
        WithCompiler(false, compiler => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            var block = TreeBlock(compiler, BB_UNITY_WEIGHT,
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewLclvNode(type, 1)));
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(false, false);

            Assert.That(compiler.lvaTable[0].lvSingleDefRegCandidate, Is.EqualTo(candidate));
            Assert.That(Compiler.varTypeNeedsPartialCalleeSave(type), Is.EqualTo(!candidate));
        });
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void PinnedLocalsAreUnpinnedOnlyForUnexposedNonGcDefinitions(bool nullValue, bool addressExposed)
    {
        WithCompiler(false, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].lvPinned = true;
            compiler.lvaTable[1].Type = TYP_BYREF;
            if (addressExposed)
            {
                compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            }
            GenTree value = nullValue ? compiler.gtNewIconNode(TYP_BYREF, 0) : compiler.gtNewLclvNode(TYP_BYREF, 1);
            var block = TreeBlock(compiler, BB_UNITY_WEIGHT, compiler.gtNewStoreLclVarNode(0, value));
            compiler.fgFirstBB = compiler.fgLastBB = block;

            compiler.lvaComputeRefCounts(false, false);

            Assert.That(compiler.lvaTable[0].lvPinned, Is.EqualTo(!nullValue || addressExposed));
            Assert.That(compiler.lvaTable[0].lvAllDefsAreNoGc, Is.EqualTo(nullValue && !addressExposed));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FrameRootImplicitReferencesApplyToTreeCallsButNotLirCalls(bool helpers)
    {
        WithCompiler(false, compiler => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            compiler.info.compLvFrameListRoot = helpers ? BAD_VAR_NUM : 0;
            if (helpers)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            }
            var call = new GenTreeCall(TYP_VOID);
            var treeBlock = TreeBlock(compiler, 3 * BB_UNITY_WEIGHT, call);
            compiler.fgFirstBB = compiler.fgLastBB = treeBlock;

            compiler.lvaComputeRefCounts(false, false);

            AssertCounts(compiler, 0, helpers ? 0 : 2, helpers ? 0 : 6 * BB_UNITY_WEIGHT);
            var lirBlock = LirBlock(3 * BB_UNITY_WEIGHT, call, compiler.gtNewLclvNode(TYP_INT, 0));
            compiler.fgFirstBB = compiler.fgLastBB = lirBlock;

            compiler.lvaComputeRefCounts(true, false);

            AssertCounts(compiler, 0, 1, 3 * BB_UNITY_WEIGHT);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BackendLifetimePredicateShortCircuitsOrQueriesAllocator(bool minopts, bool enregister)
    {
        WithCompiler(minopts, compiler => {
            var allocator = new TrackingAllocator(enregister);
            Allocator(compiler) = allocator;

            Assert.That(compiler.backendRequiresLocalVarLifetimes(), Is.EqualTo(!minopts || enregister));
            Assert.That(allocator.Queries, Is.EqualTo(minopts ? 1 : 0));
        });
    }

    private static BasicBlock TreeBlock(Compiler compiler, double weight, params GenTree[] roots)
    {
        var block = new BasicBlock(null, null) { Kind = BBJ_RETURN, bbWeight = weight };
        foreach (var root in roots)
        {
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(root));
        }

        return block;
    }

    private static BasicBlock LirBlock(double weight, params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null) { Kind = BBJ_RETURN, bbWeight = weight };
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    private static void AssertCounts(Compiler compiler, int local, int count, double weight)
    {
        Assert.That(compiler.lvaTable[local].lvRefCnt(), Is.EqualTo(count), $"V{local} references");
        Assert.That(compiler.lvaTable[local].lvRefCntWtd(), Is.EqualTo(weight), $"V{local} weight");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set__lvLiveInOutOfHandler")]
    private static extern void SetLiveInOutOfHandler(ref LclVarDsc local, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    private sealed class TrackingAllocator(bool enregister) : IRegAlloc
    {
        public int Queries;

        public bool WillEnregisterLocalVars()
        {
            Queries++;
            return enregister;
        }

        public PhaseStatus DoRegisterAllocation() => throw new NotSupportedException();

        public bool IsContainableMemoryOp(GenTree node) => throw new NotSupportedException();

        public bool IsRegCandidate(in LclVarDsc local) => throw new NotSupportedException();

        public void dumpLsraStatsCsv(System.IO.StreamWriter writer) => throw new NotSupportedException();
    }

    private static void WithCompiler(bool minopts, Action<Compiler> action, bool debugCode = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.compDbgCode = debugCode;
        compiler.opts.SetMinOpts(minopts);
        compiler.info.compInitMem = true;
        compiler.lvaRefCountState = RCS_NORMAL;
        compiler.fgCalledCount = BB_UNITY_WEIGHT;
        compiler.lvaTable = new LclVarDsc[8];
        compiler.lvaCount = compiler.lvaTable.Length;
        foreach (ref var local in compiler.lvaTable.AsSpan())
        {
            local.Type = TYP_INT;
        }
        compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
        compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
        compiler.lvaStubArgumentVar = BAD_VAR_NUM;
        compiler.lvaRetAddrVar = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
