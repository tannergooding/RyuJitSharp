// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>describes a basic block in the flowgraph.</summary>
/// <remarks>Note that this type derives from LIR.Range in order to make the LIR utilities that are polymorphic over basic block and scratch ranges faster and simpler.</remarks>
public sealed partial class BasicBlock : LIR.Range
{
    /// <summary>next BB in ascending PC offset order</summary>
    private BasicBlock? _next;

    private BasicBlock? _prev;

    private BBKinds _kind;

    private Statement? _stmtList;

    /// <summary>catch type: class token of handler, or one of BBCT_*. Only set on first block of catch handler.</summary>
    private bbCatchType bbCatchType;

    /// <summary>verifier tracked state of all entries in stack.</summary>
    private EntryState? bbEntryState;

    /// <summary>PC offset (temporary only)</summary>
    private int bbTargetOffs;

    /// <summary>Describes the jump target(s) of this block</summary>
    private object? _anonymous1;

    /// <summary>successor edge for block kinds with only one successor (BBJ_ALWAYS, etc)</summary>
    public FlowEdge? bbTargetEdge
    {
        get
        {
            return _anonymous1 as FlowEdge;
        }

        set
        {
            _anonymous1 = value;
        }
    }

    /// <summary>BBJ_COND successor edge when its condition is true (alias for bbTargetEdge)</summary>
    internal FlowEdge? bbTrueEdge
    {
        get
        {
            return _anonymous1 as FlowEdge;
        }

        set
        {
            _anonymous1 = value;
        }
    }

    public BBswtDesc bbSwtTargets
    {
        get
        {
            assert(Debugger.IsAttached || (Kind is BBJ_SWITCH));
            return (_anonymous1 as BBswtDesc)!;
        }

        set
        {
            _anonymous1 = value;
        }
    }

    /// <summary>BBJ_EHFINALLYRET descriptor</summary>
    public BBJumpTable? bbEhfTargets
    {
        get
        {
            return _anonymous1 as BBJumpTable;
        }

        set
        {
            _anonymous1 = value;
        }
    }

    /// <summary>Successor edge of a BBJ_COND block if bbTrueEdge is not taken</summary>
    internal FlowEdge? bbFalseEdge;

    private BasicBlockFlags bbFlags;

    /// <summary>the block's number</summary>
    public int bbNum;

    /// <summary>number of blocks that can reach here, either by fall-through or a branch.</summary>
    /// <remarks>If this falls to zero, the block is unreachable.</remarks>
    public int bbRefs;

    /// <summary>The dynamic execution weight of this block</summary>
    public weight_t bbWeight;

    private int _anonymous2;

    /// <summary>base# for input stack temps</summary>
    public int bbStkTempsIn
    {
        get
        {
            return _anonymous2;
        }

        set
        {
            _anonymous2 = value;
        }
    }

    /// <summary>schema index for count instrumentation</summary>
    public int bbCountSchemaIndex
    {
        get
        {
            return _anonymous2;
        }

        set
        {
            _anonymous2 = value;
        }
    }

    private int _anonymous3;

    /// <summary>base# for output stack temps</summary>
    public int bbStkTempsOut
    {
        get
        {
            return _anonymous2;
        }

        set
        {
            _anonymous2 = value;
        }
    }

    /// <summary>schema index for histogram instrumentation</summary>
    public int bbHistogramSchemaIndex
    {
        get
        {
            return _anonymous2;
        }

        set
        {
            _anonymous2 = value;
        }
    }

    /// <summary>index, into the compHndBBtab table, of innermost 'try' clause containing the BB (used for raising exceptions).</summary>
    /// <remarks>Stored as index + 1; 0 means "no try index".</remarks>
    public ushort bbTryIndex;

    /// <summary>index, into the compHndBBtab table, of innermost handler (filter, catch, fault/finally) containing the BB.</summary>
    /// <remarks>Stored as index + 1; 0 means "no handler index".</remarks>
    public ushort bbHndIndex;

    /// <summary>Basic block predecessor lists.</summary>
    /// <remarks>
    ///   <para>Predecessor lists are created by fgLinkBasicBlocks(), stored in 'bbPreds', and then maintained throughout compilation.</para>
    ///   <para>'fgPredsComputed' will be 'true' after the predecessor lists are created.</para>
    /// </remarks>
    public FlowEdge? bbPreds;

    private object? _anonymous4;

    /// <summary>Represent the closest dominator to this block (called the Immediate Dominator) used to compute the dominance tree.</summary>
    public BasicBlock? bbIDom
    {
        get
        {
            return _anonymous4 as BasicBlock;
        }

        set
        {
            _anonymous4 = value;
        }
    }

    /// <summary>Used early on by fgLinkBasicBlock/fgAddRefPred</summary>
    public FlowEdge? bbLastPred
    {
        get
        {
            return _anonymous4 as FlowEdge;
        }

        set
        {
            _anonymous4 = value;
        }
    }

    /// <summary>Used early on by fgInstrument</summary>
    public unsafe void* bbSparseProbeList;

    /// <summary>Used early on by fgIncorporateEdgeCounts</summary>
    public unsafe void* bbSparseCountInfo;

