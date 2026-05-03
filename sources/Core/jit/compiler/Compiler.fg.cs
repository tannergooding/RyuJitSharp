// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
    public uint fgBBcount;

#if DEBUG
    /// <summary>ordered vector of BBs</summary>
    public List<BasicBlock>? fgBBOrder;
#endif

    /// <summary>Used as a quick check for whether phases downstream of loop finding should look for natural loops.</summary>
    /// <remarks>true if there may be any natural loops in the flow graph, so try to find them; otherwise, false</remarks>
    public bool fgMightHaveNaturalLoops;

    /// <summary>The max bbNum that has been assigned to basic blocks</summary>
    public uint fgBBNumMax;

#if TARGET_WASM
    public List<WasmInterval>? fgWasmIntervals;

    public BasicBlock[]? fgIndexToBlockMap;

    public bool fgWasmHasCatchResumptions;

    public FlowGraphTryRegions? fgTryRegions;

    public EHClauseInfo fgWasmEHInfo;
#endif

    public bool fgBBVarSetsInited;

    /// <summary>Track how many artificial ref counts we've added to fgEntryBB (for OSR)</summary>
    public uint fgEntryBBExtraRefs;

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
    //   The first local is kept in Statement::GetTreeList() and the last
    //   local in Statement::GetTreeListEnd(). fgSequenceLocals can be used
    //   to (re-)sequence a statement into this form, and
    //   Statement::LocalsTreeList for range-based iteration. The order must
    //   match tree order.
    //
    // - fgSetBlockOrder threads all nodes. This is kept valid until LIR form.
    //   In this form the first node is given by Statement::GetTreeList and the
    //   last node is given by Statement::GetRootNode(). fgSetStmtSeq can be used
    //   to (re-)sequence a statement into this form, and Statement::TreeList for
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
    public uint fgReturnCount;

    public uint fgThrowCount;

    /// <summary>Number of times fgSsaBuild has been run.</summary>
    public uint fgSsaPassesCompleted;

    /// <summary>True if SSA info is valid and can be cross-checked versus IR</summary>
    public bool fgSsaValid;

    /// <summary>Number of times fgValueNumber has been run.</summary>
    public uint fgVNPassesCompleted;

    /// <summary>These are the current value number for the memory implicit variables while doing value numbering.</summary>
    /// <remarks>These are the value numbers under the "liberal" interpretation of memory values; the "conservative" interpretation needs no VN, since every access of memory yields an unknown value.</remarks>
    public fgCurMemoryVNInlineArray fgCurMemoryVN;

#if DEBUG
    public static unsafe fgWalkPreFn fgStress64RsltMulCB;
#endif

    /// <summary>Table of pointers to the BBs</summary>
    protected BasicBlock[]? fgBBs;

    protected Instrumentor? fgCountInstrumentor;

    protected Instrumentor? fgHistogramInstrumentor;

    protected Instrumentor? fgValueInstrumentor;

    public string? fgPgoFailReason;

    public bool fgPgoDisabled;

    public ICorJitInfo.PgoSource fgPgoSource;

    public unsafe ICorJitInfo.PgoInstrumentationSchema* fgPgoSchema;

    public unsafe byte* fgPgoData;

    public uint fgPgoSchemaCount;

    public int fgPgoQueryResult;

    public uint fgNumProfileRuns;

    public uint fgPgoBlockCounts;

    public uint fgPgoEdgeCounts;

    public uint fgPgoClassProfiles;

    public uint fgPgoMethodProfiles;

    public uint fgPgoInlineePgo;

    public uint fgPgoInlineeNoPgo;

    public uint fgPgoInlineeNoPgoSingleBlock;

    public bool fgPgoHaveWeights;

    public bool fgPgoSynthesized;

    public bool fgPgoDynamic;

    public bool fgPgoConsistent;

    public bool fgPgoSingleEdge;

#if DEBUG
    public bool fgPgoDeferredInconsistency;
#endif

    private hashBv? fgAvailableOutgoingArgTemps;

    private Stack<uint>? fgUsedSharedTemps;

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
            var result = opts.jitFlags->IsSet(JitFlag.JIT_FLAG_TIER0)
                     && !opts.jitFlags->IsSet(JitFlag.JIT_FLAG_MIN_OPT)
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

    /// <summary>Dump all basic blocks in the function.</summary>
    /// <param name="dumpTrees">if true, also dump the trees in each block</param>
    public void fgDispBasicBlocks(bool dumpTrees = false)
        => fgDispBasicBlocks(fgFirstBB, lastBlock: null, dumpTrees);

    /// <summary>Dump blocks from "firstBlock" to "lastBlock".</summary>
    /// <param name="firstBlock">the first block to dump</param>
    /// <param name="lastBlock">the last block to dump (or nullptr for all remaining blocks)</param>
    /// <param name="dumpTrees">if true, also dump the trees in each block</param>
    public void fgDispBasicBlocks(BasicBlock? firstBlock, BasicBlock? lastBlock, bool dumpTrees)
    {
        // TODO: Port Compiler.fgDispBasicBlocks
    }

    public void fgFindBasicBlocks()
    {
        // TODO: Port Compiler.fgFindBasicBlocks
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

#if DEBUG
    public void fgTableDispBasicBlock(BasicBlock block, BasicBlock? nextBlock = null, bool printEdgeLikelihoods = true, int blockTargetFieldWidth = 21, int ibcColWidth = 0)
    {
        // TODO: Port Compiler.fgTableDispBasicBlock
    }
#endif

    /// <summary>Switch the opt level from tier 0 to optimized</summary>
    /// <param name="reason">reason why opt level was switched</param>
    /// <remarks>This method is to be called at some point before <see cref="compSetOptimizationLevel" /> to switch the opt level to optimized based on information gathered in early phases.</remarks>
    protected unsafe void fgSwitchToOptimized(string reason)
    {
        assert(fgCanSwitchToOptimized);

        // Switch to optimized and re-init options
        JITDUMP($"****\n**** JIT Tier0 jit request switching to Tier1 because: {reason}\n****\n");
        assert(opts.jitFlags->IsSet(JitFlag.JIT_FLAG_TIER0));
        opts.jitFlags->Clear(JitFlag.JIT_FLAG_TIER0);
        opts.jitFlags->Clear(JitFlag.JIT_FLAG_BBINSTR);
        opts.jitFlags->Clear(JitFlag.JIT_FLAG_BBINSTR_IF_LOOPS);
        opts.jitFlags->Clear(JitFlag.JIT_FLAG_OSR);
        opts.jitFlags->Set(JitFlag.JIT_FLAG_BBOPT);

        // Leave a note for jit diagnostics
        compSwitchedToOptimized = true;

        compInitOptions(*opts.jitFlags);

        // Notify the VM of the change
        info.compCompHnd->setMethodAttribs(info.compMethodHnd, CORINFO_FLG_SWITCHED_TO_OPTIMIZED);
    }

    [InlineArray((int)(MemoryKindCount))]
    public struct fgCurMemoryVNInlineArray
    {
        public ValueNum e0;
    }

    [InlineArray((int)(TYP_COUNT))]
    private struct fgBigOffsetMorphingTempsInlineArray
    {
        public uint e0;
    }
}
