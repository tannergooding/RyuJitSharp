// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public void optRemoveRedundantZeroInits()
    {
        if (Configuration.DEBUG && verbose)
        {
            jitprintf("*************** In optRemoveRedundantZeroInits()\n");
        }

        var refCounts = new Dictionary<int, uint>();
        var bitVecTraits = new BitVecTraits(this, lvaCount);
        var zeroInitLocals = BitVecOps.MakeEmpty(bitVecTraits);
        var hasGCSafePoint = false;
        var hasImplicitControlFlow = false;

        assert(fgNodeThreading == NodeThreading.AllTrees);

        for (var block = fgFirstBB; block is not null; block = block.UniqueSucc)
        {
            if (block != fgFirstBB)
            {
                global::RyuJitSharp.FlowEdge __jitExceptionResult226;
                var __jitExceptionCoalesceLeft227 = BlockPredsWithEH(block);
                if (__jitExceptionCoalesceLeft227 != null)
                {
                    __jitExceptionResult226 = __jitExceptionCoalesceLeft227;
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NonEntryBlockInTheUniqueSuccessorScanHasNoPredecessor);
                    __jitExceptionResult226 = default;
                }
                var firstPred = __jitExceptionResult226;
                if (firstPred.NextPredEdge is not null)
                {
                    JITDUMP($"{FMT_BB(block.bbNum)} has multiple predecessors, stopping the block scan\n");
                    break;
                }
            }

            global::RyuJitSharp.FlowGraphDfsTree __jitExceptionResult228;
            var __jitExceptionCoalesceLeft229 = _dfsTree;
            if (__jitExceptionCoalesceLeft229 != null)
            {
                __jitExceptionResult228 = __jitExceptionCoalesceLeft229;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.FlowGraphDFSTreeIsRequired);
                __jitExceptionResult228 = default;
            }
            var dfsTree = __jitExceptionResult228;
            if (dfsTree.HasCycle)
            {
                var stop = false;
                for (var predEdge = BlockPredsWithEH(block); predEdge is not null; predEdge = predEdge.NextPredEdge)
                {
                    var predBlock = predEdge.SourceBlock;
                    if (dfsTree.Contains(predBlock) && dfsTree.IsAncestor(block, predBlock))
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} is part of a cycle, stopping the block scan\n");
                        stop = true;
                        break;
                    }
                }

                if (stop)
                {
                    break;
                }
            }

            JITDUMP($"Analyzing {FMT_BB(block.bbNum)}\n");

            var defsInBlock = new Dictionary<int, uint>();
            var removedTrackedDefs = false;
            var hasEHSuccs = block.HasPotentialEHSuccs(this);

            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null;)
            {
                var next = stmt.NextStmt;
                foreach (var tree in stmt.TreeList)
                {
                    hasImplicitControlFlow |= hasEHSuccs && ((tree.Flags & GTF_EXCEPT) != 0);
                    hasGCSafePoint |= IsPotentialGCSafePoint(tree);

                    if (tree.Oper is not (GT_LCL_VAR or GT_LCL_FLD or GT_LCL_ADDR or
                        GT_STORE_LCL_VAR or GT_STORE_LCL_FLD))
                    {
                        continue;
                    }

                    var lclNode = tree.AsLclVarCommon();
                    var lclNum = lclNode.LclNum;
                    refCounts[lclNum] = unchecked(refCounts.GetValueOrDefault(lclNum) + 1u);
                    if ((tree.Flags & GTF_VAR_DEF) == 0)
                    {
                        continue;
                    }

                    ref var lclDsc = ref lvaGetDesc(lclNum);
                    if (lclDsc.lvTracked)
                    {
                        defsInBlock[lclNum] = unchecked(defsInBlock.GetValueOrDefault(lclNum) + 1u);
                    }
                    else if (varTypeIsStruct(lclDsc.Type) &&
                        (lvaGetPromotionType(lvaGetLclNum(in lclDsc)) != PROMOTION_TYPE_NONE))
                    {
                        // Both full and partial parent definitions count as field definitions here.
                        for (var field = lclDsc.lvFieldLclStart;
                             field < lclDsc.lvFieldLclStart + lclDsc.lvFieldCnt; field++)
                        {
                            if (lvaGetDesc(field).lvTracked)
                            {
                                defsInBlock[field] = unchecked(defsInBlock.GetValueOrDefault(field) + 1u);
                            }
                        }
                    }

                    if (!tree.Oper.IsLocalStore)
                    {
                        continue;
                    }

                    if (refCounts[lclNum] != 1)
                    {
                        continue;
                    }

                    if (lclDsc.lvIsStructField && (refCounts.GetValueOrDefault(lclDsc.lvParentLcl) != 0))
                    {
                        continue;
                    }

                    uint fieldRefCount = 0;
                    if (lclDsc.lvPromoted)
                    {
                        for (var field = lclDsc.lvFieldLclStart;
                             (fieldRefCount == 0) && (field < lclDsc.lvFieldLclStart + lclDsc.lvFieldCnt); field++)
                        {
                            fieldRefCount = refCounts.GetValueOrDefault(field);
                        }
                    }

                    if (fieldRefCount != 0)
                    {
                        continue;
                    }

                    var removedExplicitZeroInit = false;
                    var isEntire = (tree.Oper is not GT_STORE_LCL_FLD) || !tree.AsLclFld().IsPartial(this);
                    if (tree.Data.IsIntegralConst(0))
                    {
                        // The unique-successor scan stops at cycle entries; native keeps this false.
                        const bool bbInALoop = false;
                        var bbIsReturn = block.Kind is BBJ_RETURN;
                        if (!bbInALoop || bbIsReturn)
                        {
                            var neverTracked = lclDsc.IsAddressExposed || lclDsc.lvPinned ||
                                (lclDsc.lvPromoted && varTypeIsStruct(lclDsc.Type));
                            if (BitVecOps.IsMember(bitVecTraits, zeroInitLocals, lclNum) ||
                                (lclDsc.lvIsStructField &&
                                 BitVecOps.IsMember(bitVecTraits, zeroInitLocals, lclDsc.lvParentLcl)) ||
                                ((neverTracked || !isEntire) &&
                                 !fgVarNeedsExplicitZeroInit(lclNum, bbInALoop, bbIsReturn)))
                            {
                                // The prolog or a dominating zero init already initializes this local.
                                if (ReferenceEquals(tree, stmt.RootNode))
                                {
                                    fgRemoveStmt(block, stmt);
                                    removedExplicitZeroInit = true;
                                    lclDsc.lvSuppressedZeroInit = true;

                                    if (lclDsc.lvTracked)
                                    {
                                        removedTrackedDefs = true;
                                        defsInBlock[lclNum] = unchecked(defsInBlock[lclNum] - 1u);
                                    }
                                }
                            }

                            if (isEntire)
                            {
                                BitVecOps.AddElemD(bitVecTraits, zeroInitLocals, lclNum);
                            }
                            refCounts[lclNum] = 0;
                        }
                    }

                    // Async resumption can bypass an explicit initialization.
                    if (!removedExplicitZeroInit && isEntire && !compIsAsync &&
                        (!hasImplicitControlFlow || (lclDsc.lvTracked && !lclDsc.IsLiveInOutOfHandler)))
                    {
                        assert(CORINFO_HELP_INIT_PINVOKE_FRAME.IsNoGC);
                        bool __jitExceptionCondition230;
                        bool __jitExceptionShortCircuitLeft231;
                        __jitExceptionShortCircuitLeft231 = !lclDsc.HasGCPtr;
                        if (__jitExceptionShortCircuitLeft231)
                        {
                            __jitExceptionCondition230 = true;
                        }
                        else
                        {
                            bool __jitExceptionShortCircuitLeft232;
                            bool __jitExceptionOperand233;
                            global::RyuJitSharp.ICodeGen __jitExceptionOperand234;
                            var __jitExceptionCoalesceLeft235 = codeGen;
                            if (__jitExceptionCoalesceLeft235 != null)
                            {
                                __jitExceptionOperand234 = __jitExceptionCoalesceLeft235;
                            }
                            else
                            {
                                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CodeGenerationStateIsRequired);
                                __jitExceptionOperand234 = default;
                            }

                            __jitExceptionOperand233 = __jitExceptionOperand234.Interruptible;
                            __jitExceptionShortCircuitLeft232 = !__jitExceptionOperand233;
                            if (__jitExceptionShortCircuitLeft232)
                            {
                                __jitExceptionCondition230 = !hasGCSafePoint;
                            }
                            else
                            {
                                __jitExceptionCondition230 = false;
                            }
                        }
                        if (__jitExceptionCondition230)
                        {
                            // No use or GC reporting can occur before this explicit store.
                            lclDsc.lvHasExplicitInit = true;
                            lclNode.Flags |= GTF_VAR_EXPLICIT_INIT;
                            JITDUMP($"Marking V{lclNum:D2} as having an explicit init\n");
                        }
                    }
                }
                stmt = next;
            }

            if (removedTrackedDefs)
            {
                foreach (var (lclNum, count) in defsInBlock)
                {
                    if (count == 0)
                    {
                        VarSetOps.RemoveElemD(this, block.bbVarDef, lvaGetDesc(lclNum)._varIndex);
                    }
                }
            }
        }
    }

    public bool IsPotentialGCSafePoint(GenTree tree)
    {
        if (((tree.Flags & GTF_CALL) != 0) &&
            ((tree.Oper is not GT_CALL) || !tree.AsCall().IsHelperCall() ||
             !tree.AsCall().HelperNum.IsNoGC))
        {
            return true;
        }

        // Struct-typed local stores may become calls with GC safe points in Lower.
        if (tree.Oper.IsLocalStore)
        {
            return tree.Type is TYP_STRUCT;
        }
        if (tree.Oper is GT_STORE_BLK)
        {
            return true;
        }

        return false;
    }
}