    /// <summary>the block's  preorder number in the graph [0...postOrderCount)</summary>
    public int bbPreorderNum;

    /// <summary>the block's postorder number in the graph [0...postOrderCount)</summary>
    public int bbPostorderNum;

    /// <summary>IL offset of the beginning of the block</summary>
    public IL_OFFSET bbCodeOffs;

    /// <summary>IL offset past the end of the block.</summary>
    /// <remarks>
    ///   <para>Thus, the [bbCodeOffs..bbCodeOffsEnd) range is not inclusive of the end offset.</para>
    ///   <para>The count of IL bytes in the block is bbCodeOffsEnd - bbCodeOffs, assuming neither are BAD_IL_OFFSET.</para>
    /// </remarks>
    public IL_OFFSET bbCodeOffsEnd;

    /// <summary>variables used     by block (before a definition)</summary>
    public VARSET_TP bbVarUse = [];

    /// <summary>variables assigned by block (before a use)</summary>
    public VARSET_TP bbVarDef = [];

    /// <summary>variables live on entry</summary>
    public VARSET_TP bbLiveIn = [];

    /// <summary>variables live on exit</summary>
    public VARSET_TP bbLiveOut = [];

    // Use, def, live in/out information for the implicit memory variable.
    private MemoryKindSet _bitfield;

    /// <summary>must be set for any MemoryKinds this block references</summary>
    public MemoryKind bbMemoryUse
    {
        get
        {
            return (MemoryKind)(_bitfield & 1);
        }

        set
        {
            _bitfield = (_bitfield & ~1) | ((int)(value) & 1);
        }
    }

    /// <summary>must be set for any MemoryKinds this block mutates</summary>
    public MemoryKind bbMemoryDef
    {
        get
        {
            return (MemoryKind)((_bitfield >>> 1) & 1);
        }

        set
        {
            _bitfield = (_bitfield & ~(1 << 1)) | (((int)(value) & 1) << 1);
        }
    }

    public MemoryKind bbMemoryLiveIn
    {
        get
        {
            return (MemoryKind)((_bitfield >>> 2) & 1);
        }

        set
        {
            _bitfield = (_bitfield & ~(1 << 2)) | (((int)(value) & 1) << 2);
        }
    }

    public MemoryKind bbMemoryLiveOut
    {
        get
        {
            return (MemoryKind)((_bitfield >>> 3) & 1);
        }

        set
        {
            _bitfield = (_bitfield & ~(1 << 3)) | (((int)(value) & 1) << 3);
        }
    }

    /// <summary>If true, at some point the block does an operation that leaves memory in an unknown state. (E.g., unanalyzed call, store through unknown pointer...)</summary>
    public MemoryKind bbMemoryHavoc
    {
        get
        {
            return (MemoryKind)((_bitfield >>> 4) & 1);
        }

        set
        {
            _bitfield = (_bitfield & ~(1 << 4)) | (((int)(value) & 1) << 4);
        }
    }

    /// <summary>Special value (0x1, FWIW) to represent a to-be-filled in Phi arg list for Heap.</summary>
    public static MemoryPhiArg? EmptyMemoryPhiDef;

    /// <summary>If the "in" Heap SSA var is not a phi definition, this value is null.</summary>
    /// <remarks>Otherwise, it is either the special value EmptyMemoryPhiDefn, to indicate that Heap needs a phi definition on entry, or else it is the linked list of the phi arguments.</remarks>
    public bbMemorySsaPhiFuncInlineArray bbMemorySsaPhiFunc;

    /// <summary>The SSA # of memory on entry to the block.</summary>
    public bbMemorySsaNumInInlineArray bbMemorySsaNumIn;

    /// <summary>The SSA # of memory on exit from the block.</summary>
    public bbMemorySsaNumOutInlineArray bbMemorySsaNumOut;

    // The following are the standard bit sets for dataflow analysis.
    // We perform CSE and range-checks at the same time and assertion propagation separately, thus we can union them since the two operations are completely disjunct.

    private unsafe nint* _anonymous5;

    /// <summary>CSEs computed by block</summary>
    public unsafe EXPSET_TP bbCseGen
    {
        get
        {
            return _anonymous5;
        }

        set
        {
            _anonymous5 = value;
        }
    }

    /// <summary>assertions created by block (global prop)</summary>
    public unsafe ASSERT_TP bbAssertionGen
    {
        get
        {
            return _anonymous5;
        }

        set
        {
            _anonymous5 = value;
        }
    }

    /// <summary>assertions available on exit along true/jump edge (BBJ_COND, local prop)</summary>
    public unsafe ASSERT_TP bbAssertionOutIfTrue
    {
        get
        {
            return _anonymous5;
        }

        set
        {
            _anonymous5 = value;
        }
    }

    private unsafe nint* _anonymous6;

    /// <summary>CSEs available on entry</summary>
    public unsafe EXPSET_TP bbCseIn
    {
        get
        {
            return _anonymous6;
        }

        set
        {
            _anonymous6 = value;
        }
    }

    /// <summary>assertions available on entry (global prop)</summary>
    public unsafe ASSERT_TP bbAssertionIn
    {
        get
        {
            return _anonymous6;
        }

        set
        {
            _anonymous6 = value;
        }
    }

