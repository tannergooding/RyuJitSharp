// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgValueNumber()
    {
        JITDUMP("\n*************** In fgValueNumber()\n");
        if (fgSsaPassesCompleted == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert((fgVNPassesCompleted > 0) || (vnStore is null));
        if (fgVNPassesCompleted == 0)
        {
            vnStore = new ValueNumStore(this);
        }
        else
        {
            var noValues = new ValueNumPair();
            for (var index = 0; index < lvMemoryPerSsaData.Count; index++)
            {
                lvMemoryPerSsaData.GetSsaDefByIndex(index)._vnPair = noValues;
            }
            foreach (var block in Blocks)
            {
                for (var statement = block.GetFirstNonPhiDef(); statement is not null; statement = statement.NextStmt)
                {
                    foreach (var tree in statement.TreeList)
                    {
                        tree._vnPair.SetBoth(ValueNumStore.NoVN);
                    }
                }
            }
        }

        assert(vnStore is not null);
        assert(_dfsTree is not null);
        assert(_loops is not null);
        assert(fgFirstBB is not null);
        _blockToLoop = BlockToNaturalLoopMap.Build(_loops);
        optComputeLoopSideEffects();

        for (var local = 0; local < lvaCount; local++)
        {
            if (!lvaTable[local].lvInSsa)
            {
                continue;
            }
            ref var descriptor = ref lvaGetDesc(local);
            assert(descriptor.lvTracked);
            if (descriptor.lvIsParam)
            {
                // InitVal identifies incoming values as invariant in every loop.
                var initialValue = vnStore.VNForFunc(descriptor.Type, VNF_InitVal, vnStore.VNForIntCon(local));
                ref var definition = ref descriptor.GetPerSsaData(SsaConfig.FIRST_SSA_NUM);
                definition._vnPair.SetBoth(initialValue);
                definition.Block = fgFirstBB;
            }
            else if (info.compInitMem || descriptor.lvMustInit ||
                (varTypeIsGC(descriptor.Type) && !descriptor.lvHasExplicitInit) ||
                VarSetOps.IsMember(this, fgFirstBB.bbLiveIn, descriptor._varIndex))
            {
                // Live-in use-before-def locals may need an unknown initial value
                // rather than zero; use the same prolog initialization decision.
                var isZeroed = !fgVarNeedsExplicitZeroInit(local, bbInALoop: false, bbIsReturn: false);
                var type = descriptor.Type;
                ValueNum initialValue;
                if (isZeroed)
                {
                    if (type is TYP_STRUCT)
                    {
                        var layout = descriptor.Layout;
                        assert(layout is not null);
                        initialValue = vnStore.VNForZeroObj(layout);
                    }
                    else
                    {
                        initialValue = vnStore.VNZeroForType(type);
                    }
                }
                else
                {
                    initialValue = vnStore.VNForFunc(type, VNF_InitVal, vnStore.VNForIntCon(local));
                }
#if TARGET_X86
                if ((local == lvaVarargsBaseOfStkArgs) || (local == lvaVarargsHandleArg))
                {
                    initialValue = vnStore.VNForExpr(fgFirstBB, TYP_UNKNOWN);
                }
#endif
                assert(initialValue != ValueNumStore.NoVN);
                ref var definition = ref descriptor.GetPerSsaData(SsaConfig.FIRST_SSA_NUM);
                definition._vnPair.SetBoth(initialValue);
                definition.Block = fgFirstBB;
            }
        }

        var memoryInitialValue = vnStore.VNForFunc(TYP_HEAP, VNF_InitVal, vnStore.VNForIntCon(-1));
        GetMemoryPerSsaData(SsaConfig.FIRST_SSA_NUM)._vnPair.SetBoth(memoryInitialValue);
        JITDUMP($"Memory Initial Value in BB01 is: ${memoryInitialValue:x}\n");
        vnState = new ValueNumberState(this);

        // SSA's EH-aware reverse postorder visits predecessors first when possible.
        var traits = _dfsTree.PostOrderTraits();
        var visitedBlocks = BitVecOps.MakeEmpty(traits);
        for (var index = _dfsTree.PostOrderCount; index != 0; index--)
        {
            fgValueNumberBlocks(_dfsTree.GetPostOrder(index - 1), visitedBlocks, traits);
        }
#if DEBUG
        JitTestCheckVN();
        fgDebugCheckExceptionSets();
#endif
        fgVNPassesCompleted++;
        vnState = null;

        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    public void fgValueNumberBlocks(BasicBlock block, BitVec visitedBlocks, BitVecTraits traits)
    {
        if (BitVecOps.IsMember(traits, visitedBlocks, block.bbPostorderNum))
        {
            return;
        }

        JITDUMP($"Visiting BB{block.bbNum:D2}\n");
        assert(vnState is not null);
        if (block != fgFirstBB)
        {
            var anyPredReachable = false;
            for (var pred = BlockPredsWithEH(block); pred is not null; pred = pred.NextPredEdge)
            {
                var predBlock = pred.SourceBlock;
                if (!vnState.IsReachableThroughPred(block, predBlock))
                {
                    JITDUMP($"  Unreachable through pred BB{predBlock.bbNum:D2}\n");
                    continue;
                }

                JITDUMP($"  Reachable through pred BB{predBlock.bbNum:D2}\n");
                anyPredReachable = true;
                break;
            }

            if (!anyPredReachable)
            {
                JITDUMP($"  BB{block.bbNum:D2} was proven unreachable\n");
                vnState.SetUnreachable(block);
            }
        }

        fgValueNumberBlock(block);
        BitVecOps.AddElemD(traits, visitedBlocks, block.bbPostorderNum);

        assert(_blockToLoop is not null);
        var loop = _blockToLoop.GetLoop(block);
        if ((loop is not null) && (block == loop.Header))
        {
            _ = loop.VisitLoopBlocksReversePostOrder(loopBlock =>
            {
                fgValueNumberBlocks(loopBlock, visitedBlocks, traits);
                return BasicBlockVisit.Continue;
            });

            for (var statement = block.FirstStmt; (statement is not null) && statement.IsPhiDefnStmt;
                statement = statement.NextStmt)
            {
                fgValueNumberPhiDef(statement.RootNode.AsLclVar(), block, isUpdate: true);
            }
        }
    }

    public void fgValueNumberBlock(BasicBlock block)
    {
        compCurBB = block;
        var statement = block.FirstStmt;
        for (; (statement is not null) && statement.IsPhiDefnStmt; statement = statement.NextStmt)
        {
#if DEBUG
            if (verbose)
            {
                jitprintf($"\n***** BB{block.bbNum:D2}, STMT{statement.Id:D5}(before)\n");
                gtDispTree(statement.RootNode);
                jitprintf("\n");
            }
#endif

            fgValueNumberPhiDef(statement.RootNode.AsLclVar(), block);

#if DEBUG
            if (verbose)
            {
                jitprintf($"\n***** BB{block.bbNum:D2}, STMT{statement.Id:D5}(after)\n");
                gtDispTree(statement.RootNode);
                jitprintf("\n");
                if (statement.NextStmt is not null)
                {
                    jitprintf("---------\n");
                }
            }
#endif
        }

        assert(vnStore is not null);
        assert(_blockToLoop is not null);
        var phiArgSsaNums = new List<int>();
        foreach (var kind in new AllMemoryKinds())
        {
            if (block.bbMemorySsaPhiFunc[(int)kind] is null)
            {
                var memoryVN = GetMemoryPerSsaData(block.bbMemorySsaNumIn[(int)kind])._vnPair.Liberal;
                fgSetCurrentMemoryVN(kind, memoryVN);
            }
            else
            {
                if ((kind is ByrefExposed) && byrefStatesMatchGcHeapStates)
                {
                    assert(kind < GcHeap);
                    assert(block.bbMemorySsaPhiFunc[(int)kind] == block.bbMemorySsaPhiFunc[(int)GcHeap]);
                    continue;
                }

                ValueNum memoryVN;
                var loop = _blockToLoop.GetLoop(block);
                if (bbIsHandlerBeg(block))
                {
                    // Handler memory phis cannot describe intermediate states
                    // within protected blocks, so retain native's opaque state.
                    memoryVN = vnStore.VNForExpr(block, TYP_HEAP);
                }
                else if ((loop is not null) && (loop.Header == block))
                {
                    memoryVN = fgMemoryVNForLoopSideEffects(kind, block, loop);
                }
                else
                {
                    var phiArgs = block.bbMemorySsaPhiFunc[(int)kind];
                    assert(phiArgs is not null);
                    assert(phiArgs != BasicBlock.EmptyMemoryPhiDef);
                    assert((phiArgs._nextArg is not null) || opts.IsOSR);
                    phiArgSsaNums.Clear();
                    JITDUMP($"  Building memory phi def for block BB{block.bbNum:D2}.\n");
                    var sameVN = ValueNumStore.NoVN;
                    while (phiArgs is not null)
                    {
                        var phiArgVN = GetMemoryPerSsaData(phiArgs.SsaNum)._vnPair.Liberal;
                        if (phiArgSsaNums.Count == 0)
                        {
                            sameVN = phiArgVN;
                        }
                        else if ((phiArgVN == ValueNumStore.NoVN) || (phiArgVN != sameVN))
                        {
                            sameVN = ValueNumStore.NoVN;
                        }
                        phiArgSsaNums.Add(phiArgs.SsaNum);
                        phiArgs = phiArgs._nextArg;
                    }
                    memoryVN = sameVN != ValueNumStore.NoVN
                        ? sameVN
                        : vnStore.VNForMemoryPhiDef(block, CollectionsMarshal.AsSpan(phiArgSsaNums));
                }

                GetMemoryPerSsaData(block.bbMemorySsaNumIn[(int)kind])._vnPair.Liberal = memoryVN;
                fgSetCurrentMemoryVN(kind, memoryVN);
                if ((kind is GcHeap) && byrefStatesMatchGcHeapStates)
                {
                    fgSetCurrentMemoryVN(ByrefExposed, memoryVN);
                }
            }
#if DEBUG
            if (verbose)
            {
                jitprintf($"The SSA definition for {kind} (#{block.bbMemorySsaNumIn[(int)kind]}) at start of BB{block.bbNum:D2} is ");
                vnPrint(fgCurMemoryVN[(int)kind], 1);
                jitprintf("\n");
            }
#endif
        }

        for (; statement is not null; statement = statement.NextStmt)
        {
#if DEBUG
            if (verbose)
            {
                jitprintf($"\n***** BB{block.bbNum:D2}, STMT{statement.Id:D5}(before)\n");
                gtDispTree(statement.RootNode);
                jitprintf("\n");
            }
#endif
            foreach (var tree in statement.TreeList)
            {
                compCurTree = tree;
                fgValueNumberTree(tree);
                compCurTree = null;
            }
#if DEBUG
            if (verbose)
            {
                jitprintf($"\n***** BB{block.bbNum:D2}, STMT{statement.Id:D5}(after)\n");
                gtDispTree(statement.RootNode);
                jitprintf("\n");
                if (statement.NextStmt is not null)
                {
                    jitprintf("---------\n");
                }
            }
#endif
        }

        foreach (var kind in new AllMemoryKinds())
        {
            if ((kind is GcHeap) && byrefStatesMatchGcHeapStates)
            {
                assert(kind > ByrefExposed);
                assert(block.bbMemorySsaNumOut[(int)kind] == block.bbMemorySsaNumOut[(int)ByrefExposed]);
                assert(GetMemoryPerSsaData(block.bbMemorySsaNumOut[(int)kind])._vnPair.Liberal ==
                    fgCurMemoryVN[(int)kind]);
                continue;
            }

            if (block.bbMemorySsaNumOut[(int)kind] != block.bbMemorySsaNumIn[(int)kind])
            {
                GetMemoryPerSsaData(block.bbMemorySsaNumOut[(int)kind])._vnPair.Liberal = fgCurMemoryVN[(int)kind];
            }
        }

        compCurBB = null;
    }

    public void fgValueNumberPhiDef(GenTreeLclVar newSsaDef, BasicBlock block, bool isUpdate = false)
    {
        var phiArgSsaNums = new List<int>();
        var phiNode = newSsaDef.Data.AsPhi();
        var sameValues = new ValueNumPair();
        var loopInvariantCache = new VNSet();

        foreach (var use in phiNode.Uses)
        {
            var phiArg = use.Node.AsPhiArg();
            if ((vnState is not null) && !vnState.IsReachableThroughPred(block, phiArg.PredBB))
            {
#if DEBUG
                JITDUMP($"  Phi arg [{phiArg.TreeId:D6}] is unnecessary; path through pred BB{phiArg.PredBB.bbNum:D2} cannot be taken\n");
#endif
                if ((use.Next is not null) || (phiArgSsaNums.Count > 0))
                {
                    continue;
                }
                assert(!vnState.IsReachable(block) || isUpdate);
                JITDUMP("  ..but no other path can, so we are using it anyway\n");
            }

            var values = lvaGetDesc(phiArg.LclNum).GetPerSsaData(phiArg.SsaNum)._vnPair;
            if (isUpdate && (values != phiArg._vnPair))
            {
                assert(_loops is not null);
                var loop = _loops.GetLoopByHeader(block);
                assert(loop is not null);
                var canUseNewVN = optVNIsLoopInvariant(values.Conservative, loop, loopInvariantCache);
                if (canUseNewVN)
                {
#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"Updating phi arg [{phiArg.TreeId:D6}] VN from ");
                        vnpPrint(phiArg._vnPair, 0);
                        jitprintf(" to ");
                        vnpPrint(values, 0);
                        jitprintf("\n");
                    }
#endif
                }
                else
                {
#if DEBUG
                    JITDUMP($"Can't update phi arg [{phiArg.TreeId:D6}] with ${values.Conservative:x} -- varies in L{loop.Index:D2}\n");
#endif
                    values = phiArg._vnPair;
                }
            }
            phiArg._vnPair = values;

            if (phiArgSsaNums.Count == 0)
            {
                sameValues = values;
            }
            else if (sameValues != values)
            {
                sameValues.SetBoth(ValueNumStore.NoVN);
            }
            phiArgSsaNums.Add(phiArg.SsaNum);
        }

        assert(vnStore is not null);
        ref var definition = ref lvaGetDesc(newSsaDef.LclNum).GetPerSsaData(newSsaDef.SsaNum);
        var definitionValues = definition._vnPair;
        if (sameValues.BothDefined())
        {
            definitionValues = sameValues;
        }
        else
        {
            var newPhiDef = true;
            if (isUpdate)
            {
                VNPhiDef previous = default;
                if (vnStore.GetPhiDef(definition._vnPair.Liberal, ref previous) &&
                    (previous.SsaArgs.Length == phiArgSsaNums.Count))
                {
                    newPhiDef = false;
                }
            }
            if (newPhiDef)
            {
                definitionValues.SetBoth(vnStore.VNForPhiDef(newSsaDef.Type, newSsaDef.LclNum, newSsaDef.SsaNum,
                    CollectionsMarshal.AsSpan(phiArgSsaNums)));
            }
        }

#if DEBUG
        if (isUpdate)
        {
            assert(!definition._updated);
            definition._origVNPair = definition._vnPair;
            definition._updated = true;
        }
#endif
        definition._vnPair = definitionValues;
#if DEBUG
        if (verbose)
        {
            jitprintf($"SSA PHI definition: set VN of local {newSsaDef.LclNum}/{newSsaDef.SsaNum} to ");
            vnpPrint(definitionValues, 1);
            jitprintf($" {(sameValues.BothDefined() ? "(all same)" : "")}.\n");
        }
#endif

        newSsaDef._vnPair = ValueNumStore.VNPForVoid();
        phiNode._vnPair = definitionValues;
    }

    public unsafe ValueNum fgMemoryVNForLoopSideEffects(
        MemoryKind memoryKind, BasicBlock entryBlock, FlowGraphNaturalLoop loop)
    {
        JITDUMP($"Computing {memoryKind} state for block BB{entryBlock.bbNum:D2}, entry block for loop L{loop.Index:D2}:\n");
        assert(vnStore is not null);
        assert(_loopSideEffects is not null);
        var sideEffects = _loopSideEffects[loop.Index];
        if (sideEffects.HasMemoryHavoc[(int)memoryKind])
        {
            var result = vnStore.VNForExpr(entryBlock, TYP_HEAP);
            JITDUMP($"  Loop L{loop.Index:D2} has memory havoc effect; heap state is new unique ${result:x}.\n");
            return result;
        }

        BasicBlock? nonLoopPred = null;
        var multipleNonLoopPreds = false;
        for (var pred = BlockPredsWithEH(entryBlock); pred is not null; pred = pred.NextPredEdge)
        {
            var predBlock = pred.SourceBlock;
            if (!loop.ContainsBlock(predBlock))
            {
                if (nonLoopPred is null)
                {
                    nonLoopPred = predBlock;
                }
                else
                {
                    JITDUMP($"  Entry block has >1 non-loop preds: (at least) BB{nonLoopPred.bbNum:D2} and BB{predBlock.bbNum:D2}.\n");
                    multipleNonLoopPreds = true;
                    break;
                }
            }
        }
        if (multipleNonLoopPreds)
        {
            var result = vnStore.VNForExpr(entryBlock, TYP_HEAP);
            JITDUMP($"  Therefore, memory state is new, fresh ${result:x}.\n");
            return result;
        }

        assert(nonLoopPred is not null);
        var newMemoryVN = GetMemoryPerSsaData(nonLoopPred.bbMemorySsaNumOut[(int)memoryKind])._vnPair.Liberal;
        assert(newMemoryVN != ValueNumStore.NoVN);
        JITDUMP($"  Init {memoryKind} state is ${newMemoryVN:x}, with new, fresh VN at:\n");

        if (memoryKind is GcHeap)
        {
            if (sideEffects.FieldsModified is not null)
            {
                foreach (var (field, fieldKind) in sideEffects.EnumerateModifiedFieldsInNativeOrder())
                {
                    var fieldVN = vnStore.VNForHandle((nint)field.Value, GTF_ICON_FIELD_HDL);
#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"     VNForHandle({eeGetFieldName(field, includeType: false)}) is ${fieldVN:x}\n");
                    }
#endif
                    // Instance fields and complex statics select a first-field map;
                    // simple statics select a value of the field's own type.
                    var mapType = fieldKind is FieldKindForVN.WithBaseAddr ? TYP_MEM : eeGetFieldType(field);
                    newMemoryVN = vnStore.VNForMapStore(newMemoryVN, fieldVN, vnStore.VNForExpr(entryBlock, mapType));
                }
            }
            if (sideEffects.ArrayElemTypesModified is not null)
            {
                foreach (var elementClass in sideEffects.EnumerateModifiedElemTypesInNativeOrder())
                {
#if DEBUG
                    if (verbose)
                    {
                        var elementType = DecodeElemType(elementClass);
                        var name = elementType is TYP_STRUCT ? eeGetClassName(elementClass) : elementType.Name;
                        jitprintf($"     Array map {name}[]\n");
                    }
#endif
                    var elementTypeVN = vnStore.VNForHandle((nint)elementClass.Value, GTF_ICON_CLASS_HDL);
                    var uniqueVN = vnStore.VNForExpr(entryBlock, TYP_MEM);
                    newMemoryVN = vnStore.VNForMapStore(newMemoryVN, elementTypeVN, uniqueVN);
                }
            }
        }
        else
        {
            assert(memoryKind is ByrefExposed);
            assert((sideEffects.FieldsModified is null) || sideEffects.HasMemoryHavoc[(int)memoryKind]);
            assert((sideEffects.ArrayElemTypesModified is null) || sideEffects.HasMemoryHavoc[(int)memoryKind]);
        }

        JITDUMP($"  Final {memoryKind} state is ${newMemoryVN:x}.\n");
        return newMemoryVN;
    }
}
