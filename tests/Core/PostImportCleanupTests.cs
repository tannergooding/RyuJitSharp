// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class PostImportCleanupTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void NoRemovalOutsideTryPreservesEarlyExit(bool osr)
    {
        WithCompiler(compiler => {
            var entry = AddBlock(compiler);
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
                compiler.fgOSREntryBB = entry;
            }

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.compPostImportationCleanupDone, Is.False);
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovesUnimportedBlocksAndAllDuplicatePreds(bool osr)
    {
        WithCompiler(compiler => {
            var entry = AddBlock(compiler);
            var dead = AddBlock(compiler, imported: false);
            var live = AddBlock(compiler);
            var deadReturn = AddBlock(compiler, imported: false);
            Jump(compiler, entry, live);
            dead.SetCond(compiler.fgAddRefPred(live, dead), compiler.fgAddRefPred(live, dead));
            compiler.fgReturnCount = 2;
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
                compiler.fgOSREntryBB = live;
            }

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Next, Is.SameAs(live));
            Assert.That(live.Prev, Is.SameAs(entry));
            Assert.That(compiler.fgLastBB, Is.SameAs(live));
            Assert.That(live.Next, Is.Null);
            Assert.That(dead.Prev, Is.SameAs(entry));
            Assert.That(dead.Next, Is.SameAs(live));
            Assert.That(deadReturn.Prev, Is.SameAs(live));
            Assert.That(dead.HasFlag(BBF_REMOVED) && deadReturn.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(live.bbRefs, Is.EqualTo(1));
            Assert.That(live.bbPreds, Is.SameAs(entry.TargetEdge));
            Assert.That(entry.TargetEdge.NextPredEdge, Is.Null);
            Assert.That(compiler.fgReturnCount, Is.EqualTo(1));
            Assert.That(compiler.fgBBcount, Is.EqualTo(2));
            Assert.That(compiler.compPostImportationCleanupDone, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovesConsecutiveDeadEHEntriesAndReindexesSurvivor(bool keepLast)
    {
        WithCompiler(compiler => {
            var entry = AddBlock(compiler);
            compiler.compHndBBtab = new EHblkDsc[3];
            compiler.compHndBBtabCount = 3;
            for (ushort i = 0; i < 3; i++)
            {
                var imported = keepLast && (i == 2);
                var tryBlock = AddBlock(compiler, imported);
                var handler = AddBlock(compiler, imported);
                compiler.compHndBBtab[i] = Clause(tryBlock, tryBlock, handler, handler, i);
            }

            var lastTry = compiler.compHndBBtab[2].ebdTryBeg;
            var lastHandler = compiler.compHndBBtab[2].ebdHndBeg;
            if (keepLast)
            {
                Jump(compiler, entry, lastTry);
            }

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(keepLast ? 1 : 0));
            Assert.That(compiler.compHndBBtab.Length, Is.EqualTo(3));
            if (keepLast)
            {
                Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(lastTry));
                Assert.That(lastTry.TryIndex, Is.EqualTo(0));
                Assert.That(lastHandler.HndIndex, Is.EqualTo(0));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TrimsRegionEndsThroughRemovedSentinels(bool filter)
    {
        WithCompiler(compiler => {
            var entry = AddBlock(compiler);
            var tryLast = AddBlock(compiler, imported: false);
            _ = AddBlock(compiler, imported: false);
            var filterBlock = filter ? AddBlock(compiler) : null;
            var handler = AddBlock(compiler);
            var handlerLast = AddBlock(compiler, imported: false);
            _ = AddBlock(compiler, imported: false);
            var clause = Clause(entry, tryLast, handler, handlerLast, 0);
            if (filterBlock is not null)
            {
                filterBlock.HndIndex = 0;
                filterBlock.SetFlags(BBF_DONT_REMOVE);
                clause.ebdHandlerType = EHHandlerType.EH_HANDLER_FILTER;
                clause.ebdFilter = filterBlock;
            }
            compiler.compHndBBtab = [clause];
            compiler.compHndBBtabCount = 1;

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(entry));
            Assert.That(compiler.compHndBBtab[0].ebdHndLast, Is.SameAs(handler));
            Assert.That(compiler.compHndBBtab[0].ebdFilter, Is.SameAs(filterBlock));
            Assert.That(compiler.fgLastBB, Is.SameAs(handler));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, true)]
    public static void BuildsMidTryStepBlocksAndPreservesNormalProfile(bool profile, bool zeroWeight, bool nested)
    {
        WithCompiler(compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.fgPgoHaveWeights = profile;
            var entry = AddBlock(compiler);
            var outer = nested ? AddBlock(compiler) : null;
            var tryEntry = AddBlock(compiler);
            var osrEntry = AddBlock(compiler);
            var handler = AddBlock(compiler);
            var outerHandler = nested ? AddBlock(compiler) : null;
            compiler.fgOSREntryBB = osrEntry;
            Jump(compiler, entry, osrEntry);
            Jump(compiler, tryEntry, osrEntry);
            osrEntry.TryIndex = 0;
            var inner = Clause(tryEntry, osrEntry, handler, handler, 0);
            if (outer is not null && outerHandler is not null)
            {
                Jump(compiler, outer, tryEntry);
                inner.ebdEnclosingTryIndex = 1;
                handler.TryIndex = 1;
                compiler.compHndBBtab = [inner, Clause(outer, handler, outerHandler, outerHandler, 1)];
            }
            else
            {
                compiler.compHndBBtab = [inner];
            }
            compiler.compHndBBtabCount = (ushort)compiler.compHndBBtab.Length;
            var methodWeight = zeroWeight ? 0 : 10;
            var normalWeight = zeroWeight ? 0 : 40;
            entry.setBBProfileWeight(methodWeight);
            tryEntry.setBBProfileWeight(normalWeight);
            outer?.setBBProfileWeight(normalWeight);
            var count = compiler.fgBBcount;

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count + (nested ? 2 : 1)));
            Assert.That(entry.Target, Is.SameAs(outer ?? tryEntry));
            Assert.That(tryEntry.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(tryEntry.TrueTarget, Is.SameAs(osrEntry));
            Assert.That(tryEntry.FalseTarget.Target, Is.SameAs(osrEntry));
            Assert.That(tryEntry.FalseTarget.HasFlag(BBF_DONT_REMOVE), Is.False);
            Assert.That(tryEntry.HasFlag(BBF_INTERNAL | BBF_DONT_REMOVE), Is.True);
            Assert.That(tryEntry.TrueEdge.Likelihood, Is.EqualTo(profile ? (zeroWeight ? 1 : 0.2) : 0.9));
            Assert.That(tryEntry.FalseEdge.Likelihood, Is.EqualTo(profile ? (zeroWeight ? 0 : 0.8) : 0.1));
            Assert.That(tryEntry.bbWeight, Is.EqualTo(profile ? normalWeight + methodWeight : normalWeight));
            Assert.That(tryEntry.FalseTarget.bbWeight, Is.EqualTo(normalWeight));
            if (outer is not null)
            {
                Assert.That(outer.TrueTarget, Is.SameAs(tryEntry));
                Assert.That(outer.FalseTarget.Target, Is.SameAs(tryEntry));
            }

            var initialize = (entry.FirstStmt ?? throw new InvalidOperationException()).RootNode.AsLclVarCommon();
            var set = (osrEntry.FirstStmt ?? throw new InvalidOperationException()).RootNode.AsLclVarCommon();
            Assert.That(initialize.LclNum, Is.EqualTo(set.LclNum));
            Assert.That(initialize.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
            Assert.That(set.Data.AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.compPostImportationCleanupDone, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DirectTryEntryNeedsNoStepBlocks(bool mutualProtect)
    {
        WithCompiler(compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            var entry = AddBlock(compiler);
            var osrEntry = AddBlock(compiler);
            var handler = AddBlock(compiler);
            var inner = Clause(osrEntry, osrEntry, handler, handler, 0);
            if (mutualProtect)
            {
                var outerHandler = AddBlock(compiler);
                inner.ebdEnclosingTryIndex = 1;
                var outer = Clause(osrEntry, osrEntry, outerHandler, outerHandler, 1);
                osrEntry.TryIndex = 0;
                compiler.compHndBBtab = [inner, outer];
            }
            else
            {
                compiler.compHndBBtab = [inner];
            }
            compiler.compHndBBtabCount = (ushort)compiler.compHndBBtab.Length;
            compiler.fgOSREntryBB = osrEntry;
            Jump(compiler, entry, osrEntry);
            var count = compiler.fgBBcount;

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(mutualProtect
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count));
            Assert.That(entry.Target, Is.SameAs(osrEntry));
            Assert.That(compiler.lvaCount, Is.EqualTo(mutualProtect ? 1 : 0));
            Assert.That(compiler.compPostImportationCleanupDone, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TrimsUnimportedTryStartWithoutLosingRegion(bool removeMoreThanOne)
    {
        WithCompiler(compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            var entry = AddBlock(compiler);
            var oldTry = AddBlock(compiler, imported: false);
            if (removeMoreThanOne)
            {
                _ = AddBlock(compiler, imported: false);
            }
            var osrEntry = AddBlock(compiler);
            var handler = AddBlock(compiler);
            compiler.compHndBBtab = [Clause(oldTry, osrEntry, handler, handler, 0)];
            compiler.compHndBBtabCount = 1;
            osrEntry.TryIndex = 0;
            compiler.fgOSREntryBB = osrEntry;
            Jump(compiler, entry, osrEntry);

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(osrEntry));
            Assert.That(osrEntry.HasFlag(BBF_DONT_REMOVE), Is.True);
            Assert.That(compiler.fgBBcount, Is.EqualTo(3));
            Assert.That(compiler.lvaCount, Is.Zero);
            Assert.That(entry.Target, Is.SameAs(osrEntry));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public static void RefinesOnlyEligibleInlineReturnSpills(bool singleDef, bool fresh, bool expectedExact)
    {
        WithCompiler(compiler => {
            _ = AddBlock(compiler);
            var policy = (DefaultPolicy)RuntimeHelpers.GetUninitializedObject(typeof(DefaultPolicy));
            var result = (InlineResult)RuntimeHelpers.GetUninitializedObject(typeof(InlineResult));
            Policy(result) = policy;
            compiler.compInlineResult = result;
            var cls = (CORINFO_CLASS_STRUCT_*)0x1000;
            compiler.impInlineInfo = new InlineInfo { InlineRoot = compiler, retExprClassHnd = cls, retExprClassHndIsExact = true };
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF, lvClassHnd = cls, lvSingleDef = singleDef }];
            compiler.lvaCount = 1;
            compiler.lvaInlineeReturnSpillTemp = 0;
            compiler.lvaInlineeReturnSpillTempFreshlyCreated = fresh;

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvClassIsExact, Is.EqualTo(expectedExact));
            Assert.That(compiler.compPostImportationCleanupDone, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void TrimmingPreservesDistinctOrMutuallyProtectingEntries(bool mutualProtect, bool handlerFirst)
    {
        WithCompiler(compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            var entry = AddBlock(compiler);
            var oldOuter = AddBlock(compiler, imported: false);
            var earlyHandler = handlerFirst ? AddBlock(compiler) : null;
            var innerEntry = AddBlock(compiler);
            var osrEntry = AddBlock(compiler);
            var innerHandler = earlyHandler ?? AddBlock(compiler);
            var outerHandler = AddBlock(compiler);
            var inner = Clause(innerEntry, osrEntry, innerHandler, innerHandler, 0);
            inner.ebdEnclosingTryIndex = 1;
            if (!mutualProtect)
            {
                innerHandler.TryIndex = 1;
            }
            var outerLast = (mutualProtect || handlerFirst) ? osrEntry : innerHandler;
            compiler.compHndBBtab = [inner, Clause(oldOuter, outerLast, outerHandler, outerHandler, 1)];
            compiler.compHndBBtabCount = 2;
            osrEntry.TryIndex = 0;
            compiler.fgOSREntryBB = osrEntry;
            Jump(compiler, entry, osrEntry);
            Jump(compiler, innerEntry, osrEntry);
            var count = compiler.fgBBcount;

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var newOuter = compiler.compHndBBtab[1].ebdTryBeg;
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(2));
            Assert.That(newOuter == innerEntry, Is.EqualTo(mutualProtect));
            Assert.That(entry.Target, Is.SameAs(newOuter));
            Assert.That(innerEntry.TrueTarget, Is.SameAs(osrEntry));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count + (mutualProtect ? 0 : 2)));
            if (!mutualProtect)
            {
                Assert.That(newOuter.TrueTarget, Is.SameAs(innerEntry));
                Assert.That(newOuter.TryIndex, Is.EqualTo(1));
                Assert.That(newOuter.FalseTarget.Kind, Is.EqualTo(handlerFirst ? BBJ_THROW : BBJ_ALWAYS));
                Assert.That(newOuter.HasFlag(BBF_IMPORTED | BBF_INTERNAL | BBF_DONT_REMOVE), Is.True);
            }
        });
    }

    [Test]
    public static void FailedInlineDoesNotRemoveBlocks()
    {
        WithCompiler(compiler => {
            var dead = AddBlock(compiler, imported: false);
            var policy = (DefaultPolicy)RuntimeHelpers.GetUninitializedObject(typeof(DefaultPolicy));
            Decision(policy) = InlineDecision.FAILURE;
            var result = (InlineResult)RuntimeHelpers.GetUninitializedObject(typeof(InlineResult));
            Policy(result) = policy;
            compiler.compInlineResult = result;
            compiler.impInlineInfo = new InlineInfo { InlineRoot = compiler };

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgFirstBB, Is.SameAs(dead));
            Assert.That(dead.HasFlag(BBF_REMOVED), Is.False);
        });
    }

    [TestCase(TYP_INT, true, true)]
    [TestCase(TYP_REF, false, true)]
    [TestCase(TYP_REF, true, false)]
    public static void LeavesUnknownOrAbsentReferenceSpillsAlone(var_types type, bool hasClass, bool hasSpill)
    {
        WithCompiler(compiler => {
            _ = AddBlock(compiler);
            var result = (InlineResult)RuntimeHelpers.GetUninitializedObject(typeof(InlineResult));
            Policy(result) = (DefaultPolicy)RuntimeHelpers.GetUninitializedObject(typeof(DefaultPolicy));
            compiler.compInlineResult = result;
            var cls = (CORINFO_CLASS_STRUCT_*)0x1000;
            compiler.impInlineInfo = new InlineInfo {
                InlineRoot = compiler, retExprClassHnd = hasClass ? cls : null, retExprClassHndIsExact = true
            };
            compiler.lvaTable = [new LclVarDsc { Type = type, lvClassHnd = cls, lvSingleDef = true }];
            compiler.lvaCount = 1;
            compiler.lvaInlineeReturnSpillTemp = hasSpill ? 0 : BAD_VAR_NUM;

            Assert.That(compiler.fgPostImportationCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvClassIsExact, Is.False);
            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(type));
        });
    }

    private static EHblkDsc Clause(BasicBlock first, BasicBlock last, BasicBlock handler, BasicBlock handlerLast, ushort index)
    {
        first.TryIndex = index;
        handler.HndIndex = index;
        first.SetFlags(BBF_DONT_REMOVE);
        handler.SetFlags(BBF_DONT_REMOVE);
        return new EHblkDsc {
            ebdTryBeg = first, ebdTryLast = last, ebdHndBeg = handler, ebdHndLast = handlerLast,
            ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        };
    }

    private static BasicBlock AddBlock(Compiler compiler, bool imported = true)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.bbRefs = 0;
        if (imported)
        {
            block.SetFlags(BBF_IMPORTED);
        }
        if (compiler.fgLastBB is BasicBlock last)
        {
            last.Next = block;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;
        return block;
    }

    private static void Jump(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, source));
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.runWithSPMIErrorTrap = &UnavailableClassName;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.compHndBBtab = [];
        compiler.lvaTable = [];
        compiler.fgPredsComputed = true;
        compiler.info.compIsStatic = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var jitTls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitTls.Compiler = compiler;
        JitConfig = new JitConfigValues();
        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_policy")]
    private static extern ref InlinePolicy Policy(InlineResult result);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_decision")]
    private static extern ref InlineDecision Decision(InlinePolicy policy);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte UnavailableClassName(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        return 0;
    }
}
