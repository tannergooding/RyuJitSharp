// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.FuncKind;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgCreateFunclets()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet creation outside AMD64 is not implemented.");
#else
        assert(!fgFuncletsCreated);
        var funcCount = ehFuncletCount() + 1;
        if (funcCount > ushort.MaxValue)
        {
            IMPL_LIMITATION("Too many funclets");
        }

        var funcInfo = new FuncInfoDsc[funcCount];
        assert(funcInfo[0].funKind == FUNC_ROOT);
        ushort[]? vmClauseOrderToEHTabOrder = null;
        ushort[]? ehTabOrderToVMClauseOrder = null;
        ushort funcIndex = 1;

        if (compHndBBtabCount > 0)
        {
            fgCreateFuncletPrologBlocks();
            vmClauseOrderToEHTabOrder = new ushort[compHndBBtabCount];
            ehTabOrderToVMClauseOrder = new ushort[compHndBBtabCount];
            for (ushort index = 0; index < compHndBBtabCount; index++)
            {
                vmClauseOrderToEHTabOrder[index] = index;
            }

            // Keep same-try clauses contiguous for the VM without reordering the JIT's inside-out EH table.
            vmClauseOrderToEHTabOrder.AsSpan().Sort((left, right) =>
            {
                var leftTryIndex = ehGetDsc(left).ebdTryBeg.bbTryIndex;
                var rightTryIndex = ehGetDsc(right).ebdTryBeg.bbTryIndex;
                return leftTryIndex == rightTryIndex
                    ? left.CompareTo(right)
                    : leftTryIndex.CompareTo(rightTryIndex);
            });

            foreach (var index in vmClauseOrderToEHTabOrder)
            {
                ref var handler = ref ehGetDsc(index);
                if (handler.HasFilter)
                {
                    assert(funcIndex < funcCount);
                    funcInfo[funcIndex].funKind = FUNC_FILTER;
                    funcInfo[funcIndex].funEHIndex = index;
                    funcIndex++;
                }

                assert(funcIndex < funcCount);
                funcInfo[funcIndex].funKind = FUNC_HANDLER;
                funcInfo[funcIndex].funEHIndex = index;
                handler.ebdFuncIndex = funcIndex;
                funcIndex++;
                _ = fgRelocateEHRange(index, FG_RELOCATE_TYPE.FG_RELOCATE_HANDLER);
            }

            for (ushort index = 0; index < compHndBBtabCount; index++)
            {
                ehTabOrderToVMClauseOrder[vmClauseOrderToEHTabOrder[index]] = index;
            }
        }

        assert(funcIndex == funcCount);
        compCurrFuncIdx = 0;
        compFuncInfos = funcInfo;
        compFuncInfoCount = (ushort)funcCount;
        compVMClauseOrderToEHTabOrder = vmClauseOrderToEHTabOrder;
        compEHTabOrderToVMClauseOrder = ehTabOrderToVMClauseOrder;
        fgFuncletsCreated = true;

        return compHndBBtabCount > 0 ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