    private unsafe nint* _anonymous7;

    /// <summary>CSEs available on exit</summary>
    public unsafe EXPSET_TP bbCseOut
    {
        get
        {
            return _anonymous7;
        }

        set
        {
            _anonymous7 = value;
        }
    }

    /// <summary>assertions available on exit (global prop, local prop &amp; !BBJ_COND)</summary>
    public unsafe ASSERT_TP bbAssertionOut
    {
        get
        {
            return _anonymous7;
        }

        set
        {
            _anonymous7 = value;
        }
    }

    /// <summary>assertions available on exit along false/next edge (BBJ_COND, local prop)</summary>
    public unsafe ASSERT_TP bbAssertionOutIfFalse
    {
        get
        {
            return _anonymous7;
        }

        set
        {
            _anonymous7 = value;
        }
    }

    public unsafe void* bbEmitCookie;

#if MEASURE_BLOCK_SIZE
    public static nint s_Size;

    public static nint s_Count;
#endif

#if DEBUG
    /// <summary>Native stack depth on entry (for throw-blocks)</summary>
    public int bbTgtStkDepth;

    /// <summary>The max # of tree nodes in any BB</summary>
    public static int s_nMaxTrees;

    /// <summary>This is used in integrity checks.</summary>
    /// <remarks>We semi-randomly pick a traversal stamp, label all blocks in the BB list with that stamp (in this field); then we can tell if (e.g.) predecessors are still in the BB list by whether they have the same stamp (with high probability).</remarks>
    public int bbTraversalStamp;
#endif

    /// <summary>bbID is a unique block identifier number that does not change: it does not get renumbered, like bbNum.</summary>
    public int bbID;

    public BasicBlock(GenTree? firstNode, GenTree? lastNode)
        : base(firstNode, lastNode)
    {
    }

    public GenTree? FirstLIRNode
    {
        get
        {
            return _firstNode;
        }

        set
        {
            _firstNode = value;
        }
    }

    /// <summary>Returns the first statement in the block</summary>
    public Statement? FirstStmt
    {
        get
        {
            return _stmtList;
        }

        set
        {
            _stmtList = value;
        }
    }

    public bool hasHndIndex => bbHndIndex != 0;

    public bool hasTryIndex => bbTryIndex != 0;

    public int HndIndex
    {
        get
        {
            assert(Debugger.IsAttached || (bbHndIndex != 0));
            return bbHndIndex;
        }

        set
        {
            bbHndIndex = (ushort)(value + 1);
            assert(bbHndIndex != 0);
        }
    }

    public bool IsLIR
    {
        get
        {
            assert(isValid);
            return HasFlag(BBF_IS_LIR);
        }
    }

    /// <summary>Checks that the basic block doesn't mix statements and LIR lists.</summary>
    public bool isValid
    {
        get
        {
            var isLIR = HasFlag(BBF_IS_LIR);

            if (isLIR)
            {
                // Should not have statements in LIR.
                return _stmtList is null;
            }
            else
            {
                // Should not have tree list before LIR.
                return FirstLIRNode is null;
            }
        }
    }

    /// <summary>Returns the last statement in the block</summary>
    public Statement? LastStmt
    {
        get
        {
            var result = _stmtList;

            if (result is not null)
            {
                result = result.PrevStmt;
                assert((result is not null) && (result.NextStmt is null));
            }
            return result;
        }
    }

    public BasicBlock? Next
    {
        get
        {
            return _next;
        }

        set
        {
            assert(value is not null);
            _next = value;
            value._prev = this;
        }
    }

    public BasicBlock? Prev
    {
        get
        {
            return _prev;
        }

        set
        {
            assert(value is not null);
            _prev = value;
            value._next = this;
        }
    }

    public StatementList Statements => new StatementList(_stmtList);

    public int TryIndex
    {
        get
        {
            assert(Debugger.IsAttached || (bbTryIndex != 0));
            return bbTryIndex;
        }

        set
        {
            bbTryIndex = (ushort)(value + 1);
            assert(bbTryIndex != 0);
        }
    }

    public BBKinds Kind
    {
        get
        {
            return _kind;
        }

        set
        {
            // If this block's jump kind requires a target, ensure it is already set
            assert(!HasTarget || HasInitializedTarget);

            _kind = value;

            // If new jump kind requires a target, ensure a target is already set
            assert(!HasTarget || HasInitializedTarget);
        }
    }

    [MemberNotNullWhen(false, nameof(_prev), nameof(Prev))]
    public bool IsFirst => _prev is null;

    [MemberNotNullWhen(false, nameof(_next), nameof(Next))]
    public bool IsLast => _next is null;

    public int TargetOffs => bbTargetOffs;

    // These block types should always have bbTargetEdge se
    public bool HasTarget => _kind is BBJ_ALWAYS or BBJ_CALLFINALLY or BBJ_CALLFINALLYRET or BBJ_EHCATCHRET or BBJ_EHFILTERRET or BBJ_LEAVE;

    public BasicBlock Target => TargetEdge.DestinationBlock;

