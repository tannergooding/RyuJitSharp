// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgNeedToSortEHTable;

#if DEBUG
    /// <summary>When false, assert when creating a new basic block.</summary>
    public bool fgSafeBasicBlockCreation = true;

    /// <summary>When false, assert when creating a new flow edge</summary>
    public bool fgSafeFlowEdgeCreation = true;
#endif

    /// <summary>Set to TRUE to turn off struct promotion for this method.</summary>
    public bool fgNoStructPromotion;

    /// <summary>Set to TRUE to turn off struct promotion for parameters this method.</summary>
    public bool fgNoStructParamPromotion;

    /// <summary>Beginning of the basic block list</summary>
    public BasicBlock? fgFirstBB;

    /// <summary>End of the basic block list</summary>
    public BasicBlock? fgLastBB;

    /// <summary>First block to be placed in the cold section</summary>
    public BasicBlock? fgFirstColdBlock;

    /// <summary>For OSR, the original method's entry point</summary>
    public BasicBlock? fgEntryBB;

    /// <summary>For OSR, the logical entry point (~ patchpoint)</summary>
    public BasicBlock? fgOSREntryBB;

    /// <summary>First block of outlined funclets (to allow block insertion before the funclets)</summary>
    public BasicBlock? fgFirstFuncletBB;

    /// <summary>list of BBJ_RETURN blocks</summary>
    public BasicBlockList? fgReturnBlocks;

    /// <summary># of BBs in the method (in the linked list that starts with fgFirstBB)</summary>
    public int fgBBcount;

#if DEBUG
    /// <summary>ordered vector of BBs</summary>
    public List<BasicBlock>? fgBBOrder;
#endif

    /// <summary>Used as a quick check for whether phases downstream of loop finding should look for natural loops.</summary>
    /// <remarks>true if there may be any natural loops in the flow graph, so try to find them; otherwise, false</remarks>
    public bool fgMightHaveNaturalLoops;

    /// <summary>The max bbNum that has been assigned to basic blocks</summary>
    public int fgBBNumMax;

#if TARGET_WASM
    public List<WasmInterval>? fgWasmIntervals;

    public BasicBlock[]? fgIndexToBlockMap;

    public bool fgWasmHasCatchResumptions;

    public FlowGraphTryRegions? fgTryRegions;

    public EHClauseInfo fgWasmEHInfo;
#endif

    public bool fgBBVarSetsInited;

    /// <summary>Track how many artificial ref counts we've added to fgEntryBB (for OSR)</summary>
    public int fgEntryBBExtraRefs;

    /// <summary>True if the flow graph has been modified recently</summary>
    public bool fgModified;

    /// <summary>Have we computed the bbPreds list</summary>
    public bool fgPredsComputed;

    /// <summary>any BBJ_SWITCH jumps?</summary>
    public bool fgHasSwitch;

    /// <summary>true if we know that we will throw</summary>
    public bool fgRemoveRestOfBlock;

    /// <summary>true if statement we morphed had a no-return call</summary>
    public bool fgHasNoReturnCall;

    /// <summary>true if we remove statements -> need new DFA</summary>
    public bool fgStmtRemoved;

    public FlowGraphOrder fgOrder;

    // The following are flags that keep track of the state of internal data structures

    // Even in tree form (fgOrder == FGOrderTree) the trees are threaded in a
    // doubly linked lists during certain phases of the compilation.
    // - Local morph threads all locals to be used for early liveness and
    //   forward sub when optimizing. This is kept valid until after forward sub.
    //   The first local is kept in Statement.GetTreeList() and the last
    //   local in Statement.GetTreeListEnd(). fgSequenceLocals can be used
    //   to (re-)sequence a statement into this form, and
    //   Statement.LocalsTreeList for range-based iteration. The order must
    //   match tree order.
    //
    // - fgSetBlockOrder threads all nodes. This is kept valid until LIR form.
    //   In this form the first node is given by Statement.GetTreeList and the
    //   last node is given by Statement.GetRootNode(). fgSetStmtSeq can be used
    //   to (re-)sequence a statement into this form, and Statement.TreeList for
    //   range-based iteration. The order must match tree order.
    //
    // - Rationalization links all nodes into linear form which is kept until
    //   the end of compilation. The first and last nodes are stored in the block.
    public NodeThreading fgNodeThreading;

    /// <summary>count of the number of times this method was called</summary>
    /// <remarks>This is derived from the profile data or is BB_UNITY_WEIGHT when we don't have profile data</remarks>
    public weight_t fgCalledCount = BB_UNITY_WEIGHT;

    /// <summary>true once importation has finished</summary>
    public bool fgImportDone;

    /// <summary>true if the funclet creation phase has been run</summary>
    public bool fgFuncletsCreated;

    /// <summary>indicates if we are during the global morphing phase since fgMorphTree can be called from several places</summary>
    public bool fgGlobalMorph;

    public bool fgGlobalMorphDone;

#if DEBUG
    public bool fgPrintInlinedMethods;
#endif

    public List<FlowEdge>? fgPredListSortVector;

    /// <summary>The number of separate return points in the method.</summary>
    public int fgReturnCount;

    public int fgThrowCount;

    /// <summary>Number of times fgSsaBuild has been run.</summary>
    public int fgSsaPassesCompleted;

    /// <summary>True if SSA info is valid and can be cross-checked versus IR</summary>
    public bool fgSsaValid;

    /// <summary>Number of times fgValueNumber has been run.</summary>
    public int fgVNPassesCompleted;

    /// <summary>These are the current value number for the memory implicit variables while doing value numbering.</summary>
    /// <remarks>These are the value numbers under the "liberal" interpretation of memory values; the "conservative" interpretation needs no VN, since every access of memory yields an unknown value.</remarks>
    public fgCurMemoryVNInlineArray fgCurMemoryVN;

#if DEBUG
    public static unsafe fgWalkPreFn fgStress64RsltMulCB;
#endif

    /// <summary>Table of pointers to the BBs</summary>
    protected BasicBlock[] fgBBs = [];

    protected Instrumentor? fgCountInstrumentor;

    protected Instrumentor? fgHistogramInstrumentor;

    protected Instrumentor? fgValueInstrumentor;

    public string? fgPgoFailReason;

    public bool fgPgoDisabled;

    public ICorJitInfo.PgoSource fgPgoSource;

    public unsafe ICorJitInfo.PgoInstrumentationSchema* fgPgoSchema;

    public unsafe byte* fgPgoData;

    public int fgPgoSchemaCount;

    public int fgPgoQueryResult;

    public int fgNumProfileRuns;

    public int fgPgoBlockCounts;

    public int fgPgoEdgeCounts;

    public int fgPgoClassProfiles;

    public int fgPgoMethodProfiles;

    public int fgPgoInlineePgo;

    public int fgPgoInlineeNoPgo;

    public int fgPgoInlineeNoPgoSingleBlock;

    public bool fgPgoHaveWeights;

    public bool fgPgoSynthesized;

    public bool fgPgoDynamic;

    public bool fgPgoConsistent;

    public bool fgPgoSingleEdge;

#if DEBUG
    public bool fgPgoDeferredInconsistency;
#endif

    private hashBv? fgAvailableOutgoingArgTemps;

    private Stack<int>? fgUsedSharedTemps;

#if FEATURE_SIMD
    /// <summary>used for tracking previous simd field store in function: impMarkContiguousSIMDFieldStores.</summary>
    private Statement? fgPreviousCandidateSIMDFieldStoreStmt;
#endif

    private Statement? fgMorphStmt;

    private fgBigOffsetMorphingTempsInlineArray fgBigOffsetMorphingTemps;

    private AddCodeDscMap? fgAddCodeDscMap;

    public bool fgRngChkThrowAdded;

#if DEBUG
    public static unsafe fgWalkPreFn fgDebugCheckInlineCandidates;

    public static unsafe fgWalkPreFn fgDebugCheckForTransformableIndirectCalls;
#endif

    public bool fgHasLoops;

#if DEBUG
    /// <summary>Has the flowgraph EH normalization phase been done?</summary>
    public bool fgNormalizeEHDone;
