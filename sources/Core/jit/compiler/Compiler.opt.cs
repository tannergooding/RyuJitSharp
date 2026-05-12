// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Do we require loops to be in canonical form?</summary>
    /// <remarks>
    ///   <para>The canonical form ensures that:</para>
    ///   <list type="number">
    ///     <item>All loops have preheaders (single entry blocks that always enter the loop)</item>
    ///     <item>All loop exits where bbIsHandlerBeg(exit) is false have only loop predecessors.</item>
    ///   </list>
    /// </remarks>
    public bool optLoopsCanonical;

    /// <summary>number of calls made in the method</summary>
    protected int optCallCount;

    /// <summary>number of virtual, interface and indirect calls made in the method</summary>
    protected int optIndirectCallCount;

    /// <summary>number of Pinvoke/Native calls made in the method</summary>
    protected int optNativeCallCount;

    /// <summary>number of fast tail calls made in the method</summary>
    protected int optFastTailCallCount;

    /// <summary>number of indirect (see above) fast tail calls made in the method</summary>
    protected int optIndirectFastTailCallCount;

    protected static readonly nint s_optCSEhashSizeInitial;

    protected static readonly nint s_optCSEhashGrowthFactor;

    protected static readonly nint s_optCSEhashBucketSize;

    /// <summary>The current size of hashtable</summary>
    protected nint optCSEhashSize;

    /// <summary>Number of entries in hashtable</summary>
    protected nint optCSEhashCount;

    /// <summary>Number of entries before resize</summary>
    protected nint optCSEhashMaxCountBeforeResize;

    protected unsafe CSEdsc** optCSEhash;

    protected unsafe CSEdsc** optCSEtab;

    // Treewalk helper for optCSE_DefMask and optCSE_UseMask
    // TODO: Port Compiler.optCSE_MaskHelper
    // protected static unsafe fgWalkPreFn optCSE_MaskHelper;

    /// <summary>True when we have found a duplicate CSE tree</summary>
    protected bool optDoCSE;

    /// <summary>True when we are executing the optOptimizeValnumCSEs() phase</summary>
    protected bool optValnumCSE_phase;

    /// <summary>Count of CSE candidates</summary>
    protected int optCSECandidateCount;

    /// <summary>The first local variable number that is a CSE</summary>
    protected int optCSEstart = BAD_VAR_NUM;

    /// <summary>The number of CSEs attempted so far.</summary>
    protected int optCSEattempt;

    /// <summary>The total count of CSEs introduced.</summary>
    protected int optCSEcount;

    /// <summary>Number of CSE trees unmarked</summary>
    protected int optCSEunmarks;

    /// <summary>The weight of the current block when we are doing PerformCSE</summary>
    protected weight_t optCSEweight;

    /// <summary>CSE Heuristic to use for this method</summary>
    protected CSE_HeuristicCommon? optCSEheuristic;

    public int optMethodFlags;

    public int optNoReturnCallCount;

    /// <summary>Recursion bound controls how far we can go backwards tracking for a SSA value.</summary>
    /// <remarks>No throughput diff was found with backward walk bound between 3-8.</remarks>
    public const int optEarlyPropRecurBound = 5;

    public BitVecTraits? optReachableBitVecTraits;

    public unsafe BitVec? optReachableBitVec;

    // TODO: Port Compiler.optVNAssertionPropCurStmtVisitor
    // protected static unsafe fgWalkPreFn optVNAssertionPropCurStmtVisitor;

    /// <summary>indicates that we are performing local assertion prop</summary>
    protected bool optLocalAssertionProp;

    /// <summary>set to true if we modified the trees</summary>
    protected bool optAssertionPropagated;

    protected bool optAssertionPropagatedCurrentStmt;

#if DEBUG
    protected GenTree? optAssertionPropCurrentTree;
#endif

    protected unsafe AssertionIndex* optComplementaryAssertionMap;

    /// <summary>table that holds dependent assertions (assertions using the value of a local var) for each local var</summary>
    protected List<Pointer<nint>>? optAssertionDep;

    /// <summary>table that holds info about assertions</summary>
    protected AssertionDsc? optAssertionTabPrivate;

    protected VNSet? optAssertionVNsMap;

    /// <summary>// total number of assertions in the assertion table</summary>
    protected AssertionIndex optAssertionCount;

    protected AssertionIndex optMaxAssertionCount;

    protected bool optCrossBlockLocalAssertionProp;

    protected int optAssertionOverflow;

    protected bool optCanPropLclVar;

    protected bool optCanPropEqual;

    protected bool optCanPropNonNull;

    protected bool optCanPropBndsChk;

    protected bool optCanPropSubRange;

    protected RangeCheck? optRangeCheck;

    // TODO: Port optOptimizeCSEs
    public void optOptimizeCSEs() { }

    // TODO: Port optOptimizeBools
    public PhaseStatus optOptimizeBools() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optRecognizeAndOptimizeSwitchJumps
    public PhaseStatus optRecognizeAndOptimizeSwitchJumps() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optInvertLoops
    public PhaseStatus optInvertLoops() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optOptimizeFlow
    public PhaseStatus optOptimizeFlow() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optOptimizePreLayout
    public PhaseStatus optOptimizePreLayout() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optOptimizePostLayout
    public PhaseStatus optOptimizePostLayout() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optSetBlockWeights
    public PhaseStatus optSetBlockWeights() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optFindLoopsPhase
    public PhaseStatus optFindLoopsPhase() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optCloneLoops
    public PhaseStatus optCloneLoops() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optRangeCheckCloning
    public PhaseStatus optRangeCheckCloning() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optUnrollLoops
    public PhaseStatus optUnrollLoops() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optIfConversion
    public PhaseStatus optIfConversion() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optOptimizeValnumCSEs
    public PhaseStatus optOptimizeValnumCSEs() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optVnCopyProp
    public PhaseStatus optVnCopyProp() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optVNBasedDeadStoreRemoval
    public PhaseStatus optVNBasedDeadStoreRemoval() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optEarlyProp
    public PhaseStatus optEarlyProp() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optInductionVariables
    public PhaseStatus optInductionVariables() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optRedundantBranches
    public PhaseStatus optRedundantBranches() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optAssertionPropMain
    public PhaseStatus optAssertionPropMain() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port optHoistLoopCode
    protected PhaseStatus optHoistLoopCode() => PhaseStatus.MODIFIED_NOTHING;
}