    public FlowEdge TargetEdge
    {
        get
        {
            // Only block kinds that use `bbTargetEdge` can access it, and it must be non-null.
            if (HasInitializedTarget)
            {
                assert(bbTargetEdge.SourceBlock == this);
            }
            else
            {
                assert(Debugger.IsAttached);
            }
            return bbTargetEdge!;
        }

        set
        {
            // SetKindAndTarget() nulls target for non-jump kinds, so don't use SetTargetEdge() to null bbTargetEdge without updating bbKind.
            bbTargetEdge = value;

            assert(HasInitializedTarget);
            assert(bbTargetEdge.SourceBlock == this);

            // This is the only successor edge for this block, so likelihood should be 1.0
            bbTargetEdge.Likelihood = 1.0;
        }
    }

#nullable disable
    public ref FlowEdge TargetEdgeRef
#nullable restore
    {
        get
        {
            if (HasInitializedTarget)
            {
                assert(bbTargetEdge.SourceBlock == this);
                return ref Unsafe.As<object?, FlowEdge?>(ref _anonymous1);
            }
            else
            {
                assert(Debugger.IsAttached);
                return ref Unsafe.NullRef<FlowEdge?>();
            }
        }
    }

    public bbCatchType CatchType
    {
        get
        {
            return bbCatchType;
        }

        set
        {
            bbCatchType = value;
        }
    }

    public FlowEdge FalseEdge
    {
        get
        {
            if ((Kind is BBJ_COND) && (bbFalseEdge is not null))
            {
                assert(bbFalseEdge.SourceBlock == this);
            }
            else
            {
                assert(Debugger.IsAttached);
            }
            return bbFalseEdge!;
        }

        set
        {
            assert(Kind is BBJ_COND);
            assert(value.SourceBlock == this);
            bbFalseEdge = value;
        }
    }

#nullable disable
    public ref FlowEdge FalseEdgeRef
#nullable restore
    {
        get
        {
            if ((Kind is BBJ_COND) && (bbFalseEdge is not null))
            {
                assert(bbFalseEdge.SourceBlock == this);
            }
            else
            {
                assert(Debugger.IsAttached);
            }
            return ref bbFalseEdge;
        }
    }

    public BasicBlock FalseTarget => FalseEdge.DestinationBlock;

    [MemberNotNullWhen(true, nameof(bbTargetEdge), nameof(TargetEdge))]
    public bool HasInitializedTarget
    {
        get
        {
            assert(Debugger.IsAttached || HasTarget);
            return bbTargetEdge is not null;
        }
    }

    public bool HasFlag(BasicBlockFlags flag)
    {
        assert(long.IsPositive((long)(flag)));
        return (bbFlags & flag) != 0;
    }

    public bool JumpsToNext => Target == _next;

    public PredBlockList PredBlocks => new PredBlockList(bbPreds, allowEdits: false);

    public BBJumpTableList SwitchSuccs
    {
        get
        {
            if (_kind is not BBJ_SWITCH)
            {
                assert(Debugger.IsAttached);
            }
            return new BBJumpTableList(bbSwtTargets!);
        }
    }

    public FlowEdge TrueEdge
    {
        get
        {
            if ((Kind is BBJ_COND) && (bbTrueEdge is not null))
            {
                assert(bbTrueEdge.SourceBlock == this);
            }
            else
            {
                assert(Debugger.IsAttached);
            }
            return bbTrueEdge!;
        }

        set
        {
            assert(Kind is BBJ_COND);
            assert(value.SourceBlock == this);
            bbTrueEdge = value;
        }
    }

#nullable disable
    public ref FlowEdge TrueEdgeRef
#nullable restore
    {
        get
        {
            if ((Kind is BBJ_COND) && (bbTrueEdge is not null))
            {
                assert(bbTrueEdge.SourceBlock == this);
                return ref Unsafe.As<object?, FlowEdge?>(ref _anonymous1);
            }
            else
            {
                assert(Debugger.IsAttached);
                return ref Unsafe.NullRef<FlowEdge?>();
            }
        }
    }

    public BasicBlock TrueTarget => TrueEdge.DestinationBlock;

    public BBswtDesc SwitchTargets
    {
        get
        {
            assert(Debugger.IsAttached || (Kind is BBJ_SWITCH));
            return bbSwtTargets!;
        }

        set
        {
            _kind = BBJ_SWITCH;
            bbSwtTargets = value;
        }
    }

    public int CountOfInEdges => bbRefs;

    public BBJumpTable? EhfTargets
    {
        get
        {
            assert(Debugger.IsAttached || (Kind is BBJ_EHFINALLYRET));
            return bbEhfTargets;
        }

        set
        {
            assert(Kind is BBJ_EHFINALLYRET);
            bbEhfTargets = value;
        }
    }

    public bool isRunRarely => bbWeight == BB_ZERO_WEIGHT;

    public bool isLoopAlign => HasFlag(BBF_LOOP_ALIGN);

    public bool hasAlign => HasFlag(BBF_HAS_ALIGN);

    public bool hasProfileWeight => HasFlag(BBF_PROF_WEIGHT);

    public bool isMaxBBWeight => (bbWeight >= BB_MAX_WEIGHT);