#endif

    /// <summary>Note that this one is used outside of debug.</summary>
    public bool fgLocalVarLivenessDone;

    public bool fgDidEarlyLiveness;

    /// <summary>Determines if conditions are met to allow switching the opt level to optimized</summary>
    /// <remarks>This method is to be called at some point before <see cref="compSetOptimizationLevel" /> to determine if the opt level may be changed based on information gathered in early phases.</remarks>
    public unsafe bool fgCanSwitchToOptimized
    {
        get
        {
            var result = opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0)
                     && !opts.jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT)
                     && !opts.compDbgCode
                     && !compIsForInlining;

            if (result)
            {
                // Ensure that it would be safe to change the opt level
                assert(opts.compFlags == CLFLG_MINOPT);
                assert(!opts.IsMinOptsSet);
            }
            return result;
        }
    }

    [MemberNotNullWhen(true, nameof(fgAddCodeDscMap))]
    public bool fgHasAddCodeDscMap => fgAddCodeDscMap is not null;

    /// <summary>Check if we have a profile that has weights.</summary>
    /// <remarks>These weights may come from instrumentation or from synthesis.</remarks>
    public bool fgHaveProfileWeights => fgPgoHaveWeights;

    /// <summary>check if profile data is available and is sufficient enough to be trustful.</summary>
    /// <remarks>See notes for fgHaveProfileData.</remarks>
    public bool fgHaveSufficientProfileWeights
    {
        get
        {
            if (!fgHaveProfileWeights)
            {
                return false;
            }

            switch (fgPgoSource)
            {
                case ICorJitInfo.PgoSource.Dynamic:
                case ICorJitInfo.PgoSource.Text:
                case ICorJitInfo.PgoSource.Blend:
                {
                    return true;
                }

                case ICorJitInfo.PgoSource.Synthesis:
                {
                    // Single-edge methods always have sufficient profile data.
                    // Assuming we don't synthesize value and class profile data (which we don't currently).
                    return fgPgoSingleEdge;
                }

                case ICorJitInfo.PgoSource.Static:
                {
                    // We sometimes call this very early, eg evaluating the prejit root.
                    if (fgFirstBB is not null)
                    {
                        var sufficientSamples = 1000.0;
                        return fgFirstBB.bbWeight > sufficientSamples;
                    }
                    return true;
                }

                default:
                {
                    return false;
                }
            }
        }
    }

    /// <summary>check if profile data is available</summary>
    /// <remarks>
    ///   <para>In most cases it is more appropriate to call fgHaveProfileWeights, since that tells you if blocks have profile-based weights.</para>
    ///   <para>This method literally checks if the runtime had a profile schema, from which we can derive weights.</para>
    ///   <para>Schema-based data comes from Tier0 methods, which currently do not do any inlining; thus inlinee profile data should be available and representative.</para>
    /// </remarks>
    protected unsafe bool fgHaveProfileData => fgPgoSchema is not null;

    /// <summary>true if we have real profile data for this method or if we have some fake profile data for the stress mode</summary>
    public bool fgIsUsingProfileWeights => fgHaveProfileWeights || fgStressBBProf();

    /// <summary>find acd map key for a given block</summary>
    /// <param name="blk">block that may eventually throw an exception</param>
    /// <param name="dsg">designator for which region controls throw block placement</param>
    /// <returns>encoded region value to use in acd key formation</returns>
    public int bbThrowIndex(BasicBlock blk, out AcdKeyDesignator dsg)
    {
        var tryIndex = blk.bbTryIndex;
        var hndIndex = blk.bbHndIndex;
        var inTry = tryIndex > 0;
        var inHnd = hndIndex > 0;

        if (!inTry && !inHnd)
        {
            dsg = AcdKeyDesignator.KD_NONE;
            return 0;
        }

        assert(inTry || inHnd);

        if (inTry && (!inHnd || (tryIndex < hndIndex)))
        {
            // The most enclosing region is a try body, use it
            dsg = AcdKeyDesignator.KD_TRY;
            return tryIndex;
        }

        // The most enclosing region is a handler which will be a funclet
        // Now we have to figure out if blk is in the filter or handler
        assert(hndIndex >= 1);

        if (ehGetDsc(hndIndex - 1).InFilterRegionBBRange(blk))
        {
            dsg = AcdKeyDesignator.KD_FLT;
            return hndIndex | int.MinValue;
        }

        dsg = AcdKeyDesignator.KD_HND;
        return hndIndex | 0x40000000;
    }

    /// <summary>Check whether the address tree may represent a heap address.</summary>
    /// <param name="addr">Address to check</param>
    /// <returns>True if address could be a heap address; false otherwise (i.e. stack, native memory, etc.)</returns>
    public bool fgAddrCouldBeHeap(GenTree addr)
    {
        var op = addr;
        var oper = op.Oper;

        while (oper is GT_FIELD_ADDR)
        {
            var fieldAddr = op.AsFieldAddr();

            if (!fieldAddr.IsInstance)
            {
                break;
            }

            op = fieldAddr.FldObj;
            oper = op.Oper;
        }

        // Ignore the offset for locals
        gtPeelOffsets(ref op, out _);

        var result = true;

        if (oper is GT_LCL_ADDR)
        {
            result = false;
        }
        else if (oper.IsScalarLocal && (op.AsLclVarCommon().LclNum == impInlineRoot.info.compRetBuffArg))
        {
            // RetBuf is known to be on the stack
            result = false;
        }
        return result;
    }

    /// <summary>Check whether the address tree can represent null.</summary>
    /// <param name="addr">Address to check</param>
    /// <returns>True if address could be null; false otherwise</returns>
    public bool fgAddrCouldBeNull(GenTree addr)
    {
        switch (addr.Oper)
        {
            case GT_LCL_VAR:
            {
                return !lvaIsImplicitByRefLocal(addr.AsLclVar().LclNum);
            }

            case GT_LCL_ADDR:
            case GT_CNS_STR:
            case GT_FIELD_ADDR:
            {
                return false;
            }

            case GT_CNS_INT:
            {
                return !addr.AsIntCon().IsIconHandle();
            }

            case GT_IND:
            {
                return (addr.Flags & GTF_IND_NONNULL) == 0;
            }

            case GT_BOX:
            {
                var box = addr.AsBox();
                return !box.IsBoxedValue;
            }

            case GT_ARR_ADDR:
            {
                return (addr.Flags & GTF_ARR_ADDR_NONNULL) == 0;
            }

            case GT_ADD:
            {
                var op = addr.AsOp();

                var op1 = op.Op1;
                var op2 = op.Op2;

                if (op1.Oper.IsCnsIntOrI)
                {
                    var intCon1 = op1.AsIntCon();

                    if (intCon1.IsIconHandle())
                    {
                        // Is Op2 also a constant?
                        if (op2.Oper.IsCnsIntOrI)
                        {
                            var intCon2 = op2.AsIntCon();

                            // Is this an addition of a handle and constant
                            if (!intCon2.IsIconHandle())
                            {
                                if (!fgIsBigOffset(intCon2.IconVal))
                                {
                                    // Op2 was an ordinary small constant, so we can't have a null address
                                    return false;
                                }
                            }
                        }
                    }
                    else if (!fgIsBigOffset(intCon1.IconVal))
                    {
                        // Op1 was an ordinary small constant
                        return fgAddrCouldBeNull(op2);
                    }
                }
                else if (op2.Oper.IsCnsIntOrI)
                {
                    var intCon2 = op2.AsIntCon();

                    // Is this an addition of a small constant
                    if (!intCon2.IsIconHandle())
                    {
                        if (!fgIsBigOffset(intCon2.IconVal))
                        {
                            // Op2 was an ordinary small constant
                            return fgAddrCouldBeNull(op1);
                        }
                    }
                }
                break;
            }

            case GT_COMMA:
            {
                return fgAddrCouldBeNull(addr.EffectiveVal);
            }

            case GT_INDEX_ADDR:
            {
                return !addr.AsIndexAddr().IsNotNull;
            }

            case GT_CALL:
            {
                var call = addr.AsCall();
                return !call.IsHelperCall() || !call.HelperNum.NonNullReturn;
            }

            default:
            {
                break;
            }
        }

        // default result: addr could be null.
        return true;
    }

    /// <summary>Increment block->bbRefs by one and add "blockPred" to the predecessor list of "block".</summary>
    /// <param name="block">A block to operate on.</param>
    /// <param name="blockPred">The predecessor block to add to the predecessor list.</param>
    /// <param name="oldEdge">If non-null, and a new edge is created (and the dup count of an existing edge is not just incremented), the edge weights are copied from this edge.</param>
    /// <param name="initializingPreds">Only set to "true" when the initial preds computation is happening.</param>
    /// <returns>The flow edge representing the predecessor.</returns>
    /// <remarks>
    ///   <para>block.bbRefs is incremented by one to account for the increase in incoming edges.</para>
    ///   <para>block.bbRefs is adjusted even if preds haven't been computed. If preds haven't been computed, the preds themselves aren't touched.</para>
    ///   <para>fgModified is set if a new flow edge is created (but not if an existing flow edge dup count is incremented), indicating that the flow graph shape has changed.</para>
    /// </remarks>
    public FlowEdge fgAddRefPred(BasicBlock block, BasicBlock blockPred, FlowEdge? oldEdge = null, bool initializingPreds = false)
    {
        assert(fgPredsComputed ^ initializingPreds);

        block.bbRefs++;

        // Keep the predecessor list in lowest to highest bbID order.
        //
        // If we are initializing preds, we rely on the fact that we are adding references in increasing
        // order of blockPred->bbID to avoid searching the list.
        //
        var flow = null as FlowEdge;
        ref var listp = ref Unsafe.NullRef<FlowEdge>();

        if (initializingPreds)
        {
            // List is sorted order and we're adding references in
            // increasing blockPred->bbID order. The only possible
            // dup list entry is the last one.

            listp = ref block.bbPreds;
            var flowLast = block.bbLastPred;

            if (flowLast is not null)
            {
                listp = ref flowLast.NextPredEdgeRef;

                assert(flowLast.SourceBlock.bbID <= blockPred.bbID);

                if (flowLast.SourceBlock == blockPred)
                {
                    flow = flowLast;
                }
            }
        }
        else
        {
            // References are added randomly, so we have to search.
            listp = ref fgGetPredInsertPoint(blockPred, block);

            if ((listp is not null) && (listp.SourceBlock == blockPred))
            {
                flow = listp;
            }
        }

        if (flow is not null)
        {
            // The predecessor block already exists in the flow list; simply add to its duplicate count.
            //
            noway_assert(flow.DupCount != 0);
            flow.incrementDupCount();
        }
        else
        {
#if DEBUG
            // Create a new edge
            // We may be disallowing edge creation, except for edges targeting special blocks.
            assert(fgSafeFlowEdgeCreation || block.HasFlag(BBF_CAN_ADD_PRED));
#endif

#if MEASURE_BLOCK_SIZE
            genFlowNodeCnt += 1;
            genFlowNodeSize += sizeof(FlowEdge);
#endif

            // Any changes to the flow graph invalidate the dominator sets.
            fgModified = true;

            // Create new edge in the list in the correct ordered location.
            //
            flow = new FlowEdge(blockPred, block, listp);
            flow.incrementDupCount();
            listp = flow;

            if (initializingPreds)
            {
                block.bbLastPred = flow;
            }
            else if (oldEdge is not null)
            {
                // Copy likelihood from old edge.
                flow.Likelihood = oldEdge.Likelihood;
                flow.isHeuristicBased = oldEdge.isHeuristicBased;
            }
        }

#if DEBUG
        // Pred list should (still) be ordered.
        assert(block.checkPredListOrder());
#endif

        return flow;
    }

    /// <summary>update var table for cases where the this pointer value can change.</summary>
    /// <remarks>
    ///   <para>Modifies lvaArg0Var to refer to a temp if the value of 'this' can change.</para>
    ///   <para>The original this (info.compThisArg) then remains unmodified in the method.</para>
    ///   <para>fgAddInternal is responsible for adding the code to copy the initial this into the temp.</para>
    /// </remarks>
    public void fgAdjustForAddressExposedOrWrittenThis()
    {
        ref var thisVarDsc = ref lvaGetDesc(info.compThisArg);

        // Optionally enable adjustment during stress.
        if (compStressCompile(STRESS_GENERIC_VARN, 15))
        {
            JITDUMP("JitStress: creating modifiable `this`\n");
            thisVarDsc.lvHasILStoreOp = true;
        }

        // If this is exposed or written to, create a temp for the modifiable this
        if (thisVarDsc.IsAddressExposed || thisVarDsc.lvHasILStoreOp)
        {
            // If there is a "ldarga 0" or "starg 0", grab and use the temp.
            lvaArg0Var = lvaGrabTemp(false, "Address-exposed, or written this pointer");
            noway_assert(lvaArg0Var > (uint)info.compThisArg);
            ref var arg0varDsc = ref lvaGetDesc(lvaArg0Var);
            arg0varDsc.Type = thisVarDsc.Type;
            arg0varDsc.SetAddressExposed(thisVarDsc.IsAddressExposed, thisVarDsc.AddrExposedReason);
            arg0varDsc.lvDoNotEnregister = thisVarDsc.lvDoNotEnregister;
#if DEBUG
            arg0varDsc.DoNotEnregisterReason = thisVarDsc.DoNotEnregisterReason;
#endif
            arg0varDsc.lvHasILStoreOp = thisVarDsc.lvHasILStoreOp;

            // Note that here we don't clear `m_doNotEnregReason` and it stays `doNotEnreg` with `AddrExposed` reason.
            thisVarDsc.CleanAddressExposed();
            thisVarDsc.lvHasILStoreOp = false;
        }
    }

    public void fgAllocEHTable()
    {
        // We need to allocate space for EH clauses that will be used by funclets
        // as well as one for each EH clause from the IL. Nested EH clauses pulled
        // out as funclets create one EH clause for each enclosing region. Thus,
        // the maximum number of clauses we will need might be very large. We allocate
        // twice the number of EH clauses in the IL, which should be good in practice.
        // In extreme cases, we might need to abandon this and reallocate. See
        // fgTryAddEHTableEntries() for more details.

#if DEBUG
        // force the resizing code to hit more frequently in DEBUG
        var compHndBBtabLength = info.compXcptnsCount;
#else
        var compHndBBtabLength = info.compXcptnsCount * 2;
#endif

        compHndBBtab = new EHblkDsc[compHndBBtabLength];
        compHndBBtabCount = info.compXcptnsCount;
    }

    /// <summary>Check control flow constraints for well formed IL. Bail if any of the constraints are violated.</summary>
    public void fgCheckBasicBlockControlFlow()
    {
#if DEBUG
        // These rules aren't quite correct after EH normalization has introduced new blocks
        assert(!fgNormalizeEHDone);
#endif

        foreach (var blk in Blocks)
        {
            if (blk.HasFlag(BBF_INTERNAL))
            {
                continue;
            }

            switch (blk.Kind)
            {
                case BBJ_ALWAYS:
                {
                    // block does unconditional jump to target
                    fgControlFlowPermitted(blk, blk.Target);
                    break;
                }

                case BBJ_COND:
                {
                    // block conditionally jumps to the target
                    fgControlFlowPermitted(blk, blk.FalseTarget);
                    fgControlFlowPermitted(blk, blk.TrueTarget);
                    break;
                }

                case BBJ_RETURN:
                {
                    // block ends with 'ret'

                    if (blk.hasTryIndex || blk.hasHndIndex)
                    {
                        BADCODE($"Return from a protected block. Before offset {blk.bbCodeOffsEnd:X4}");
                    }
                    break;
                }

                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                case BBJ_EHFILTERRET:
                {
                    if (!blk.hasHndIndex)
                    {
                        // must be part of a handler
                        BADCODE($"Missing handler. Before offset {blk.bbCodeOffsEnd:X4}");
                    }

                    ref var HBtab = ref ehGetDsc(blk.HndIndex);

                    // Endfilter allowed only in a filter block
                    if (blk.Kind is BBJ_EHFILTERRET)
                    {
                        if (!HBtab.HasFilter)
                        {
                            BADCODE("Unexpected endfilter");
                        }
                    }
                    else if (blk.Kind is BBJ_EHFINALLYRET)
                    {
                        // endfinally allowed only in a finally block
                        if (!HBtab.HasFinallyHandler)
                        {
                            BADCODE("Unexpected endfinally");
                        }
                    }
                    else if (blk.Kind is BBJ_EHFAULTRET)
                    {
                        // 'endfault' (alias of IL 'endfinally') allowed only in a fault block
                        if (!HBtab.HasFaultHandler)
                        {
                            BADCODE("Unexpected endfault");
                        }
                    }

                    // The handler block should be the innermost block
                    // Exception blocks are listed, innermost first.
                    if (blk.hasTryIndex && (blk.TryIndex < blk.HndIndex))
                    {
                        BADCODE("endfinally / endfault / endfilter in nested try block");
                    }
                    break;
                }

                case BBJ_THROW:
                {
                    // block ends with 'throw'
                    // throw is permitted from every BB, so nothing to check
                    // importer makes sure that rethrow is done from a catch
                    break;
                }

                case BBJ_LEAVE:
                {
                    // block always jumps to the target, maybe out of guarded region.
                    // Used temporarily until importing

                    fgControlFlowPermitted(blk, blk.Target, isLeave: true);
                    break;
                }

                case BBJ_SWITCH:
                {
                    // block ends with a switch statement

                    foreach (var bTarget in blk.SwitchSuccs)
                    {
                        fgControlFlowPermitted(blk, bTarget);
                    }
                    break;
                }

                case BBJ_EHCATCHRET:  // block ends with a leave out of a catch
                case BBJ_CALLFINALLY: // block always calls the target finally
                default:
                {
                    // these blocks don't get created until importing
                    noway_assert(false, "Unexpected bbKind");
                    break;
                }
            }
        }
    }

    /// <summary>scan blocks seeing if any handler block is a backedge target.</summary>
    /// <remarks>
    ///   <para>Sets compHasBackwardJumpInHandler if so.</para>
    ///   <para>This will disable setting patchpoints in this method and prompt the jit to optimize the method instead.</para>
    ///   <para>We assume any late-added handler (say for synchronized methods) will not introduce any loops.</para>
    /// </remarks>
    public unsafe void fgCheckForLoopsInHandlers()
    {
        // We only care about this if we are going to set OSR patchpoints and the method has exception handling.
        if (!opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0))
        {
            return;
        }

        if (JitConfig[ConfigInteger.TC_OnStackReplacement] == 0)
        {
            return;
        }

        if (info.compXcptnsCount == 0)
        {
            return;
        }

        // Walk blocks in handlers and filters, looking for a backedge target.
        assert(!compHasBackwardJumpInHandler);

        foreach (var blk in Blocks)
        {
            if (blk.hasHndIndex)
            {
                if (blk.HasFlag(BBF_BACKWARD_JUMP_TARGET))
                {
                    JITDUMP($"\nHandler block {FMT_BB(blk.bbNum)} is backward jump target; can't have patchpoints in this method\n");
                    compHasBackwardJumpInHandler = true;
                    break;
                }
            }
        }
    }

    /// <summary>Check that the leave from the block is legal.</summary>
    /// <param name="blkSrc">the source block</param>
    /// <param name="blkDest">the destination block</param>
    /// <param name="isLeave">true if this is a leave instruction</param>
    /// <remarks>Consider removing this check here if we can do it cheaply during importing.</remarks>
    public void fgControlFlowPermitted(BasicBlock blkSrc, BasicBlock blkDest, bool isLeave = false)
    {
#if DEBUG
        // These rules aren't quite correct after EH normalization has introduced new blocks
        assert(!fgNormalizeEHDone);
#endif

        var srcInCatch = false;
        ref var srcHndTab = ref ehInitHndRange(blkSrc, out var srcHndBeg, out var srcHndEnd, out var srcInFilter);
        _ = ehInitHndRange(blkDest, out var destHndBeg, out var destHndEnd, out var destInFilter);

        // Impose the rules for leaving or jumping from handler blocks

        if (blkSrc.hasHndIndex)
        {
            srcInCatch = srcHndTab.HasCatchHandler && srcHndTab.InHndRegionILRange(blkSrc);

            // Are we jumping within the same handler index?
            if (BasicBlock.sameHndRegion(blkSrc, blkDest))
            {
                // Do we have a filter clause?
                if (srcHndTab.HasFilter)
                {
                    // filters and catch handlers share same eh index
                    // we need to check for control flow between them.
                    if (srcInFilter != destInFilter)
                    {
                        if (!jitIsBetween(blkDest.bbCodeOffs, srcHndBeg, srcHndEnd))
                        {
                            BADCODE($"Illegal control flow between filter and handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                        }
                    }
                }
            }
            else
            {
                // The handler indexes of blkSrc and blkDest are different
                if (isLeave)
                {
                    // Any leave instructions must not enter the dest handler from outside
                    if (!jitIsBetween(srcHndBeg, destHndBeg, destHndEnd))
                    {
                        BADCODE($"Illegal use of leave to enter handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                    }
                }
                else
                {
                    // We must use a leave to exit a handler
                    BADCODE($"Illegal control flow out of a handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                }

                // Do we have a filter clause?
                if (srcHndTab.HasFilter)
                {
                    // It is ok to leave from the handler block of a filter,
                    // but not from the filter block of a filter
                    if (srcInFilter != destInFilter)
                    {
                        BADCODE($"Illegal to leave a filter handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                    }
                }

                // We should never leave a finally handler
                if (srcHndTab.HasFinallyHandler)
                {
                    BADCODE($"Illegal to leave a finally handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                }

                // We should never leave a fault handler
                if (srcHndTab.HasFaultHandler)
                {
                    BADCODE($"Illegal to leave a fault handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                }
            }
        }
        else if (blkDest.hasHndIndex)
        {
            // blkSrc was not inside a handler, but blkDst is inside a handler
            BADCODE($"Illegal control flow into a handler. Before offset {blkSrc.bbCodeOffsEnd:X4}");
        }

        // Are we jumping from a catch handler into the corresponding try?
        // VB uses this for "on error goto "

        if (isLeave && srcInCatch)
        {
            // inspect all handlers containing the jump source

            // are we jumping in a valid way from a catch to the corresponding try?
            var bValidJumpToTry = false;

            // false if we are jumping out of a non-catch handler
            var bCatchHandlerOnly = true;

            for (var i = 0; bCatchHandlerOnly && (i < compHndBBtabCount); i++)
            {
                ref var ehDsc = ref ehGetDsc(i);

                if (ehDsc.InHndRegionILRange(blkSrc))
                {
                    if (ehDsc.HasCatchHandler)
                    {
                        if (ehDsc.InTryRegionILRange(blkDest))
                        {
                            // If we already considered the jump for a different try/catch,
                            // we would have two overlapping try regions with two overlapping catch
                            // regions, which is illegal.
                            noway_assert(!bValidJumpToTry);

                            // Allowed if it is the first instruction of an inner try
                            // (and all trys in between)
                            //
                            // try {
                            //  ..
                            // _tryAgain:
                            //  ..
                            //      try {
                            //      _tryNestedInner:
                            //        ..
                            //          try {
                            //          _tryNestedIllegal:
                            //            ..
                            //          } catch {
                            //            ..
                            //          }
                            //        ..
                            //      } catch {
                            //        ..
                            //      }
                            //  ..
                            // } catch {
                            //  ..
                            //  leave _tryAgain         // Allowed
                            //  ..
                            //  leave _tryNestedInner   // Allowed
                            //  ..
                            //  leave _tryNestedIllegal // Not Allowed
                            //  ..
                            // }
                            //
                            // Note: The leave is allowed also from catches nested inside the catch shown above.

                            // The common case where leave is to the corresponding try
                            if (ehDsc.ebdIsSameTry(this, (ushort)(blkDest.TryIndex)) ||
                                // Also allowed is a leave to the start of a try which starts in the handler's try
                                fgFlowToFirstBlockOfInnerTry(ehDsc.ebdTryBeg, blkDest, sibling: false))
                            {
                                bValidJumpToTry = true;
                            }
                        }
                    }
                    else
                    {
                        // We are jumping from a handler which is not a catch handler.

                        // If it's a handler, but not a catch handler, it must be either a finally or fault
                        if (!ehDsc.HasFinallyOrFaultHandler)
                        {
                            BADCODE($"Handlers must be catch, finally, or fault. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                        }

                        // Are we jumping out of this handler?
                        if (!ehDsc.InHndRegionILRange(blkDest))
                        {
                            bCatchHandlerOnly = false;
                        }
                    }
                }
                else if (ehDsc.InFilterRegionILRange(blkSrc))
                {
                    // Are we jumping out of a filter?
                    if (!ehDsc.InFilterRegionILRange(blkDest))
                    {
                        bCatchHandlerOnly = false;
                    }
                }
            }

            if (bCatchHandlerOnly)
            {
                if (bValidJumpToTry)
                {
                    return;
                }
                else
                {
                    // FALL THROUGH
                    // This is either the case of a leave to outside the try/catch,
                    // or a leave to a try not nested in this try/catch.
                    // The first case is allowed, the second one will be checked
                    // later when we check the try block rules (it is illegal if we
                    // jump to the middle of the destination try).
                }
            }
            else
            {
                BADCODE($"illegal leave to exit a finally, fault or filter. Before offset {blkSrc.bbCodeOffsEnd:X4}");
            }
        }

        // Check all the try block rules

        _ = ehInitTryRange(blkSrc, out var srcTryBeg, out var srcTryEnd);
        _ = ehInitTryRange(blkDest, out var destTryBeg, out var destTryEnd);

        // Are we jumping between try indexes?
        if (!BasicBlock.sameTryRegion(blkSrc, blkDest))
        {
            // Are we exiting from an inner to outer try?
            if (jitIsBetween(srcTryBeg, destTryBeg, destTryEnd) && jitIsBetween(srcTryEnd - 1, destTryBeg, destTryEnd))
            {
                if (!isLeave)
                {
                    BADCODE($"exit from try block without a leave. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                }
            }
            else if (jitIsBetween(destTryBeg, srcTryBeg, srcTryEnd))
            {
                // check that the dest Try is first instruction of an inner try
                if (!fgFlowToFirstBlockOfInnerTry(blkSrc, blkDest, sibling: false))
                {
                    BADCODE($"control flow into middle of try. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                }
            }
            else // there is no nesting relationship between src and dest
            {
                if (isLeave)
                {
                    // check that the dest Try is first instruction of an inner try sibling
                    if (!fgFlowToFirstBlockOfInnerTry(blkSrc, blkDest, sibling: true))
                    {
                        BADCODE($"illegal leave into middle of try. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                    }
                }
                else
                {
                    BADCODE($"illegal control flow in to/out of try block. Before offset {blkSrc.bbCodeOffsEnd:X4}");
                }
            }
        }
    }

#if DEBUG
    // TODO: Port fgDebugCheckBBlist
    public void fgDebugCheckBBlist(bool checkBBNum = false, bool checkBBRefs = true) { }

    /// <summary>Check that the block list bbNum are in increasing order in the bbNext traversal</summary>
    /// <remarks>
    ///   <para>Given a block B1 and its bbNext successor B2, this means `B1->bbNum &lt; B2-&gt;bbNum`, but not that `B1->bbNum + 1 == B2-&gt;bbNum`.</para>
    ///   <para>This can be used as a precondition to a phase that expects this ordering to compare block numbers (say, to look for backwards branches).</para>
    /// </remarks>
    public void fgDebugCheckBBNumIncreasing()
    {
        foreach (var block in Blocks)
        {
            assert(block.IsLast || (block.bbNum < block.Next.bbNum));
        }
    }

    // TODO: Port fgDebugCheckInitBB
    public void fgDebugCheckInitBB() { }

    // TODO: Port fgDebugCheckFlowGraphAnnotations
    public void fgDebugCheckFlowGraphAnnotations() { }

    // TODO: Port fgDebugCheckLinkedLocals
    public void fgDebugCheckLinkedLocals() { }

    // TODO: Port fgDebugCheckLinks
    public void fgDebugCheckLinks(bool morphTrees = false) { }

    // TODO: Port fgDebugCheckLoops
    public void fgDebugCheckLoops() { }

    // TODO: Port fgDebugCheckNodesUniqueness
    public void fgDebugCheckNodesUniqueness() { }

    // TODO: Port fgDebugCheckProfile
    public void fgDebugCheckProfile(PhaseChecks checks = PhaseChecks.CHECK_NONE) { }

    // TODO: Port fgStress64RsltMul
    public void fgStress64RsltMul() { }

    // TODO: Port fgVerifyHandlerTab
    public void fgVerifyHandlerTab() { }
#endif

#if DUMP_FLOWGRAPHS
    public bool fgDumpFlowGraph(Phases phase, PhasePosition pos)
    {
        // TODO: Port Compiler.fgDumpFlowGraph
        return false;
    }
#endif

#if DEBUG
    /// <summary>Dump all basic blocks in the function.</summary>
    /// <param name="dumpTrees">if true, also dump the trees in each block</param>
    public void fgDispBasicBlocks(bool dumpTrees = false)
        => fgDispBasicBlocks(fgFirstBB, lastBlock: null, dumpTrees);

    /// <summary>Dump blocks from "firstBlock" to "lastBlock".</summary>
    /// <param name="firstBlock">the first block to dump</param>
    /// <param name="lastBlock">the last block to dump (or null for all remaining blocks)</param>
    /// <param name="dumpTrees">if true, also dump the trees in each block</param>
    public void fgDispBasicBlocks(BasicBlock? firstBlock, BasicBlock? lastBlock, bool dumpTrees)
    {
        // Build vector of blocks in order.
        fgBBOrder ??= [];

        fgBBOrder.Capacity = fgBBcount;
        fgBBOrder.Clear();

        var ibcColWidth = 0;

        for (var block = firstBlock; block is not null; block = block.Next)
        {
            if (block.hasProfileWeight)
            {
                var thisIbcWidth = CountDigits(block.bbWeight);
                ibcColWidth      = int.Max(ibcColWidth, thisIbcWidth);
            }

            fgBBOrder.Add(block);

            if (block == lastBlock)
            {
                break;
            }
        }

        if (ibcColWidth > 0)
        {
            ibcColWidth = int.Max(ibcColWidth, 3) + 1; // + 1 for the leading space
        }

        var inDefaultOrder = true;

        // Optionally sort
        if (JitConfig[ConfigInteger.JitDumpFgBlockOrder] == 1)
        {
            fgBBOrder.Sort((bb1, bb2) => bb1.bbNum.CompareTo(bb2.bbNum));
            inDefaultOrder = false;
        }
        else if (JitConfig[ConfigInteger.JitDumpFgBlockOrder] == 2)
        {
            fgBBOrder.Sort((bb1, bb2) => bb1.bbID.CompareTo(bb2.bbID));
            inDefaultOrder = false;
        }

        var bbNumMax = fgBBNumMax;
        var maxBlockNumWidth = CountDigits(bbNumMax);
        maxBlockNumWidth = int.Max(maxBlockNumWidth, 2);
        var padWidth = maxBlockNumWidth - 2; // Account for functions with a large number of blocks.

        const bool printEdgeLikelihoods = true; // TODO: parameterize?

        // Edge likelihoods are printed as "(0.123)", so take 7 characters maximum.
        var edgeLikelihoodsWidth = printEdgeLikelihoods ? 7 : 0;

        // Calculate the field width allocated for the block target. The field width is allocated to allow for two blocks
        // for BBJ_COND. It does not include any extra space for variable-sized BBJ_EHFINALLYRET and BBJ_SWITCH.
        // "-> "(3) + "BB"(2) + blockNum + likelihoods + comma(1) + "BB"(2) + blockNum + likelihoods + space(1) + kind(8)
        var blockTargetFieldWidth = 3 + 2 + maxBlockNumWidth + edgeLikelihoodsWidth + 1 + 2 + maxBlockNumWidth + edgeLikelihoodsWidth + 1 + 8; // kind: "(xxxxxx)"

        jitprintf("\n");
        jitprintf($"------{new string('-', int.Max(padWidth, 12))}-------------------------------------{new string('-', int.Max(ibcColWidth, 12))}--------------------------{new string('-', int.Max(blockTargetFieldWidth, 46))}--------------------------\n");
        jitprintf($"BBnum {new string(' ', padWidth)}BBid ref try hnd {(fgPredsComputed ? "preds      " : "           ")}     weight  {new string(' ', (ibcColWidth > 0) ? ibcColWidth - 3 : 0)}{((ibcColWidth > 0) ? "IBC" : "")}[IL range]   [jump]{new string(' ', blockTargetFieldWidth - 8)} [EH region]        [flags]\n");
        jitprintf($"------{new string('-', int.Max(padWidth, 12))}-------------------------------------{new string('-', int.Max(ibcColWidth, 12))}--------------------------{new string('-', int.Max(blockTargetFieldWidth, 46))}--------------------------\n");

        for (var blockIndex = 0; blockIndex < fgBBOrder.Count; blockIndex++)
        {
            var block = fgBBOrder[blockIndex];

            var nextBlockIndex = blockIndex + 1;
            var nextBlock = (nextBlockIndex < fgBBOrder.Count) ? fgBBOrder[nextBlockIndex] : null;

            // First, do some checking on the bbPrev links
            if (!block.IsFirst)
            {
                assert(block.Prev is not null);

                if (block.Prev.Next != block)
                {
                    jitprintf("bad prev link\n");
                }
            }
            else if (block != fgFirstBB)
            {
                jitprintf("bad prev link!\n");
            }

            if (inDefaultOrder && block.IsFirstColdBlock(this))
            {
                jitprintf($"~~~~~~{new string('~', int.Max(padWidth, 12))}~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~{new string('~', int.Max(ibcColWidth, 12))}~~~~~~~~~~~~~~~~~~~~~~~~~~{new string('~', int.Max(blockTargetFieldWidth, 46))}~~~~~~~~~~~~~~~~~~~~~~~~~~\n");
            }

            if (inDefaultOrder && (block == fgFirstFuncletBB))
            {
                jitprintf($"++++++{new string('+', int.Max(padWidth, 12))}+++++++++++++++++++++++++++++++++++++{new string('+', int.Max(ibcColWidth, 12))}++++++++++++++++++++++++++{new string('+', int.Max(blockTargetFieldWidth, 46))}++++++++++++++++++++++++++ funclets follow\n");
            }

            fgTableDispBasicBlock(block, nextBlock, printEdgeLikelihoods, blockTargetFieldWidth, ibcColWidth);

            if (block == lastBlock)
            {
                break;
            }
        }

        jitprintf($"------{new string('-', int.Max(padWidth, 12))}-------------------------------------{new string('-', int.Max(ibcColWidth, 12))}--------------------------{new string('-', int.Max(blockTargetFieldWidth, 46))}--------------------------\n");

        if (dumpTrees)
        {
            foreach (var block in fgBBOrder)
            {
                fgDumpBlock(block);
            }
            jitprintf("\n-------------------------------------------------------------------------------------------------------------------\n");
        }
    }

    public void fgDispHandlerTab()
    {
        jitprintf("\n***************  Exception Handling table");

        if (compHndBBtabCount == 0)
        {
            jitprintf(" is empty\n");
            return;
        }

        jitprintf("\n  id,  index  ");
        jitprintf("eTry, eHnd\n");

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref compHndBBtab[XTnum];
            HBtab.DispEntry(XTnum);
        }
    }

    public void fgDumpBlock(BasicBlock block)
    {
        jitprintf("\n------------ ");
        block.dspBlockHeader();

        if (fgSsaValid)
        {
            fgDumpBlockMemorySsaIn(block);
        }

        if (!block.IsLIR)
        {
            foreach (var stmt in block.Statements)
        {
                fgDumpStmtTree(block, stmt);
            }
        }
        else
        {
            gtDispRange(block);
        }

        if (fgSsaValid)
        {
            jitprintf("\n");
            fgDumpBlockMemorySsaOut(block);
        }
    }

    public void fgDumpBlockMemorySsaIn(BasicBlock block)
    {
        foreach (var memoryKind in new AllMemoryKinds())
        {
            if (byrefStatesMatchGcHeapStates)
            {
                jitprintf($"SSA MEM: {ByrefExposed}, {GcHeap}");
            }
            else
            {
                jitprintf($"SSA MEM: {memoryKind}");
            }

            if (block.bbMemorySsaPhiFunc[(int)(memoryKind)] is null)
            {
                jitprintf($" = m:{block.bbMemorySsaNumIn[(int)(memoryKind)]}\n");
            }
            else if (block.bbMemorySsaPhiFunc[(int)(memoryKind)] == BasicBlock.EmptyMemoryPhiDef)
            {
                jitprintf(" = phi([not filled])\n");
            }
            else
            {
                jitprintf(" = phi(");
                var sep = "";

                for (var arg = block.bbMemorySsaPhiFunc[(int)(memoryKind)]; arg is not null; arg = arg.m_nextArg)
                {
                    jitprintf($"{sep}m:{arg.SsaNum}");
                    sep = ", ";
                }
                jitprintf(")\n");
            }

            if (byrefStatesMatchGcHeapStates)
            {
                break;
            }
        }
    }

    public void fgDumpBlockMemorySsaOut(BasicBlock block)
    {
        foreach (var memoryKind in new AllMemoryKinds())
        {
            if (byrefStatesMatchGcHeapStates)
            {
                jitprintf($"SSA MEM: {ByrefExposed}, {GcHeap}");
            }
            else
            {
                jitprintf($"SSA MEM: {memoryKind}");
            }

            jitprintf($" = m:{block.bbMemorySsaNumOut[(int)(memoryKind)]}\n");

            if (byrefStatesMatchGcHeapStates)
            {
                break;
            }
        }
    }

    public void fgDumpStmtTree(BasicBlock block, Statement stmt)
    {
        jitprintf($"\n***** {block.dspToString()}\n");
        gtDispStmt(stmt);
    }
#endif

    /// <summary>Main entry point to discover the basic blocks for the current function.</summary>
    public unsafe void fgFindBasicBlocks()
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"*************** In fgFindBasicBlocks() for {info.compFullName}\n");
        }

        // Call this here so any dump printing it inspires doesn't appear in the bb table.
        //
        fgStressBBProf();
#endif

        // Allocate the 'jump target' bit vector
        var jumpTarget = new BitArray(info.compILCodeSize + 1);

        // Walk the instrs to find all jump targets
        if (compInlineResult is not null)
        {
            fgFindJumpTargets(info.compCode, info.compILCodeSize, jumpTarget, makeInlineObservations: true);
        }
        else
        {
            fgFindJumpTargets(info.compCode, info.compILCodeSize, jumpTarget, makeInlineObservations: false);
        }

        if (compDonotInline)
        {
            return;
        }

        ushort XTnum;

        // Are there any exception handlers?
        if ((info.compXcptnsCount > 0) || ((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) != 0))
        {
            assert(!compIsForInlining || opts.compInlineMethodsWithEH);

            if (compIsForInlining)
            {
                // Verify we can expand the EH table as needed to incorporate the callee's EH clauses.
                // Failing here should be extremely rare.
                var numEHEntries = info.compXcptnsCount;

                // We will introduce another EH clause before inlining finishes to restore async contexts
                if ((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) != 0)
                {
                    numEHEntries++;
                }

                var dscIdx = fgTryAddEHTableEntries(0, numEHEntries, deferAdding: true);

                if (dscIdx is not -1)
                {
                    compInlineResult.NoteFatal(InlineObservation.CALLSITE_EH_TABLE_FULL);
                }
            }

            // Check and mark all the exception handlers
            for (XTnum = 0; XTnum < info.compXcptnsCount; XTnum++)
            {
                CORINFO_EH_CLAUSE clause;
                info.compCompHnd->getEHinfo(info.compMethodHnd, XTnum, &clause);
                noway_assert(clause.HandlerLength != -1);

                // If we're inlining, and the inlinee has a catch clause, we are currently
                // unable to convey the type of the catch properly, as it is represented
                // by a token. So, abandon inlining.
                //
                // TODO: if inlining methods with catches is rare, consider
                // transforming class catches into runtime filters like we do in
                // fgCreateFiltersForGenericExceptions

                if (compIsForInlining)
                {
                    var isFinallyFaultOrFilter = (clause.Flags & (CORINFO_EH_CLAUSE_FINALLY | CORINFO_EH_CLAUSE_FAULT | CORINFO_EH_CLAUSE_FILTER)) != 0;

                    if (!isFinallyFaultOrFilter)
                    {
                        JITDUMP($"Inlinee EH clause {XTnum} is a catch; we can't inline these (yet)\n");
                        compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_EH);
                        return;
                    }
                }

                if (clause.TryLength <= 0)
                {
                    BADCODE("try block length <=0");
                }

                // Mark the 'try' block extent and the handler itself

                if (clause.TryOffset > info.compILCodeSize)
                {
                    BADCODE("try offset is > codesize");
                }
                jumpTarget[clause.TryOffset] = true;

                var tryEnd = clause.TryOffset + clause.TryLength;

                if (tryEnd > info.compILCodeSize)
                {
                    BADCODE("try end is > codesize");
                }
                jumpTarget[tryEnd] = true;

                if (clause.HandlerOffset > info.compILCodeSize)
                {
                    BADCODE("handler offset > codesize");
                }
                jumpTarget[clause.HandlerOffset] = true;

                var handlerEnd = clause.HandlerOffset + clause.HandlerLength;

                if (handlerEnd > info.compILCodeSize)
                {
                    BADCODE("handler end > codesize");
                }
                jumpTarget[handlerEnd] = true;

                if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
                {
                    if (clause.FilterOffset > info.compILCodeSize)
                    {
                        BADCODE("filter offset > codesize");
                    }
                    jumpTarget[clause.FilterOffset] = true;
                }
            }
        }

#if DEBUG
        if (verbose)
        {
            var anyJumpTargets = false;
            jitprintf("Jump targets:\n");

            for (var i = 0; i < info.compILCodeSize + 1; i++)
            {
                if (jumpTarget[i])
                {
                    anyJumpTargets = true;
                    jitprintf($"  IL_{i:X4}\n");
                }
            }

            if (!anyJumpTargets)
            {
                jitprintf("  none\n");
            }
        }
#endif

        // Now create the basic blocks
        fgMakeBasicBlocks(info.compCode, info.compILCodeSize, jumpTarget);

        if (compIsForInlining)
        {
            if (compInlineResult.IsFailure)
            {
                return;
            }

            // Use a spill temp for the return value if there are multiple return blocks,
            // if the inlinee has GC ref locals, or if async contexts need save/restore.
            // In the latter cases we will need to insert IR after the return.
            if ((info.compRetNativeType is not TYP_VOID) &&
                ((fgReturnCount > 1) || impInlineInfo.HasGcRefLocals || ((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) != 0)))
            {
                // If we've spilled the ret expr to a temp we can reuse the temp
                // as the inlinee return spill temp.
                //
                // Todo: see if it is even better to always use this existing temp
                // for return values, even if we otherwise wouldn't need a return spill temp...
                lvaInlineeReturnSpillTemp = impInlineInfo.inlineCandidateInfo.preexistingSpillTemp;

                if (lvaInlineeReturnSpillTemp != BAD_VAR_NUM)
                {
                    // This temp should already have the type of the return value.
                    JITDUMP($"\nInliner: re-using pre-existing spill temp V{lvaInlineeReturnSpillTemp:D2}\n");

                    // We may have co-opted an existing temp for the return spill.
                    // We likely assumed it was single-def at the time, but now we can see it has multiple definitions.
                    if (fgReturnCount > 1)
                    {
                        ref var lvaDsc = ref lvaTable[lvaInlineeReturnSpillTemp];

                        if (lvaDsc.lvSingleDef)
                        {
                            // Make sure it is no longer marked single def. This is only safe
                            // to do if we haven't ever updated the type.
                            if (info.compRetType == TYP_REF)
                            {
#if DEBUG
                                assert(!lvaDsc.lvClassInfoUpdated);
#endif
                            }

                            JITDUMP($"Marked return spill temp V{lvaInlineeReturnSpillTemp:D2} as NOT single def temp\n");
                            lvaDsc.lvSingleDef = false;
                        }
                    }
                }
                else
                {
                    // The lifetime of this var might expand multiple BBs. So it is a long lifetime compiler temp.
                    lvaInlineeReturnSpillTemp = lvaGrabTemp(shortLifetime: false, "Inline return value spill temp");
                    ref var lvaDesc = ref lvaTable[lvaInlineeReturnSpillTemp];
                    lvaDesc.Type = info.compRetType;

                    if (varTypeIsStruct(info.compRetType))
                    {
                        lvaSetStruct(lvaInlineeReturnSpillTemp, info.compMethodInfo->args.retTypeClass, unsafeValueClsCheck: false);
                    }

                    // The return spill temp is single def only if the method has a single return block.
                    if (fgReturnCount == 1)
                    {
                        lvaDesc.lvSingleDef = true;
                        JITDUMP($"Marked return spill temp V{lvaInlineeReturnSpillTemp:D2} as a single def temp\n");
                    }

                    // If the method returns a ref class, set the class of the spill temp to the method's return value.
                    // We may update this later if it turns out we can prove the method returns a more specific type.
                    if (info.compRetType == TYP_REF)
                    {
                        var retClassHnd = impInlineInfo.inlineCandidateInfo.methInfo.args.retTypeClass;

                        if (retClassHnd is not null)
                        {
                            lvaSetClass(lvaInlineeReturnSpillTemp, retClassHnd);
                        }
                    }
                    lvaInlineeReturnSpillTempFreshlyCreated = true;
                }
            }
        }

        // Mark all blocks within 'try' blocks as such

        if (info.compXcptnsCount == 0)
        {
            return;
        }

        if (info.compXcptnsCount > MAX_XCPTN_INDEX)
        {
            IMPL_LIMITATION("too many exception clauses");
        }

        // Allocate the exception handler table

        fgAllocEHTable();

        // Assume we don't need to sort the EH table (such that nested try/catch
        // appear before their try or handler parent). The EH verifier will notice
        // when we do need to sort it.

        fgNeedToSortEHTable = false;

        verInitEHTree(info.compXcptnsCount);
        var initRootId = ehnNextId; // remember the original root since it may get modified during insertion

        // Annotate BBs with exception handling information required for generating correct eh code
        // as well as checking for correct IL

        for (XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            CORINFO_EH_CLAUSE clause;
            info.compCompHnd->getEHinfo(info.compMethodHnd, XTnum, &clause);
            noway_assert(clause.HandlerLength != -1); // @DEPRECATED

#if DEBUG
            if (verbose)
            {
                dispIncomingEHClause(XTnum, clause);
            }
#endif

            var tryBegOff = clause.TryOffset;
            var tryEndOff = tryBegOff + clause.TryLength;
            var filterBegOff = 0;
            var hndBegOff = clause.HandlerOffset;
            var hndEndOff = hndBegOff + clause.HandlerLength;

            if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
            {
                filterBegOff = clause.FilterOffset;
            }

            if (tryEndOff > info.compILCodeSize)
            {
                BADCODE($"end of try block beyond end of method for try at offset {tryBegOff:X4}");
            }

            if (hndEndOff > info.compILCodeSize)
            {
                BADCODE($"end of hnd block beyond end of method for try at offset {tryBegOff:X4}");
            }

            ref var HBtab = ref compHndBBtab[XTnum];

            HBtab.ebdID = impInlineRoot.compEHID++;
            HBtab._ebdTryBegOffset = tryBegOff;
            HBtab._ebdTryEndOffset = tryEndOff;
            HBtab._ebdFilterBegOffset = filterBegOff;
            HBtab._ebdHndBegOffset = hndBegOff;
            HBtab._ebdHndEndOffset = hndEndOff;

            // Convert the various addresses to basic blocks

            var tryBegBB = fgLookupBB(tryBegOff);
            var tryEndBB = fgLookupBB(tryEndOff); // note: this can be null if the try region is at the end of the function
            var hndBegBB = fgLookupBB(hndBegOff);
            var hndEndBB = null as BasicBlock;
            var filtBB = null as BasicBlock;

            // Assert that the try/hnd beginning blocks are set up correctly
            if (tryBegBB is null)
            {
                BADCODE("Try Clause is invalid");
            }

            if (hndBegBB is null)
            {
                BADCODE("Handler Clause is invalid");
            }

            if (hndEndOff < info.compILCodeSize)
            {
                hndEndBB = fgLookupBB(hndEndOff);
            }

            if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
            {
                filtBB = fgLookupBB(clause.FilterOffset);
                assert(filtBB is not null);

                HBtab.ebdFilter = filtBB;
                filtBB.CatchType = BBCT_FILTER;
                hndBegBB.CatchType = BBCT_FILTER_HANDLER;

                var block = filtBB;

                // Mark all BBs that belong to the filter with the XTnum of the corresponding handler
                while (true)
                {
                    if (block is null)
                    {
                        BADCODE($"Missing endfilter for filter at offset {filtBB.bbCodeOffs:X4}");
                        return;
                    }

                    // Still inside the filter
                    block.HndIndex = XTnum;

                    if (block.Kind is BBJ_EHFILTERRET)
                    {
                        // Mark catch handler as successor.
                        var newEdge = fgAddRefPred(hndBegBB, block);
                        block.TargetEdge = newEdge;
                        assert(hndBegBB.CatchType is BBCT_FILTER_HANDLER);
                        break;
                    }

                    block = block.Next;
                }

                if (block.IsLast || (block.Next != hndBegBB))
                {
                    BADCODE($"Filter does not immediately precede handler for filter at offset {filtBB.bbCodeOffs:X4}");
                }
            }
            else
            {
                // Set ebdTyp and bbCatchType as appropriate
                if ((clause.Flags & CORINFO_EH_CLAUSE_FINALLY) != 0)
                {
                    hndBegBB.CatchType = BBCT_FINALLY;
                    HBtab.ebdTyp = 0;
                }
                else
                {
                    if ((clause.Flags & CORINFO_EH_CLAUSE_FAULT) != 0)
                    {
                        hndBegBB.CatchType = BBCT_FAULT;
                        HBtab.ebdTyp = 0;
                    }
                    else
                    {
                        // These values should be non-zero value that will
                        // not collide with real tokens for bbCatchType
                        if (clause.ClassToken == 0)
                        {
                            BADCODE("Exception catch type is Null");
                        }

                        hndBegBB.CatchType = (bbCatchType)(clause.ClassToken);
                        HBtab.ebdTyp = (bbCatchType)(clause.ClassToken);

                        noway_assert(HBtab.ebdTyp is not BBCT_FAULT);
                        noway_assert(HBtab.ebdTyp is not BBCT_FINALLY);
                        noway_assert(HBtab.ebdTyp is not BBCT_FILTER);
                        noway_assert(HBtab.ebdTyp is not BBCT_FILTER_HANDLER);
                    }
                }
            }

            // Prevent future optimizations of removing the first block
            // of a TRY block and the first block of an exception handler

            tryBegBB.SetFlags(BBF_DONT_REMOVE);
            hndBegBB.SetFlags(BBF_DONT_REMOVE);
            hndBegBB.bbRefs++; // The first block of a handler gets an extra, "artificial" reference count.

            if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
            {
                assert(filtBB is not null);
                filtBB.SetFlags(BBF_DONT_REMOVE);
                filtBB.bbRefs++; // The first block of a filter gets an extra, "artificial" reference count.
            }

            //
            // Store the info to the table of EH block handlers
            //

            HBtab.ebdHandlerType = ToEHHandlerType(clause.Flags);

            HBtab.ebdTryBeg = tryBegBB;
            HBtab.ebdTryLast = (tryEndBB is null) ? fgLastBB! : tryEndBB.Prev!;

            HBtab.ebdHndBeg = hndBegBB;
            HBtab.ebdHndLast = (hndEndBB is null) ? fgLastBB! : hndEndBB.Prev!;

            // Assert that all of our try/hnd blocks are setup correctly.
            if (HBtab.ebdTryLast is null)
            {
                BADCODE("Try Clause is invalid");
            }

            if (HBtab.ebdHndLast is null)
            {
                BADCODE("Handler Clause is invalid");
            }

            // Verify that it's legal
            verInsertEhNode(clause, ref HBtab);
        }

        fgSortEHTable();

        // Next, set things related to nesting that depend on the sorting being complete.

        for (XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            // Mark all blocks in the finally/fault or catch clause
            ref var HBtab = ref compHndBBtab[XTnum];

            var tryBegBB = HBtab.ebdTryBeg;
            var hndBegBB = HBtab.ebdHndBeg;

            var tryBegOff = HBtab._ebdTryBegOffset;
            var tryEndOff = HBtab._ebdTryEndOffset;

            var hndBegOff = HBtab._ebdHndBegOffset;
            var hndEndOff = HBtab._ebdHndEndOffset;

            var block = hndBegBB;

            while ((block is not null) && (block.bbCodeOffs < hndEndOff))
            {
                if (!block.hasHndIndex)
                {
                    block.HndIndex = XTnum;

                    // If the most nested EH handler region of this block is a 'fault' region, then change any
                    // BBJ_EHFINALLYRET that were imported to BBJ_EHFAULTRET.
                    if ((hndBegBB.CatchType is BBCT_FAULT) && (block.Kind is BBJ_EHFINALLYRET))
                    {
                        block.Kind = BBJ_EHFAULTRET;
                    }
                }

                // All blocks in a catch handler or filter are rarely run, except the entry
                if ((block != hndBegBB) && (hndBegBB.CatchType is not BBCT_FINALLY))
                {
                    block.bbSetRunRarely();
                }

                block = block.Next;
            }

            // Mark all blocks within the covered range of the try

            for (block = tryBegBB; (block is not null) && (block.bbCodeOffs < tryEndOff); block = block.Next)
            {
                // Mark this BB as belonging to a 'try' block

                if (!block.hasTryIndex)
                {
                    block.TryIndex = XTnum;
                }

#if DEBUG
                // Note: the BB can't span the 'try' block

                if (!block.HasFlag(BBF_INTERNAL))
                {
                    noway_assert(tryBegOff <= block.bbCodeOffs);
                    noway_assert(tryEndOff >= block.bbCodeOffsEnd || tryEndOff == tryBegOff);
                }
#endif
            }

            // Init ebdHandlerNestingLevel of current clause, and bump up value for all
            //  enclosed clauses (which have to be before it in the table).
            //  Innermost try-finally blocks must precede outermost
            //  try-finally blocks.

            HBtab.ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX;
            HBtab.ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX;

            noway_assert(XTnum < compHndBBtabCount);
            noway_assert(XTnum == ehGetIndex(HBtab));

            for (ushort XTnum2 = 0; XTnum2 < XTnum; XTnum2++)
            {
                ref var xtab = ref compHndBBtab[XTnum2];

                // If we haven't recorded an enclosing try index for xtab then see
                //  if this EH region should be recorded.  We check if the
                //  first offset in the xtab lies within our region.  If so,
                //  the last offset also must lie within the region, due to
                //  nesting rules. verInsertEhNode(), below, will check for proper nesting.
                if (xtab.ebdEnclosingTryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    var begBetween = jitIsBetween(xtab.ebdTryBegOffs, tryBegOff, tryEndOff);

                    if (begBetween)
                    {
                        // Record the enclosing scope link
                        xtab.ebdEnclosingTryIndex = XTnum;
                    }
                }

                // Do the same for the enclosing handler index.
                if (xtab.ebdEnclosingHndIndex == EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    var begBetween = jitIsBetween(xtab.ebdTryBegOffs, hndBegOff, hndEndOff);

                    if (begBetween)
                    {
                        // Record the enclosing scope link
                        xtab.ebdEnclosingHndIndex = XTnum;
                    }
                }
            }
        }

        // always run these checks for a debug build
        verCheckNestingLevel(initRootId);

#if !DEBUG
        // fgNormalizeEH assumes that this test has been passed and Ssa assumes that fgNormalizeEHTable has been run.
        // So do this unless we're in minOpts mode (and always in debug).
        if (!opts.MinOpts)
#endif
        {
            fgCheckBasicBlockControlFlow();
        }

#if DEBUG
        if (verbose)
        {
            JITDUMP("*************** After fgFindBasicBlocks() has created the EH table\n");
            fgDispHandlerTab();
        }

        // We can't verify the handler table until all the IL legality checks have been done (above), since bad IL
        // (such as illegal nesting of regions) will trigger asserts here.
        fgVerifyHandlerTab();
#endif

        fgNormalizeEH();

        fgCheckForLoopsInHandlers();
    }

    /// <summary>walk the IL stream, determining jump target offsets</summary>
    /// <param name="codeAddr">base address of the IL code buffer</param>
    /// <param name="codeSize">number of bytes in the IL code buffer</param>
    /// <param name="jumpTarget">bit vector for flagging jump targets</param>
    /// <param name="makeInlineObservations">true to record inline observations about the method</param>
    /// <remarks>
    ///   <para>May throw an exception if the IL is malformed.</para>
    ///   <para>jumpTarget[N] is set to 1 if IL offset N is a jump target in the method.</para>
    ///   <para>Also sets m_addrExposed and lvHasILStoreOp, ilHasMultipleILStoreOp in lvaTable[].</para>
    /// </remarks>
    public unsafe void fgFindJumpTargets(byte* codeAddr, IL_OFFSET codeSize, BitArray jumpTarget, bool makeInlineObservations)
    {
        var codeBegp = codeAddr;
        var codeEndp = codeAddr + codeSize;

        // Track offsets where IL instructions begin in DEBUG builds.
        // Used to validate debug info generated by the JIT.
        assert(compInlineContext is not null);
        assert(codeSize == compInlineContext.ILSize);

#if DEBUG
        var ilInstsSet = new BitArray(codeSize);
#endif

        var prevOpcode = CEE_NOP;
        var fgStack = new FgStack();
        var retBlocks = 0;
        var prefixFlags = 0;
        var preciseScan = false;
        var isReturnsArrayKnown = false;
        var returnsArray = false;

        if (makeInlineObservations)
        {
            assert(compInlineResult is not null);

            // Set default values for profile (to avoid NoteFailed in CALLEE_IL_CODE_SIZE's handler) these will be overridden later.
            compInlineResult.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, true);
            compInlineResult.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, 1.0);

            // Observe force inline state and code size.
            compInlineResult.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, (info.compFlags & CORINFO_FLG_FORCEINLINE) != 0);
            compInlineResult.NoteBool(InlineObservation.CALLEE_IS_INTRINSIC_TYPE, (info.compClassAttr & CORINFO_FLG_INTRINSIC_TYPE) != 0);
            compInlineResult.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, codeSize);

            if (compIsForInlining)
            {
                var iciBlock = impInlineInfo.iciBlock;
                assert(iciBlock is not null);

                // Determine if call site is within a try.
                if (iciBlock.hasTryIndex)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_IN_TRY_REGION);
                }

                // Determine if the call site is in a no-return block
                if (iciBlock.Kind is BBJ_THROW)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_IN_NORETURN_REGION);
                }

                // Determine if the call site is in a loop.
                if (iciBlock.HasFlag(BBF_BACKWARD_JUMP))
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_IN_LOOP);
                }

#if DEBUG
                // If inlining, this method should still be a candidate.
                assert(compInlineResult.IsCandidate);
#endif
            }

            // note that we're starting to look at the opcodes.
            compInlineResult.Note(InlineObservation.CALLEE_BEGIN_OPCODE_SCAN);
            preciseScan = compInlineResult.Policy.RequiresPreciseScan;
        }

        while (codeAddr < codeEndp)
        {
            var opcode = (OPCODE)(codeAddr[0]);

#if DEBUG
            ilInstsSet[(int)(codeAddr - codeBegp)] = true;
#endif

            codeAddr += sizeof(byte);

            if (opcode == CEE_PREFIX1)
            {
                if (codeAddr >= codeEndp)
                {
                    TooFar(codeAddr, codeBegp);
                }

                opcode = (OPCODE)(0x0100 + codeAddr[0]);
                codeAddr += sizeof(byte);
            }

            if (opcode >= CEE_COUNT)
            {
                BADCODE($"Illegal opcode: {(int)(opcode):X2}");
            }

            var sz = opcode.Size;
            var typeIsNormed = false;

            if (opcode < CEE_UNALIGNED)
            {
                if (opcode is (>= CEE_LDARG_0 and <= CEE_STLOC_S) or (>= CEE_LDARG and <= CEE_STLOC))
                {
                    opts.lvRefCount++;
                }

                var jmpDist = 0;
                var varType = TYP_UNDEF;
                var varNum = 0;

                switch (opcode)
                {
                    case CEE_STLOC_0:
                    case CEE_STLOC_1:
                    case CEE_STLOC_2:
                    case CEE_STLOC_3:
                    {
                        varNum = (opcode - CEE_STLOC_0);
                        StoreLocal(this, varNum);
                        break;
                    }

                    case CEE_LDARGA_S:
                    case CEE_LDARGA:
                    {
                        // Handle address-taken args or locals
                        noway_assert(sz is sizeof(byte) or sizeof(ushort));

                        if (codeAddr > (codeEndp - sz))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        varNum = (sz is sizeof(byte)) ? codeAddr[0] : BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));

                        if (compIsForInlining)
                        {
                            varType = impInlineInfo.lclVarInfo[varNum].lclTypeInfo;
                            impInlineInfo.inlArgInfo[varNum].argHasLdargaOp = true;
                        }
                        else
                        {
                            if (varNum >= info.compILargsCount)
                            {
                                BADCODE("bad argument number");
                            }

                            // account for possible hidden param
                            varNum = compMapILargNum(varNum);
                            varType = LoadAddress(this, codeAddr, codeEndp, sz, varNum);
                        }

                        typeIsNormed = !varTypeIsGC(varType) && !varTypeIsStruct(varType);
                        break;
                    }

                    case CEE_STARG_S:
                    case CEE_STARG:
                    {
                        noway_assert(sz is sizeof(byte) or sizeof(ushort));

                        if (codeAddr > (codeEndp - sz))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        varNum = (sz is sizeof(byte)) ? codeAddr[0] : BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));

                        if (compIsForInlining)
                        {
                            if (varNum < impInlineInfo.argCnt)
                            {
                                impInlineInfo.inlArgInfo[varNum].argHasStargOp = true;
                            }
                        }
                        else
                        {
                            // account for possible hidden param
                            varNum = compMapILargNum(varNum);

                            // This check is only intended to prevent an AV.
                            // Bad varNum values will later be handled properly by the verifier.
                            if (varNum < lvaTable.Length)
                            {
                                // In non-inline cases, note written-to arguments.
                                lvaTable[varNum].lvHasILStoreOp = true;
                            }
                        }
                        break;
                    }

                    case CEE_LDLOCA_S:
                    case CEE_LDLOCA:
                    {
                        // Handle address-taken args or locals
                        noway_assert(sz is sizeof(byte) or sizeof(ushort));

                        if (codeAddr > (codeEndp - sz))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        varNum = (sz is sizeof(byte)) ? codeAddr[0] : BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));

                        if (compIsForInlining)
                        {
                            varType = impInlineInfo.lclVarInfo[varNum + impInlineInfo.argCnt].lclTypeInfo;
                            impInlineInfo.lclVarInfo[varNum + impInlineInfo.argCnt].lclHasLdlocaOp = true;
                        }
                        else
                        {
                            if (varNum >= info.compMethodInfo->locals.numArgs)
                            {
                                BADCODE("bad local number");
                            }

                            varNum += info.compArgsCount;
                            varType = LoadAddress(this, codeAddr, codeEndp, sz, varNum);
                        }

                        typeIsNormed = !varTypeIsGC(varType) && !varTypeIsStruct(varType);
                        break;
                    }

                    case CEE_STLOC_S:
                    case CEE_STLOC:
                    {
                        noway_assert(sz is sizeof(byte) or sizeof(ushort));

                        if (codeAddr > (codeEndp - sz))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        varNum = (sz is sizeof(byte)) ? codeAddr[0] : BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));
                        StoreLocal(this, varNum);
                        break;
                    }

#if !TARGET_X86 && !TARGET_ARM
                    case CEE_JMP:
                    {
                        if (!compIsForInlining)
                        {
                            // We transform this into a set of ldarg's + tail call and
                            // thus may push more onto the stack than originally thought.
                            // This doesn't interfere with verification because CEE_JMP
                            // is never verifiable, and there's nothing unsafe you can
                            // do with a an IL stack overflow if the JIT is expecting it.
                            info.compMaxStack = int.Max(info.compMaxStack, info.compILargsCount);
                        }
                        break;
                    }
#endif

                    case CEE_CALL:
                    case CEE_CALLVIRT:
                    {
                        // There has to be code after the call, otherwise the inlinee is unverifiable.
                        if (compIsForInlining)
                        {
                            noway_assert(codeAddr < (codeEndp - sz));
                        }
                        break;
                    }

                    // Jumps
                    case CEE_BR_S:
                    case CEE_BRFALSE_S:
                    case CEE_BRTRUE_S:
                    case CEE_BEQ_S:
                    case CEE_BGE_S:
                    case CEE_BGT_S:
                    case CEE_BLE_S:
                    case CEE_BLT_S:
                    case CEE_BNE_UN_S:
                    case CEE_BGE_UN_S:
                    case CEE_BGT_UN_S:
                    case CEE_BLE_UN_S:
                    case CEE_BLT_UN_S:
                    case CEE_BR:
                    case CEE_BRFALSE:
                    case CEE_BRTRUE:
                    case CEE_BEQ:
                    case CEE_BGE:
                    case CEE_BGT:
                    case CEE_BLE:
                    case CEE_BLT:
                    case CEE_BNE_UN:
                    case CEE_BGE_UN:
                    case CEE_BGT_UN:
                    case CEE_BLE_UN:
                    case CEE_BLT_UN:
                    case CEE_LEAVE:
                    case CEE_LEAVE_S:
                    {
                        if (codeAddr > (codeEndp - sz))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        // Compute jump target address
                        jmpDist = (sz is sizeof(sbyte)) ? unchecked((sbyte)(codeAddr[0])) : BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));

                        if ((jmpDist == 0) && (opcode is CEE_BR_S or CEE_BR or CEE_LEAVE_S or CEE_LEAVE) && opts.DoEarlyBlockMerging)
                        {
                            // NOP
                            break;
                        }

                        var jmpAddr = (IL_OFFSET)(codeAddr - codeBegp) + (sz + jmpDist);

                        // Make sure target is reasonable
                        if (jmpAddr >= codeSize)
                        {
                            BADCODE($"code jumps to outer space at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                        }

                        // Mark the jump target
                        jumpTarget[jmpAddr] = true;
                        break;
                    }

                    case CEE_SWITCH:
                    {
                        // Make sure we don't go past the end reading the number of cases
                        if (codeAddr > (codeEndp - sizeof(int)))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        // Read the number of cases
                        var jmpCnt = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                        codeAddr += sizeof(int);

                        if (jmpCnt > (codeSize / sizeof(int)))
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        // Find the end of the switch table
                        var jmpBase = (IL_OFFSET)(codeAddr - codeBegp) + (jmpCnt * sizeof(int));

                        // Make sure there is more code after the switch
                        if (jmpBase >= codeSize)
                        {
                            TooFar(codeAddr, codeBegp);
                        }

                        // jmpBase is also the target of the default case, so mark it
                        jumpTarget[jmpBase] = true;

                        // Process table entries
                        while (jmpCnt > 0)
                        {
                            var jmpAddr = jmpBase + BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                            codeAddr += sizeof(int);

                            if (jmpAddr >= codeSize)
                            {
                                BADCODE($"jump target out of range at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                            }

                            jumpTarget[jmpAddr] = true;
                            jmpCnt--;
                        }

                        // We've advanced past all the bytes in this instruction
                        sz = 0;
                        break;
                    }

                    case CEE_PREFIX7:
                    case CEE_PREFIX6:
                    case CEE_PREFIX5:
                    case CEE_PREFIX4:
                    case CEE_PREFIX3:
                    case CEE_PREFIX2:
                    case CEE_PREFIXREF:
                    {
                        BADCODE($"Illegal opcode: {(int)(opcode):X2}");
                        break;
                    }

                    case CEE_LOCALLOC:
                    {
                        compLocallocSeen = true;
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                if (makeInlineObservations)
                {
                    assert(compInlineResult is not null);
                    var toSkip = 0;

                    switch (opcode)
                    {
                        case CEE_JMP:
                        {
                            retBlocks++;

#if !TARGET_X86 && !TARGET_ARM
                            if (!compIsForInlining)
                            {
                                // We transform this into a set of ldarg's + tail call and
                                // thus may push more onto the stack than originally thought.
                                // This doesn't interfere with verification because CEE_JMP
                                // is never verifiable, and there's nothing unsafe you can
                                // do with a an IL stack overflow if the JIT is expecting it.
                                info.compMaxStack = int.Max(info.compMaxStack, info.compILargsCount);
                                break;
                            }
#endif
                            // If we are inlining, we need to fail for a CEE_JMP opcode, just like the list of other opcodes (for all platforms).
                            goto case CEE_MKREFANY;
                        }

                        case CEE_CALL:
                        case CEE_CALLVIRT:
                        {
                            if ((codeAddr < (codeEndp - sz)) && ((OPCODE)(codeAddr[sz]) is CEE_RET))
                            {
                                // If the method has a call followed by a ret, assume that it is a wrapper method.
                                compInlineResult.Note(InlineObservation.CALLEE_LOOKS_LIKE_WRAPPER);
                            }
                            break;
                        }

                        case CEE_RET:
                        {
                            retBlocks++;
                            break;
                        }

                        // Jumps
                        case CEE_BR_S:
                        case CEE_BRFALSE_S:
                        case CEE_BRTRUE_S:
                        case CEE_BEQ_S:
                        case CEE_BGE_S:
                        case CEE_BGT_S:
                        case CEE_BLE_S:
                        case CEE_BLT_S:
                        case CEE_BNE_UN_S:
                        case CEE_BGE_UN_S:
                        case CEE_BGT_UN_S:
                        case CEE_BLE_UN_S:
                        case CEE_BLT_UN_S:
                        case CEE_BR:
                        case CEE_BRFALSE:
                        case CEE_BRTRUE:
                        case CEE_BEQ:
                        case CEE_BGE:
                        case CEE_BGT:
                        case CEE_BLE:
                        case CEE_BLT:
                        case CEE_BNE_UN:
                        case CEE_BGE_UN:
                        case CEE_BGT_UN:
                        case CEE_BLE_UN:
                        case CEE_BLT_UN:
                        case CEE_LEAVE:
                        case CEE_LEAVE_S:
                        {
                            if (jmpDist < 0)
                            {
                                compInlineResult.Note(InlineObservation.CALLEE_BACKWARD_JUMP);
                            }
                            break;
                        }

                        case CEE_SWITCH:
                        {
                            compInlineResult.Note(InlineObservation.CALLEE_HAS_SWITCH);

                            // Fail fast, if we're inlining and can't handle this.
                            if (compIsForInlining && compInlineResult.IsFailure)
                            {
                                return;
                            }
                            break;
                        }

                        case CEE_THROW:
                        {
                            compInlineResult.Note(InlineObservation.CALLEE_THROW_BLOCK);
                            break;
                        }

                        case CEE_BOX:
                        {
                            toSkip = impBoxPatternMatch(null, codeAddr + sz, codeEndp, BoxPatterns.MakeInlineObservation);
                            break;
                        }

                        case CEE_MKREFANY:
                        {
                            // Arguably this should be NoteFatal, but the legacy behavior is to ignore this for the prejit root.
                            compInlineResult.Note(InlineObservation.CALLEE_UNSUPPORTED_OPCODE);

                            // Fail fast if we're inlining...
                            if (compIsForInlining)
                            {
                                assert(compInlineResult.IsFailure);
                                return;
                            }
                            break;
                        }

                        case CEE_LOCALLOC:
                        {
                            // We now allow localloc callees to become candidates in some cases.
                            compInlineResult.Note(InlineObservation.CALLEE_HAS_LOCALLOC);

                            if (compIsForInlining && compInlineResult.IsFailure)
                            {
                                return;
                            }
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

                    if (preciseScan)
                    {
                        if (opcode is >= CEE_LDNULL and <= CEE_LDC_R8)
                        {
                            fgStack.PushConstant();
                        }

                        switch (opcode)
                        {
                            case CEE_DUP:
                            {
                                fgStack.Push(fgStack.Top());
                                break;
                            }

                            case CEE_CALL:
                            case CEE_CALLVIRT:
                            {
                                impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Method);
                                var methodHnd = resolvedToken.hMethod;

                                if (eeIsIntrinsic(methodHnd))
                                {
                                    var ni = lookupNamedIntrinsic(methodHnd);
                                    ObserveNamedIntrinsicPrecise(this, ni, ref fgStack);
                                }
                                else if (FgStack.IsArgument(fgStack.Top()))
                                {
                                    // Optimistically assume that "call(arg)" returns something arg-dependent.
                                    // However, we don't know how many args it expects and its return type.
                                }
                                else
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            // Unary Jumps
                            case CEE_BRFALSE_S:
                            case CEE_BRTRUE_S:
                            case CEE_BRFALSE:
                            case CEE_BRTRUE:
                            {
                                var op1 = fgStack.Top();

                                if (FgStack.IsConstant(op1))
                                {
                                    compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_BRANCH);
                                }
                                else if (FgStack.IsArgument(op1))
                                {
                                    if (FgStack.IsConstArgument(op1, impInlineInfo))
                                    {
                                        compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_BRANCH);
                                        compInlineResult.Note(InlineObservation.CALLSITE_CONSTANT_ARG_FEEDS_TEST);
                                    }

                                    // E.g. brtrue is basically "if (X == 0)"
                                    compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_CONSTANT_TEST);
                                }
                                fgStack.PushUnknown();
                                break;
                            }

                            // Binary Jumps
                            case CEE_BEQ_S:
                            case CEE_BGE_S:
                            case CEE_BGT_S:
                            case CEE_BLE_S:
                            case CEE_BLT_S:
                            case CEE_BNE_UN_S:
                            case CEE_BGE_UN_S:
                            case CEE_BGT_UN_S:
                            case CEE_BLE_UN_S:
                            case CEE_BLT_UN_S:
                            case CEE_BEQ:
                            case CEE_BGE:
                            case CEE_BGT:
                            case CEE_BLE:
                            case CEE_BLT:
                            case CEE_BNE_UN:
                            case CEE_BGE_UN:
                            case CEE_BGT_UN:
                            case CEE_BLE_UN:
                            case CEE_BLT_UN:
                            {
                                ObserveComparisonPrecise(this, opcode, ref fgStack, isBranch: true);
                                break;
                            }

                            case CEE_SWITCH:
                            {
                                if (FgStack.IsConstantOrConstArg(fgStack.Top(), impInlineInfo))
                                {
                                    compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_SWITCH);
                                }

                                assert(!compIsForInlining || !compInlineResult.IsFailure);
                                fgStack.PushUnknown();
                                break;
                            }

                            case CEE_LDIND_I1:
                            case CEE_LDIND_U1:
                            case CEE_LDIND_I2:
                            case CEE_LDIND_U2:
                            case CEE_LDIND_I4:
                            case CEE_LDIND_U4:
                            case CEE_LDIND_I8:
                            case CEE_LDIND_I:
                            case CEE_LDIND_R4:
                            case CEE_LDIND_R8:
                            case CEE_LDIND_REF:
                            {
                                if (!FgStack.IsArgument(fgStack.Top()))
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            // Binary operators:
                            case CEE_ADD:
                            case CEE_SUB:
                            case CEE_MUL:
                            case CEE_DIV:
                            case CEE_DIV_UN:
                            case CEE_REM:
                            case CEE_REM_UN:
                            case CEE_AND:
                            case CEE_OR:
                            case CEE_XOR:
                            case CEE_SHL:
                            case CEE_SHR:
                            case CEE_SHR_UN:
                            case CEE_ADD_OVF:
                            case CEE_ADD_OVF_UN:
                            case CEE_MUL_OVF:
                            case CEE_MUL_OVF_UN:
                            case CEE_SUB_OVF:
                            case CEE_SUB_OVF_UN:
                            {
                                ObserveBinaryPrecise(this, opcode, ref fgStack);
                                break;
                            }

                            case CEE_CEQ:
                            case CEE_CGT:
                            case CEE_CGT_UN:
                            case CEE_CLT:
                            case CEE_CLT_UN:
                            {
                                ObserveComparisonPrecise(this, opcode, ref fgStack, isBranch: false);
                                break;
                            }

                            // Unary operators:
                            case CEE_NEG:
                            case CEE_NOT:
                            case CEE_CONV_I1:
                            case CEE_CONV_I2:
                            case CEE_CONV_I4:
                            case CEE_CONV_I8:
                            case CEE_CONV_R4:
                            case CEE_CONV_R8:
                            case CEE_CONV_U4:
                            case CEE_CONV_U8:
                            case CEE_CONV_R_UN:
                            case CEE_CONV_OVF_I1_UN:
                            case CEE_CONV_OVF_I2_UN:
                            case CEE_CONV_OVF_I4_UN:
                            case CEE_CONV_OVF_I8_UN:
                            case CEE_CONV_OVF_U1_UN:
                            case CEE_CONV_OVF_U2_UN:
                            case CEE_CONV_OVF_U4_UN:
                            case CEE_CONV_OVF_U8_UN:
                            case CEE_CONV_OVF_I_UN:
                            case CEE_CONV_OVF_U_UN:
                            case CEE_CONV_OVF_I1:
                            case CEE_CONV_OVF_U1:
                            case CEE_CONV_OVF_I2:
                            case CEE_CONV_OVF_U2:
                            case CEE_CONV_OVF_I4:
                            case CEE_CONV_OVF_U4:
                            case CEE_CONV_OVF_I8:
                            case CEE_CONV_OVF_U8:
                            case CEE_CONV_U2:
                            case CEE_CONV_U1:
                            case CEE_CONV_I:
                            case CEE_CONV_OVF_I:
                            case CEE_CONV_OVF_U:
                            case CEE_CONV_U:
                            {
                                var arg = fgStack.Top();

                                if (FgStack.IsArgument(arg))
                                {
                                    if (FgStack.IsConstArgument(arg, impInlineInfo))
                                    {
                                        compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_EXPR_UN);
                                    }
                                }
                                else if (!FgStack.IsConstant(arg))
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            case CEE_LDSTR:
                            case CEE_LDTOKEN:
                            {
                                fgStack.PushConstant();
                                break;
                            }

                            case CEE_CASTCLASS:
                            case CEE_ISINST:
                            {
                                var slot = fgStack.Top();

                                if (FgStack.IsConstant(slot))
                                {
                                    compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_EXPR_UN);
                                }
                                else if (FgStack.IsArgument(slot))
                                {
                                    if (FgStack.IsConstArgument(slot, impInlineInfo) || FgStack.IsExactArgument(slot, impInlineInfo))
                                    {
                                        compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_EXPR_UN);
                                    }
                                    else
                                    {
                                        compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_CAST);
                                    }
                                }
                                else
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            case CEE_UNBOX:
                            case CEE_UNBOX_ANY:
                            {
                                var slot = fgStack.Top();

                                if (FgStack.IsArgument(slot))
                                {
                                    if (FgStack.IsExactArgument(slot, impInlineInfo))
                                    {
                                        compInlineResult.Note(InlineObservation.CALLSITE_UNBOX_EXACT_ARG);
                                    }
                                    else
                                    {
                                        compInlineResult.Note(InlineObservation.CALLEE_UNBOX_ARG);
                                    }
                                }
                                fgStack.PushUnknown();
                                break;
                            }

                            case CEE_THROW:
                            {
                                fgStack.Clear();
                                break;
                            }

                            case CEE_LDFLD:
                            case CEE_LDFLDA:
                            case CEE_STFLD:
                            {
                                if (FgStack.IsArgument(fgStack.Top()))
                                {
                                    compInlineResult.Note(InlineObservation.CALLEE_ARG_STRUCT_FIELD_ACCESS);
                                }
                                else
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            case CEE_BOX:
                            {
                                if (toSkip > 0)
                                {
                                    // toSkip > 0 means we most likely will hit a pattern (e.g. box+isinst+brtrue) that will be folded into a const
                                    codeAddr += toSkip;
                                }
                                fgStack.PushUnknown();
                                break;
                            }

                            case CEE_LDELEM_I1:
                            case CEE_LDELEM_U1:
                            case CEE_LDELEM_I2:
                            case CEE_LDELEM_U2:
                            case CEE_LDELEM_I4:
                            case CEE_LDELEM_U4:
                            case CEE_LDELEM_I8:
                            case CEE_LDELEM_I:
                            case CEE_LDELEM_R4:
                            case CEE_LDELEM_R8:
                            case CEE_LDELEM_REF:
                            case CEE_STELEM_I:
                            case CEE_STELEM_I1:
                            case CEE_STELEM_I2:
                            case CEE_STELEM_I4:
                            case CEE_STELEM_I8:
                            case CEE_STELEM_R4:
                            case CEE_STELEM_R8:
                            case CEE_STELEM_REF:
                            case CEE_LDELEM:
                            case CEE_STELEM:
                            {
                                if (FgStack.IsArgument(fgStack.Top()) || FgStack.IsArgument(fgStack.Top(1)))
                                {
                                    compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK);
                                }
                                else
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            case CEE_LDLOC_0:
                            case CEE_LDLOC_1:
                            case CEE_LDLOC_2:
                            case CEE_LDLOC_3:
                            {
                                if (prevOpcode == (CEE_STLOC_3 - (CEE_LDLOC_3 - opcode)))
                                {
                                    // Fold stloc+ldloc by throwing away SLOT_UNKNOWN inserted by STLOC
                                    fgStack.Push(fgStack.Top(1));
                                }
                                else
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            case CEE_LDARGA:
                            case CEE_LDARGA_S:
                            {
                                if (compIsForInlining)
                                {
                                    fgStack.PushArgument(varNum);
                                }
                                else
                                {
                                    fgStack.PushUnknown();
                                }
                                break;
                            }

                            case CEE_LDARG_0:
                            case CEE_LDARG_1:
                            case CEE_LDARG_2:
                            case CEE_LDARG_3:
                            {
                                fgStack.PushArgument(opcode - CEE_LDARG_0);
                                break;
                            }

                            case CEE_LDARG_S:
                            case CEE_LDARG:
                            {
                                fgStack.PushArgument(varNum);
                                break;
                            }

                            case CEE_LDLEN:
                            {
                                fgStack.PushArrayLen();
                                break;
                            }

                            case CEE_NEWARR:
                            {
                                if (!isReturnsArrayKnown)
                                {
                                    if (info.compRetType is TYP_REF)
                                    {
                                        var retClass = info.compMethodInfo->args.retTypeClass;

                                        if (retClass != NO_CLASS_HANDLE)
                                        {
                                            var retClassAttribs = info.compCompHnd->getClassAttribs(retClass);
                                            returnsArray = (retClassAttribs & CORINFO_FLG_ARRAY) != 0;
                                        }
                                    }
                                    isReturnsArrayKnown = true;
                                }

                                if (returnsArray && fgStack.IsStackAtLeastOneDeep)
                                {
                                    var slot0 = fgStack.Slot0;

                                    if (FgStack.IsConstantOrConstArg(slot0, impInlineInfo))
                                    {
                                        compInlineResult.Note(InlineObservation.CALLEE_MAY_RETURN_SMALL_ARRAY);
                                    }
                                }

                                fgStack.PushUnknown();
                                break;
                            }

                            default:
                            {
                                fgStack.PushUnknown();
                                break;
                            }
                        }
                    }
                }

                // Clear any prefix flags that may have been set
                prefixFlags = 0;
            }
            else
            {
                switch (opcode)
                {
                    case CEE_UNALIGNED:
                    {
                        noway_assert(sz is sizeof(byte));

                        prefixFlags |= PREFIX_UNALIGNED;
                        codeAddr += sizeof(byte);

                        impValidateMemoryAccessOpcode(codeAddr, codeEndp, volatilePrefix: false);
                        break;
                    }

                    case CEE_VOLATILE:
                    {
                        noway_assert(sz == 0);
                        prefixFlags |= PREFIX_VOLATILE;

                        impValidateMemoryAccessOpcode(codeAddr, codeEndp, volatilePrefix: true);
                        break;
                    }

                    case CEE_TAILCALL:
                    {
                        noway_assert(sz == 0);
                        prefixFlags |= PREFIX_TAILCALL_EXPLICIT;

                        var actualOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

                        if (!impOpcodeIsCallOpcode(actualOpcode))
                        {
                            BADCODE("tailcall. has to be followed by call, callvirt or calli");
                        }
                        break;
                    }

                    case CEE_CONSTRAINED:
                    {
                        noway_assert(sz is sizeof(int));
                        prefixFlags |= PREFIX_CONSTRAINED;

                        codeAddr += sizeof(int);
                        var actualOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

                        if (actualOpcode is not CEE_CALLVIRT and not CEE_CALL and not CEE_LDFTN)
                        {
                            BADCODE("constrained. has to be followed by callvirt, call or ldftn");
                        }
                        break;
                    }

                    case CEE_RETHROW:
                    {
                        if (makeInlineObservations)
                        {
                            assert(compInlineResult is not null);

                            // Arguably this should be NoteFatal, but the legacy behavior is to ignore this for the prejit root.
                            compInlineResult.Note(InlineObservation.CALLEE_UNSUPPORTED_OPCODE);

                            // Fail fast if we're inlining...
                            if (compIsForInlining)
                            {
                                assert(compInlineResult.IsFailure);
                                return;
                            }
                        }
                        goto default;
                    }

                    case CEE_SIZEOF:
                    {
                        if (preciseScan)
                        {
                            fgStack.PushConstant();
                        }
                        break;
                    }

                    case CEE_READONLY:
                    {
                        noway_assert(sz == 0);
                        prefixFlags |= PREFIX_READONLY;

                        var actualOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

                        if ((actualOpcode is not CEE_LDELEMA) && !impOpcodeIsCallOpcode(actualOpcode))
                        {
                            BADCODE("readonly. has to be followed by ldelema or call");
                        }
                        break;
                    }

                    default:
                    {
                        fgStack.PushUnknown();
                        break;
                    }
                }
            }

            if (prefixFlags == 0)
            {
                // Skip any remaining operands this opcode may have
                codeAddr += sz;

                // Increment the number of observed instructions
                opts.instrCount++;
            }

            if (makeInlineObservations)
            {
                assert(compInlineResult is not null);
                var obs = typeIsNormed ? InlineObservation.CALLEE_OPCODE_NORMED : InlineObservation.CALLEE_OPCODE;
                compInlineResult.NoteInt(obs, (int)(opcode));
            }
            prevOpcode = opcode;
        }

        if (codeAddr != codeEndp)
        {
            TooFar(codeAddr, codeBegp);
        }

#if DEBUG
        compInlineContext.m_ILInstsSet = ilInstsSet;
#endif

        if (makeInlineObservations)
        {
            assert(impInlineInfo is not null);
            assert(compInlineResult is not null);

            compInlineResult.Note(InlineObservation.CALLEE_END_OPCODE_SCAN);

            // If there are no return blocks we know it does not return, however if there
            // return blocks we don't know it returns as it may be counting unreachable code.
            // However we will still make the CALLEE_DOES_NOT_RETURN observation.

            compInlineResult.NoteBool(InlineObservation.CALLEE_DOES_NOT_RETURN, retBlocks == 0);

            if ((retBlocks == 0) && compIsForInlining)
            {
                var iciCall = impInlineInfo.iciCall;
                assert(iciCall is not null);

                if (info.compCompHnd->notifyMethodInfoUsage(iciCall._callMethHnd))
                {
                    // Mark the call node as "no return" as it can impact caller's code quality.
                    setCallDoesNotReturn(iciCall);

                    // NOTE: we also ask VM whether we're allowed to do so - we don't want to mark a call
                    // as "no-return" if its IL may change.
                }
            }

            // If the inline is viable and discretionary, do the profitability screening.
            if (compInlineResult.IsDiscretionaryCandidate)
            {
                // Make some callsite specific observations that will feed into the profitability model.
                impMakeDiscretionaryInlineObservations(impInlineInfo, compInlineResult);

                // None of those observations should have changed the inline's viability.
                assert(compInlineResult.IsCandidate);

                if (compIsForInlining)
                {
                    // Assess profitability...
                    ref readonly var methodInfo = ref impInlineInfo.inlineCandidateInfo.methInfo;
                    compInlineResult.DetermineProfitability(methodInfo);

                    if (compInlineResult.IsFailure)
                    {
                        assert(impInlineRoot.m_inlineStrategy is not null);
                        impInlineRoot.m_inlineStrategy.NoteUnprofitable();
                        JITDUMP("\n\nInline expansion aborted, inline not profitable\n");
                        return;
                    }
                    else
                    {
                        // The inline is still viable.
                        assert(compInlineResult.IsCandidate);
                    }
                }
                else
                {
                    // Prejit root case.
                    // Profitability assessment for this is done over in compCompileHelper.
                }
            }
        }

        // None of the local vars in the inlinee should have address taken or been written to.
        // Therefore we should NOT need to enter this "if" statement.
        if (!compIsForInlining && !info.compIsStatic)
        {
            fgAdjustForAddressExposedOrWrittenThis();
        }

        // Now that we've seen the IL, set lvSingleDef for root method locals.
        //
        // We could also do this for root method arguments but single-def
        // arguments are set by the caller and so we don't know anything
        // about the possible values or types.
        //
        // For inlinees we do this over in impInlineFetchLocal and
        // impInlineFetchArg (here args are included as we sometimes get
        // new information about the types of inlinee args).
        if (!compIsForInlining)
        {
            var firstLcl = info.compArgsCount;
            var lastLcl = firstLcl + info.compMethodInfo->locals.numArgs;

            for (var lclNum = firstLcl; lclNum < lastLcl; lclNum++)
            {
                ref var lclDsc = ref lvaGetDesc(lclNum);
                assert(!lclDsc.lvSingleDef);

                lclDsc.lvSingleDef = !lclDsc.lvHasMultipleILStoreOp && !lclDsc.lvHasLdAddrOp;

                if (lclDsc.lvSingleDef)
                {
                    JITDUMP($"Marked V{lclNum:D2} as a single def local\n");
                }
            }
        }

        static var_types LoadAddress(Compiler compiler, byte* codeAddr, byte* codeEndp, byte sz, int varNum)
        {
            // Determine if the next instruction will consume
            // the address. If so we won't mark this var as
            // address taken.
            //
            // We will put structs on the stack and changing
            // the addrTaken of a local requires an extra pass
            // in the morpher so we won't apply this
            // optimization to structs.
            //
            // Debug code spills for every IL instruction, and
            // therefore it will split statements, so we will
            // need the address.  Note that this optimization
            // is based in that we know what trees we will
            // generate for this ldfld, and we require that we
            // won't need the address of this local at all

            ref var lvaDsc = ref compiler.lvaTable[varNum];

            if ((codeAddr < (codeEndp - sz)) && !compiler.opts.compDbgCode && compiler.impILConsumesAddr(codeAddr + sz, codeEndp))
            {
                // We can skip the addrtaken, as next IL instruction consumes the address.
            }
            else
            {
                lvaDsc.lvHasLdAddrOp = true;

                if (!compiler.info.compIsStatic)
                {
                    var thisArg = compiler.info.compThisArg;

                    if (varNum == thisArg)
                    {
                        // Addr taken on "this" pointer is significant, go ahead to mark it as permanently addr-exposed here.
                        // This may be conservative, but probably not very.
                        compiler.lvaSetVarAddrExposed(thisArg, AddressExposedReason.TOO_CONSERVATIVE);
                    }
                }
            }

            return lvaDsc.Type;
        }

        static void ObserveBinaryPrecise(Compiler compiler, OPCODE opcode, ref FgStack fgStack)
        {
            var compInlineResult = compiler.compInlineResult;
            var impInlineInfo = compiler.impInlineInfo;

            assert(compInlineResult is not null);

            var op1 = fgStack.Top(1);
            var op2 = fgStack.Top(0);

            var isOp1Const = FgStack.IsConstantOrConstArg(op1, impInlineInfo);
            var isOp2Const = FgStack.IsConstantOrConstArg(op2, impInlineInfo);

            if (isOp2Const)
            {
                if (opcode is >= CEE_DIV and <= CEE_REM_UN)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_DIV_BY_CNS);
                }

                if (isOp1Const)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_EXPR);
                }

                compInlineResult.Note(InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS);
                fgStack.Push(op1);
            }
            else if (isOp1Const)
            {
                compInlineResult.Note(InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS);
            }
            else
            {
                fgStack.PushUnknown();
            }
        }

        static void ObserveNamedIntrinsicPrecise(Compiler compiler, NamedIntrinsic ni, ref FgStack fgStack)
        {
            var compInlineResult = compiler.compInlineResult;
            var impInlineInfo = compiler.impInlineInfo;

            assert(compInlineResult is not null);

            var stackAlreadyCorrect = false;
            var foldableIntrinsic = false;

            if (compiler.IsMathIntrinsic(ni))
            {
                // Most Math(F) intrinsics have single arguments
                foldableIntrinsic = FgStack.IsConstantOrConstArg(fgStack.Top(), impInlineInfo);

                if (compiler.IsTargetIntrinsic(ni))
                {
                    compInlineResult.Note(InlineObservation.CALLEE_INTRINSIC);
                }
            }
            else if (ni is not NI_Illegal)
            {
                switch (ni)
                {
                    // These are most likely foldable without arguments
                    case NI_System_Collections_Generic_Comparer_get_Default:
                    case NI_System_Collections_Generic_EqualityComparer_get_Default:
                    case NI_System_Enum_HasFlag:
                    case NI_System_GC_KeepAlive:
                    {
                        fgStack.PushUnknown();
                        foldableIntrinsic = true;
                        break;
                    }

                    case NI_System_SpanHelpers_ClearWithoutReferences:
                    case NI_System_SpanHelpers_Fill:
                    case NI_System_SpanHelpers_SequenceEqual:
                    case NI_System_SpanHelpers_Memmove:
                    {
                        if (FgStack.IsConstArgument(fgStack.Top(), impInlineInfo))
                        {
                            // Constant (at its call-site) argument feeds the Memmove/Memcmp length argument.
                            // We most likely will be able to unroll it.
                            // It is important to only raise this hint for constant arguments, if it's just a
                            // constant in the inlinee itself then we don't need to inline it for unrolling.
                            compInlineResult.Note(InlineObservation.CALLSITE_UNROLLABLE_MEMOP);
                        }
                        break;
                    }

                    case NI_System_Span_get_Item:
                    case NI_System_ReadOnlySpan_get_Item:
                    {
                        if (FgStack.IsArgument(fgStack.Top(0)) || FgStack.IsArgument(fgStack.Top(1)))
                        {
                            compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK);
                        }
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant:
                    {
                        if (FgStack.IsConstArgument(fgStack.Top(), impInlineInfo))
                        {
                            compInlineResult.Note(InlineObservation.CALLEE_CONST_ARG_FEEDS_ISCONST);
                        }
                        else
                        {
                            compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_ISCONST);
                        }

                        // RuntimeHelpers.IsKnownConstant is always folded into a const
                        fgStack.PushConstant();
                        foldableIntrinsic = true;
                        break;
                    }

                    // These are foldable if the first argument is a constant
#if FEATURE_HW_INTRINSICS
                    case NI_Vector128_Create:
                    case NI_Vector128_CreateScalar:
                    case NI_Vector128_CreateScalarUnsafe:
#if TARGET_ARM64
                    case NI_Vector64_Create:
                    case NI_Vector64_CreateScalar:
                    case NI_Vector64_CreateScalarUnsafe:
                    case NI_ArmBase_LeadingZeroCount:
                    case NI_ArmBase_ReverseElementBits:
                    case NI_ArmBase_Arm64_LeadingZeroCount:
                    case NI_ArmBase_Arm64_ReverseElementBits:
#elif TARGET_XARCH
                    case NI_Vector256_Create:
                    case NI_Vector256_CreateScalar:
                    case NI_Vector256_CreateScalarUnsafe:
                    case NI_Vector512_Create:
                    case NI_Vector512_CreateScalar:
                    case NI_Vector512_CreateScalarUnsafe:
                    case NI_X86Base_BitScanForward:
                    case NI_X86Base_BitScanReverse:
                    case NI_X86Base_PopCount:
                    case NI_X86Base_X64_BitScanForward:
                    case NI_X86Base_X64_BitScanReverse:
                    case NI_X86Base_X64_PopCount:
                    case NI_AVX2_LeadingZeroCount:
                    case NI_AVX2_TrailingZeroCount:
                    case NI_AVX2_X64_LeadingZeroCount:
                    case NI_AVX2_X64_TrailingZeroCount:
#endif
#endif
                    case NI_PRIMITIVE_LeadingZeroCount:
                    case NI_PRIMITIVE_Log2:
                    case NI_PRIMITIVE_PopCount:
                    case NI_PRIMITIVE_TrailingZeroCount:
                    case NI_System_Type_get_IsEnum:
                    case NI_System_Type_GetEnumUnderlyingType:
                    case NI_System_Type_get_IsValueType:
                    case NI_System_Type_get_IsPrimitive:
                    case NI_System_Type_get_IsByRefLike:
                    case NI_System_Type_get_IsGenericType:
                    case NI_System_Type_GetTypeFromHandle:
                    case NI_System_Type_GetGenericTypeDefinition:
                    case NI_System_String_get_Length:
                    case NI_System_Buffers_Binary_BinaryPrimitives_ReverseEndianness:
                    {
                        // Top() in order to keep it as is in case of foldableIntrinsic
                        if (FgStack.IsConstantOrConstArg(fgStack.Top(), impInlineInfo))
                        {
                            foldableIntrinsic = true;
                        }
                        break;
                    }

                    // These are foldable if two arguments are constants
                    case NI_PRIMITIVE_RotateLeft:
                    case NI_PRIMITIVE_RotateRight:
                    case NI_System_Type_op_Equality:
                    case NI_System_Type_op_Inequality:
                    case NI_System_String_get_Chars:
                    case NI_System_Type_IsAssignableTo:
                    case NI_System_Type_IsAssignableFrom:
                    {
                        if (FgStack.IsConstantOrConstArg(fgStack.Top(0), impInlineInfo) &&
                            FgStack.IsConstantOrConstArg(fgStack.Top(1), impInlineInfo))
                        {
                            foldableIntrinsic = true;
                            fgStack.PushConstant();
                        }
                        break;
                    }

                    case NI_IsSupported_True:
                    case NI_IsSupported_False:
                    case NI_IsSupported_Type:
                    {
                        foldableIntrinsic = true;
                        fgStack.PushConstant();
                        break;
                    }

                    case NI_Vector_GetCount:
                    {
                        foldableIntrinsic = true;
                        fgStack.PushConstant();
                        // TODO: for FEATURE_SIMD check if it's a loop condition - we unroll such loops.
                        break;
                    }

                    case NI_SRCS_UNSAFE_Add:
                    case NI_SRCS_UNSAFE_AddByteOffset:
                    case NI_SRCS_UNSAFE_ByteOffset:
                    case NI_SRCS_UNSAFE_Subtract:
                    case NI_SRCS_UNSAFE_SubtractByteOffset:
                    {
                        ObserveBinaryPrecise(compiler, CEE_CALL, ref fgStack);
                        break;
                    }

                    case NI_SRCS_UNSAFE_AreSame:
                    case NI_SRCS_UNSAFE_IsAddressGreaterThan:
                    case NI_SRCS_UNSAFE_IsAddressGreaterThanOrEqualTo:
                    case NI_SRCS_UNSAFE_IsAddressLessThan:
                    case NI_SRCS_UNSAFE_IsAddressLessThanOrEqualTo:
                    case NI_SRCS_UNSAFE_IsNullRef:
                    {
                        ObserveComparisonPrecise(compiler, CEE_CALL, ref fgStack, isBranch: false);
                        break;
                    }

                    case NI_SRCS_UNSAFE_AsPointer:
                    {
                        // These are effectively primitive unary operations so the
                        // handling roughly mirrors the handling for CEE_CONV_U and
                        // friends that exists elsewhere in this method

                        var arg = fgStack.Top();

                        if (FgStack.IsConstArgument(arg, impInlineInfo))
                        {
                            foldableIntrinsic = true;
                        }
                        else if (FgStack.IsArgument(arg))
                        {
                            stackAlreadyCorrect = true;
                        }
                        else if (FgStack.IsConstant(arg))
                        {
                            // input is a constant so we still want to track this as foldable, unlike
                            // what is done for the regular unary operator handling, since we have
                            // a CEE_CALL node and not something more primitive
                            foldableIntrinsic = true;
                        }

                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case NI_Vector128_As:
                    case NI_Vector128_AsByte:
                    case NI_Vector128_AsDouble:
                    case NI_Vector128_AsInt16:
                    case NI_Vector128_AsInt32:
                    case NI_Vector128_AsInt64:
                    case NI_Vector128_AsNInt:
                    case NI_Vector128_AsNUInt:
                    case NI_Vector128_AsSByte:
                    case NI_Vector128_AsSingle:
                    case NI_Vector128_AsUInt16:
                    case NI_Vector128_AsUInt32:
                    case NI_Vector128_AsUInt64:
                    case NI_Vector128_AsVector4:
                    case NI_Vector128_op_UnaryPlus:
#if TARGET_ARM64
                    case NI_Vector64_As:
                    case NI_Vector64_AsByte:
                    case NI_Vector64_AsDouble:
                    case NI_Vector64_AsInt16:
                    case NI_Vector64_AsInt32:
                    case NI_Vector64_AsInt64:
                    case NI_Vector64_AsNInt:
                    case NI_Vector64_AsNUInt:
                    case NI_Vector64_AsSByte:
                    case NI_Vector64_AsSingle:
                    case NI_Vector64_AsUInt16:
                    case NI_Vector64_AsUInt32:
                    case NI_Vector64_AsUInt64:
                    case NI_Vector64_op_UnaryPlus:
#elif TARGET_XARCH
                    case NI_Vector256_As:
                    case NI_Vector256_AsByte:
                    case NI_Vector256_AsDouble:
                    case NI_Vector256_AsInt16:
                    case NI_Vector256_AsInt32:
                    case NI_Vector256_AsInt64:
                    case NI_Vector256_AsNInt:
                    case NI_Vector256_AsNUInt:
                    case NI_Vector256_AsSByte:
                    case NI_Vector256_AsSingle:
                    case NI_Vector256_AsUInt16:
                    case NI_Vector256_AsUInt32:
                    case NI_Vector256_AsUInt64:
                    case NI_Vector256_op_UnaryPlus:
                    case NI_Vector512_As:
                    case NI_Vector512_AsByte:
                    case NI_Vector512_AsDouble:
                    case NI_Vector512_AsInt16:
                    case NI_Vector512_AsInt32:
                    case NI_Vector512_AsInt64:
                    case NI_Vector512_AsNInt:
                    case NI_Vector512_AsNUInt:
                    case NI_Vector512_AsSByte:
                    case NI_Vector512_AsSingle:
                    case NI_Vector512_AsUInt16:
                    case NI_Vector512_AsUInt32:
                    case NI_Vector512_AsUInt64:
                    case NI_Vector512_op_UnaryPlus:
#endif
#endif
                    case NI_SRCS_UNSAFE_As:
                    case NI_SRCS_UNSAFE_AsRef:
                    case NI_SRCS_UNSAFE_BitCast:
                    case NI_SRCS_UNSAFE_SkipInit:
                    {
                        // TODO-CQ: These are no-ops in that they never produce any IR
                        // and simply return op1 untouched. We should really track them
                        // as such and adjust the multiplier even more, but we'll settle
                        // for marking it as foldable until additional work can happen.

                        foldableIntrinsic = true;
                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case NI_Vector128_get_AllBitsSet:
                    case NI_Vector128_get_E:
                    case NI_Vector128_get_Epsilon:
                    case NI_Vector128_get_NaN:
                    case NI_Vector128_get_NegativeInfinity:
                    case NI_Vector128_get_NegativeOne:
                    case NI_Vector128_get_NegativeZero:
                    case NI_Vector128_get_One:
                    case NI_Vector128_get_Pi:
                    case NI_Vector128_get_PositiveInfinity:
                    case NI_Vector128_get_Tau:
                    case NI_Vector128_get_Zero:
#if TARGET_ARM64
                    case NI_Vector64_get_AllBitsSet:
                    case NI_Vector64_get_E:
                    case NI_Vector64_get_Epsilon:
                    case NI_Vector64_get_NaN:
                    case NI_Vector64_get_NegativeInfinity:
                    case NI_Vector64_get_NegativeOne:
                    case NI_Vector64_get_NegativeZero:
                    case NI_Vector64_get_One:
                    case NI_Vector64_get_Pi:
                    case NI_Vector64_get_PositiveInfinity:
                    case NI_Vector64_get_Tau:
                    case NI_Vector64_get_Zero:
#elif TARGET_XARCH
                    case NI_Vector256_get_AllBitsSet:
                    case NI_Vector256_get_E:
                    case NI_Vector256_get_Epsilon:
                    case NI_Vector256_get_NaN:
                    case NI_Vector256_get_NegativeInfinity:
                    case NI_Vector256_get_NegativeOne:
                    case NI_Vector256_get_NegativeZero:
                    case NI_Vector256_get_One:
                    case NI_Vector256_get_Pi:
                    case NI_Vector256_get_PositiveInfinity:
                    case NI_Vector256_get_Tau:
                    case NI_Vector256_get_Zero:
                    case NI_Vector512_get_AllBitsSet:
                    case NI_Vector512_get_E:
                    case NI_Vector512_get_Epsilon:
                    case NI_Vector512_get_NaN:
                    case NI_Vector512_get_NegativeInfinity:
                    case NI_Vector512_get_NegativeOne:
                    case NI_Vector512_get_NegativeZero:
                    case NI_Vector512_get_One:
                    case NI_Vector512_get_Pi:
                    case NI_Vector512_get_PositiveInfinity:
                    case NI_Vector512_get_Tau:
                    case NI_Vector512_get_Zero:
#endif
                    {
                        // These always produce a vector constant

                        foldableIntrinsic = true;

                        // TODO-CQ: We should really push a constant onto the stack
                        // However, this isn't trivially possible without the inliner
                        // understanding a new type of "vector constant" so it doesn't
                        // negatively impact other possible checks/handling

                        break;
                    }
#endif

                    case NI_SRCS_UNSAFE_NullRef:
                    case NI_SRCS_UNSAFE_SizeOf:
                    {
                        // These always produce a constant

                        foldableIntrinsic = true;
                        fgStack.PushConstant();

                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                compInlineResult.Note(InlineObservation.CALLEE_INTRINSIC);
            }

            if (foldableIntrinsic)
            {
                compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_INTRINSIC);
            }

            if (!stackAlreadyCorrect)
            {
                fgStack.PushUnknown();
            }
        }

        static void ObserveComparisonPrecise(Compiler compiler, OPCODE opcode, ref FgStack fgStack, bool isBranch)
        {
            var compInlineResult = compiler.compInlineResult;
            var impInlineInfo = compiler.impInlineInfo;

            assert(compInlineResult is not null);

            var op1 = fgStack.Top(1);
            var op2 = fgStack.Top(0);

            var isOp1Const = false;
            var isOp1Arg = false;

            if (FgStack.IsConstant(op1))
            {
                isOp1Const = true;
            }
            else if (FgStack.IsArgument(op1))
            {
                isOp1Arg = true;
                isOp1Const = FgStack.IsConstArgument(op1, impInlineInfo);
            }

            var isOp2Const = false;
            var isOp2Arg = false;

            if (FgStack.IsConstant(op2))
            {
                isOp2Const = true;
            }
            else if (FgStack.IsArgument(op2))
            {
                isOp2Arg = true;
                isOp2Const = FgStack.IsConstArgument(op2, impInlineInfo);
            }

            if (isOp2Const)
            {
                if (isOp1Const && isBranch)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_FOLDABLE_BRANCH);
                }

                if (isOp1Arg)
                {
                    compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_CONSTANT_TEST);
                }

                if (isOp2Arg)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_CONSTANT_ARG_FEEDS_TEST);
                }

                compInlineResult.Note(InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS);

                if (!isBranch)
                {
                    fgStack.Push(op1);
                }
            }
            else if (isOp1Const)
            {
                if (isOp2Arg)
                {
                    compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_CONSTANT_TEST);
                }

                if (isOp1Arg)
                {
                    compInlineResult.Note(InlineObservation.CALLSITE_CONSTANT_ARG_FEEDS_TEST);
                }

                compInlineResult.Note(InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS);
            }

            if (isOp1Arg)
            {
                if (FgStack.IsArgument(op2))
                {
                    compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK);
                }
                compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_TEST);
            }
            else if (isOp2Arg)
            {
                if (FgStack.IsArgument(op1))
                {
                    compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK);
                }
                compInlineResult.Note(InlineObservation.CALLEE_ARG_FEEDS_TEST);
            }

            if (isBranch)
            {
                fgStack.PushUnknown();
            }
        }

        static void StoreLocal(Compiler compiler, int varNum)
        {
            if (compiler.compIsForInlining)
            {
                var inlineInfo = compiler.impInlineInfo;
                ref var lclInfo = ref inlineInfo.lclVarInfo[varNum + inlineInfo.argCnt];

                if (lclInfo.lclHasStlocOp)
                {
                    lclInfo.lclHasMultipleStlocOp = true;
                }
                else
                {
                    lclInfo.lclHasStlocOp = true;
                }
            }
            else
            {
                varNum += compiler.info.compArgsCount;
                var lvaTable = compiler.lvaTable;

                // This check is only intended to prevent an AV.
                // Bad varNum values will later be handled properly by the verifier.

                if (varNum < lvaTable.Length)
                {
                    ref var lvaDesc = ref lvaTable[varNum];

                    // In non-inline cases, note written-to locals.
                    if (lvaDesc.lvHasILStoreOp)
                    {
                        lvaDesc.lvHasMultipleILStoreOp = true;
                    }
                    else
                    {
                        lvaDesc.lvHasILStoreOp = true;
                    }
                }
            }
        }

        [DoesNotReturn]
        static void TooFar(byte* codeAddr, byte* codeBegp)
        {
            BADCODE($"Code ends in the middle of an opcode, or there is a branch past the end of the method at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
        }
    }

    /// <summary>Check that "blkDest" is the first block of an inner try or a sibling with no intervening trys in between.</summary>
    /// <param name="blkSrc">the source block</param>
    /// <param name="blkDest">the destination block</param>
    /// <param name="sibling">true if checking for sibling try regions</param>
    /// <returns>true if flow from "blkSrc" to "blkDest" is legal</returns>
    public bool fgFlowToFirstBlockOfInnerTry(BasicBlock blkSrc, BasicBlock blkDest, bool sibling)
    {
#if DEBUG
        // These rules aren't quite correct after EH normalization has introduced new blocks
        assert(!fgNormalizeEHDone);
#endif

        noway_assert(blkDest.hasTryIndex);

        var XTnum = blkDest.TryIndex;
        var lastXTnum = blkSrc.hasTryIndex ? blkSrc.TryIndex : compHndBBtabCount;

        noway_assert(XTnum < compHndBBtabCount);
        noway_assert(lastXTnum <= compHndBBtabCount);

        ref var HBtab = ref ehGetDsc(XTnum);

        // check that we are not jumping into middle of try
        if (HBtab.ebdTryBeg != blkDest)
        {
            return false;
        }

        if (sibling)
        {
            noway_assert(!BasicBlock.sameTryRegion(blkSrc, blkDest));

            // find the l.u.b of the two try ranges
            // Set lastXTnum to the l.u.b.

            HBtab = ref ehGetDsc(++lastXTnum);

            while (lastXTnum < compHndBBtabCount)
            {
                if (jitIsBetweenInclusive(blkDest.bbNum, HBtab.ebdTryBeg.bbNum, HBtab.ebdTryLast.bbNum))
                {
                    break;
                }
                HBtab = ref ehGetDsc(++lastXTnum);
            }
        }

        // now check there are no intervening trys between dest and l.u.b
        // (it is ok to have intervening trys as long as they all start at
        //  the same code offset)

        HBtab = ref ehGetDsc(++XTnum);

        while (XTnum < lastXTnum)
        {
            if ((HBtab.ebdTryBeg.bbNum < blkDest.bbNum) && (blkDest.bbNum <= HBtab.ebdTryLast.bbNum))
            {
                return false;
            }
            HBtab = ref ehGetDsc(++XTnum);
        }

        return true;
    }

    /// <summary>Find and return the predecessor edge corresponding to a given predecessor block.</summary>
    /// <param name="block">The block with the predecessor list to operate on.</param>
    /// <param name="blockPred">The predecessor block to find in the predecessor list.</param>
    /// <returns>The FlowEdge edge corresponding to "blockPred". If "blockPred" is not in the predecessor list of "block", then returns null.</returns>
    public FlowEdge? fgGetPredForBlock(BasicBlock block, BasicBlock blockPred)
    {
        foreach (var pred in block.PredEdges)
        {
            if (blockPred == pred.SourceBlock)
            {
                return pred;
            }
        }
        return null;
    }

    public ref FlowEdge? fgGetPredForBlock(BasicBlock block, BasicBlock blockPred, out FlowEdge? pred)
    {
        ref var predPrevAddr = ref block.bbPreds;
        pred = predPrevAddr;

        while (pred is not null)
        {
            if (blockPred == pred.SourceBlock)
            {
                return ref predPrevAddr;
            }

            predPrevAddr = ref pred.NextPredEdgeRef;
            pred = predPrevAddr;
        }

        pred = null;
        return ref Unsafe.NullRef<FlowEdge?>();
    }

    /// <summary>Initialize the basic block lookup table used by fgLookupBB.</summary>
    public void fgInitBBLookup()
    {
        var dscBBs = new BasicBlock[fgBBcount];
        fgBBs = dscBBs;

        // Walk all the basic blocks, filling in the table
        var i = 0;

        foreach (var block in Blocks)
        {
            dscBBs[i++] = block;
        }
        noway_assert(i == fgBBcount);
    }

    /// <summary>Inserts basic block before existing basic block.</summary>
    /// <param name="insertBeforeBlk">the block before which the new block is inserted</param>
    /// <param name="newBlk">the new block to insert</param>
    /// <remarks>
    ///   <para>If "insertBeforeBlk" is in the funclet region, then "newBlk" will be in the funclet region.</para>
    ///   <para>If "insertBeforeBlk" is the first block of the funclet region, then "newBlk" will be the new first block of the funclet region.</para> 
    /// </remarks>
    public void fgInsertBBbefore(BasicBlock insertBeforeBlk, BasicBlock newBlk)
    {
        if (insertBeforeBlk == fgFirstBB)
        {
            newBlk.Next = fgFirstBB;
            fgFirstBB = newBlk;
            assert(fgFirstBB.IsFirst);
        }
        else
        {
            assert(insertBeforeBlk.Prev is not null);
            fgInsertBBafter(insertBeforeBlk.Prev, newBlk);
        }

        if (insertBeforeBlk == fgFirstFuncletBB)
        {
            fgFirstFuncletBB = newBlk;
        }
    }

    /// <summary>Inserts basic block after existing basic block.</summary>
    /// <param name="insertAfterBlk">the block after which the new block is inserted</param>
    /// <param name="newBlk">the new block to insert</param>
    /// <remarks>
    ///   <para>If "insertAfterBlk" is in the funclet region, then "newBlk" will be in the funclet region.</para>
    ///   <para>It can't be used to insert a block as the first block of the funclet region.</para> 
    /// </remarks>
    public void fgInsertBBafter(BasicBlock insertAfterBlk, BasicBlock newBlk)
    {
        if (fgLastBB == insertAfterBlk)
        {
            fgLastBB = newBlk;
            fgLastBB.Next = null;
        }
        else
        {
            newBlk.Next = insertAfterBlk.Next;
        }
        insertAfterBlk.Next = newBlk;
    }

    public bool fgIsBigOffset(nint offset)
        => offset > compMaxUncheckedOffsetForNullObject;

