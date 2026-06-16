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
    protected List<ASSERT_TP>? optAssertionDep;

    /// <summary>table that holds info about assertions</summary>
    protected AssertionDsc? optAssertionTabPrivate;

    protected VNSet? optAssertionVNsMap;

    /// <summary>// total number of assertions in the assertion table</summary>
    protected AssertionIndex optAssertionCount;

    protected AssertionIndex optMaxAssertionCount;

    protected bool optCrossBlockLocalAssertionProp;

    protected int optAssertionOverflow;

    protected bool optCanPropLclVar;

    protected RangeCheck? optRangeCheck;

    public nint optGetArrayRefScaleAndIndex(GenTreeOp mul, out GenTree index, bool bRngChk)
    {
        assert(mul.Oper is GT_MUL or GT_LSH);
        assert(mul.AsOp().Op2.Oper.IsCnsIntOrI);

        var scale = mul.Op2.AsIntConCommon().IconValue;

        if (mul.Oper is GT_LSH)
        {
            scale = 1 << (int)(scale);
        }

        index = mul.Op1;

        if (index.Oper is GT_MUL)
        {
            var maybeIntCon = index.AsOp().Op2;

            if (maybeIntCon.Oper.IsCnsIntOrI)
            {
                // case of two cascading multiplications for constant int (e.g.  * 20 morphed to * 5 * 4):
                // When index->gtOper is GT_MUL and index->AsOp()->gtOp2->gtOper is GT_CNS_INT (i.e. * 5),
                //     we can bump up the scale from 4 to 5*4, and then change index to index->AsOp()->gtOp1.
                // Otherwise, we cannot optimize it. We will simply keep the original scale and index.
                scale *= maybeIntCon.AsIntCon().IconValue;
                index = index.AsOp().Op1;
            }
        }

        assert(!bRngChk || (index.Oper is not GT_COMMA));
        return scale;
    }

    /// <summary>Determine if the execution order of two nodes can be swapped.</summary>
    /// <param name="op1">The first node</param>
    /// <param name="op2">The second node</param>
    /// <returns>Return true iff it safe to swap the execution order of 'op1' and 'op2', considering only the locations of the CSE defs and uses.</returns>
    /// <remarks>'op1' currently occurse before 'op2' in the execution order.</remarks>
    public bool optCSE_canSwap(GenTree op1, GenTree op2)
    {
        // the default result unless proven otherwise.
        var canSwap = true;

        // If we haven't setup cseMaskTraits, do it now
        cseMaskTraits ??= new BitVecTraits(this, optCSECandidateCount);

        optCSE_GetMaskData(op1, out var op1MaskData);
        optCSE_GetMaskData(op2, out var op2MaskData);

        // We cannot swap if op1 contains a CSE def that is used by op2
        if (!BitVecOps.IsEmptyIntersection(cseMaskTraits, op1MaskData.CSE_defMask, op2MaskData.CSE_useMask))
        {
            canSwap = false;
        }
        else
        {
            // We also cannot swap if op2 contains a CSE def that is used by op1.
            if (!BitVecOps.IsEmptyIntersection(cseMaskTraits, op2MaskData.CSE_defMask, op1MaskData.CSE_useMask))
            {
                canSwap = false;
            }
        }

        return canSwap;
    }

    /// <summary>This functions walks all the node for an given tree and return the mask of CSE defs and uses for the tree</summary>
    /// <param name="tree"></param>
    /// <param name="maskData"></param>
    public void optCSE_GetMaskData(GenTree tree, out optCSE_MaskData maskData)
    {
        assert(cseMaskTraits is not null);

        maskData = new optCSE_MaskData {
            CSE_defMask = BitVecOps.MakeEmpty(cseMaskTraits),
            CSE_useMask = BitVecOps.MakeEmpty(cseMaskTraits),
        };

        var walker = new MaskDataWalker(this, ref maskData);
        _ = walker.WalkTree(ref tree, null);
    }

    // TODO: Port phase - optOptimizeCSEs
    public void optOptimizeCSEs() { }

    // TODO: Port phase - optOptimizeBools
    public PhaseStatus optOptimizeBools() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optRecognizeAndOptimizeSwitchJumps
    public PhaseStatus optRecognizeAndOptimizeSwitchJumps() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optInvertLoops
    public PhaseStatus optInvertLoops() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optOptimizeFlow
    public PhaseStatus optOptimizeFlow() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optOptimizePreLayout
    public PhaseStatus optOptimizePreLayout() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optOptimizePostLayout
    public PhaseStatus optOptimizePostLayout() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optSetBlockWeights
    public PhaseStatus optSetBlockWeights() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optFindLoopsPhase
    public PhaseStatus optFindLoopsPhase() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optCloneLoops
    public PhaseStatus optCloneLoops() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optRangeCheckCloning
    public PhaseStatus optRangeCheckCloning() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optBoundsCheckCoalesce
    public PhaseStatus optBoundsCheckCoalesce() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optUnrollLoops
    public PhaseStatus optUnrollLoops() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optIfConversion
    public PhaseStatus optIfConversion() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optOptimizeValnumCSEs
    public PhaseStatus optOptimizeValnumCSEs() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optVnCopyProp
    public PhaseStatus optVnCopyProp() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optVNBasedDeadStoreRemoval
    public PhaseStatus optVNBasedDeadStoreRemoval() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optEarlyProp
    public PhaseStatus optEarlyProp() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optInductionVariables
    public PhaseStatus optInductionVariables() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optRedundantBranches
    public PhaseStatus optRedundantBranches() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optAssertionPropMain
    public PhaseStatus optAssertionPropMain() => PhaseStatus.MODIFIED_NOTHING;

    // TODO: Port phase - optHoistLoopCode
    protected PhaseStatus optHoistLoopCode() => PhaseStatus.MODIFIED_NOTHING;
}