    public bool HasTerminator => _kind is BBJ_EHFINALLYRET or BBJ_EHFAULTRET or BBJ_EHFILTERRET or BBJ_COND or BBJ_SWITCH or BBJ_RETURN;

    public BasicBlockFlags FlagsRaw
    {
        get
        {
            return bbFlags;
        }

        set
        {
            bbFlags = value;
        }
    }

    public PredEdgeList PredEdges => new PredEdgeList(bbPreds, allowEdits: false);

    public BBSuccBlockList Succs => new BBSuccBlockList(this);

    public static BasicBlock New(Compiler compiler)
    {
#if DEBUG
        assert(compiler.fgSafeBasicBlockCreation);
#endif

        // scopeInfo needs to be able to differentiate between blocks which
        // correspond to some instrs (and so may have some LocalVarInfo
        // boundaries), or have been inserted by the JIT

        var block = new BasicBlock(firstNode: null, lastNode: null) {
            bbCodeOffs = BAD_IL_OFFSET,
            bbCodeOffsEnd = BAD_IL_OFFSET,
            bbID = compiler.compBasicBlockID++
        };

#if MEASURE_BLOCK_SIZE
        s_Count += 1;
        s_Size += sizeof(BasicBlock);
#endif

#if DEBUG
        // fgLookupBB() is invalid until fgInitBBLookup() is called again.
        compiler.fgInvalidateBBLookup();
#endif

        // Give the block a number, set the ancestor count and weight

        compiler.fgBBcount++;
        block.bbNum = ++compiler.fgBBNumMax;

        if (compiler.compRationalIRForm)
        {
            block.SetFlags(BBF_IS_LIR);
        }

        block.bbRefs = 1;
        block.bbWeight = BB_UNITY_WEIGHT;

        block.bbStkTempsIn = NO_BASE_TMP;
        block.bbStkTempsOut = NO_BASE_TMP;

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"New Basic Block {block.dspToString()} created.\n");
        }
