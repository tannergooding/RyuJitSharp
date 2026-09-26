// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoopSideEffectTests
{
    [Test]
    public static void LoopInvariantVNsRespectDefinitionLocationAndFunctionArguments()
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var blocks = CreateNestedLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            compiler.vnStore = new ValueNumStore(compiler);
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_INT;
            var outsideDef = local.lvPerSsaData.AllocSsaNum();
            var insideDef = local.lvPerSsaData.AllocSsaNum();
            local.GetPerSsaData(outsideDef).Block = blocks[0];
            local.GetPerSsaData(insideDef).Block = blocks[2];

            var outer = compiler._blockToLoop.GetLoop(blocks[1])!;
            var inner = compiler._blockToLoop.GetLoop(blocks[2])!;
            var store = compiler.vnStore;
            var cache = new Dictionary<int, bool>();
            var outerCache = new Dictionary<int, bool>();

            Assert.That(compiler.optVNIsLoopInvariant(ValueNumStore.NoVN, inner, cache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(ValueNumStore.VNForVoid(), inner, cache), Is.True);
            var constant = store.VNForIntCon(7);
            Assert.That(compiler.optVNIsLoopInvariant(constant, inner, cache), Is.True);
            Assert.That(cache, Is.Empty);

            var unknown = store.VNForExpr(null, TYP_INT);
            var outside = store.VNForExpr(blocks[0], TYP_INT);
            var outerValue = store.VNForExpr(blocks[1], TYP_INT);
            var innerValue = store.VNForExpr(blocks[2], TYP_INT);
            Assert.That(compiler.optVNIsLoopInvariant(unknown, inner, cache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(outside, outer, outerCache), Is.True);
            Assert.That(compiler.optVNIsLoopInvariant(outerValue, inner, cache), Is.True);
            Assert.That(compiler.optVNIsLoopInvariant(outerValue, outer, outerCache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(innerValue, outer, outerCache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(innerValue, inner, cache), Is.False);

            var invariantExpr = store.VNForFunc(TYP_INT, VNF_ADD, outerValue, constant);
            var variantExpr = store.VNForFunc(TYP_INT, VNF_ADD, outside, innerValue);
            Assert.That(compiler.optVNIsLoopInvariant(invariantExpr, inner, cache), Is.True);
            Assert.That(compiler.optVNIsLoopInvariant(variantExpr, inner, cache), Is.False);

            var outsidePhi = store.VNForPhiDef(TYP_INT, 0, outsideDef, []);
            var insidePhi = store.VNForPhiDef(TYP_INT, 0, insideDef, []);
            Assert.That(compiler.optVNIsLoopInvariant(outsidePhi, inner, cache), Is.True);
            Assert.That(compiler.optVNIsLoopInvariant(insidePhi, outer, outerCache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(insidePhi, inner, cache), Is.False);

            var outsideMemoryPhi = store.VNForMemoryPhiDef(blocks[0], []);
            var insideMemoryPhi = store.VNForMemoryPhiDef(blocks[2], []);
            Assert.That(compiler.optVNIsLoopInvariant(outsideMemoryPhi, inner, cache), Is.True);
            Assert.That(compiler.optVNIsLoopInvariant(insideMemoryPhi, outer, outerCache), Is.False);

            var mapOutside = store.VNForFunc(TYP_HEAP, VNF_MapStore,
                outsideMemoryPhi, constant, outside, ValueNumStore.NoLoop);
            var mapInside = store.VNForFunc(TYP_HEAP, VNF_MapStore,
                outsideMemoryPhi, constant, outside, inner.Index);
            Assert.That(compiler.optVNIsLoopInvariant(mapOutside, inner, cache), Is.True);
            Assert.That(compiler.optVNIsLoopInvariant(mapInside, inner, cache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(mapInside, outer, outerCache), Is.False);
            Assert.That(compiler.optVNIsLoopInvariant(
                store.VNForFunc(TYP_HEAP, VNF_MapStore, outsideMemoryPhi, constant, outside, outer.Index),
                inner, cache), Is.True);

            cache[insidePhi] = true;
            Assert.That(compiler.optVNIsLoopInvariant(insidePhi, inner, cache), Is.True);
            Assert.That(cache, Does.ContainKey(invariantExpr));
            Assert.That(cache, Does.ContainKey(variantExpr));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void ModifiedLocationsFollowNativeBucketAndCollisionOrder()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var effects = new LoopSideEffects();

        foreach (var handle in new nint[] { 0x1000, 0x1008, 0x1010, 0x1019 })
        {
            effects.AddModifiedField(compiler, (CORINFO_FIELD_STRUCT_*)handle, default);
        }
        effects.AddModifiedField(compiler, (CORINFO_FIELD_STRUCT_*)0x1000, FieldKindForVN.WithBaseAddr);

        var fields = new List<nint>();
        foreach (var entry in effects.EnumerateModifiedFieldsInNativeOrder())
        {
            fields.Add((nint)entry.Key.Value);
            if ((nint)entry.Key.Value == 0x1000)
            {
                Assert.That(entry.Value, Is.EqualTo(FieldKindForVN.WithBaseAddr));
            }
        }
        Assert.That(fields, Is.EqualTo(new nint[] { 0x1008, 0x1000, 0x1019, 0x1010 }));

        foreach (var handle in new nint[] { 9, 18, 27 })
        {
            effects.AddModifiedElemType(compiler, (CORINFO_CLASS_STRUCT_*)handle);
        }
        var elements = new List<nint>();
        foreach (var elemType in effects.EnumerateModifiedElemTypesInNativeOrder())
        {
            elements.Add((nint)elemType.Value);
        }
        Assert.That(elements, Is.EqualTo(new nint[] { 27, 18, 9 }));
    }

    [Test]
    public static void RehashReversesNativeCollisionChainEvenForAnOverwrite()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var effects = new LoopSideEffects();

        foreach (var handle in new nint[] { 8, 1664, 3320, 4976, 6632, 8288 })
        {
            effects.AddModifiedField(compiler, (CORINFO_FIELD_STRUCT_*)handle, default);
        }

        // A duplicate Set checks growth before its lookup at six entries.
        effects.AddModifiedField(compiler, (CORINFO_FIELD_STRUCT_*)8, FieldKindForVN.WithBaseAddr);
        effects.AddModifiedField(compiler, (CORINFO_FIELD_STRUCT_*)9944, default);

        var fields = new List<nint>();
        foreach (var entry in effects.EnumerateModifiedFieldsInNativeOrder())
        {
            fields.Add((nint)entry.Key.Value);
        }
        Assert.That(fields, Is.EqualTo(new nint[] { 9944, 8, 1664, 3320, 4976, 6632, 8288 }));
        Assert.That(effects.FieldsModified, Has.Count.EqualTo(7));
    }

    [Test]
    public static void EarlierArrayAddressSsaValueClassifiesLaterByrefStore()
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var blocks = CreateNestedLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            compiler.vnStore = new ValueNumStore(compiler);
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_BYREF;
            var ssa = local.lvPerSsaData.AllocSsaNum();
            foreach (var block in blocks)
            {
                block.bbLiveIn = VarSetOps.MakeEmpty(compiler);
                block.bbLiveOut = VarSetOps.MakeEmpty(compiler);
                block.bbVarUse = VarSetOps.MakeEmpty(compiler);
                block.bbVarDef = VarSetOps.MakeEmpty(compiler);
            }

            var arrayAddress = new GenTreeArrAddr(
                compiler.gtNewIconNode(TYP_I_IMPL, 0x4000), TYP_INT, null, 16);
            var definition = compiler.gtNewStoreLclVarNode(0, arrayAddress);
            definition.SsaNum = ssa;
            var definitionStmt = compiler.gtNewStmt(definition);
            compiler.fgSetStmtSeq(definitionStmt);
            compiler.fgInsertStmtAtEnd(blocks[2], definitionStmt);

            var use = compiler.gtNewLclvNode(TYP_BYREF, 0);
            use.SsaNum = ssa;
            var indirect = compiler.gtNewStoreIndNode(TYP_INT, use, compiler.gtNewIconNode(TYP_INT, 4));
            var indirectStmt = compiler.gtNewStmt(indirect);
            compiler.fgSetStmtSeq(indirectStmt);
            compiler.fgInsertStmtAtEnd(blocks[2], indirectStmt);

            compiler.optComputeLoopSideEffects();

            var arrayVN = arrayAddress._vnPair.Liberal;
            Assert.That(arrayVN, Is.Not.EqualTo(ValueNumStore.NoVN));
            Assert.That(local.GetPerSsaData(ssa)._vnPair.Liberal, Is.EqualTo(arrayVN));
            Assert.That(compiler._loopSideEffects, Has.Length.EqualTo(2));
            foreach (var effects in compiler._loopSideEffects!)
            {
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.ByrefExposed], Is.True);
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.GcHeap], Is.False);
                var elemType = new Pointer<CORINFO_CLASS_STRUCT_>(
                    (CORINFO_CLASS_STRUCT_*)(((nint)TYP_INT << 1) | 1));
                Assert.That(effects.ArrayElemTypesModified, Does.ContainKey(elemType));
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void AcyclicGraphHasNoLoopEffectRecords()
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 1;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgPredsComputed = true;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);

            compiler.optComputeLoopSideEffects();
            Assert.That(compiler._loopSideEffects, Is.Null);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(CORINFO_HELP_DIV, false, false)]
    [TestCase(CORINFO_HELP_GET_GCSTATIC_BASE, false, true)]
    [TestCase(CORINFO_HELP_GET_GCSTATIC_BASE, true, false)]
    [TestCase(CORINFO_HELP_ASSIGN_REF, true, true)]
    public static void HelperCallEffectsRespectHeapAndCctorProperties(
        CorInfoHelpFunc helper, bool hoistable, bool hasHavoc)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var blocks = CreateNestedLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            compiler.vnStore = new ValueNumStore(compiler);
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            foreach (var block in blocks)
            {
                block.bbLiveIn = VarSetOps.MakeEmpty(compiler);
                block.bbLiveOut = VarSetOps.MakeEmpty(compiler);
                block.bbVarUse = VarSetOps.MakeEmpty(compiler);
                block.bbVarDef = VarSetOps.MakeEmpty(compiler);
            }

            var call = new GenTreeCall(TYP_VOID)
            {
                _callType = CT_HELPER,
                _callMethHnd = Compiler.eeFindHelper(helper),
            };
            if (hoistable)
            {
                call.Flags |= GTF_CALL_HOISTABLE;
            }

            var stmt = compiler.gtNewStmt(call);
            compiler.fgSetStmtSeq(stmt);
            compiler.fgInsertStmtAtEnd(blocks[2], stmt);
            compiler.optComputeLoopSideEffects();

            Assert.That(compiler._loopSideEffects, Has.Length.EqualTo(2));
            foreach (var effects in compiler._loopSideEffects!)
            {
                Assert.That(effects.ContainsCall, Is.True);
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.ByrefExposed], Is.EqualTo(hasHavoc));
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.GcHeap], Is.EqualTo(hasHavoc));
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase("array", false, false)]
    [TestCase("field", false, false)]
    [TestCase("unknown", true, false)]
    [TestCase("array", true, true)]
    public static void FullAnalysisDistinguishesModifiedLocationsAndRetainsCalls(
        string location, bool includeCall, bool includeVolatileLoad)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaTrackedCount = 4;
        compiler.lvaTrackedCountInSizeTUnits = 1;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isFieldStatic = &IsFieldStatic;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo };
        JitTls.Compiler = compiler;

        try
        {
            var blocks = CreateNestedLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            compiler.vnStore = new ValueNumStore(compiler);
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            foreach (var block in blocks)
            {
                block.bbLiveIn = VarSetOps.MakeEmpty(compiler);
                block.bbLiveOut = VarSetOps.MakeEmpty(compiler);
                block.bbVarUse = VarSetOps.MakeEmpty(compiler);
                block.bbVarDef = VarSetOps.MakeEmpty(compiler);
            }
            VarSetOps.AddElemD(compiler, blocks[2].bbLiveIn, 0);
            VarSetOps.AddElemD(compiler, blocks[2].bbLiveOut, 1);
            VarSetOps.AddElemD(compiler, blocks[2].bbVarUse, 2);
            VarSetOps.AddElemD(compiler, blocks[2].bbVarDef, 3);

            var inner = compiler._blockToLoop.GetLoop(blocks[2]);
            Assert.That(inner, Is.Not.Null);
            var field = location == "field"
                ? new FieldSeq((CORINFO_FIELD_STRUCT_*)0x1000, 0x1000,
                    FieldSeq.FieldKind.SimpleStaticKnownAddress)
                : null;
            GenTree address = location switch
            {
                "array" => new GenTreeArrAddr(compiler.gtNewIconNode(TYP_I_IMPL, 0x4000), TYP_UINT, null, 16),
                "field" => compiler.gtNewIconNode(0x1010,
                    field ?? throw new InvalidOperationException("Missing field sequence.")),
                _ => compiler.gtNewIconNode(TYP_I_IMPL, 0x8000),
            };
            if (location == "field")
            {
                address.Flags |= GTF_ICON_STATIC_HDL;
            }

            var store = compiler.gtNewStoreIndNode(TYP_INT, address, compiler.gtNewIconNode(TYP_INT, 7));
            var storeStatement = compiler.gtNewStmt(store);
            compiler.fgSetStmtSeq(storeStatement);
            compiler.fgInsertStmtAtEnd(blocks[2], storeStatement);
            store._vnPair.SetBoth(compiler.vnStore.VNForIntCon(7));

            if (includeVolatileLoad)
            {
                var load = compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 0x9000),
                    GTF_IND_VOLATILE);
                var stmt = compiler.gtNewStmt(load);
                compiler.fgSetStmtSeq(stmt);
                compiler.fgInsertStmtAtEnd(blocks[2], stmt);
            }

            if (includeCall)
            {
                var call = new GenTreeCall(TYP_VOID) { _callType = CT_USER_FUNC };
                var stmt = compiler.gtNewStmt(call);
                compiler.fgSetStmtSeq(stmt);
                compiler.fgInsertStmtAtEnd(blocks[2], stmt);
            }

            compiler.optComputeLoopSideEffects();

            Assert.That(compiler._loopSideEffects, Has.Length.EqualTo(2));
            foreach (var loop in new[] { inner!, inner!.Parent! })
            {
                var effects = compiler._loopSideEffects![loop.Index];
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.ByrefExposed], Is.True);
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.GcHeap],
                    Is.EqualTo(location == "unknown" || includeVolatileLoad || includeCall));
                Assert.That(VarSetOps.IsMember(compiler, effects.VarInOut, 0), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, effects.VarInOut, 1), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, effects.VarUseDef, 2), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, effects.VarUseDef, 3), Is.True);
                Assert.That(effects.ContainsCall, Is.EqualTo(includeCall));
                Assert.That(effects.ArrayElemTypesModified is not null,
                    Is.EqualTo(location == "array"));
                Assert.That(effects.FieldsModified is not null, Is.EqualTo(location == "field"));

                if (location == "array")
                {
                    var encodedElemType = ((nint)TYP_INT << 1) | 1;
                    var elemKey = new Pointer<CORINFO_CLASS_STRUCT_>((CORINFO_CLASS_STRUCT_*)encodedElemType);
                    Assert.That(effects.ArrayElemTypesModified, Does.ContainKey(elemKey));
                    if (address is GenTreeArrAddr arrAddr)
                    {
                        VNFuncApp app = default;
                        Assert.That(compiler.vnStore.GetVNFunc(arrAddr._vnPair.Liberal, ref app), Is.True);
                        Assert.That(app.FuncIs(VNFunc.VNF_PtrToArrElem), Is.True);
                    }
                }
                else if (location == "field")
                {
                    var fieldKey = new Pointer<CORINFO_FIELD_STRUCT_>((CORINFO_FIELD_STRUCT_*)0x1000);
                    Assert.That(effects.FieldsModified, Does.ContainKey(fieldKey));
                }
            }

            Assert.That(store._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void EffectsPropagateFromInnerLoopToAllContainingLoops()
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaTrackedCount = 4;
        compiler.lvaTrackedCountInSizeTUnits = 1;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var blocks = CreateNestedLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            Assert.That(compiler._loops.NumLoops, Is.EqualTo(2));
            compiler._loopSideEffects = [new LoopSideEffects(), new LoopSideEffects()];
            foreach (var effects in compiler._loopSideEffects)
            {
                effects.VarInOut = VarSetOps.MakeEmpty(compiler);
                effects.VarUseDef = VarSetOps.MakeEmpty(compiler);
            }

            var inner = compiler._blockToLoop.GetLoop(blocks[2]);
            Assert.That(inner, Is.Not.Null);
            var outer = inner!.Parent;
            Assert.That(outer, Is.Not.Null);
            var block = blocks[2];
            block.bbLiveIn = VarSetOps.MakeSingleton(compiler, 0);
            block.bbLiveOut = VarSetOps.MakeSingleton(compiler, 1);
            block.bbVarUse = VarSetOps.MakeSingleton(compiler, 2);
            block.bbVarDef = VarSetOps.MakeSingleton(compiler, 3);

            compiler.AddVariableLivenessAllContainingLoops(inner, block);
            compiler.optRecordLoopNestsMemoryHavoc(inner, 1 << (int)MemoryKind.GcHeap);
            compiler.AddContainsCallAllContainingLoops(inner);
            var field = new Pointer<CORINFO_FIELD_STRUCT_>((CORINFO_FIELD_STRUCT_*)0x100);
            var elem = new Pointer<CORINFO_CLASS_STRUCT_>((CORINFO_CLASS_STRUCT_*)0x200);
            compiler.AddModifiedFieldAllContainingLoops(inner, field, default);
            compiler.AddModifiedElemTypeAllContainingLoops(inner, elem);

            foreach (var loop in new[] { inner, outer! })
            {
                var effects = compiler._loopSideEffects[loop.Index];
                Assert.That(VarSetOps.IsMember(compiler, effects.VarInOut, 0), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, effects.VarInOut, 1), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, effects.VarUseDef, 2), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, effects.VarUseDef, 3), Is.True);
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.GcHeap], Is.True);
                Assert.That(effects.HasMemoryHavoc[(int)MemoryKind.ByrefExposed], Is.False);
                Assert.That(effects.ContainsCall, Is.True);
                Assert.That(effects.FieldsModified, Does.ContainKey(field));
                Assert.That(effects.ArrayElemTypesModified, Does.ContainKey(elem));
            }

            compiler.optRecordLoopNestsMemoryHavoc(outer!, 1 << (int)MemoryKind.ByrefExposed);
            Assert.That(compiler._loopSideEffects[outer!.Index].HasMemoryHavoc[(int)MemoryKind.ByrefExposed],
                Is.True);
            Assert.That(compiler._loopSideEffects[inner.Index].HasMemoryHavoc[(int)MemoryKind.ByrefExposed],
                Is.False);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static BasicBlock[] CreateNestedLoop(Compiler compiler)
    {
        BasicBlock[] blocks =
        [
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
        ];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgPredsComputed = true;
        blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
        blocks[1].SetCond(compiler.fgAddRefPred(blocks[2], blocks[1]),
            compiler.fgAddRefPred(blocks[4], blocks[1]));
        blocks[2].SetCond(compiler.fgAddRefPred(blocks[2], blocks[2]),
            compiler.fgAddRefPred(blocks[3], blocks[2]));
        blocks[3].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[3]));
        return blocks;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsFieldStatic(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* field)
        => 1;
}