#endif
    }

    public uint ehFuncletCount()
    {
        uint count = 0;
        foreach (ref var handler in new EHClauses(this))
        {
            if (handler.HasFilter)
            {
                count++;
            }
            count++;
        }

        return count;
    }

    public bool bbIsFuncletBeg(BasicBlock block)
    {
        assert(fgFuncletsCreated);
        return bbIsHandlerBeg(block);
    }

    public ref FuncInfoDsc funCurrentFunc()
    {
        return ref funGetFunc(compCurrFuncIdx);
    }

    public void funSetCurrentFunc(uint funcIndex)
    {
        assert(fgFuncletsCreated);
        assert(funcIndex <= ushort.MaxValue);
        noway_assert(funcIndex < compFuncInfoCount);
        compCurrFuncIdx = (ushort)funcIndex;
    }

    public ref FuncInfoDsc funGetFunc(uint funcIndex)
    {
        assert(fgFuncletsCreated);
        assert(funcIndex < compFuncInfoCount);
        return ref compFuncInfos[funcIndex];
    }

    public uint funGetFuncIdx(BasicBlock block)
    {
        assert(bbIsFuncletBeg(block));
        ref var handler = ref ehGetDsc(block.HndIndex);
        uint funcIndex = handler.ebdFuncIndex;
        if (handler.ebdHndBeg != block)
        {
            // A filter immediately precedes its handler in the descriptor table.
            noway_assert(handler.HasFilter);
            noway_assert(handler.ebdFilter == block);
            assert(funGetFunc(funcIndex).funKind == FUNC_HANDLER);
            assert(funGetFunc(funcIndex).funEHIndex == funGetFunc(funcIndex - 1).funEHIndex);
            assert(funGetFunc(funcIndex - 1).funKind == FUNC_FILTER);
            funcIndex--;
        }

        return funcIndex;
    }

    private void fgCreateFuncletPrologBlocks()
    {
        noway_assert(fgPredsComputed);
        assert(!fgFuncletsCreated);
        var prologBlocksCreated = false;

        foreach (ref var handler in new EHClauses(this))
        {
            var head = handler.ebdHndBeg;
            if (fgAnyIntraHandlerPreds(head))
            {
                // A loop backedge must not reexecute the funclet prolog.
                // Native does not check filters, whose entry exception object normally prevents such loops.
                fgInsertFuncletPrologBlock(head);
                prologBlocksCreated = true;
            }
        }

        if (prologBlocksCreated)
        {
            // Dominators have not been computed yet.
            fgModified = false;
#if DEBUG
            if (verbose)
            {
                JITDUMP("\nAfter fgCreateFuncletPrologBlocks()");
                fgDispBasicBlocks();
                fgDispHandlerTab();
            }
            fgVerifyHandlerTab();
            fgDebugCheckBBlist();
#endif
        }
    }

    public void fgInsertFuncletPrologBlock(BasicBlock block)
    {
        JITDUMP($"\nCreating funclet prolog header for {FMT_BB(block.bbNum)}\n");
        assert(block.hasHndIndex);
        assert(fgFirstBlockOfHandler(block) == block);

        var newHead = BasicBlock.New(this);
        newHead.SetFlags(BBF_INTERNAL);
        newHead.inheritWeight(block);
        newHead.bbRefs = 0;
        fgInsertBBbefore(block, newHead);
        fgExtendEHRegionBefore(block);

        // External calls enter through the prolog; intra-handler backedges retain their old target.
        var incomingWeight = BB_ZERO_WEIGHT;
        foreach (var predecessor in block.PredBlocksEditing)
        {
            if (!fgIsIntraHandlerPred(predecessor, block))
            {
                switch (predecessor.Kind)
                {
                    case BBJ_CALLFINALLY:
                    {
                        noway_assert(predecessor.Target == block);
                        fgRedirectEdge(ref predecessor.TargetEdgeRef, newHead);
                        incomingWeight += predecessor.bbWeight;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
        }

        assert(fgGetPredForBlock(block, newHead) is null);
        var edge = fgAddRefPred(block, newHead);
        newHead.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
        assert(newHead.JumpsToNext);
        if (block.hasProfileWeight)
        {
            newHead.setBBProfileWeight(incomingWeight);
        }
    }

    private BasicBlock fgFirstBlockOfHandler(BasicBlock block)
    {
        assert(block.hasHndIndex);
        return ehGetDsc(block.HndIndex).ebdHndBeg;
    }

    private bool fgAnyIntraHandlerPreds(BasicBlock block)
    {
        assert(block.hasHndIndex);
        assert(fgFirstBlockOfHandler(block) == block);
        foreach (var predecessor in block.PredBlocks)
        {
            if (fgIsIntraHandlerPred(predecessor, block))
            {
                return true;
            }
        }

        return false;
    }

    private bool fgIsIntraHandlerPred(BasicBlock predecessor, BasicBlock block)
    {
        assert(!fgFuncletsCreated);
        assert(fgGetPredForBlock(block, predecessor) is not null);
        assert(block.hasHndIndex);
        ref var handler = ref ehGetDsc(block.HndIndex);

        if (handler.HasFinallyHandler)
        {
            assert((handler.ebdHndBeg == block) ||
                ((handler.ebdHndBeg.Next == block) && handler.ebdHndBeg.HasFlag(BBF_INTERNAL)));
            if (predecessor.Kind == BBJ_CALLFINALLY)
            {
                assert(predecessor.Target == block);
                // Call-finally thunks live in the try's parent region, not within the handler.
                var tryIndex = handler.ebdEnclosingTryIndex;
                if (tryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    assert(!predecessor.hasTryIndex);
                }
                else
                {
                    assert(predecessor.hasTryIndex);
                    assert(tryIndex == predecessor.TryIndex);
                    assert(ehGetDsc(tryIndex).InTryRegionBBRange(predecessor));
                }

                return false;
            }
        }

        assert(predecessor.hasHndIndex || predecessor.hasTryIndex);
        if (predecessor.hasTryIndex)
        {
            // EH clauses are inside-out. Walk their nesting, not lexical ranges that relocation will change.
            var tryIndex = predecessor.TryIndex;
            while (tryIndex < block.HndIndex)
            {
                tryIndex = ehGetEnclosingTryIndex(tryIndex);
            }
            assert((tryIndex == EHblkDsc.NO_ENCLOSING_INDEX) || ehGetDsc(tryIndex).InTryRegionBBRange(predecessor));

            if (tryIndex == block.HndIndex)
            {
                assert(handler.InTryRegionBBRange(predecessor));
                assert(!handler.InHndRegionBBRange(predecessor));
                return false;
            }
            assert((tryIndex == EHblkDsc.NO_ENCLOSING_INDEX) || ehGetDsc(tryIndex).InTryRegionBBRange(block));
        }

        if (handler.HasFilter && (predecessor.Kind == BBJ_EHFILTERRET))
        {
            assert(!handler.InHndRegionBBRange(predecessor));
            return false;
        }

        assert(!handler.InTryRegionBBRange(predecessor));
        assert(handler.InHndRegionBBRange(predecessor));
        return true;
    }
}