#endif

        // We will give all the blocks var sets after the number of tracked variables
        // is determined and frozen.  After that, if we dynamically create a basic block,
        // we will initialize its var sets.
        if (compiler.fgBBVarSetsInited)
        {
            VarSetOps.AssignNoCopy(compiler, ref block.bbVarUse, VarSetOps.MakeEmpty(compiler));
            VarSetOps.AssignNoCopy(compiler, ref block.bbVarDef, VarSetOps.MakeEmpty(compiler));
            VarSetOps.AssignNoCopy(compiler, ref block.bbLiveIn, VarSetOps.MakeEmpty(compiler));
            VarSetOps.AssignNoCopy(compiler, ref block.bbLiveOut, VarSetOps.MakeEmpty(compiler));
        }
        else
        {
            VarSetOps.AssignNoCopy(compiler, ref block.bbVarUse, VarSetOps.UninitVal());
            VarSetOps.AssignNoCopy(compiler, ref block.bbVarDef, VarSetOps.UninitVal());
            VarSetOps.AssignNoCopy(compiler, ref block.bbLiveIn, VarSetOps.UninitVal());
            VarSetOps.AssignNoCopy(compiler, ref block.bbLiveOut, VarSetOps.UninitVal());
        }

        return block;
    }

    public static BasicBlock New(Compiler compiler, BBKinds kind)
    {
        var block = New(compiler);
        block._kind = kind;

        if (block.Kind is BBJ_THROW)
        {
            block.bbSetRunRarely();
        }
        return block;
    }
    public static BasicBlock New(Compiler compiler, BBJumpTable ehfTargets)
    {
        var block = New(compiler);
        block.SetEhf(ehfTargets);
        return block;
    }

    public static BasicBlock New(Compiler compiler, BBswtDesc swtTargets)
    {
        var block = New(compiler);
        block.SwitchTargets = swtTargets;
        return block;
    }

    public static BasicBlock New(Compiler compiler, BBKinds kind, int targetOffs)
    {
        var block = New(compiler);
        block._kind = kind;
        block.bbTargetOffs = targetOffs;
        return block;
    }

    public static bool sameEHRegion(BasicBlock blk1, BasicBlock blk2)
        => sameTryRegion(blk1, blk2) && sameHndRegion(blk1, blk2);

    public static bool sameHndRegion(BasicBlock blk1, BasicBlock blk2)
        => blk1.bbHndIndex == blk2.bbHndIndex;

    public static bool sameTryRegion(BasicBlock blk1, BasicBlock blk2)
        => blk1.bbTryIndex == blk2.bbTryIndex;

    public void clearHndIndex()
    {
        bbHndIndex = 0;
    }

    public void clearTryIndex()
    {
        bbTryIndex = 0;
    }

    public void copyEHRegion(BasicBlock source)
    {
        bbHndIndex = source.bbHndIndex;
        bbTryIndex = source.bbTryIndex;
    }

    /// <summary>Copy all the flags from another block.</summary>
    /// <param name="source"></param>
    /// <remarks>This is a complete copy; any flags that were previously set on this block are overwritten.</remarks>
    public void CopyFlags(BasicBlock source)
    {
        bbFlags = source.bbFlags;
    }

    /// <summary>Copy the values of a specific set of flags from another block.</summary>
    /// <param name="source"></param>
    /// <param name="mask"></param>
    /// <remarks>
    ///   <para>All flags not in the mask are preserved.</para>
    ///   <para>Note however, that only set flags are copied; if a flag in the mask is already set in this block, it will not be reset!</para>
    ///   <para>Perhaps we should have a `ReplaceFlags` function that first clears the bits in `mask` before doing the copy.</para>
    ///   <para>Possibly we should assert that `(bbFlags &amp; mask) == 0` under the assumption that we copy flags when creating a new block from scratch.</para>
    /// </remarks>
    public void CopyFlags(BasicBlock source, BasicBlockFlags mask)
    {
        bbFlags |= (source.bbFlags & mask);
    }

    public void copyHndIndex(BasicBlock source)
    {
        bbHndIndex = source.bbHndIndex;
    }

    public void copyTryIndex(BasicBlock source)
    {
        bbTryIndex = source.bbTryIndex;
    }

    /// <summary>get the normalized weight of this block</summary>
    /// <param name="comp">Compiler instance</param>
    /// <returns></returns>
    /// <remarks>With profile data: number of expected executions of this block, given one call to the method.</remarks>
    public weight_t getBBWeight(Compiler comp)
    {
        if (bbWeight == BB_ZERO_WEIGHT)
        {
            return BB_ZERO_WEIGHT;
        }
        else
        {
            // Normalize the bbWeight.
            var calledCount = getCalledCount(comp);
            return (bbWeight / calledCount) * BB_UNITY_WEIGHT;
        }
    }

    /// <summary>get the value used to normalized weights for this method</summary>
    /// <param name="comp">Compiler instance</param>
    /// <returns></returns>
    /// <remarks>If we don't have profile data then getCalledCount will return BB_UNITY_WEIGHT (100) otherwise it returns the number of times that profile data says the method was called.</remarks>
    public static weight_t getCalledCount(Compiler comp)
    {
        // when we don't have profile data then fgCalledCount will be BB_UNITY_WEIGHT (100)
        var calledCount = comp.fgCalledCount;

        // If we haven't yet reach the place where we setup fgCalledCount it could still be zero
        // so return a reasonable value to use until we set it.
        //
        if (calledCount == 0)
        {
            if (comp.fgIsUsingProfileWeights)
            {
                // When we use profile data block counts we have exact counts,
                // not multiples of BB_UNITY_WEIGHT (100)
                calledCount = 1;
            }
            else
            {
                assert(comp.fgFirstBB is not null);
                calledCount = comp.fgFirstBB.bbWeight;

                if (calledCount == 0)
                {
                    calledCount = BB_UNITY_WEIGHT;
                }
            }
        }
        return calledCount;
    }

    public void inheritWeight(BasicBlock source)
    {
        inheritWeightPercentage(source, 100);
    }

    /// <summary>// Similar to inheritWeight(), but we're splitting a block (such as creating blocks for qmark removal).</summary>
    /// <param name="source"></param>
    /// <param name="percentage">A percentage [0, 100] of the weight the block should inherit.</param>
    /// <remarks>Can be invoked as a self-rescale, eg: block->inheritWeightPercentage(block, 50)</remarks>
    public void inheritWeightPercentage(BasicBlock source, int percentage)
    {
        assert(percentage is >= 0 and <= 100);
        bbWeight = (source.bbWeight * percentage) / 100;

        var hasProfileWeight = source.FlagsRaw & BBF_PROF_WEIGHT;

        RemoveFlags(BBF_PROF_WEIGHT);
        SetFlags(hasProfileWeight);
    }

    /// <summary>see if this is the first block in the cold section</summary>
    /// <param name="compiler">current compiler instance</param>
    /// <returns>true if this is fgFirstColdBlock</returns>
    public bool IsFirstColdBlock(Compiler compiler) => this == compiler.fgFirstColdBlock;

    public void RemoveFlags(BasicBlockFlags flags)
    {
        bbFlags &= ~flags;
    }

    public void scaleBBWeight(weight_t scale)
    {
        bbWeight *= scale;
    }

    public void SetEhf(BBJumpTable ehfTargets)
    {
        _kind = BBJ_EHFINALLYRET;
        bbEhfTargets = ehfTargets;
    }

    public void SetFlags(BasicBlockFlags flags)
    {
        bbFlags |= flags;
    }

    public void SetKindAndTargetEdge(BBKinds kind, FlowEdge targetEdge)
    {
        _kind = kind;
        bbTargetEdge = targetEdge;

        if (targetEdge is not null)
        {
            assert(HasInitializedTarget);

            // This is the only successor edge for this block, so likelihood should be 1.0
            bbTargetEdge.Likelihood = 1.0;
        }
        else
        {
            assert(!HasTarget);
        }
    }

    public void bbSetRunRarely() => scaleBBWeight(BB_ZERO_WEIGHT);