#if DEBUG
    /// <summary>In non-Release builds, set fgBBs to empty.</summary>
    /// <remarks>After calling this, fgInitBBLookup must be called before using fgBBs again.</remarks>
    public void fgInvalidateBBLookup()
    {
        fgBBs = [];
    }
#endif

    /// <summary>set block jump targets and add pred edges</summary>
    /// <remarks>
    ///   <para>Pred edges for BBJ_EHFILTERRET are set later by fgFindBasicBlocks.</para>
    ///   <para>Pred edges for BBJ_EHFINALLYRET are set later by impFixPredLists, after setting up the callfinally blocks.</para>
    /// </remarks>
    public void fgLinkBasicBlocks()
    {
        // Create the basic block lookup tables
        fgInitBBLookup();

#if DEBUG
        // Verify blocks are in increasing bbNum order and all pred list info is in initial state.
        fgDebugCheckBBNumIncreasing();

        foreach (var block in Blocks)
        {
            assert(block.bbPreds is null);
            assert(block.bbLastPred is null);
            assert(block.bbRefs is 0);
        }
#endif

        // First block is always reachable
        assert(fgFirstBB is not null);
        fgFirstBB.bbRefs = 1;

        foreach (var curBBdesc in Blocks)
        {
            switch (curBBdesc.Kind)
            {
                case BBJ_COND:
                {
                    var trueTarget = fgLookupBB(curBBdesc.TargetOffs);
                    var falseTarget = curBBdesc.Next;

                    assert(trueTarget is not null);
                    assert(falseTarget is not null);

                    var trueEdge = fgAddRefPred(trueTarget, curBBdesc, initializingPreds: true);
                    var falseEdge = fgAddRefPred(falseTarget, curBBdesc, initializingPreds: true);

                    curBBdesc.TrueEdge = trueEdge;
                    curBBdesc.FalseEdge = falseEdge;

                    // Avoid making BBJ_THROW successors look likely, if possible.
                    //
                    if (trueEdge == falseEdge)
                    {
                        assert(trueEdge.DupCount is 2);
                        trueEdge.Likelihood = 1.0;
                    }
                    else if (trueTarget.Kind is BBJ_THROW)
                    {
                        if (falseTarget.Kind is not BBJ_THROW)
                        {
                            trueEdge.Likelihood = 0.0;
                            falseEdge.Likelihood = 1.0;
                        }
                        else
                        {
                            trueEdge.Likelihood = 0.5;
                            falseEdge.Likelihood = 0.5;
                        }
                    }
                    else if (falseTarget.Kind is BBJ_THROW)
                    {
                        trueEdge.Likelihood = 1.0;
                        falseEdge.Likelihood = 0.0;
                    }
                    else
                    {
                        trueEdge.Likelihood = 0.5;
                        falseEdge.Likelihood = 0.5;
                    }

                    if (trueTarget.bbNum <= curBBdesc.bbNum)
                    {
                        fgMarkBackwardJump(trueTarget, curBBdesc);
                    }

                    if (curBBdesc.IsLast)
                    {
                        BADCODE("Fall thru the end of a method");
                    }
                    break;
                }

                case BBJ_ALWAYS:
                case BBJ_LEAVE:
                {
                    // Avoid fgLookupBB overhead for blocks that jump to next block
                    // (curBBdesc cannot be the last block if it jumps to the next block)

                    var jumpsToNext = (curBBdesc.TargetOffs == curBBdesc.bbCodeOffsEnd);
                    assert(!curBBdesc.IsLast || !jumpsToNext);

                    var jumpDest = jumpsToNext ? curBBdesc.Next : fgLookupBB(curBBdesc.TargetOffs);
                    assert(jumpDest is not null);

                    // Redundantly use SetKindAndTargetEdge() instead of SetTargetEdge() just this once,
                    // so we don't break the HasInitializedTarget() invariant of SetTargetEdge().

                    var newEdge = fgAddRefPred(jumpDest, curBBdesc, initializingPreds: true);
                    curBBdesc.SetKindAndTargetEdge(curBBdesc.Kind, newEdge);

                    if (curBBdesc.Target.bbNum <= curBBdesc.bbNum)
                    {
                        fgMarkBackwardJump(curBBdesc.Target, curBBdesc);
                    }
                    break;
                }

                case BBJ_EHFILTERRET:
                {
                    // We can't set up the pred list for these just yet.
                    // We do it in fgFindBasicBlocks.
                    break;
                }

                case BBJ_EHFINALLYRET:
                {
                    // We can't set up the pred list for these just yet.
                    // We do it in impFixPredLists.
                    break;
                }

                case BBJ_EHFAULTRET:
                case BBJ_THROW:
                case BBJ_RETURN:
                {
                    break;
                }

                case BBJ_SWITCH:
                {
                    var switchTargets = curBBdesc.SwitchTargets;
                    var cases = switchTargets.Cases;
                    var caseOffsets = switchTargets.CaseOffsets;
                    var succs = switchTargets.Succs;
                    var numUnique = 0;

                    for (var i = 0; i < caseOffsets.Length; i++)
                    {
                        var jumpDest = fgLookupBB(caseOffsets[i]);
                        assert(jumpDest is not null);

                        var newEdge = fgAddRefPred(jumpDest, curBBdesc, initializingPreds: true);
                        assert(newEdge is not null);

                        newEdge.Likelihood = (1.0 / caseOffsets.Length) * newEdge.DupCount;
                        cases[i] = newEdge;

                        if (newEdge.DupCount is 1)
                        {
                            succs[numUnique++] = newEdge;
                            if (jumpDest.bbNum <= curBBdesc.bbNum)
                            {
                                fgMarkBackwardJump(jumpDest, curBBdesc);
                            }
                        }
                    }
                    switchTargets.SetSuccCount(numUnique);

                    // Default case of CEE_SWITCH (next block), is at end of cases[]
                    noway_assert(curBBdesc.Next == cases[^1].DestinationBlock);

                    break;
                }

                case BBJ_CALLFINALLY:
                case BBJ_EHCATCHRET:
                {
                    // BBJ_CALLFINALLY and BBJ_EHCATCHRET don't appear until later
                    goto default;
                }

                default:
                {
                    noway_assert(false, "Unexpected bbKind");
                    break;
                }
            }
        }

        if (opts.IsOSR)
        {
            // If this is an OSR compile, note the original entry and the OSR entry block.
            // We don't yet alter flow; see fgFixEntryFlowForOSR.

            assert(info.compILEntry >= 0);

            fgEntryBB = fgLookupBB(0);
            fgOSREntryBB = fgLookupBB(info.compILEntry);
        }

        // Pred lists now established.
        fgPredsComputed = true;
    }

    /// <summary>Find a basic block given its IL offset.</summary>
    /// <param name="addr">the IL offset to look up</param>
    /// <returns>The basic block corresponding to the given IL offset.</returns>
    public BasicBlock? fgLookupBB(int addr)
    {
        // Do a binary search

        var lo = 0;
        var hi = fgBBcount - 1;

        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var dsc = fgBBs[mid];

            // We introduce internal blocks for BBJ_CALLFINALLY. Skip over these.

            while (dsc.HasFlag(BBF_INTERNAL))
            {
                dsc = dsc.Next;
                assert(dsc is not null);
                mid++;

                // We skipped over too many, Set hi back to the original mid - 1

                if (mid > hi)
                {
                    mid = (lo + hi) / 2;
                    hi = mid - 1;
                    continue;
                }
            }

            var pos = dsc.bbCodeOffs;

            if (pos < addr)
            {
                if ((lo == hi) && (lo == (fgBBcount - 1)))
                {
                    noway_assert(addr == dsc.bbCodeOffsEnd);
                    return null; // NULL means the end of method
                }

                lo = mid + 1;
                continue;
            }

            if (pos > addr)
            {
                hi = mid - 1;
                continue;
            }

            return dsc;
        }