#if DEBUG
    /// <summary>see if pred list is properly ordered</summary>
    /// <returns>false if pred list is not in increasing bbID order.</returns>
    public bool checkPredListOrder()
    {
        var lastBBID = -1;

        foreach (var predBlock in PredBlocks)
        {
            var bbID = predBlock.bbID;

            if (bbID <= lastBBID)
            {
                assert(bbID != lastBBID);
                return false;
            }
            lastBBID = bbID;
        }
        return true;
    }

    /// <summary>Print a simple basic block header for various output, including a list of predecessors and successors.</summary>
    /// <param name="showKind"></param>
    /// <param name="showFlags"></param>
    /// <param name="showPreds"></param>
    public void dspBlockHeader(bool showKind = true, bool showFlags = false, bool showPreds = true)
    {
        jitprintf($"{dspToString()} ");
        dspBlockILRange();

        if (showKind)
        {
            dspKind();
        }

        if (showPreds)
        {
            jitprintf(", preds={{");
            _ = dspPreds();
            jitprintf("}} succs={{");
            dspSuccs();
            jitprintf("}}");
        }

        if (showFlags)
        {
            var lowFlags = (int)(bbFlags);
            var highFlags = (int)((long)(bbFlags) >>> 32);
            jitprintf($" flags=0x{highFlags:X8}.{lowFlags:X8}: ");
            dspFlags();
        }
        jitprintf("\n");
    }

    public void dspBlockILRange()
    {
        if (bbCodeOffs != BAD_IL_OFFSET)
        {
            jitprintf($"[{bbCodeOffs:X3}..");
        }
        else
        {
            jitprintf("[???..");
        }

        if (bbCodeOffsEnd != BAD_IL_OFFSET)
        {
            jitprintf($"{bbCodeOffsEnd:X3})");
        }
        else
        {
            jitprintf("???)");
        }
    }

    /// <summary>Print the flags</summary>
    public void dspFlags()
    {
        dspFlag(this, BBF_IMPORTED, "i", sep: "");
        dspFlag(this, BBF_IS_LIR, "LIR");
        dspFlag(this, BBF_PROF_WEIGHT, "IBC");
        dspFlag(this, BBF_MARKED, "m");
        dspFlag(this, BBF_REMOVED, "del");
        dspFlag(this, BBF_DONT_REMOVE, "keep");
        dspFlag(this, BBF_INTERNAL, "internal");
        dspFlag(this, BBF_HAS_SUPPRESSGC_CALL, "sup-gc");
        dspFlag(this, BBF_HAS_LABEL, "label");
        dspFlag(this, BBF_HAS_JMP, "jmp");
        dspFlag(this, BBF_HAS_CALL, "hascall");
        dspFlag(this, BBF_DOMINATED_BY_EXCEPTIONAL_ENTRY, "xentry");
        dspFlag(this, BBF_GC_SAFE_POINT, "gcsafe");
        dspFlag(this, BBF_HAS_NEWOBJ, "newobj");
        dspFlag(this, BBF_HAS_NEWARR, "newarr");
        dspFlag(this, BBF_BACKWARD_JUMP, "bwd");
        dspFlag(this, BBF_BACKWARD_JUMP_TARGET, "bwd-target");
        dspFlag(this, BBF_BACKWARD_JUMP_SOURCE, "bwd-src");
        dspFlag(this, BBF_OSR_PATCHPOINT, "osr-ppoint");
        dspFlag(this, BBF_PARTIAL_COMPILATION_PATCHPOINT, "pc-ppoint");
        dspFlag(this, BBF_HAS_HISTOGRAM_PROFILE, "hist");
        dspFlag(this, BBF_TAILCALL_SUCCESSOR, "tail-succ");
        dspFlag(this, BBF_RECURSIVE_TAILCALL, "r-tail");
        dspFlag(this, BBF_NO_CSE_IN, "no-cse");
        dspFlag(this, BBF_CAN_ADD_PRED, "add-pred");
        dspFlag(this, BBF_RETLESS_CALL, "retless");
        dspFlag(this, BBF_COLD, "cold");
        dspFlag(this, BBF_KEEP_BBJ_ALWAYS, "KEEP");
        dspFlag(this, BBF_CLONED_FINALLY_BEGIN, "cfb");
        dspFlag(this, BBF_CLONED_FINALLY_END, "cfe");
        dspFlag(this, BBF_LOOP_ALIGN, "align");
        dspFlag(this, BBF_HAS_ALIGN, "has-align");
        dspFlag(this, BBF_HAS_MDARRAYREF, "mdarr");
        dspFlag(this, BBF_NEEDS_GCPOLL, "gcpoll");
        dspFlag(this, BBF_HAS_VALUE_PROFILE, "val-prof");
        dspFlag(this, BBF_MAY_HAVE_BOUNDS_CHECKS, "bnds-chk");
        dspFlag(this, BBF_ASYNC_RESUMPTION, "a-resume");
        dspFlag(this, BBF_CATCH_RESUMPTION, "c-resume");
        dspFlag(this, BBF_THROW_HELPER, "throw-hlpr");

        static void dspFlag(BasicBlock block, BasicBlockFlags flag, string displayString, string sep = " ")
        {
            if (block.HasFlag(flag))
            {
                jitprintf($"{sep}{displayString}");
            }
        }
    }

    /// <summary>Print the predecessors (bbPreds)</summary>
    public int dspPreds()
    {
        var count = 0;

        foreach (var pred in PredEdges)
        {
            if (count != 0)
            {
                jitprintf(",");
                count += 1;
            }

            var predSourceBBNum = pred.SourceBlock.bbNum;

            jitprintf(FMT_BB(predSourceBBNum));
            count += 4;

            // Account for D2 only handling 2 digits, but we can display more than that.
            var digits = CountDigits(predSourceBBNum);

            if (digits > 2)
            {
                count += digits - 2;
            }

            // Does this predecessor have an interesting dup count? If so, display it.
            var predDupCount = pred.DupCount;

            if (predDupCount > 1)
            {
                jitprintf($"({predDupCount})");
                count += 2 + CountDigits(predDupCount);
            }
        }
        return count;
    }

    /// <summary>Print the successors.</summary>
    public void dspSuccs()
    {
        var sep = "";
        foreach (var succ in Succs)
        {
            jitprintf($"{sep}{FMT_BB(succ.bbNum)}");
            sep = ",";
        }
    }

    /// <summary>Print the block jump kind (e.g., BBJ_ALWAYS, BBJ_COND, etc.).</summary>
    public void dspKind()
    {
        switch (_kind)
        {
            case BBJ_EHFINALLYRET:
            {
                jitprintf(" ->");

                // Early in compilation, we display the jump kind before the BBJ_EHFINALLYRET successors have been set.
                if (bbEhfTargets is null)
                {
                    jitprintf(" ????");
                }
                else
                {
                    var sep = " ";
                    for (var i = 0; i < bbEhfTargets.Succs.Length; i++)
                    {
                        jitprintf($"{sep}{dspBlockNum(bbEhfTargets.Succs[i])}");
                        sep = ",";
                    }
                }

                jitprintf(" (finret)");
                break;
            }

            case BBJ_EHFAULTRET:
            {
                jitprintf(" (falret)");
                break;
            }

            case BBJ_EHFILTERRET:
            {
                jitprintf($" -> {dspBlockNum(TargetEdge)} (fltret)");
                break;
            }

            case BBJ_EHCATCHRET:
            {
                jitprintf($" -> {dspBlockNum(TargetEdge)} (cret)");
                break;
            }

            case BBJ_THROW:
            {
                jitprintf(" (throw)");
                break;
            }

            case BBJ_RETURN:
            {
                jitprintf(" (return)");
                break;
            }

            case BBJ_ALWAYS:
            {
                if (HasFlag(BBF_KEEP_BBJ_ALWAYS))
                {
                    jitprintf($" -> {dspBlockNum(TargetEdge)} (ALWAYS)");
                }
                else
                {
                    jitprintf($" -> {dspBlockNum(TargetEdge)} (always)");
                }
                break;
            }

            case BBJ_LEAVE:
            {
                jitprintf($" -> {dspBlockNum(TargetEdge)} (leave)");
                break;
            }

            case BBJ_CALLFINALLY:
            {
                jitprintf($" -> {dspBlockNum(TargetEdge)} (callf)");
                break;
            }

            case BBJ_CALLFINALLYRET:
            {
                jitprintf($" -> {dspBlockNum(TargetEdge)} (callfr)");
                break;
            }

            case BBJ_COND:
            {
                jitprintf($" -> {dspBlockNum(TrueEdge)},{dspBlockNum(FalseEdge)} (cond)");
                break;
            }

            case BBJ_SWITCH:
            {
                jitprintf(" ->");
                var jumpTab = bbSwtTargets.Cases;

                for (var i = 0; i < jumpTab.Length; i++)
                {
                    jitprintf($"{((i == 0) ? ' ' : ',')}{dspBlockNum(jumpTab[i])}");

                    if (bbSwtTargets.HasDefaultCase && (i == (jumpTab.Length - 1)))
                    {
                        jitprintf("[def]");
                    }

                    if (bbSwtTargets.HasDominantCase && (i == bbSwtTargets.DominantCase))
                    {
                        jitprintf("[dom]");
                    }
                }

                jitprintf(" (switch)");
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        static string dspBlockNum(FlowEdge e)
        {
            var b = e.DestinationBlock;
            var result = (b is not null) ? FMT_BB(b.bbNum) : "NULL"; 

            const bool printEdgeLikelihoods = true; // TODO: parameterize this?

            if (printEdgeLikelihoods)
            {
                if (e.hasLikelihood)
                {
                    result = $"{result}({FMT_WT(e.Likelihood)})";
                }
            }
            return result;
        }
    }

    public string dspToString(int blockNumPadding = 0)
        => $"{FMT_BB(bbNum)}{new string(' ', blockNumPadding)} [{bbID:D4}]";
#endif

    [InlineArray((int)(MemoryKindCount))]
    public struct bbMemorySsaPhiFuncInlineArray
    {
        public MemoryPhiArg? e0;
    }

    [InlineArray((int)(MemoryKindCount))]
    public struct bbMemorySsaNumInInlineArray
    {
        public int e0;
    }

    [InlineArray((int)(MemoryKindCount))]
    public struct bbMemorySsaNumOutInlineArray
    {
        public int e0;
    }
}