#if DEBUG
        jitprintf($"ERROR: Couldn't find basic block at offset {addr:X4}\n");
#endif

        NO_WAY("fgLookupBB failed.");
        return null;
    }

    /// <summary>walk the IL creating basic blocks, and look for operations that might get optimized if this method were to be inlined.</summary>
    /// <param name="codeAddr">starting address of the method's IL stream</param>
    /// <param name="codeSize">length of the IL stream</param>
    /// <param name="jumpTarget">bit vector of jump targets found by fgFindJumpTargets</param>
    /// <remarks>
    ///   <para>Invoked for prejitted and jitted methods, and for all inlinees.</para>
    ///   <para>Sets fgReturnCount and fgThrowCount</para>
    /// </remarks>
    public unsafe void fgMakeBasicBlocks(byte* codeAddr, IL_OFFSET codeSize, BitArray jumpTarget)
    {
        var codeBegp = codeAddr;
        var codeEndp = codeAddr + codeSize;

        // Keep track of where we are in the scope lists, as we will also create blocks at scope boundaries.
        if (opts.compDbgCode && (info.compVarScopesCount > 0))
        {
            compResetScopeLists();

            // Ignore scopes beginning at offset 0
            while (!Unsafe.IsNullRef(in compGetNextEnterScope(offs: 0)))
            {
                // do nothing
            }

            while (!Unsafe.IsNullRef(in compGetNextExitScope(offs: 0)))
            {
                // do nothing
            }
        }

        var retBlocks = 0;
        var throwBlocks = 0;
        var tailCall = false;
        var curBBoffs = 0;

        BasicBlock curBBdesc;

        while (codeAddr < codeEndp)
        {
            var jmpAddr = BAD_IL_OFFSET;
            var bbFlags = BBF_EMPTY;
            var swtDsc = null as BBswtDesc;
            var jmpKind = BBJ_COUNT;

            var opcode = (OPCODE)(codeAddr[0]);
            codeAddr += sizeof(byte);

            if (opcode == CEE_PREFIX1)
            {
                if (jumpTarget[(int)(codeAddr - codeBegp)])
                {
                    BADCODE($"jump target between prefix 0xFE and opcode at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                }

                opcode = (OPCODE)(0x0100 + codeAddr[0]);
                codeAddr += sizeof(byte);
            }

            noway_assert(opcode < CEE_COUNT);

            var sz = opcode.Size;
            var actualSz = sz;
            int jmpDist;

            if (opcode is >= CEE_BR_S and <= CEE_BLT_UN)
            {
                jmpDist = (sz is 1) ? unchecked((sbyte)(codeAddr[0])) : BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                jmpAddr = (IL_OFFSET)(codeAddr - codeBegp) + (sz + jmpDist);

                if (opcode is CEE_BR_S or CEE_BR)
                {
                    jmpKind = BBJ_ALWAYS;

                    if ((jmpDist is 0) && opts.DoEarlyBlockMerging)
                    {
                        // NOP
                        continue;
                    }
                }
                else
                {
                    jmpKind = BBJ_COND;
                }
            }
            else
            {
                switch (opcode)
                {
                    case CEE_LEAVE:
                    case CEE_LEAVE_S:
                    {
                        jmpDist = (sz is 1) ? unchecked((sbyte)(codeAddr[0])) : BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                        jmpAddr = (IL_OFFSET)(codeAddr - codeBegp) + (sz + jmpDist);

                        // We need to check if we are jumping out of a finally-protected try.
                        jmpKind = BBJ_LEAVE;
                        break;
                    }

                    case CEE_SWITCH:
                    {
                        // Read the number of entries in the table, excluding default
                        var jmpCnt = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                        codeAddr += sizeof(int);

                        // Compute  the base offset for the opcode
                        var jmpBase = (IL_OFFSET)((codeAddr - codeBegp) + (jmpCnt * sizeof(int)));

                        // Allocate the jump table, ensuring there's space for all cases, the default case, and unique succs
                        var jmpTab = new FlowEdge[jmpCnt + 1];
                        var cases = new int[jmpCnt + 1];

                        // Fill in the jump table
                        for (var i = 0; i < jmpCnt; i++)
                        {
                            jmpDist = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                            codeAddr += sizeof(int);

                            // we change these in fgLinkBasicBlocks().
                            cases[i] = jmpBase + jmpDist;
                        }

                        // Append the default label to the target table

                        cases[jmpCnt] = jmpBase;

                        // Compute the size of the switch opcode operands
                        actualSz = (byte)(sizeof(int) + (jmpCnt * sizeof(int)));

                        // Allocate the switch descriptor; we will initialize the unique successors in fgLinkBasicBlocks
                        swtDsc = new BBswtDesc(succs: [], cases, hasDefault: true);

                        // This is definitely a jump
                        jmpKind = BBJ_SWITCH;
                        fgHasSwitch = true;

                        if (opts.compProcedureSplitting)
                        {
                            // TODO-CQ: We might need to create a switch table; we won't know for sure until much later.
                            // However, switch tables don't work with hot/cold splitting, currently. The switch table data needs
                            // a relocation such that if the base (the first block after the prolog) and target of the switch
                            // branch are put in different sections, the difference stored in the table is updated. However, our
                            // relocation implementation doesn't support three different pointers (relocation address, base, and
                            // target). So, we need to change our switch table implementation to be more like
                            // JIT64: put the table in the code section, in the same hot/cold section as the switch jump itself
                            // (maybe immediately after the switch jump), and make the "base" address be also in that section,
                            // probably the address after the switch jump.
                            opts.compProcedureSplitting = false;
                            JITDUMP("Turning off procedure splitting for this method, as it might need switch tables; implementation limitation.\n");
                        }
                        break;
                    }

                    case CEE_ENDFILTER:
                    {
                        bbFlags |= BBF_DONT_REMOVE;
                        jmpKind = BBJ_EHFILTERRET;
                        break;
                    }

                    case CEE_ENDFINALLY:
                    {
                        // Start with BBJ_EHFINALLYRET; change to BBJ_EHFAULTRET later if it's in a 'fault' clause.
                        jmpKind = BBJ_EHFINALLYRET;
                        break;
                    }

                    case CEE_TAILCALL:
                    {
                        if (compIsForInlining)
                        {
                            // TODO-CQ: We can inline some callees with explicit tail calls if we can guarantee that the calls
                            // can be dispatched as tail calls from the caller.
                            compInlineResult.NoteFatal(InlineObservation.CALLEE_EXPLICIT_TAIL_PREFIX);
                            retBlocks++;
                            fgReturnCount = retBlocks;
                            return;
                        }

                        tailCall = true;
                        goto case CEE_UNALIGNED;
                    }

                    case CEE_UNALIGNED:
                    case CEE_VOLATILE:
                    case CEE_CONSTRAINED:
                    case CEE_READONLY:
                    {
                        // fgFindJumpTargets should have ruled out this possibility
                        //   (i.e. a prefix opcodes as last instruction in a block)
                        noway_assert(codeAddr < codeEndp);

                        if (jumpTarget[(int)(codeAddr - codeBegp)])
                        {
                            BADCODE($"jump target between prefix and an opcode at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                        }
                        break;
                    }

                    case CEE_CALL:
                    case CEE_CALLI:
                    case CEE_CALLVIRT:
                    {
                        opts.callInstrCount++;

                        if (compIsForInlining || (!tailCall && !compTailCallStress))
                        {
                            // Ignore tail call in the inlinee. Period.
                            // A new BB with BBJ_RETURN would have been created after a tailcall statement.
                            //
                            // We need to keep this invariant if we want to stress the tailcall.
                            // That way, the potential (tail)call statement is always the last statement in the block.
                            // Otherwise, we will assert at the following line in fgMorphCall()
                            //     noway_assert(fgMorphStmt.NextStmt is null);

                            // Neither .tailcall prefix, no tailcall stress. So move on.
                            break;
                        }

                        // Make sure the code sequence is legal for the tail call.
                        // If so, mark this BB as having a BBJ_RETURN.

                        if (codeAddr >= codeEndp - sz)
                        {
                            BADCODE($"No code found after the call instruction at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                        }

                        if (tailCall)
                        {
                            // impIsTailCallILPattern uses isRecursive flag to determine whether ret in a fallthrough block is
                            // allowed. We don't know at this point whether the call is recursive so we conservatively pass
                            // false. This will only affect explicit tail calls when IL verification is not needed for the
                            // method.
                            var isRecursive = false;

                            if (!impIsTailCallILPattern(tailCall, opcode, codeAddr + sz, codeEndp, isRecursive))
                            {
                                BADCODE($"tail call not followed by ret at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                            }

                            if (fgMayExplicitTailCall())
                            {
                                compTailPrefixSeen = true;
                            }
                        }
                        else
                        {
                            var nextOpcode = (OPCODE)(codeAddr[sz]);

                            if (nextOpcode != CEE_RET)
                            {
                                noway_assert(compTailCallStress);
                                // Next OPCODE is not a CEE_RET, bail the attempt to stress the tailcall.
                                // (I.e. We will not make a new BB after the "call" statement.)
                                break;
                            }
                        }

                        // For tail call, we just call CORINFO_HELP_TAILCALL, and it jumps to the
                        // target. So we don't need an epilog - just like CORINFO_HELP_THROW.
                        // Make the block BBJ_RETURN, but we will change it to BBJ_THROW
                        // if the tailness of the call is satisfied.
                        // NOTE : The next instruction is guaranteed to be a CEE_RET
                        // and it will create another BasicBlock. But there may be an
                        // jump directly to that CEE_RET. If we want to avoid creating
                        // an unnecessary block, we need to check if the CEE_RETURN is
                        // the target of a jump.

                        goto case CEE_JMP;
                    }

                    case CEE_JMP:
                    case CEE_RET:
                    {
                        // The opcodes other than CEE_RET are equivalent to a return from the current method
                        // But instead of directly returning to the caller we jump and execute something else in between

                        retBlocks++;
                        jmpKind = BBJ_RETURN;
                        break;
                    }

                    case CEE_THROW:
                    case CEE_RETHROW:
                    {
                        throwBlocks++;
                        jmpKind = BBJ_THROW;
                        break;
                    }

#if DEBUG
                    // These ctrl-flow opcodes don't need any special handling
                    case CEE_NEWOBJ:
                    {
                        // CTRL_CALL
                        opts.callInstrCount++;
                        break;
                    }

                    default:
                    {
                        if (opcode.FlowKind is FLOW_BREAK or FLOW_NEXT)
                        {
                            break;
                        }

                        // what's left are forgotten instructions
                        BADCODE("Unrecognized control Opcode");
                        break;
                    }
#else
                    default:
                    {
                        break;
                    }
#endif
                }
            }

            // Jump over the operand
            codeAddr += sz;

            // Make sure a jump target isn't in the middle of our opcode
            if (actualSz is not 0)
            {
                // offset of the operand
                var offs = (IL_OFFSET)(codeAddr - codeBegp) - actualSz;

                for (var i = 0; i < actualSz; i++, offs++)
                {
                    if (jumpTarget[offs])
                    {
                        BADCODE($"jump into the middle of an opcode at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                    }
                }
            }

            // Compute the offset of the next opcode
            var nxtBBoffs = (IL_OFFSET)(codeAddr - codeBegp);

            var foundScope = false;

            if (opts.compDbgCode && (info.compVarScopesCount > 0))
            {
                while (!Unsafe.IsNullRef(in compGetNextEnterScope(nxtBBoffs)))
                {
                    foundScope = true;
                }

                while (!Unsafe.IsNullRef(in compGetNextExitScope(nxtBBoffs)))
                {
                    foundScope = true;
                }
            }

            // Do we have a jump?
            if (jmpKind == BBJ_COUNT)
            {
                // No jump; make sure we don't fall off the end of the function

                if (codeAddr == codeEndp)
                {
                    BADCODE($"missing return opcode at offset {(IL_OFFSET)(codeAddr - codeBegp):X4}");
                }

                // If a label follows this opcode, we'll have to make a new BB

                var makeBlock = jumpTarget[nxtBBoffs];

                if (!makeBlock && foundScope)
                {
                    makeBlock = true;
#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"Splitting at BBoffs = {nxtBBoffs:X4}\n");
                    }
#endif
                }

                if (!makeBlock)
                {
                    continue;
                }

                // Jump to the next block
                jmpKind = BBJ_ALWAYS;
                jmpAddr = nxtBBoffs;
            }

            assert(jmpKind != BBJ_COUNT);

            // We need to create a new basic block

            switch (jmpKind)
            {
                case BBJ_SWITCH:
                {
                    assert(swtDsc is not null);
                    curBBdesc = BasicBlock.New(this, swtDsc);
                    break;
                }

                case BBJ_COND:
                case BBJ_ALWAYS:
                case BBJ_LEAVE:
                {
                    noway_assert(jmpAddr != BAD_IL_OFFSET);
                    curBBdesc = BasicBlock.New(this, jmpKind, jmpAddr);
                    break;
                }

                default:
                {
                    curBBdesc = BasicBlock.New(this, jmpKind);
                    break;
                }
            }

            curBBdesc.SetFlags(bbFlags);
            curBBdesc.bbRefs = 0;

            curBBdesc.bbCodeOffs = curBBoffs;
            curBBdesc.bbCodeOffsEnd = nxtBBoffs;

            // Append the block to the end of the global basic block list

            if (fgFirstBB is not null)
            {
                assert(fgLastBB is not null);
                fgLastBB.Next = curBBdesc;
            }
            else
            {
                fgFirstBB = curBBdesc;
                assert(fgFirstBB.IsFirst);
            }
            fgLastBB = curBBdesc;

#if DEBUG
            if (verbose)
            {
                curBBdesc.dspBlockHeader(showKind: false, showFlags: false, showPreds: false);
            }
#endif

            // Remember where the next BB will start
            curBBoffs = nxtBBoffs;
        }
        noway_assert(codeAddr == codeEndp);

        // Finally link up the targets of the blocks together
        fgLinkBasicBlocks();

        fgReturnCount = retBlocks;
        fgThrowCount = throwBlocks;
    }

    /// <summary>mark blocks indicating there is a jump backwards in IL, from a higher to lower IL offset.</summary>
    /// <param name="targetBlock">target of the jump</param>
    /// <param name="sourceBlock">source of the jump</param>
    public void fgMarkBackwardJump(BasicBlock targetBlock, BasicBlock sourceBlock)
    {
        noway_assert(targetBlock.bbNum <= sourceBlock.bbNum);

        foreach (var block in new BasicBlockRangeList(targetBlock, sourceBlock))
        {
            if (!block.HasFlag(BBF_BACKWARD_JUMP) && (block.Kind is not BBJ_RETURN))
            {
                block.SetFlags(BBF_BACKWARD_JUMP);
                compHasBackwardJump = true;
            }
        }

        sourceBlock.SetFlags(BBF_BACKWARD_JUMP_SOURCE);
        targetBlock.SetFlags(BBF_BACKWARD_JUMP_TARGET);
    }

    /// <summary>Estimates conservatively for an explicit tail call, if the importer may actually use a tail call.</summary>
    /// <returns>true if a tail call *may* be generated; otherwise, false</returns>
    /// <remarks>
    ///   <para>compInitOptions() has been called</para>
    ///   <para>info.compIsVarArgs has been initialized</para>
    ///   <para>An explicit tail call has been seen</para>
    ///   <para>compSetOptimizationLevel() has not been called</para>
    /// </remarks>
    public bool fgMayExplicitTailCall()
    {
        assert(!compIsForInlining);

        if ((info.compFlags & CORINFO_FLG_SYNCH) != 0)
        {
            // Caller is synchronized
            return false;
        }

        if (opts.IsReversePInvoke)
        {
            // Reverse P/Invoke
            return false;
        }

#if !FEATURE_FIXED_OUT_ARGS
        if (info.compIsVarArgs)
        {
            // Caller is varargs
            return false;
        }
#endif // FEATURE_FIXED_OUT_ARGS

        return true;
    }

    public void fgNormalizeEH()
    {
        // Enforce the following invariants:
        //
        //   1. No block is both the first block of a handler and the first block of a try. In IL (and on entry
        //      to this function), this can happen if the "try" is more nested than the handler.
        //
        //      For example, consider:
        //
        //               try1 ----------------- BB01
        //               |                      BB02
        //               |--------------------- BB03
        //               handler1
        //               |----- try2 ---------- BB04
        //               |      |               BB05
        //               |      handler2 ------ BB06
        //               |      |               BB07
        //               |      --------------- BB08
        //               |--------------------- BB09
        //
        //      Thus, the start of handler1 and the start of try2 are the same block. We will transform this to:
        //
        //               try1 ----------------- BB01
        //               |                      BB02
        //               |--------------------- BB03
        //               handler1 ------------- BB10 // empty block
        //               |      try2 ---------- BB04
        //               |      |               BB05
        //               |      handler2 ------ BB06
        //               |      |               BB07
        //               |      --------------- BB08
        //               |--------------------- BB09
        //
        //   2. No block is the first block of more than one try or handler region.
        //      (Note that filters cannot have EH constructs nested within them, so there can be no nested try or
        //      handler that shares the filter begin or last block. For try/filter/filter-handler constructs nested
        //      within a try or handler region, note that the filter block cannot be the first block of the try,
        //      nor can it be the first block of the handler, since you can't "fall into" a filter, which that situation
        //      would require.)
        //
        //      For example, we will transform this:
        //
        //               try3   try2   try1
        //               |---   |---   |---   BB01
        //               |      |      |      BB02
        //               |      |      |---   BB03
        //               |      |             BB04
        //               |      |------------ BB05
        //               |                    BB06
        //               |------------------- BB07
        //
        //      to this:
        //
        //               try3 -------------   BB08  // empty BBJ_ALWAYS block
        //               |      try2 ------   BB09  // empty BBJ_ALWAYS block
        //               |      |      try1
        //               |      |      |---   BB01
        //               |      |      |      BB02
        //               |      |      |---   BB03
        //               |      |             BB04
        //               |      |------------ BB05
        //               |                    BB06
        //               |------------------- BB07
        //
        //      The benefit of this is that adding a block to an EH region will not require examining every EH region,
        //      looking for possible shared "first" blocks to adjust. It also makes it easier to put code at the top
        //      of a particular EH region.
        //
        //      These empty blocks (BB08, BB09) will generate no code (unless some code is subsequently placed into them),
        //      and will have the same native code offset as BB01 after code is generated. There may be labels generated
        //      for them, if they are branch targets, so it is possible to have multiple labels targeting the same native
        //      code offset. The blocks will not be merged with the blocks they are split from, because they will have a
        //      different EH region, and we don't merge blocks from two different EH regions.
        //
        //      In the example, if there are branches to BB01, we need to distribute them to BB01, BB08, or BB09, appropriately.
        //      1. A branch from BB01/BB02/BB03 to BB01 will still go to BB01. Branching to BB09 or BB08 would not be legal,
        //         since it would branch out of a try region.
        //      2. A branch from BB04/BB05 to BB01 will instead branch to BB09. Branching to BB08 would not be legal. Note
        //         that branching to BB01 would still be legal, so we have a choice. It makes the most sense to branch to BB09,
        //         so the source and target of a branch are in the same EH region.
        //      3. Similarly, a branch from BB06/BB07 to BB01 will go to BB08, even though branching to BB09 would be legal.
        //      4. A branch from outside this loop (at the top-level) to BB01 will go to BB08. This is one case where the
        //         source and target of the branch are not in the same EH region.
        //
        //      The EH nesting rules for IL branches are described in the ECMA spec section 12.4.2.8.2.7 "Branches" and
        //      section 12.4.2.8.2.9 "Examples".
        //
        //      There is one exception to this normalization rule: we do not change "mutually protect" regions. These are cases
        //      where two EH table entries have exactly the same 'try' region, used to implement C# "try / catch / catch".
        //      The first handler appears by our nesting to be an "inner" handler, with ebdEnclosingTryIndex pointing to the
        //      second one. It is not true nesting, though, since they both protect the same "try". Both the these EH table
        //      entries must keep the same "try" region begin/last block pointers. A block in this "try" region has a try index
        //      of the first ("most nested") EH table entry.
        //
        //   3. No block is the last block of more than one try or handler region. Again, as described above,
        //      filters need not be considered.
        //
        //      For example, we will transform this:
        //
        //               try3 ----------------- BB01
        //               |      try2 ---------- BB02
        //               |      |      handler1 BB03
        //               |      |      |        BB04
        //               |----- |----- |------- BB05
        //
        //      (where all three try regions end at BB05) to this:
        //
        //               try3 ----------------- BB01
        //               |      try2 ---------- BB02
        //               |      |      handler1 BB03
        //               |      |      |        BB04
        //               |      |      |------- BB05
        //               |      |-------------- BB06 // empty BBJ_ALWAYS block
        //               |--------------------- BB07 // empty BBJ_ALWAYS block
        //
        //      No branches need to change: if something branched to BB05, it will still branch to BB05. If BB05 is a
        //      BBJ_ALWAYS block to the next block, then control flow will fall through the newly added blocks as well.
        //      If it is anything else, it will retain that block branch type and BB06 and BB07 will be unreachable.
        //
        //      The benefit of this is, once again, to remove the need to consider every EH region when adding new blocks.
        //
        // Overall, a block can appear in the EH table exactly once: as the begin or last block of a single try, filter, or
        // handler. There is one exception: for a single-block EH region, the block can appear as both the "begin" and "last"
        // block of the try, or the "begin" and "last" block of the handler (note that filters don't have a "last" block stored,
        // so this case doesn't apply.)
        // (Note: we could remove this special case if we wanted, and if it helps anything, but it doesn't appear that it will
        // help.)
        //
        // These invariants simplify a number of things. When inserting a new block into a region, it is not necessary to
        // traverse the entire EH table looking to see if any EH region needs to be updated. You only ever need to update a
        // single region (except for mutually-protect "try" regions).
        //
        // Also, for example, when we're trying to determine the successors of a block B1 that leads into a try T1, if a block
        // B2 violates invariant #3 by being the first block of both the handler of T1, and an enclosed try T2, inserting a
        // block to enforce this invariant prevents us from having to consider the first block of T2's handler as a possible
        // successor of B1. This is somewhat akin to breaking of "critical edges" in a flowgraph.

        if (compHndBBtabCount == 0)
        {
            // No EH? Nothing to do.
#if DEBUG
            fgNormalizeEHDone = true;
#endif
            return;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf("*************** In fgNormalizeEH()\n");
            fgDispBasicBlocks();
            fgDispHandlerTab();
        }
#endif

        var modified = false;

        // Case #1: Prevent the first block of a handler from also being the first block of a 'try'.
        modified |= fgNormalizeEHCase1();

        // Case #2: Prevent any two EH regions from starting with the same block (after case #3, we only need to worry about 'try' blocks).
        modified |= fgNormalizeEHCase2();

        _ = modified;

#if false
        // Case 3 normalization is disabled. The JIT really doesn't like having extra empty blocks around, especially
        // blocks that are unreachable. There are lots of asserts when such things occur. We will re-evaluate whether we
        // can do this normalization.
        // Note: there are cases in fgVerifyHandlerTab() that are also disabled to match this.

        // Case #3: Prevent any two EH regions from ending with the same block.
        if (fgNormalizeEHCase3())
        {
            modified = true;
        }
#endif

#if DEBUG
        fgNormalizeEHDone = true;

        if (modified)
        {
            JITDUMP("Added at least one basic block in fgNormalizeEH.\n");

            if (verbose)
            {
                fgDispBasicBlocks();
                fgDispHandlerTab();
            }

            fgVerifyHandlerTab();
        }
        else
        {
            JITDUMP("No EH normalization performed.\n");
        }
#endif
    }

    public bool fgNormalizeEHCase1()
    {
        var modified = false;

        // Case #1: Is the first block of a handler also the first block of any try?
        //
        // Do this as a separate loop from case #2 to simplify the logic for cases where we have both multiple identical
        // 'try' begin blocks as well as this case, e.g.:
        //     try {
        //     } finally { try { try {
        //         } catch {}
        //         } catch {}
        //     }
        // where the finally/try/try are all the same block.
        //
        // We also do this before case #2, so when we get to case #2, we only need to worry about updating 'try' begin
        // blocks (and only those within the 'try' region's parents), not handler begin blocks, when we are inserting new
        // header blocks.

        for (var XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var eh = ref ehGetDsc(XTnum);

            var handlerStart = eh.ebdHndBeg;
            ref var handlerStartContainingTry = ref ehGetBlockTryDsc(handlerStart);

            // If the handler start block is in a try, and is in fact the first block of that try...
            if (!Unsafe.IsNullRef(in handlerStartContainingTry) && (handlerStartContainingTry.ebdTryBeg == handlerStart))
            {
                // ...then we want to insert an empty, non-removable block outside the try to be the new first block of the
                // handler.
                var newHndStart = BasicBlock.New(this);
                fgInsertBBbefore(handlerStart, newHndStart);

                var newEdge = fgAddRefPred(handlerStart, newHndStart);
                newHndStart.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);

                // Handler begins have an extra implicit ref count.
                // BasicBlock::New has already handled this for newHndStart.
                // Remove handlerStart's implicit ref count.
                //
                assert(newHndStart.bbRefs == 1);
                assert(handlerStart.bbRefs >= 2);
                handlerStart.bbRefs--;

#if DEBUG
                if (verbose)
                {
                    jitprintf($"Handler begin for EH#{XTnum:D2} and 'try' begin for EH{ehGetIndex(handlerStartContainingTry):D2} are the same block; inserted new {FMT_BB(newHndStart.bbNum)} before {FMT_BB(eh.ebdHndBeg.bbNum)} as new handler begin for EH#{XTnum}.\n");
                }
#endif

                // The new block is the new handler begin.
                eh.ebdHndBeg = newHndStart;

                // Try index is the same as the enclosing try, if any, of eh:
                if (eh.ebdEnclosingTryIndex is EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    newHndStart.clearTryIndex();
                }
                else
                {
                    newHndStart.TryIndex = eh.ebdEnclosingTryIndex;
                }

                newHndStart.HndIndex = XTnum;
                newHndStart.CatchType = handlerStart.CatchType;
                handlerStart.CatchType = BBCT_NONE; // Now handlerStart is no longer the start of a handler...
                newHndStart.bbCodeOffs = handlerStart.bbCodeOffs;
                newHndStart.bbCodeOffsEnd = newHndStart.bbCodeOffs; // code size = 0. TODO: use BAD_IL_OFFSET instead?
                newHndStart.inheritWeight(handlerStart);
                newHndStart.SetFlags(BBF_DONT_REMOVE | BBF_INTERNAL);
                modified = true;

#if DEBUG
                if (false && verbose)
                {
                    // Normally this is way too verbose, but it is useful for debugging
                    jitprintf("*************** fgNormalizeEH() made a change\n");
                    fgDispBasicBlocks();
                    fgDispHandlerTab();
                }
#endif
            }
        }
        return modified;
    }

    public bool fgNormalizeEHCase2()
    {
        var modified = false;

        // Case #2: Make sure no two 'try' have the same begin block (except for mutually-protect regions).
        // Note that this can only happen for nested 'try' regions, so we only need to look through the
        // 'try' nesting hierarchy.

        var interestingPreds = new Stack<BasicBlock>();

        for (var XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var eh = ref ehGetDsc(XTnum);

            if (eh.ebdEnclosingTryIndex is not EHblkDsc.NO_ENCLOSING_INDEX)
            {
                var tryStart = eh.ebdTryBeg;
                var insertBeforeBlk = tryStart; // If we need to insert new blocks, we insert before this block.

                // We need to keep track of the last "mutually protect" region so we can properly not add additional header
                // blocks to the second and subsequent mutually protect try blocks. We can't just keep track of the EH
                // region pointer, because we're updating the 'try' begin blocks as we go. So, we need to keep track of the
                // pre-update 'try' begin/last blocks themselves.
                var mutualTryBeg = eh.ebdTryBeg;
                var mutualTryLast = eh.ebdTryLast;
                var mutualProtectIndex = XTnum;

                ref var ehOuter = ref eh;
                do
                {
                    var ehOuterTryIndex = ehOuter.ebdEnclosingTryIndex;
                    ehOuter = ref ehGetDsc(ehOuterTryIndex);
                    var outerTryStart = ehOuter.ebdTryBeg;

                    if (outerTryStart == tryStart)
                    {
                        // We found two EH regions with the same 'try' begin! Should we do something about it?

                        if (ehOuter.ebdIsSameTry(mutualTryBeg, mutualTryLast))
                        {
                            // clang-format off
                            // Don't touch mutually-protect regions: their 'try' regions must remain identical!
                            // We want to continue the looping outwards, in case we have something like this:
                            //
                            //               try3   try2   try1
                            //               |---   |----  |----  BB01
                            //               |      |      |      BB02
                            //               |      |----  |----  BB03
                            //               |                    BB04
                            //               |------------------- BB05
                            //
                            // (Thus, try1 & try2 are mutually-protect 'try' regions from BB01 to BB03. They are nested inside try3,
                            // which also starts at BB01. The 'catch' clauses have been elided.)
                            // In this case, we'll decline to add a new header block for try2, but we will add a new one for try3, ending with:
                            //
                            //               try3   try2   try1
                            //               |------------------- BB06
                            //               |      |----  |----  BB01
                            //               |      |      |      BB02
                            //               |      |----  |----  BB03
                            //               |                    BB04
                            //               |------------------- BB05
                            //
                            // More complicated (yes, this is real):
                            //
                            // try {
                            //     try {
                            //         try {
                            //             try {
                            //                 try {
                            //                     try {
                            //                         try {
                            //                             try {
                            //                             }
                            //                             catch {} // mutually-protect set #1
                            //                             catch {}
                            //                         } finally {}
                            //                     }
                            //                     catch {} // mutually-protect set #2
                            //                     catch {}
                            //                     catch {}
                            //                 } finally {}
                            //             } catch {}
                            //         } finally {}
                            //     } catch {}
                            //  } finally {}
                            //
                            // In this case, all the 'try' start at the same block! Note that there are two sets of mutually-protect regions,
                            // separated by some nesting.
                            // clang-format on

#if DEBUG
                            if (verbose)
                            {
                                jitprintf($"Mutually protect regions EH#{mutualProtectIndex} and EH#{ehGetIndex(ehOuter)}; leaving identical 'try' begin blocks.\n");
                            }
#endif

                            // We still need to update the tryBeg, if something more nested already did that.
                            ehOuter.ebdTryBeg = insertBeforeBlk;
                        }
                        else
                        {
                            // We're in a new set of mutual protect regions, so don't compare against the original.
                            mutualTryBeg = ehOuter.ebdTryBeg;
                            mutualTryLast = ehOuter.ebdTryLast;
                            mutualProtectIndex = ehOuterTryIndex;

                            // We've got multiple 'try' blocks starting at the same place!
                            // Add a new first 'try' block for 'ehOuter' that will be outside 'eh'.

                            var newTryStart = BasicBlock.New(this);
                            newTryStart.bbRefs = 0;

                            fgInsertBBbefore(insertBeforeBlk, newTryStart);

                            var newEdge = fgAddRefPred(insertBeforeBlk, newTryStart);
                            newTryStart.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);

                            // It's possible for a try to start at the beginning of a method. If so, we need
                            // to adjust the implicit ref counts as we've just created a new first bb
                            //
                            if (newTryStart == fgFirstBB)
                            {
                                assert(insertBeforeBlk.bbRefs >= 2);
                                insertBeforeBlk.bbRefs--;
                                newTryStart.bbRefs++;
                            }

                            // Same for OSR's protected entry BB.
                            if (insertBeforeBlk == fgEntryBB)
                            {
                                fgEntryBB = newTryStart;
                            }

                            JITDUMP($"'try' begin for EH#{ehOuterTryIndex} and EH#{XTnum} are same block; inserted new {FMT_BB(newTryStart.bbNum)} before {FMT_BB(insertBeforeBlk.bbNum)} as new 'try' begin for EH#{ehOuterTryIndex}.\n");

                            // The new block is the new 'try' begin.
                            ehOuter.ebdTryBeg = newTryStart;

                            newTryStart.copyEHRegion(tryStart);       // Copy the EH region info
                            newTryStart.TryIndex = ehOuterTryIndex; // ... but overwrite the 'try' index
                            newTryStart.CatchType = BBCT_NONE;
                            newTryStart.bbCodeOffs = tryStart.bbCodeOffs;
                            newTryStart.bbCodeOffsEnd = newTryStart.bbCodeOffs; // code size = 0. TODO: use BAD_IL_OFFSET instead?
                            newTryStart.inheritWeight(tryStart);

                            // Note that we don't need to clear any flags on the old try start, since it is still a 'try'
                            // start.
                            newTryStart.SetFlags(BBF_DONT_REMOVE | BBF_INTERNAL);
                            newTryStart.CopyFlags(insertBeforeBlk, BBF_BACKWARD_JUMP_TARGET);

                            // Now we need to split any flow edges targeting the old try begin block between the old
                            // and new block. Note that if we are handling a multiply-nested 'try', we may have already
                            // split the inner set. So we need to split again, from the most enclosing block that we've
                            // already created, namely, insertBeforeBlk.
                            //
                            // For example:
                            //
                            //               try3   try2   try1
                            //               |----  |----  |----  BB01
                            //               |      |      |      BB02
                            //               |      |      |----  BB03
                            //               |      |-----------  BB04
                            //               |------------------  BB05
                            //
                            // We'll loop twice, to create two header blocks, one for try2, and the second time for try3
                            // (in that order).
                            // After the first loop, we have:
                            //
                            //               try3   try2   try1
                            //                      |----         BB06
                            //               |----  |      |----  BB01
                            //               |      |      |      BB02
                            //               |      |      |----  BB03
                            //               |      |-----------  BB04
                            //               |------------------  BB05
                            //
                            // And all the external edges have been changed to point at try2. On the next loop, we'll create
                            // a unique header block for try3, and split the edges between try2 and try3, leaving us with:
                            //
                            //               try3   try2   try1
                            //               |----                BB07
                            //               |      |----         BB06
                            //               |      |      |----  BB01
                            //               |      |      |      BB02
                            //               |      |      |----  BB03
                            //               |      |-----------  BB04
                            //               |------------------  BB05

                            interestingPreds.Clear();
                            foreach (var predBlock in insertBeforeBlk.PredBlocks)
                            {
                                if ((predBlock == newTryStart) || BasicBlock.sameTryRegion(insertBeforeBlk, predBlock))
                                {
                                    continue;
                                }

                                interestingPreds.Push(predBlock);
                            }

                            while (interestingPreds.Count > 0)
                            {
                                var predBlock = interestingPreds.Pop();

                                // Change pred branches.
                                fgReplaceJumpTarget(predBlock, insertBeforeBlk, newTryStart);

                                JITDUMP($"Redirect {FMT_BB(predBlock.bbNum)} target from {FMT_BB(insertBeforeBlk.bbNum)} to {FMT_BB(newTryStart.bbNum)}.\n");
                            }

                            // We don't need to update the tryBeg block of other EH regions here because we are looping
                            // outwards in enclosing try index order, and we'll get to them later.

                            // Move the insert block backwards, to the one we just inserted.
                            insertBeforeBlk = insertBeforeBlk.Prev;
                            assert(insertBeforeBlk == newTryStart);

                            modified = true;

#if DEBUG
                            if (false && verbose)
                            {
                                // Normally this is way too verbose, but it is useful for debugging
                                jitprintf("*************** fgNormalizeEH() made a change\n");
                                fgDispBasicBlocks();
                                fgDispHandlerTab();
                            }
#endif
                        }
                    }
                    else
                    {
                        // If the 'try' start block in the outer block isn't the same, then none of the more-enclosing
                        // try regions (if any) can have the same 'try' start block, so we're done.
                        // Note that we could have a situation like this:
                        //
                        //        try4   try3   try2   try1
                        //        |---   |---   |      |      BB01
                        //        |      |      |      |      BB02
                        //        |      |      |----  |----  BB03
                        //        |      |      |             BB04
                        //        |      |      |------------ BB05
                        //        |      |                    BB06
                        //        |      |------------------- BB07
                        //        |-------------------------- BB08
                        //
                        // (Thus, try1 & try2 start at BB03, and are nested inside try3 & try4, which both start at BB01.)
                        // In this case, we'll process try1 and try2, then break out. Later, we'll get to try3 and process
                        // it and try4.

                        break;
                    }
                }
                while (ehOuter.ebdEnclosingTryIndex is not EHblkDsc.NO_ENCLOSING_INDEX);
            }
        }
        return modified;
    }

    public bool fgNormalizeEHCase3()
    {
        var modified = false;

        // Case #3: Make sure no two 'try' or handler regions have the same 'last' block (except for mutually protect 'try'
        // regions). As above, there has to be EH region nesting for this to occur. However, since we need to consider
        // handlers, there are more cases.
        //
        // There are four cases to consider:
        //      (1) try     nested in try
        //      (2) handler nested in try
        //      (3) try     nested in handler
        //      (4) handler nested in handler
        //
        // Note that, before funclet generation, it would be unusual, though legal IL, for a 'try' to come at the end
        // of an EH region (either 'try' or handler region), since that implies that its corresponding handler precedes it.
        // That will never happen in C#, but is legal in IL.
        //
        // Only one of these cases can happen. For example, if we have case (2), where a try/catch is nested in a 'try' and
        // the nested handler has the same 'last' block as the outer handler, then, due to nesting rules, the nested 'try'
        // must also be within the outer handler, and obviously cannot share the same 'last' block.

        for (var XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var eh = ref ehGetDsc(XTnum);

            // Find the EH region 'eh' is most nested within, either 'try' or handler or none.
            var ehOuterIndex = eh.ebdGetEnclosingRegionIndex(out var outerIsTryRegion);

            if (ehOuterIndex is not EHblkDsc.NO_ENCLOSING_INDEX)
            {
                ref var ehInner = ref eh;    // This gets updated as we loop outwards in the EH nesting
                var ehInnerIndex = XTnum; // This gets updated as we loop outwards in the EH nesting
                bool innerIsTryRegion;

                ref var ehOuter = ref ehGetDsc(ehOuterIndex);

#if DEBUG
                // Debugging: say what type of block we're updating.
                var outerType = "";
                var innerType = "";
#endif

                // 'insertAfterBlk' is the place we will insert new "normalization" blocks. We don't know yet if we will
                // insert them after the innermost 'try' or handler's "last" block, so we set it to nullptr. Once we
                // determine the innermost region that is equivalent, we set this, and then update it incrementally as we
                // loop outwards.

                var insertAfterBlk = null as BasicBlock;
                var foundMatchingLastBlock = false;

                // This is set to 'false' for mutual protect regions for which we will not insert a normalization block.
                var insertNormalizationBlock = true;

                // Keep track of what the 'try' index and handler index should be for any new normalization block that we
                // insert. If we have a sequence of alternating nested 'try' and handlers with the same 'last' block, we'll
                // need to update these as we go. For example:
                //      try { // EH#5
                //          ...
                //          catch { // EH#4
                //              ...
                //              try { // EH#3
                //                  ...
                //                  catch { // EH#2
                //                      ...
                //                      try { // EH#1
                //                          BB01 // try=1, hnd=2
                //      }   }   }   }   } // all the 'last' blocks are the same
                //
                // after normalization:
                //
                //      try { // EH#5
                //          ...
                //          catch { // EH#4
                //              ...
                //              try { // EH#3
                //                  ...
                //                  catch { // EH#2
                //                      ...
                //                      try { // EH#1
                //                          BB01 // try=1, hnd=2
                //                      }
                //                      BB02 // try=3, hnd=2
                //                  }
                //                  BB03 // try=3, hnd=4
                //              }
                //              BB04 // try=5, hnd=4
                //          }
                //          BB05 // try=5, hnd=0 (no enclosing hnd)
                //      }

                int nextTryIndex = EHblkDsc.NO_ENCLOSING_INDEX; // Initialization only needed to quell compiler warnings.
                int nextHndIndex = EHblkDsc.NO_ENCLOSING_INDEX;

                // We compare the outer region against the inner region's 'try' or handler, determined by the
                // 'outerIsTryRegion' variable. Once we decide that, we know exactly the 'last' pointer that we will use to
                // compare against all enclosing EH regions.
                //
                // For example, if we have these nested EH regions (omitting some corresponding try/catch clauses for each
                // nesting level):
                //
                //      try {
                //          ...
                //          catch {
                //              ...
                //              try {
                //      }   }   } // all the 'last' blocks are the same
                //
                // then we determine that the innermost region we are going to compare against is the 'try' region. There's
                // no reason to compare against its handler region for any enclosing region (since it couldn't possibly
                // share a 'last' block with the enclosing region). However, there's no harm, either (and it simplifies
                // the code for the first set of comparisons to be the same as subsequent, more enclosing cases).
                var lastBlockPtrToCompare = null as BasicBlock;

                // We need to keep track of the last "mutual protect" region so we can properly not add additional blocks
                // to the second and subsequent mutual protect try blocks. We can't just keep track of the EH region
                // pointer, because we're updating the last blocks as we go. So, we need to keep track of the
                // pre-update 'try' begin/last blocks themselves. These only matter if the "last" blocks that match are
                // from two (or more) nested 'try' regions.
                var mutualTryBeg = null as BasicBlock;
                var mutualTryLast = null as BasicBlock;

                if (outerIsTryRegion)
                {
                    nextTryIndex = EHblkDsc.NO_ENCLOSING_INDEX; // unused, since the outer block is a 'try' region.

                    // The outer (enclosing) region is a 'try'
                    if (ehOuter.ebdTryLast == ehInner.ebdTryLast)
                    {
                        // Case (1) try nested in try.
                        foundMatchingLastBlock = true;

#if DEBUG
                        innerType = "try";
                        outerType = "try";
#endif

                        insertAfterBlk = ehOuter.ebdTryLast;
                        lastBlockPtrToCompare = insertAfterBlk;

                        if (EHblkDsc.ebdIsSameTry(ehOuter, ehInner))
                        {
                            // We can't touch this 'try', since it's mutual protect.
#if DEBUG
                            if (verbose)
                            {
                                jitprintf($"Mutual protect regions EH#{ehOuterIndex} and EH#{ehInnerIndex}; leaving identical 'try' last blocks.\n");
                            }
#endif

                            insertNormalizationBlock = false;
                        }
                        else
                        {
                            nextHndIndex = ehInner.ebdTryLast.hasHndIndex ? ehInner.ebdTryLast.HndIndex : EHblkDsc.NO_ENCLOSING_INDEX;
                        }
                    }
                    else if (ehOuter.ebdTryLast == ehInner.ebdHndLast)
                    {
                        // Case (2) handler nested in try.
                        foundMatchingLastBlock = true;

#if DEBUG
                        innerType = "handler";
                        outerType = "try";
#endif

                        insertAfterBlk = ehOuter.ebdTryLast;
                        lastBlockPtrToCompare = insertAfterBlk;

                        assert(ehInner.ebdHndLast.HndIndex == ehInnerIndex);
                        nextHndIndex = ehInner.ebdEnclosingHndIndex;
                    }
                    else
                    {
                        // No "last" pointers match!
                    }

                    if (foundMatchingLastBlock)
                    {
                        // The outer might be part of a new set of mutual protect regions (if it isn't part of one already).
                        mutualTryBeg = ehOuter.ebdTryBeg;
                        mutualTryLast = ehOuter.ebdTryLast;
                    }
                }
                else
                {
                    nextHndIndex = EHblkDsc.NO_ENCLOSING_INDEX; // unused, since the outer block is a handler region.

                    // The outer (enclosing) region is a handler (note that it can't be a filter; there is no nesting
                    // within a filter).
                    if (ehOuter.ebdHndLast == ehInner.ebdTryLast)
                    {
                        // Case (3) try nested in handler.
                        foundMatchingLastBlock = true;

#if DEBUG
                        innerType = "try";
                        outerType = "handler";
#endif

                        insertAfterBlk = ehOuter.ebdHndLast;
                        lastBlockPtrToCompare = insertAfterBlk;

                        assert(ehInner.ebdTryLast.TryIndex == ehInnerIndex);
                        nextTryIndex = ehInner.ebdEnclosingTryIndex;
                    }
                    else if (ehOuter.ebdHndLast == ehInner.ebdHndLast)
                    {
                        // Case (4) handler nested in handler.
                        foundMatchingLastBlock = true;
#if DEBUG
                        innerType = "handler";
                        outerType = "handler";
#endif
                        insertAfterBlk = ehOuter.ebdHndLast;
                        lastBlockPtrToCompare = insertAfterBlk;

                        nextTryIndex = ehInner.ebdTryLast.hasTryIndex ? ehInner.ebdTryLast.TryIndex : EHblkDsc.NO_ENCLOSING_INDEX;
                    }
                    else
                    {
                        // No "last" pointers match!
                    }
                }

                while (foundMatchingLastBlock)
                {
                    assert(lastBlockPtrToCompare is not null);
                    assert(insertAfterBlk is not null);
                    assert(ehOuterIndex is not EHblkDsc.NO_ENCLOSING_INDEX);
                    assert(!Unsafe.IsNullRef(in ehOuter));

                    // Add a normalization block

                    if (insertNormalizationBlock)
                    {
                        // Add a new last block for 'ehOuter' that will be outside the EH region with which it encloses and
                        // shares a 'last' pointer

                        var newLast = BasicBlock.New(this);
                        newLast.bbRefs = 0;

                        assert(insertAfterBlk is not null);
                        fgInsertBBafter(insertAfterBlk, newLast);

#if DEBUG
                        if (verbose)
                        {
                            jitprintf($"last {outerType} block for EH#{ehOuterIndex} and last {innerType} block for EH#{ehInnerIndex} are same block; inserted new {FMT_BB(newLast.bbNum)} after {FMT_BB(insertAfterBlk.bbNum)} as new last {outerType} block for EH#{ehOuterIndex}.\n");
                        }
#endif

                        if (outerIsTryRegion)
                        {
                            ehOuter.ebdTryLast = newLast;
                            newLast.TryIndex = ehOuterIndex;

                            if (nextHndIndex is EHblkDsc.NO_ENCLOSING_INDEX)
                            {
                                newLast.clearHndIndex();
                            }
                            else
                            {
                                newLast.HndIndex = nextHndIndex;
                            }
                        }
                        else
                        {
                            ehOuter.ebdHndLast = newLast;

                            if (nextTryIndex is EHblkDsc.NO_ENCLOSING_INDEX)
                            {
                                newLast.clearTryIndex();
                            }
                            else
                            {
                                newLast.TryIndex = nextTryIndex;
                            }
                            newLast.HndIndex = ehOuterIndex;
                        }

                        newLast.CatchType = BBCT_NONE; // bbCatchType is only set on the first block of a handler, which is this not
                        newLast.bbCodeOffs = insertAfterBlk.bbCodeOffsEnd;
                        newLast.bbCodeOffsEnd = newLast.bbCodeOffs; // code size = 0. TODO: use BAD_IL_OFFSET instead?
                        newLast.inheritWeight(insertAfterBlk);
                        newLast.SetFlags(BBF_INTERNAL);
                        var newEdge = fgAddRefPred(newLast, insertAfterBlk);
                        insertAfterBlk.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);

                        // Move the insert pointer. More enclosing equivalent 'last' blocks will be inserted after this.
                        insertAfterBlk = newLast;

                        modified = true;

#if DEBUG
                        if (false && verbose) // Normally this is way too verbose, but it is useful for debugging
                        {
                            jitprintf("*************** fgNormalizeEH() made a change\n");
                            fgDispBasicBlocks();
                            fgDispHandlerTab();
                        }
#endif
                    }

                    // Now find the next outer enclosing EH region and see if it also shares the last block.
                    foundMatchingLastBlock = false; // assume nothing will match
                    ehInner = ref ehOuter;
                    ehInnerIndex = ehOuterIndex;
                    innerIsTryRegion = outerIsTryRegion;

                    // Loop outwards in the EH nesting.
                    ehOuterIndex = ehOuter.ebdGetEnclosingRegionIndex(out outerIsTryRegion);

                    if (ehOuterIndex is not EHblkDsc.NO_ENCLOSING_INDEX)
                    {
                        // There are more enclosing regions; check for equivalent 'last' pointers.

#if DEBUG
                        innerType = outerType;
                        outerType = "";
#endif

                        ehOuter = ref ehGetDsc(ehOuterIndex);

                        insertNormalizationBlock = true; // assume it's not mutual protect

                        if (outerIsTryRegion)
                        {
                            nextTryIndex = EHblkDsc.NO_ENCLOSING_INDEX; // unused, since the outer block is a 'try' region.

                            // The outer (enclosing) region is a 'try'
                            if (ehOuter.ebdTryLast == lastBlockPtrToCompare)
                            {
                                // Case (1) and (2): try or handler nested in try.
                                foundMatchingLastBlock = true;

#if DEBUG
                                outerType = "try";
#endif

                                assert(mutualTryBeg is not null);
                                assert(mutualTryLast is not null);

                                if (innerIsTryRegion && ehOuter.ebdIsSameTry(mutualTryBeg, mutualTryLast))
                                {
                                    // We can't touch this 'try', since it's mutual protect.

#if DEBUG
                                    if (verbose)
                                    {
                                        jitprintf($"Mutual protect regions EH#{ehOuterIndex} and EH#{ehInnerIndex}; leaving identical 'try' last blocks.\n");
                                    }
#endif

                                    insertNormalizationBlock = false;

                                    // We still need to update the 'last' pointer, in case someone inserted a normalization
                                    // block before the start of the mutual protect 'try' region.
                                    ehOuter.ebdTryLast = insertAfterBlk;
                                }
                                else
                                {
                                    if (innerIsTryRegion)
                                    {
                                        // Case (1) try nested in try.
                                        nextHndIndex = ehInner.ebdTryLast.hasHndIndex ? ehInner.ebdTryLast.HndIndex : EHblkDsc.NO_ENCLOSING_INDEX;
                                    }
                                    else
                                    {
                                        // Case (2) handler nested in try.
                                        assert(ehInner.ebdHndLast.HndIndex == ehInnerIndex);
                                        nextHndIndex = ehInner.ebdEnclosingHndIndex;
                                    }
                                }

                                // The outer might be part of a new set of mutual protect regions (if it isn't part of one already).
                                mutualTryBeg = ehOuter.ebdTryBeg;
                                mutualTryLast = ehOuter.ebdTryLast;
                            }
                        }
                        else
                        {
                            nextHndIndex = EHblkDsc.NO_ENCLOSING_INDEX; // unused, since the outer block is a handler region.

                            // The outer (enclosing) region is a handler (note that it can't be a filter; there is no
                            // nesting within a filter).
                            if (ehOuter.ebdHndLast == lastBlockPtrToCompare)
                            {
                                // Case (3) and (4): try nested in try or handler.
                                foundMatchingLastBlock = true;

#if DEBUG
                                outerType = "handler";
#endif

                                if (innerIsTryRegion)
                                {
                                    // Case (3) try nested in handler.
                                    assert(ehInner.ebdTryLast.TryIndex == ehInnerIndex);
                                    nextTryIndex = ehInner.ebdEnclosingTryIndex;
                                }
                                else
                                {
                                    // Case (4) handler nested in handler.
                                    nextTryIndex = ehInner.ebdTryLast.hasTryIndex ? ehInner.ebdTryLast.TryIndex : EHblkDsc.NO_ENCLOSING_INDEX;
                                }
                            }
                        }
                    }

                    // If we get to here and foundMatchingLastBlock is false, then the inner and outer region don't share
                    // any 'last' blocks, so we're done. Note that we could have a situation like this:
                    //
                    //        try4   try3   try2   try1
                    //        |----  |      |      |      BB01
                    //        |      |----  |      |      BB02
                    //        |      |      |----  |      BB03
                    //        |      |      |      |----- BB04
                    //        |      |      |----- |----- BB05
                    //        |----  |------------------- BB06
                    //
                    // (Thus, try1 & try2 end at BB05, and are nested inside try3 & try4, which both end at BB06.)
                    // In this case, we'll process try1 and try2, then break out. Later, as we iterate through the EH table,
                    // we'll get to try3 and process it and try4.
                }
            }
        }
        return modified;
    }

    /// <summary>check if two profile weights are equal (or nearly so)</summary>
    /// <param name="weight1">first weight</param>
    /// <param name="weight2">second weight</param>
    /// <param name="epsilon">maximum absolute difference for weights to be considered equal</param>
    /// <returns>true if the weights are within epsilon of each other</returns>
    /// <remarks>In most cases you should probably call fgProfileWeightsConsistent instead of this method.</remarks>
    public static bool fgProfileWeightsEqual(weight_t weight1, weight_t weight2, weight_t epsilon = 0.01)
    {
        return weight_t.Abs(weight1 - weight2) <= epsilon;
    }

    /// <summary>Sets the given edge's target block to 'newTarget', updating pred lists as needed.</summary>
    /// <param name="edgeRef">The edge to update. Note that this is a reference type to support automatic updates to BasicBlock members (bbTargetEdge et al).</param>
    /// <param name="newTarget">The new successor of the edge.</param>
    public void fgRedirectEdge(ref FlowEdge edgeRef, BasicBlock newTarget)
    {
        if (edgeRef.DestinationBlock == newTarget)
        {
            return;
        }

        var block    = edgeRef.SourceBlock;
        var dupCount = edgeRef.DupCount;
        _ = fgRemoveAllRefPreds(edgeRef.DestinationBlock, block);

        ref var predListPtr = ref fgGetPredInsertPoint(block, newTarget);
        var predEdge = predListPtr;

        if ((predEdge is not null) && (predEdge.SourceBlock == block))
        {
            edgeRef = predEdge;
            edgeRef.incrementDupCount(dupCount);
        }
        else
        {
            edgeRef.NextPredEdge = predEdge;
            predListPtr = edgeRef;
            edgeRef.DestinationBlock = newTarget;
        }

        newTarget.bbRefs += dupCount;

#if DEBUG
        // Pred list of target should still be ordered
        assert(newTarget.checkPredListOrder());
#endif
    }

    /// <summary>Removes a predecessor edge from one block to another, no matter what the "dup count" is.</summary>
    /// <param name="block">A block to operate on.</param>
    /// <param name="blockPred">The predecessor block to remove from the predecessor list. It must be a predecessor of "block".</param>
    /// <returns>Returns the flow graph edge that was removed. The dup count on the edge is no longer valid.</returns>
    /// <remarks>
    ///   <para>"blockPred" must be a predecessor block of "block".</para>
    ///   <para>block->bbRefs is decremented to account for the reduction in incoming edges.</para>
    /// </remarks>
    public FlowEdge fgRemoveAllRefPreds(BasicBlock block, BasicBlock blockPred)
    {
        assert(fgPredsComputed);
        assert(block.CountOfInEdges > 0);

        ref var ptrToPred = ref fgGetPredForBlock(block, blockPred, out var pred);
        assert(pred is not null);
        assert(pred.DupCount > 0);

        assert(block.bbRefs >= pred.DupCount);
        block.bbRefs -= pred.DupCount;

        // Now splice out the predecessor edge.
        ptrToPred = pred.NextPredEdge;

        // Any changes to the flow graph invalidate the dominator sets.
        fgModified = true;

        return pred;
    }

    /// <summary>For a given block, replace the target 'oldTarget' with 'newTarget'.</summary>
    /// <param name="block">the block in which a jump target will be replaced.</param>
    /// <param name="oldTarget">the new branch target of the block.</param>
    /// <param name="newTarget">the old branch target of the block.</param>
    public void fgReplaceJumpTarget(BasicBlock block, BasicBlock oldTarget, BasicBlock newTarget)
    {
        // Notes:
        // 1. Only branches are changed: BBJ_ALWAYS, BBJ_COND, BBJ_SWITCH, etc.
        //    We assert for other jump kinds.
        // 2. All branch targets found are updated. If there are multiple ways for a block
        //    to reach 'oldTarget' (e.g., multiple arms of a switch), all of them are changed.
        // 3. The predecessor lists are updated.

        assert(fgPredsComputed);
        assert(fgGetPredForBlock(oldTarget, block) is not null);

        switch (block.Kind)
        {
            case BBJ_CALLFINALLY:
            case BBJ_CALLFINALLYRET:
            case BBJ_ALWAYS:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            case BBJ_LEAVE:
            {
                // This function can be called before import, so we still have BBJ_LEAVE
                assert(block.Target == oldTarget);
                fgRedirectEdge(ref block.TargetEdgeRef, newTarget);
                break;
            }

            case BBJ_COND:
            {
                if (block.TrueTarget == oldTarget)
                {
                    fgRedirectEdge(ref block.TrueEdgeRef, newTarget);
                }
                else
                {
                    assert(block.FalseTarget == oldTarget);
                    fgRedirectEdge(ref block.FalseEdgeRef, newTarget);
                }

                if (block.TrueEdge == block.FalseEdge)
                {
                    // Block became degenerate, simplify
                    fgRemoveConditionalJump(block);
                    assert(block.Kind is BBJ_ALWAYS);
                    assert(block.Target == newTarget);
                }
                break;
            }

            case BBJ_SWITCH:
            {
                var switchTargets = block.SwitchTargets;
                var jumpTab = switchTargets.Cases;
                var oldEdge = null as FlowEdge;
                var newEdge = null as FlowEdge;
                var changed = false;

                for (var i = 0; i < jumpTab.Length; i++)
                {
                    // If the new target already has an edge from this switch statement,
                    // we will need to add the likelihood from the edge we're redirecting
                    // to the existing edge, so save the old and new targets' edges.
                    // Note that we can visit the same edge multiple times
                    // if there are multiple switch cases with the same target.
                    // The edge has a dup count and a single likelihood for all the possible
                    // paths to the target, so we only want to add the likelihood once
                    // despite visiting the duplicated edges in the `jumpTab` array
                    // multiple times.

                    // If there are duplicate edges to 'oldTarget',
                    // and there is already an edge to 'newTarget',
                    // overwrite the duplicate edges with 'newEdge'.
                    if (jumpTab[i] == oldEdge)
                    {
                        assert(oldEdge is not null);
                        assert(newEdge is not null);
                        jumpTab[i] = newEdge;
                    }
                    else if (jumpTab[i].DestinationBlock == oldTarget)
                    {
                        // Else, we have found the edge we need to redirect to 'newTarget'.
                        assert(oldEdge is null);
                        assert(newEdge is null);

                        oldEdge = jumpTab[i];
                        fgRedirectEdge(ref jumpTab[i], newTarget);

                        newEdge = jumpTab[i];
                        changed = true;
                    }
                }

                // If the edge to 'oldTarget' isn't the same as the edge pointing to 'newTarget',
                // then 'block' already had an edge to 'newTarget' (i.e. 'newEdge').
                // Increase the likelihood of 'newEdge' accordingly.
                if (oldEdge != newEdge)
                {
                    assert(oldEdge is not null);
                    assert(oldEdge.SourceBlock == block);
                    assert(oldEdge.DestinationBlock == oldTarget);
                    assert(newEdge is not null);
                    assert(newEdge.SourceBlock == block);
                    assert(newEdge.DestinationBlock == newTarget);
                    newEdge.AddLikelihood(oldEdge.Likelihood);

                    var succs = switchTargets.Succs;

                    for (var i = succs.Length; i != 0; i--)
                    {
                        if (succs[i - 1] == oldEdge)
                        {
                            // Remove the old edge from the unique successor table.
                            switchTargets.RemoveSucc(i - 1);
                            break;
                        }
                    }
                }

                // If we simply redirected 'oldEdge' to 'newTarget', we don't need to update the switch map entry,
                // because we did not remove any edges.
                assert(changed);
                break;
            }

            case BBJ_EHFINALLYRET:
            {
                fgReplaceEhfSuccessor(block, oldTarget, newTarget);
                break;
            }

            default:
            {
                assert(false, "Block doesn't have a jump target!");
                unreached();
                break;
            }
        }
    }

    public void fgFixEntryFlowForOSR()
    {
        // TODO: Port Compiler.fgFixEntryFlowForOSR
    }

    public void fgInvalidateDfsTree()
    {
        // TODO: Port Compiler.fgInvalidateDfsTree
    }

    /// <summary>Clear up annotations for any struct promotion temps created for implicit byrefs.</summary>
    public void fgMarkDemotedImplicitByRefArgs()
    {
        // TODO: Port Compiler.fgMarkDemotedImplicitByRefArgs
    }

    /// <summary>Optimize a BBJ_COND block that unconditionally jumps to the same target</summary>
    /// <param name="block">BBJ_COND block with identical true/false targets</param>
    public void fgRemoveConditionalJump(BasicBlock block)
    {
        assert(block.Kind is BBJ_COND);
        assert(block.TrueEdge == block.FalseEdge);

        var target = block.TrueTarget;

#if DEBUG
        if (verbose)
        {
            jitprintf($"Block {FMT_BB(block.bbNum)} becoming a BBJ_ALWAYS to {FMT_BB(target.bbNum)} (jump target is the same whether the condition is true or false)\n");
        }
#endif

        if (block.IsLIR)
        {
            var jmp = block.LastNode;

            assert(jmp is not null);
            assert(jmp.Oper.IsConditionalJump);

            bool isClosed;
            GenTreeFlags sideEffects;
            LIR.ReadOnlyRange jmpRange;

            if (jmp.Oper is GT_JCC)
            {
                // For JCC we have an invariant until resolution that the
                // previous node sets those CPU flags.

                var prevNode = jmp.Prev;
                assert((prevNode is not null) && ((prevNode.Flags & GTF_SET_FLAGS) != 0));
                prevNode.Flags &= ~GTF_SET_FLAGS;

                jmpRange = block.GetTreeRange(prevNode, out isClosed, out sideEffects);
                jmpRange = new LIR.ReadOnlyRange(jmpRange.FirstNode, jmp);
            }
            else
            {
                jmpRange = block.GetTreeRange(jmp, out isClosed, out sideEffects);
            }

            if (isClosed && ((sideEffects & GTF_SIDE_EFFECT) == 0))
            {
                // If the jump and its operands form a contiguous, side-effect-free range, remove them.
                block.Delete(jmpRange);
            }
            else
            {
                // Otherwise, just remove the jump node itself.
                block.Remove(jmp, true);
            }
        }
        else
        {
            var condStmt = block.LastStmt;
            assert(condStmt is not null);

            var cond = condStmt.RootNode;
            noway_assert(cond.Oper is GT_JTRUE);

            // check for SIDE_EFFECTS
            if ((cond.Flags & GTF_SIDE_EFFECT) != 0)
            {
                // Extract the side effects from the conditional
                var sideEffectList = null as GenTree;

                gtExtractSideEffList(cond, ref sideEffectList);

                if (sideEffectList is null)
                {
                    compCurBB = block;
                    fgRemoveStmt(block, condStmt);
                }
                else
                {
                    noway_assert((sideEffectList.Flags & GTF_SIDE_EFFECT) != 0);
#if DEBUG
                    if (verbose)
                    {
                        jitprintf("\nConditional has side effects! Extracting side effects...\n");
                        gtDispTree(cond);
                        jitprintf("\n");
                        gtDispTree(sideEffectList);
                        jitprintf("\n");
                    }
#endif

                    // Replace the conditional statement with the list of side effects
                    noway_assert(sideEffectList.Oper is not GT_JTRUE);

                    condStmt.RootNode = sideEffectList;

                    if (fgNodeThreading == NodeThreading.AllTrees)
                    {
                        compCurBB = block;

                        // Update ordering, costs, FP levels, etc.
                        gtSetStmtInfo(condStmt);

                        // Re-link the nodes for this statement
                        fgSetStmtSeq(condStmt);
                    }
                }
            }
            else
            {
                compCurBB = block;
                // conditional has NO side effect - remove it
                fgRemoveStmt(block, condStmt);
            }
        }

        // Conditional is gone - always jump to target

        block.SetKindAndTargetEdge(BBJ_ALWAYS, block.TrueEdge);
        assert(block.Target == target);

        // Update bbRefs and bbNum - Conditional predecessors to the same
        // block are counted twice so we have to remove one of them

        noway_assert(target.CountOfInEdges > 1);
        fgRemoveRefPred(block.TargetEdge);
    }

    /// <summary>Remove all traces of profile info</summary>
    /// <param name="reason">string describing why profile data is being removed</param>
    /// <remarks>
    ///   <para>Needed if the jit initially thought it was going to optimize the method, but then decided not to.</para>
    ///   <para>Does not modify any block fields, so should be called before we start to incorporate profile data.</para>
    /// </remarks>
    public unsafe void fgRemoveProfileData(string reason)
    {
        fgPgoFailReason = reason;
        fgPgoQueryResult = E_FAIL;
        fgPgoHaveWeights = false;
        fgPgoData = null;
        fgPgoSchema = null;
        fgPgoDisabled = true;
        fgPgoDynamic = false;
    }

    /// <summary>update BBJ_EHFINALLYRET block to remove the successor at `succIndex` in the block's jump table.</summary>
    /// <param name="block">BBJ_EHFINALLYRET block</param>
    /// <param name="succIndex">index of the successor in the block's jump table</param>
    /// <remarks>Don't update the predecessor list of the successor; the caller is expected to handle this.</remarks>
    public void fgRemoveEhfSuccFromTable(BasicBlock block, int succIndex)
    {
        assert(block.Kind is BBJ_EHFINALLYRET);

        var ehfDesc = block.EhfTargets;
        assert(ehfDesc is not null);

        var succEdge = ehfDesc.Succs[succIndex];
        ehfDesc.RemoveSucc(succIndex);

        // Recompute the likelihoods of the block's other successor edges.
        var removedLikelihood = succEdge.Likelihood;
        var succs = ehfDesc.Succs;

        for (var i = 0; i < succs.Length; i++)
        {
            // If we removed all of the flow out of 'block', distribute flow among the remaining edges evenly.
            var edge = succs[i];
            var currLikelihood = edge.Likelihood;
            var newLikelihood = (removedLikelihood == 1.0) ? (1.0 / succs.Length) : (currLikelihood / (1.0 - removedLikelihood));
            edge.Likelihood = weight_t.Min(1.0, newLikelihood);
        }

#if DEBUG
        // We only expect to see a successor once in the table.
        for (var i = succIndex; i < succs.Length; i++)
        {
            assert(succs[i].DestinationBlock != succEdge.DestinationBlock);
        }
#endif
    }

    /// <summary>Decrements the reference count of `edge`, removing it from its successor block's pred list if the reference count is zero.</summary>
    /// <param name="edge">The FlowEdge* to decrement the reference count of.</param>
    /// <remarks>
    ///   <para>succBlock.bbRefs is decremented by one to account for the reduction in incoming edges.</para>
    ///   <para>fgModified is set if a flow edge is removed, indicating that the flow graph shape has changed.</para>
    /// </remarks>
    public void fgRemoveRefPred(FlowEdge edge)
    {
        assert(fgPredsComputed);

        var predBlock = edge.SourceBlock;
        var succBlock = edge.DestinationBlock;

        succBlock.bbRefs--;

        assert(edge.DupCount > 0);
        edge.decrementDupCount();

        if (edge.DupCount == 0)
        {
            // Splice out the predecessor edge in succBlock's pred list, since it's no longer necessary.
            ref var ptrToPred = ref fgGetPredForBlock(succBlock, predBlock, out var pred);

            assert(pred == edge);
            ptrToPred = pred.NextPredEdge;

            // Any changes to the flow graph invalidate the dominator sets.
            fgModified = true;
        }
    }

    /// <summary>remove a statement from a block's statement list</summary>
    /// <param name="block">the block from which 'stmt' will be removed</param>
    /// <param name="stmt">the statement to be removed</param>
    /// <param name="isUnlink">ultimate plan is to move the statement, not delete it</param>
    public void fgRemoveStmt(BasicBlock block, Statement stmt, bool isUnlink = false)
    {
        assert(fgOrder == FGOrderTree);

#if DEBUG
        // Don't print if it is a GT_NOP. Too much noise from the inliner.
        if (verbose && (stmt.RootNode.Oper is not GT_NOP))
        {
            jitprintf($"\n{(isUnlink ? "unlinking" : "removing useless")} ");
            gtDispStmt(stmt);
            jitprintf($" from {FMT_BB(block.bbNum)}\n");
        }
#endif

        if (opts.compDbgCode && (stmt.PrevStmt != stmt) && stmt.DebugInfo.IsValid)
        {
            // TODO: For debuggable code, should we remove significant
            // statement boundaries. Or should we leave a GT_NO_OP in its place?
        }

        var firstStmt = block.FirstStmt;
        if (firstStmt == stmt) // Is it the first statement in the list?
        {
            if (firstStmt.NextStmt is null)
            {
                assert(firstStmt == block.LastStmt);

                // this is the only statement - basic block becomes empty
                block.FirstStmt = null;
            }
            else
            {
                block.FirstStmt = firstStmt.NextStmt;
                block.FirstStmt.PrevStmt = firstStmt.PrevStmt;
            }
        }
        else if (stmt == block.LastStmt) // Is it the last statement in the list?
        {
            assert(stmt.PrevStmt is not null);
            assert(block.FirstStmt is not null);

            stmt.PrevStmt.NextStmt = null;
            block.FirstStmt.PrevStmt = stmt.PrevStmt;
        }
        else // The statement is in the middle.
        {
            assert(stmt.PrevStmt is not null && stmt.NextStmt is not null);

            var prev = stmt.PrevStmt;

            prev.NextStmt = stmt.NextStmt;
            stmt.NextStmt.PrevStmt = prev;
        }

        noway_assert(!optValnumCSE_phase);
        fgStmtRemoved = true;

#if DEBUG
        if (verbose)
        {
            if (block.FirstStmt is null)
            {
                jitprintf($"\n{FMT_BB(block.bbNum)} becomes empty\n");
            }
        }
#endif
    }

    /// <summary>update BBJ_EHFINALLYRET block so that all control that previously flowed to oldSucc now flows to newSucc.</summary>
    /// <param name="block">BBJ_EHFINALLYRET block</param>
    /// <param name="oldSucc">new successor</param>
    /// <param name="newSucc">old successor</param>
    /// <remarks>
    ///   <para>It is assumed that oldSucc is currently a successor of `block`.</para>
    ///   <para>We only allow a successor block to appear once in the successor list.</para>
    ///   <para>Thus, if the new successor already exists in the list, we simply remove the old successor.</para>
    /// </remarks>
    public void fgReplaceEhfSuccessor(BasicBlock block, BasicBlock oldSucc, BasicBlock newSucc)
    {
        assert(block.Kind is BBJ_EHFINALLYRET);
        assert(fgPredsComputed);

        var ehfDesc = block.EhfTargets;
        assert(ehfDesc is not null);
        var succTab = ehfDesc.Succs;

        // Walk the successor table looking for the old successor, which we expect to find only once.

        var oldSuccNum = -1;
        var newSuccNum = -1;
        var higherNum = 0;

        for (var i = 0; i < succTab.Length; i++)
        {
            var succ = succTab[i];

            assert(succ.SourceBlock == block);

            if (succ.DestinationBlock == newSucc)
            {
                assert(newSuccNum == -1);
                higherNum = i;
                newSuccNum = i;
            }

            if (succ.DestinationBlock == oldSucc)
            {
                assert(oldSuccNum == -1);
                higherNum = i;
                oldSuccNum = i;
            }
        }

        noway_assert((oldSuccNum != -1), "Did not find oldSucc in succTab[]");

        fgRedirectEdge(ref succTab[oldSuccNum], newSucc);

        if (newSuccNum != -1)
        {
            // The old and new succ edges are now duplicates.
            // Remove the one at the higher index from the table.
            // If we're lucky, the higher index is at the end of the table,
            // so we don't have to copy anything over.
            assert(succTab[oldSuccNum] == succTab[newSuccNum]);
            fgRemoveEhfSuccFromTable(block, higherNum);

            JITDUMP($"Remove existing BBJ_EHFINALLYRET {FMT_BB(block.bbNum)} successor {FMT_BB(oldSucc.bbNum)}; replacement successor {FMT_BB(newSucc.bbNum)} already exists in list\n");
        }
        else
        {
            JITDUMP($"Replace BBJ_EHFINALLYRET {FMT_BB(block.bbNum)} successor {FMT_BB(oldSucc.bbNum)} with {FMT_BB(newSucc.bbNum)}\n");
        }
    }

    /// <summary>Reset any data structures to the state expected by "fgSsaBuild", so it can be run again.</summary>
    /// <param name="deepClean"></param>
    public void fgResetForSsa(bool deepClean)
    {
        // TODO: Port Compiler.fgResetForSsa
    }

    public void fgSetOptions()
    {
        // TODO: Port Compiler.fgSetOptions
    }

    public void fgSetStmtSeq(Statement stmt)
    {
        var rootNode = stmt.RootNode;
        stmt.TreeListBegin = fgSetTreeSeq(rootNode);

#if DEBUG
        // Keep track of the highest # of tree nodes.
        if (BasicBlock.s_nMaxTrees < rootNode._seqNum)
        {
            BasicBlock.s_nMaxTrees = rootNode._seqNum;
        }
#endif
    }

    /// <summary>Sequence the tree, setting the "gtPrev" and "gtNext" links.</summary>
    /// <param name="tree">the tree to sequence</param>
    /// <param name="isLIR">whether the sequencing is being done for LIR. If so, the GTF_REVERSE_OPS flag will be cleared on all nodes.</param>
    /// <returns>The first node to execute in the sequenced tree.</returns>
    /// <remarks>Also sets the sequence numbers for dumps. The last and first node of the resulting "range" will have their "gtNext" and "gtPrev" links set to "null".</remarks>
    private GenTree fgSetTreeSeq(GenTree tree, bool isLIR = false)
    {
#if DEBUG
        if (isLIR)
        {
            assert((fgNodeThreading == NodeThreading.LIR) || (mostRecentlyActivePhase == PHASE_RATIONALIZE));
        }
        else
        {
            assert((fgNodeThreading == NodeThreading.AllTrees) || (mostRecentlyActivePhase == PHASE_SET_BLOCK_ORDER));
        }
#endif

        return new SetTreeSeqVisitor(this, tree, isLIR).Sequence();
    }

    public void fgSortEHTable()
    {
        if (!fgNeedToSortEHTable)
        {
            return;
        }

        // Now, all fields of the EH table are set except for those that are related
        // to nesting. We need to first sort the table to ensure that an EH clause
        // appears before any try or handler that it is nested within. The CLI spec
        // requires this for nesting in 'try' clauses, but does not require this
        // for handler clauses. However, parts of the JIT do assume this ordering.
        //
        // For example:
        //
        //      try { // A
        //      } catch {
        //          try { // B
        //          } catch {
        //          }
        //      }
        //
        // In this case, the EH clauses for A and B have no required ordering: the
        // clause for either A or B can come first, despite B being nested within
        // the catch clause for A.
        //
        // The CLI spec, section 12.4.2.5 "Overview of exception handling", states:
        // "The ordering of the exception clauses in the Exception Handler Table is
        // important. If handlers are nested, the most deeply nested try blocks shall
        // come before the try blocks that enclose them."
        //
        // Note, in particular, that it doesn't say "shall come before the *handler*
        // blocks that enclose them".
        //
        // Also, the same section states, "When an exception occurs, the CLI searches
        // the array for the first protected block that (1) Protects a region including the
        // current instruction pointer and (2) Is a catch handler block and (3) Whose
        // filter wishes to handle the exception."
        //
        // Once again, nothing about the ordering of the catch blocks.
        //
        // A more complicated example:
        //
        //      try { // A
        //      } catch {
        //          try { // B
        //              try { // C
        //              } catch {
        //              }
        //          } catch {
        //          }
        //      }
        //
        // The clause for C must come before the clause for B, but the clause for A can
        // be anywhere. Thus, we could have these orderings: ACB, CAB, CBA.
        //
        // One more example:
        //
        //      try { // A
        //      } catch {
        //          try { // B
        //          } catch {
        //              try { // C
        //              } catch {
        //              }
        //          }
        //      }
        //
        // There is no ordering requirement: the EH clauses can come in any order.
        //
        // In Dev11 (Visual Studio 2012), x86 did not sort the EH table (it never had before)
        // but ARM did. It turns out not sorting the table can cause the EH table to incorrectly
        // set the bbHndIndex value in some nested cases, and that can lead to a security exploit
        // that allows the execution of arbitrary code.

#if DEBUG
        if (verbose)
        {
            jitprintf("fgSortEHTable: Sorting EH table\n");
        }
#endif

        for (var xtabnum1 = 0; xtabnum1 < compHndBBtabCount; xtabnum1++)
        {
            ref var xtab1 = ref compHndBBtab[xtabnum1];

            for (var xtabnum2 = xtabnum1 + 1; xtabnum2 < compHndBBtabCount; xtabnum2++)
            {
                ref var xtab2 = ref compHndBBtab[xtabnum2];

                // If the nesting is wrong, swap them. The nesting is wrong if
                // EH region 2 is nested in the try, handler, or filter of EH region 1.
                // Note that due to proper nesting rules, if any of 2 is nested in
                // the try or handler or filter of 1, then all of 2 is nested.
                // We must be careful when comparing the offsets of the 'try' clause, because
                // for "mutually-protect" try/catch, the 'try' bodies will be identical.
                // For this reason, we use the handler region to check nesting. Note
                // that we must check both beginning and end: a nested region can have a 'try'
                // body that starts at the beginning of a handler. Thus, if we just compared the
                // handler begin offset, we might get confused and think it is nested.

                var hndBegOff = xtab2._ebdHndBegOffset;
                var hndEndOff = xtab2._ebdHndEndOffset;
                assert(hndEndOff > hndBegOff);

                // Note that end of filter is beginning of handler
                if (((hndBegOff >= xtab1._ebdTryBegOffset) && (hndEndOff <= xtab1._ebdTryEndOffset)) ||
                    ((hndBegOff >= xtab1._ebdHndBegOffset) && (hndEndOff <= xtab1._ebdHndEndOffset)) ||
                    (xtab1.HasFilter && ((hndBegOff >= xtab1._ebdFilterBegOffset) && (hndEndOff <= xtab1._ebdHndBegOffset))))
                {
#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"fgSortEHTable: Swapping out-of-order EH#{xtabnum1} and EH#{xtabnum2}\n");
                    }

                    // Assert that the 'try' region is also nested in the same place as the handler

                    var tryBegOff = xtab2._ebdTryBegOffset;
                    var tryEndOff = xtab2._ebdTryEndOffset;
                    assert(tryEndOff > tryBegOff);

                    if ((hndBegOff >= xtab1._ebdTryBegOffset) && (hndEndOff <= xtab1._ebdTryEndOffset))
                    {
                        assert((tryBegOff >= xtab1._ebdTryBegOffset) && (tryEndOff <= xtab1._ebdTryEndOffset));
                    }

                    if ((hndBegOff >= xtab1._ebdHndBegOffset) && (hndEndOff <= xtab1._ebdHndEndOffset))
                    {
                        assert((tryBegOff >= xtab1._ebdHndBegOffset) && (tryEndOff <= xtab1._ebdHndEndOffset));
                    }

                    if (xtab1.HasFilter && ((hndBegOff >= xtab1._ebdFilterBegOffset) && (hndEndOff <= xtab1._ebdHndBegOffset)))
                    {
                        assert((tryBegOff >= xtab1._ebdFilterBegOffset) && (tryEndOff <= xtab1._ebdHndBegOffset));
                    }
#endif

                    // Swap them!
                    (xtab1, xtab2) = (xtab2, xtab1);
                }
            }
        }
    }

    public void fgSsaLiveness()
    {
        // TODO: Port Compiler.fgSsaLiveness
    }

#if DEBUG
    public void fgTableDispBasicBlock(BasicBlock block, BasicBlock? nextBlock = null, bool printEdgeLikelihoods = true, int blockTargetFieldWidth = 21, int ibcColWidth = 0)
    {
        var flags = block.FlagsRaw;
        var bbNumMax = fgBBNumMax;

        var maxBlockNumWidth = CountDigits(bbNumMax);
        maxBlockNumWidth = int.Max(maxBlockNumWidth, 2);

        var blockNumWidth = CountDigits(block.bbNum);
        blockNumWidth = int.Max(blockNumWidth, 2);

        var blockNumPadding = maxBlockNumWidth - blockNumWidth;

        // Instead of displaying a block number, should we instead display "*" when the specified block is
        // the next block?
        var terseNext = JitConfig[ConfigInteger.JitDumpTerseNextBlock] != 0;

        jitprintf($"{block.dspToString(blockNumPadding)} {block.bbRefs:D2}");

        //
        // Display EH 'try' region index
        //

        if (block.hasTryIndex)
        {
            jitprintf($" {block.TryIndex:D2}");
        }
        else
        {
            jitprintf("   ");
        }

        //
        // Display EH handler region index
        //

        if (block.hasHndIndex)
        {
            jitprintf($" {block.HndIndex:D2}");
        }
        else
        {
            jitprintf("   ");
        }

        jitprintf(" ");

        //
        // Display block predecessor list
        //

        var charCnt = block.dspPreds();

        if (charCnt < 19)
        {
            jitprintf(new string(' ', int.Max(0, 19 - charCnt)));
        }

        jitprintf(" ");

        //
        // Display block weight
        //

        if (block.isMaxBBWeight)
        {
            jitprintf(" MAX  ");
        }
        else
        {
            var weight = block.getBBWeight(this);

            if (weight > 99999) // Is it going to be more than 6 characters?
            {
                if (weight <= (99999 * BB_UNITY_WEIGHT))
                {
                    // print weight in this format ddddd.
                    jitprintf($"{(int)(weight_t.Round(weight / BB_UNITY_WEIGHT))}.");
                }
                else // print weight in terms of k (i.e. 156k )
                {
                    // print weight in this format dddddk
                    var weightK = weight / 1000;
                    jitprintf($"{(int)(weight_t.Round(weightK / BB_UNITY_WEIGHT))}k");
                }
            }
            else // print weight in this format ddd.dd
            {
                jitprintf($"{refCntWtd2str(weight, padForDecimalPlaces: true),6}");
            }
        }

        //
        // Display optional IBC weight column.
        // Note that iColWidth includes one character for a leading space, if there is an IBC column.
        //

        if (ibcColWidth > 0)
        {
            if (block.hasProfileWeight)
            {
                var bbWeightStr = $"{(int)(weight_t.Round(block.bbWeight))}";
                jitprintf($"{new string(' ', int.Max(0, ibcColWidth - bbWeightStr.Length))}{bbWeightStr}");
            }
            else
            {
                // No IBC data. Just print spaces to align the column.
                jitprintf(new string(' ', ibcColWidth));
            }
        }

        jitprintf(" ");

        //
        // Display block IL range
        //

        block.dspBlockILRange();

        //
        // Display block branch target
        //

        int printedBlockWidth;

        if ((flags & BBF_REMOVED) != 0)
        {
            printedBlockWidth = 10;
            jitprintf($"[removed] {new string(' ', blockTargetFieldWidth - printedBlockWidth)}");
        }
        else
        {
            switch (block.Kind)
            {
                case BBJ_COND:
                {
                    printedBlockWidth = 3 + 1 + 9; // "-> " + comma + kind
                    jitprintf($"-> {DspBlockNum(block.bbTrueEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)},{DspBlockNum(block.bbFalseEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} ( cond )");
                    break;
                }

                case BBJ_CALLFINALLY:
                {
                    printedBlockWidth = 3 + 9; // "-> " + kind
                    jitprintf($"-> {DspBlockNum(block.bbTargetEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (callf )");
                    break;
                }

                case BBJ_CALLFINALLYRET:
                {
                    printedBlockWidth = 3 + 9; // "-> " + kind
                    jitprintf($"-> {DspBlockNum(block.bbTargetEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (callfr)");
                    break;
                }

                case BBJ_ALWAYS:
                {
                    var label = ((flags & BBF_KEEP_BBJ_ALWAYS) != 0) ? "ALWAYS" : "always";
                    printedBlockWidth = 3 + 9; // "-> " + kind
                    jitprintf($"-> {DspBlockNum(block.bbTargetEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} ({label})");
                    break;
                }

                case BBJ_LEAVE:
                {
                    printedBlockWidth = 3 + 9; // "-> " + kind
                    jitprintf($"-> {DspBlockNum(block.bbTargetEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (leave )");
                    break;
                }

                case BBJ_EHFINALLYRET:
                {
                    jitprintf("->");
                    printedBlockWidth = 2 + 9; // kind

                    var ehfDesc = block.EhfTargets;
                    if (ehfDesc is null)
                    {
                        jitprintf(" ????");
                        printedBlockWidth += 5;
                    }
                    else
                    {
                        // Very early in compilation, we won't have fixed up the BBJ_EHFINALLYRET successors yet.

                        var succs = ehfDesc.Succs;

                        for (var i = 0; i < succs.Length; i++)
                        {
                            printedBlockWidth += 1; // space/comma
                            jitprintf($"{((i == 0) ? ' ' : ',')}{DspBlockNum(succs[i], printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                        }
                    }

                    if (printedBlockWidth < blockTargetFieldWidth)
                    {
                        jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)}");
                    }

                    jitprintf(" (finret)");
                    break;
                }

                case BBJ_EHFAULTRET:
                {
                    printedBlockWidth = 9; // kind
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (falret)");
                    break;
                }

                case BBJ_EHFILTERRET:
                {
                    printedBlockWidth = 3 + 9; // "-> " + kind
                    jitprintf($"-> {DspBlockNum(block.bbTargetEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (fltret)");
                    break;
                }

                case BBJ_EHCATCHRET:
                {
                    printedBlockWidth = 3 + 9; // "-> " + kind
                    jitprintf($"-> {DspBlockNum(block.bbTargetEdge, printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} ( cret )");
                    break;
                }

                case BBJ_THROW:
                {
                    printedBlockWidth = 9; // kind
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (throw )");
                    break;
                }

                case BBJ_RETURN:
                {
                    printedBlockWidth = 9; // kind
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (return)");
                    break;
                }

                case BBJ_SWITCH:
                {
                    jitprintf("->");
                    printedBlockWidth = 2 + 9; // kind

                    var jumpSwt = block.SwitchTargets;
                    var jumpTab = jumpSwt.Cases;

                    for (var i = 0; i < jumpTab.Length; i++)
                    {
                        printedBlockWidth += 1; // space/comma
                        jitprintf($"{((i == 0) ? ' ' : ',')}{DspBlockNum(jumpTab[i], printEdgeLikelihoods, terseNext, nextBlock, ref printedBlockWidth)}");

                        var isDefault = jumpSwt.HasDefaultCase && (i == (jumpTab.Length - 1));
                        if (isDefault)
                        {
                            jitprintf("[def]");
                            printedBlockWidth += 5;
                        }

                        var isDominant = jumpSwt.HasDominantCase && (i == jumpSwt.DominantCase);
                        if (isDominant)
                        {
                            jitprintf("[dom]");
                            printedBlockWidth += 5;
                        }
                    }

                    if (printedBlockWidth < blockTargetFieldWidth)
                    {
                        jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)}");
                    }

                    jitprintf(" (switch)");
                }
                break;

                default:
                {
                    // Bad Kind
                    printedBlockWidth = 9; // kind
                    jitprintf($"{new string(' ', blockTargetFieldWidth - printedBlockWidth)} (ERROR )");
                    break;
                }
            }
        }

        jitprintf(" ");

        //
        // Display block EH region and type, including nesting indicator
        //

        if (block.hasTryIndex)
        {
            jitprintf($"T{block.TryIndex} ");
        }
        else
        {
            jitprintf("   ");
        }

        if (block.hasHndIndex)
        {
            jitprintf($"H{block.HndIndex} ");
        }
        else
        {
            jitprintf("   ");
        }

        var cnt = 0;

        switch (block.CatchType)
        {
            case BBCT_NONE:
            {
                break;
            }

            case BBCT_FAULT:
            {
                jitprintf("fault ");
                cnt += 6;
                break;
            }

            case BBCT_FINALLY:
            {
                jitprintf("finally ");
                cnt += 8;
                break;
            }

            case BBCT_FILTER:
            {
                jitprintf("filter ");
                cnt += 7;
                break;
            }

            case BBCT_FILTER_HANDLER:
            {
                jitprintf("filtHnd ");
                cnt += 8;
                break;
            }

            default:
            {
                jitprintf("catch ");
                cnt += 6;
                break;
            }
        }

        if (block.CatchType is not BBCT_NONE)
        {
            cnt += 2;
            jitprintf("{{ ");
            // brace matching editor workaround to compensate for the preceding line: }
        }

        if (bbIsTryBeg(block))
        {
            // Output a brace for every try region that this block opens

            foreach (var HBtab in new EHClauses(this))
            {
                if (HBtab.ebdTryBeg == block)
                {
                    cnt += 6;
                    jitprintf("try { ");
                    // brace matching editor workaround to compensate for the preceding line: }
                }
            }
        }

        foreach (var HBtab in new EHClauses(this))
        {
            if (HBtab.ebdTryLast == block)
            {
                cnt += 2;
                // brace matching editor workaround to compensate for the following line: {
                jitprintf("} ");
            }

            if (HBtab.ebdHndLast == block)
            {
                cnt += 2;
                // brace matching editor workaround to compensate for the following line: {
                jitprintf("} ");
            }

            if (HBtab.HasFilter && (block.Next == HBtab.ebdHndBeg))
            {
                cnt += 2;
                // brace matching editor workaround to compensate for the following line: {
                jitprintf("} ");
            }
        }

        while (cnt < 12)
        {
            cnt++;
            jitprintf(" ");
        }

        //
        // Display block flags
        //

        block.dspFlags();

        // Display OSR info
        //
        if (opts.IsOSR)
        {
            if (block == fgEntryBB)
            {
                jitprintf(" original-entry");
            }

            if (block == fgOSREntryBB)
            {
                jitprintf(" osr-entry");
            }
        }

        // Indicate if it's the merged return block.
        if (block == genReturnBB)
        {
            jitprintf(" merged-return");
        }

        jitprintf("\n");

        // Call `dspBlockNum()` to get the block number to print, and update `printedBlockWidth` with the width
        // of the generated string. Note that any computation using `printedBlockWidth` must be done after all
        // calls to this function.
        static string DspBlockNum(FlowEdge? e, bool printEdgeLikelihoods, bool terseNext, BasicBlock? nextBlock, ref int printedBlockWidth)
        {
            if (e is null)
            {
                return "NULL";
                printedBlockWidth += 4;
            }

            var b = e.DestinationBlock;
            var stringBuilder = new StringBuilder();

            if (b is null)
            {
                _ = stringBuilder.Append("NULL");
            }
            else if (terseNext && (b == nextBlock))
            {
                _ = stringBuilder.Append('*');
            }
            else
            {
                _ = stringBuilder.Append(FMT_BB(b.bbNum));
            }

            if (printEdgeLikelihoods && e.hasLikelihood)
            {
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $"({FMT_WT_NARROW(e.Likelihood)})");
            }

            printedBlockWidth += stringBuilder.Length;
            return stringBuilder.ToString();
        }
    }
#endif

    /// <summary>try to add new EH table entries</summary>
    /// <param name="XTnum">new entries will be added before this entry (use compHndBBtabCount to add at end)</param>
    /// <param name="count">number of entries to add</param>
    /// <param name="deferAdding">if true, don't actually add new entries, just check if they can be added; return <c>-1</c> if not.</param>
    /// <returns>The index of the entry or <c>-1</c> if the table cannot be expanded to hold the new entries</returns>
    /// <remarks>
    ///   <para>Note that this changes the size of the exception table.</para>
    ///   <para>All the blocks referring to the various index values are updated.</para>
    ///   <para>The new table entries are not filled in.</para>
    ///   <para>Note mid-table insertions can be expensive as they must walk all blocks to update block EH region indices.</para>
    ///   <para>If there are active ACDs, these are updated as needed.</para>
    ///   <para>Callers who are making room for cloned EH must take pains to find and clone these as well...</para>
    /// </remarks>
    public int fgTryAddEHTableEntries(ushort XTnum, ushort count = 1, bool deferAdding = false)
    {
        assert(!ehTableFinalized);

        var currentTable = compHndBBtab;
        var currentTableCount = compHndBBtabCount;

        var reallocate = false;
        var insert = (XTnum != currentTableCount);
        var newCount = currentTableCount + count;

        if (newCount > MAX_XCPTN_INDEX)
        {
            // We have run out of indices. Fail.
            return -1;
        }

        if (deferAdding)
        {
            // We can add count entries...
            //   we may not have allocated a table, so return a dummy non-null entry
            return 0;
        }

        if (newCount > currentTable.Length)
        {
            // We need to reallocate the table
            reallocate = true;
        }

        if (insert)
        {
            // Update all enclosing links that will get invalidated by inserting an entry at 'XTnum'
            foreach (ref var xtab in new EHClauses(this))
            {
                if ((xtab.ebdEnclosingTryIndex != EHblkDsc.NO_ENCLOSING_INDEX) && (xtab.ebdEnclosingTryIndex >= XTnum))
                {
                    // Update the enclosing scope link
                    xtab.ebdEnclosingTryIndex += count;
                }

                if ((xtab.ebdEnclosingHndIndex != EHblkDsc.NO_ENCLOSING_INDEX) && (xtab.ebdEnclosingHndIndex >= XTnum))
                {
                    // Update the enclosing scope link
                    xtab.ebdEnclosingHndIndex += count;
                }
            }

            // We need to update the BasicBlock bbTryIndex and bbHndIndex field for all blocks
            foreach (var blk in Blocks)
            {
                if (blk.hasTryIndex && (blk.TryIndex >= XTnum))
                {
                    blk.TryIndex += count;
                }

                if (blk.hasHndIndex && (blk.HndIndex >= XTnum))
                {
                    blk.HndIndex += count;
                }
            }

            // Update impacted ACDs
            if (fgHasAddCodeDscMap)
            {
                var map = fgAddCodeDscMap;
                var modified = new Stack<AddCodeDsc>();

                foreach (var add in map.Values)
                {
                    var isModified = false;
                    var oldKey = new AddCodeDscKey(add);

                    if (add.acdTryIndex > XTnum)
                    {
                        add.acdTryIndex += count;
                        isModified = true;
                    }

                    if (add.acdHndIndex > XTnum)
                    {
                        isModified = true;
                        add.acdHndIndex += count;
                    }

                    if (isModified)
                    {
                        _ = add.UpdateKeyDesignator(this);

                        var removed = map.Remove(oldKey);
                        assert(removed);

                        modified.Push(add);
                    }
                }

                while (modified.Count > 0)
                {
                    var add = modified.Pop();
                    var newKey = new AddCodeDscKey(add);
                    map[newKey] = add;

#if DEBUG
                    JITDUMP($"ACD{add.acdNum} updated\n");

                    if (verbose)
                    {
                        add.Dump();
                    }
#endif
                }
            }
        }

        // If necessary, increase the number of entries in the EH table
        if (reallocate)
        {
            // Increase the table size. Note that if the table isn't allocated
            // yet, such as when we add an EH region for synchronized methods that don't already have one,
            // we start at zero, so we need to make sure the new table has at least one entry.

            var newTableCount = int.Max(1, newCount);
            noway_assert(currentTable.Length < newTableCount); // check for overflow

            if (newTableCount > MAX_XCPTN_INDEX)
            {
                newTableCount = MAX_XCPTN_INDEX; // increase to the maximum size we allow
            }

            JITDUMP($"*********** fgTryAddEHTableEntries: increasing EH table size from {currentTable.Length} to {newTableCount}\n");
            var newTable = new EHblkDsc[newTableCount];

            // Move over the stuff before the new entries
            currentTable.AsSpan(0, XTnum).CopyTo(newTable);

            if (XTnum != currentTableCount)
            {
                // Move over the stuff after the new entry
                currentTable.AsSpan(XTnum).CopyTo(newTable.AsSpan(XTnum + count));
            }

            // Now set the new table as the table to use.
            compHndBBtab = newTable;
        }
        else if (XTnum != currentTableCount)
        {
            // Leave the elements before the new elements alone. Move the ones after it, to make space.
            currentTable.AsSpan(XTnum).CopyTo(currentTable.AsSpan(XTnum + count));
        }

        // Now the entry is there, but not filled in
        compHndBBtabCount = (ushort)(newCount);
        return XTnum + count - 1;
    }

    // TODO: Port fgAddInternal
    public PhaseStatus fgAddInternal() => PhaseStatus.MODIFIED_NOTHING;

#if SWIFT_SUPPORT
    // TODO: Port fgAddSwiftErrorReturns
    public PhaseStatus fgAddSwiftErrorReturns() => PhaseStatus.MODIFIED_NOTHING;
#endif

    // TODO: Port fgCanonicalizeFirstBB
    public PhaseStatus fgCanonicalizeFirstBB() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgCloneFinally
    public PhaseStatus fgCloneFinally() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgComputeBlockWeights
    public PhaseStatus fgComputeBlockWeights() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgCreateFunclets
    public PhaseStatus fgCreateFunclets() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgDetermineFirstColdBlock
    public PhaseStatus fgDetermineFirstColdBlock() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgDfsBlocksAndRemove
    public PhaseStatus fgDfsBlocksAndRemove() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgEarlyLiveness
    public PhaseStatus fgEarlyLiveness() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgExpandHelper
    public PhaseStatus fgExpandHelper(bool skipRarelyRunBlocks) => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgExpandQmarkNodes
    public PhaseStatus fgExpandQmarkNodes(bool early) => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgExpandRuntimeLookups
    public PhaseStatus fgExpandRuntimeLookups() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgExpandStackArrayAllocations
    public PhaseStatus fgExpandStackArrayAllocations() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgExpandStaticInit
    public PhaseStatus fgExpandStaticInit() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgExpandThreadLocalAccess
    public PhaseStatus fgExpandThreadLocalAccess() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgFindOperOrder
    public PhaseStatus fgFindOperOrder() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgHeadTailMerge
    public PhaseStatus fgHeadTailMerge(bool early) => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgImport
    public PhaseStatus fgImport() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgInline
    public PhaseStatus fgInline() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgInsertGCPolls
    public PhaseStatus fgInsertGCPolls() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgLateCastExpansion
    public PhaseStatus fgLateCastExpansion() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgMergeFinallyChains
    public PhaseStatus fgMergeFinallyChains() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgMorphArrayOps
    public PhaseStatus fgMorphArrayOps() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgMorphBlocks
    public PhaseStatus fgMorphBlocks() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgMorphInit
    public PhaseStatus fgMorphInit() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgPostImportationCleanup
    public PhaseStatus fgPostImportationCleanup() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgRemoveEmptyFinally
    public PhaseStatus fgRemoveEmptyFinally() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgRemoveEmptyTry
    public PhaseStatus fgRemoveEmptyTry() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgRemoveEmptyTryCatchOrTryFault
    public PhaseStatus fgRemoveEmptyTryCatchOrTryFault() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgRepairProfile
    public PhaseStatus fgRepairProfile() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgResolveGDVs
    public PhaseStatus fgResolveGDVs() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgSearchImprovedLayout
    public PhaseStatus fgSearchImprovedLayout() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgSetBlockOrder
    public PhaseStatus fgSetBlockOrder() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgSsaBuild
    public PhaseStatus fgSsaBuild() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgTailMergeThrows
    public PhaseStatus fgTailMergeThrows() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgTransformIndirectCalls
    public PhaseStatus fgTransformIndirectCalls() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgTransformPatchpoints
    public PhaseStatus fgTransformPatchpoints() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgUpdateFlowGraphPhase
    public PhaseStatus fgUpdateFlowGraphPhase() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgValueNumber
    public PhaseStatus fgValueNumber() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgVNBasedIntrinsicExpansion
    public PhaseStatus fgVNBasedIntrinsicExpansion() => PhaseStatus.MODIFIED_NOTHING;

#if TARGET_WASM
    // TODO: Port fgWasmEhFlow
    public PhaseStatus fgWasmEhFlow() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgWasmControlFlow
    public PhaseStatus fgWasmControlFlow() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgWasmTransformSccs
    public PhaseStatus fgWasmTransformSccs() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgWasmVirtualIP
    public PhaseStatus fgWasmVirtualIP() => PhaseStatus.MODIFIED_NOTHING;
#endif

    // TODO: Port fgComputeDominators
    protected PhaseStatus fgComputeDominators() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgIncorporateProfileData
    protected PhaseStatus fgIncorporateProfileData() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgInstrumentMethod
    protected PhaseStatus fgInstrumentMethod() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgInstrumentMethodCore
    protected PhaseStatus fgInstrumentMethodCore() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgPrepareToInstrumentMethod
    protected PhaseStatus fgPrepareToInstrumentMethod() => PhaseStatus.MODIFIED_NOTHING;

#if DEBUG
    protected bool fgStressBBProf() => (JitConfig[ConfigInteger.JitStressBBProf] != 0) || compStressCompile(STRESS_BB_PROFILE, 15);
#else
    protected bool fgStressBBProf() => false;
#endif

    /// <summary>Switch the opt level from tier 0 to optimized</summary>
    /// <param name="reason">reason why opt level was switched</param>
    /// <remarks>This method is to be called at some point before <see cref="compSetOptimizationLevel" /> to switch the opt level to optimized based on information gathered in early phases.</remarks>
    protected unsafe void fgSwitchToOptimized(string reason)
    {
        assert(fgCanSwitchToOptimized);

        // Switch to optimized and re-init options
        JITDUMP($"****\n**** JIT Tier0 jit request switching to Tier1 because: {reason}\n****\n");
        assert(opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0));
        opts.jitFlags->Clear(JitFlags.JIT_FLAG_TIER0);
        opts.jitFlags->Clear(JitFlags.JIT_FLAG_BBINSTR);
        opts.jitFlags->Clear(JitFlags.JIT_FLAG_BBINSTR_IF_LOOPS);
        opts.jitFlags->Clear(JitFlags.JIT_FLAG_OSR);
        opts.jitFlags->Set(JitFlags.JIT_FLAG_BBOPT);

        // Leave a note for jit diagnostics
        compSwitchedToOptimized = true;

        compInitOptions(opts.jitFlags);

        // Notify the VM of the change
        info.compCompHnd->setMethodAttribs(info.compMethodHnd, CORINFO_FLG_SWITCHED_TO_OPTIMIZED);
    }

    // TODO: Port fgForwardSub
    private PhaseStatus fgForwardSub() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgLocalMorph
    private PhaseStatus fgLocalMorph() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgMarkImplicitByRefCopyOmissionCandidates
    private PhaseStatus fgMarkImplicitByRefCopyOmissionCandidates() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgOptimizeMaskConversions
    private PhaseStatus fgOptimizeMaskConversions() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgPromoteStructs
    private PhaseStatus fgPromoteStructs() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port fgRetypeImplicitByRefArgs
    private PhaseStatus fgRetypeImplicitByRefArgs() => PhaseStatus.MODIFIED_NOTHING;

#nullable disable
    /// <summary>Searches newTarget->bbPreds for where to insert an edge from blockPred.</summary>
    /// <param name="blockPred">The block we want to make a predecessor of newTarget (it could already be one).</param>
    /// <param name="newTarget">The block whose pred list we will search.</param>
    /// <returns>Returns a pointer to the next pointer of an edge in newTarget's pred list. A new edge from blockPred to newTarget can be inserted here without disrupting bbPreds' sorting invariant.</returns>
    private ref FlowEdge fgGetPredInsertPoint(BasicBlock blockPred, BasicBlock newTarget)
    {
        assert(fgPredsComputed);
        ref var listp = ref newTarget.bbPreds;

        // Search pred list for insertion point
        while ((listp is not null) && (listp.SourceBlock.bbID < blockPred.bbID))
        {
            listp = ref listp.NextPredEdgeRef;
        }
        return ref listp;
    }
#nullable restore

    [InlineArray((int)(MemoryKindCount))]
    public struct fgCurMemoryVNInlineArray
    {
        public ValueNum e0;
    }

    [InlineArray((int)(TYP_COUNT))]
    private struct fgBigOffsetMorphingTempsInlineArray
    {
        public int e0;
    }
}
