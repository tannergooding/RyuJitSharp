// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Emitter
{
    protected Compiler? m_compiler;

    protected GCInfo gcInfo;

    protected CodeGen? codeGen;

    private nuint m_debugInfoSize;

    protected uint emitInsCount;

#if DEBUG
    protected uint emitVarRefOffs;
#else
    protected const uint emitVarRefOffs = 0;
#endif

    protected uint emitPrologEndPos;

    protected uint emitEpilogCnt;

    protected UNATIVE_OFFSET emitEpilogSize;

#if TARGET_XARCH
    protected emitLocation emitExitSeqBegLoc;

    /// <summary>minimum size of any return sequence - the 'ret' after the epilog</summary>
    protected UNATIVE_OFFSET emitExitSeqSize;
#endif

    /// <summary>per method placeholder list - head</summary>
    protected insGroup? emitPlaceholderList;

    /// <summary>per method placeholder list - tail</summary>
    protected insGroup? emitPlaceholderLast;

#if JIT32_GCENCODER
    /// <summary>per method epilog list - head</summary>
    protected EpilogList? emitEpilogList;

    /// <summary>per method epilog list - tail</summary>
    protected EpilogList? emitEpilogLast;
#endif

#if DEBUG
    /// <summary>If we have started issuing instructions from the list of instrDesc, this is set</summary>
    public bool emitIssuing;
#endif

    /// <summary>Hot code block</summary>
    public unsafe byte* emitCodeBlock;

    /// <summary>Cold code block</summary>
    public unsafe byte* emitColdCodeBlock;

    public unsafe AllocMemChunk* emitDataChunks;

    public unsafe uint* emitDataChunkOffsets;

    public uint emitNumDataChunks;

    /// <summary>Offset applied to a code address to get memory location that can be written</summary>
    public nuint writeableOffset;

    public UNATIVE_OFFSET emitTotalHotCodeSize;

    public UNATIVE_OFFSET emitTotalColdCodeSize;

#if TARGET_LOONGARCH64
    public uint emitCounts_INS_OPTS_J;
#endif

    public bool emitHasFramePtr;

#if DEBUG
    /// <summary>perform some alignment checks</summary>
    public bool emitChkAlign;
#endif

    public insGroup? emitCurIG;

#if TARGET_AMD64
    private regMaskFlt rbmFltCalleeTrash;

    private regMaskInt rbmAllInt;

    private regMaskInt rbmIntCalleeTrash;
#endif

#if TARGET_XARCH
    private regMaskMsk rbmMskCalleeTrash;
#endif

    private nuint emitIGbuffSize;

    /// <summary>first  instruction group</summary>
    private insGroup? emitIGlist;

    /// <summary>last   instruction group</summary>
    private insGroup? emitIGlast;

    /// <summary>issued instruction group</summary>
    private insGroup? emitIGthis;

    /// <summary>prolog instruction group</summary>
    private insGroup? emitPrologIG;

    /// <summary>list of local jumps in method</summary>
    private instrDescJmp? emitJumpList;

    /// <summary>last of local jumps in method</summary>
    private instrDescJmp? emitJumpLast;

    private bool emitContainsRemovableJmpCandidates;

#if FEATURE_LOOP_ALIGN
    /// <summary>list of align instructions in current IG</summary>
    private instrDescAlign? emitCurIGAlignList;

    /// <summary>Start IG of last inner loop</summary>
    private uint emitLastLoopStart;

    /// <summary>End IG of last inner loop</summary>
    private uint emitLastLoopEnd;

    /// <summary>last IG that has align instruction</summary>
    private uint emitLastAlignedIgNum;

    /// <summary>list of all align instructions in method</summary>
    private instrDescAlign? emitAlignList;

    /// <summary>last align instruction in method</summary>
    private instrDescAlign? emitAlignLast;

    /// <summary>Points to the most recent added align instruction.</summary>
    /// <remarks>If there are multiple align instructions like in arm64 or non-adaptive alignment on xarch, this points to the first align instruction of the series of align instructions.</remarks>
    private instrDescAlign? emitAlignLastGroup;
#endif

    /// <summary>forward jumps present?</summary>
    private bool emitFwdJumps;

    /// <summary>Count of number of nested "NO GC" region requests we have.</summary>
    private uint emitNoGCRequestCount;

    /// <summary>Are we generating IGF_NOGCINTERRUPT insGroups (for prologs, epilogs, etc.)</summary>
    private bool emitNoGCIG;

    /// <summary>If we generate an instruction, and not another instruction group, force create a new emitAdd instruction group.</summary>
    private bool emitForceNewIG;

    /// <summary>next available byte in buffer</summary>
    private unsafe byte* emitCurIGfreeNext;

    /// <summary>one byte past the last available byte in buffer</summary>
    private unsafe byte* emitCurIGfreeEndp;

    /// <summary>first byte address</summary>
    private unsafe byte* emitCurIGfreeBase;

    /// <summary># of collected instr's in buffer</summary>
    private uint emitCurIGinsCnt;

    /// <summary>estimated code size of current group in bytes</summary>
    private uint emitCurIGsize;

    /// <summary>current code offset within group</summary>
    private UNATIVE_OFFSET emitCurCodeOffset;

    /// <summary>bytes of code in entire method</summary>
    private UNATIVE_OFFSET emitTotalCodeSize;

    /// <summary>first cold instruction group</summary>
    private insGroup? emitFirstColdIG;

    /// <summary>current code offset adjustment</summary>
    private int emitOffsAdj;

    /// <summary>list of jumps   in current IG</summary>
    private instrDescJmp? emitCurIGjmpList;

    // emitPrev* and emitInit* are only used during code generation, not during
    // emission (issuing), to determine what GC values to store into an IG.
    // Note that only the Vars ones are actually used, apparently due to bugs
    // in that tracking. See emitSavIG(): the important use of ByrefRegs is commented
    // out, and GCrefRegs is always saved.

    private VARSET_TP emitPrevGCrefVars;

    private regMaskInt emitPrevGCrefRegs;

    private regMaskInt emitPrevByrefRegs;

    private VARSET_TP emitInitGCrefVars;

    private regMaskInt emitInitGCrefRegs;

    private regMaskInt emitInitByrefRegs;

    /// <summary>If this is set, we ignore comparing emitPrev* and emitInit* to determine whether to save GC state (to save space in the IG), and always save it.</summary>
    private bool emitForceStoreGCState;

    /// <summary>This flag is used together with `emitForceStoreGCState`.</summary>
    /// <remarks>
    ///   <para>After we set emitForceStoreGCState = true, we will mark `emitAddedLabel` to true whenever we see a label IG.</para>
    ///   <para>In emitSavIG, we will reset `emitForceStoreGCState = false` only after seeing `emitAddedLabel == true`.</para>
    ///   <para>Until then, we will keep recording GC_VARS on the IGs.</para>
    /// </remarks>
    private bool emitAddedLabel;

    // emitThis* variables are used during emission, to track GC updates
    // on a per-instruction basis. During code generation, per-instruction
    // tracking is done with variables gcVarPtrSetCur, gcRegGCrefSetCur,
    // and gcRegByrefSetCur. However, these are also used for a slightly
    // different purpose during code generation: to try to minimize the
    // amount of GC data stored to an IG, by only storing deltas from what
    // we expect to see at an IG boundary. Also, only emitThisGCrefVars is
    // really the only one used; the others seem to be calculated, but not
    // used due to bugs.

    private VARSET_TP emitThisGCrefVars;

    /// <summary>Current set of registers holding GC references</summary>
    private regMaskInt emitThisGCrefRegs;

    /// <summary>Current set of registers holding BYREF references</summary>
    private regMaskInt emitThisByrefRegs;

    /// <summary>Is "emitThisGCrefVars" up to date?</summary>
    private bool emitThisGCrefVset;

    /// <summary>where is "this" enregistered for synchronized methods?</summary>
    private regNumber emitSyncThisObjReg;

    private uint emitNxtIGnum;

    private instrDesc? emitLastIns;

    private insGroup? emitLastInsIG;

#if EMIT_BACKWARDS_NAVIGATION
    private uint emitLastInsFullSize;
#endif

#if TARGET_ARMARCH
    private instrDesc? emitLastMemBarrier;
#endif

    private uint emitTrkVarCnt;

    /// <summary>Offsets of tracked stack ptr vars (varTrkIndex -> stkOffs)</summary>
    private unsafe int* emitGCrFrameOffsTab;

    /// <summary>Number of       tracked stack ptr vars</summary>
    private uint emitGCrFrameOffsCnt;

    /// <summary>Min offset of a tracked stack ptr var</summary>
    private int emitGCrFrameOffsMin;

    /// <summary>Max offset of a tracked stack ptr var</summary>
    private int emitGCrFrameOffsMax;

    /// <summary>All lcl between emitGCrFrameOffsMin/Max are only tracked stack ptr vars</summary>
    private bool emitContTrkPtrLcls;

    /// <summary>Cache of currently live varPtrs (stkOffs -> varPtrDsc)</summary>
    private GCInfo.varPtrDsc[]? emitGCrFrameLiveTab;

    private int emitArgFrameOffsMin;

    private int emitArgFrameOffsMax;

    private int emitLclFrameOffsMin;

    private int emitLclFrameOffsMax;

    /// <summary>what is the offset of "this" for synchronized methods?</summary>
    private int emitSyncThisObjOffs;

    private unsafe CORINFO_METHOD_HANDLE emitAsyncResumeStub;

    private unsafe void* emitAsyncResumeStubEntryPoint;

    /// <summary>full arg info (including non-ptr arg)?</summary>
    public bool emitFullArgInfo;

    /// <summary>full GC pointer maps?</summary>
    public bool emitFullGCinfo;

    /// <summary>fully interruptible code?</summary>
    public bool emitFullyInt;

#if EMIT_TRACK_STACK_DEPTH
    /// <summary>0 in prolog/epilog, One DWORD elsewhere</summary>
    public uint emitCntStackDepth;

    /// <summary>actual computed max. stack depth</summary>
    public uint emitMaxStackDepth;
#endif

    /// <summary>using the "simple" stack table?</summary>
    public bool emitSimpleStkUsed;

    private _Anonymous_e__Union _anonymous;

    public ref _Anonymous_e__Union._u1_e__Struct u1 => ref _anonymous.u1;

    public ref _Anonymous_e__Union._u2_e__Struct u2 => ref _anonymous.u2;

    /// <summary>amount of bytes pushed on stack</summary>
    public uint emitCurStackLvl;

    public dataSecDsc emitConsDsc;

    public dataSection? emitDataSecCur;

    public unsafe COMP_HANDLE emitCmpHandle;

    /// <summary>Record some info about the method about to be emitted.</summary>
    /// <param name="comp"></param>
    /// <param name="cmpHandle"></param>
    public unsafe void emitBegCG(Compiler comp, COMP_HANDLE cmpHandle)
    {
        m_compiler = comp;
        emitCmpHandle = cmpHandle;
        m_debugInfoSize = (uint)(sizeof(instrDescDebugInfo));

#if !DEBUG
        if (!comp.opts.disAsm)
        {
            m_debugInfoSize = 0;
        }
#endif

#if TARGET_AMD64
        rbmFltCalleeTrash = m_compiler.rbmFltCalleeTrash;
        rbmIntCalleeTrash = m_compiler.rbmIntCalleeTrash;
        rbmAllInt = m_compiler.rbmAllInt;
#endif

#if TARGET_XARCH
        rbmMskCalleeTrash = m_compiler.rbmMskCalleeTrash;
#endif
    }

    public void emitEndCG()
    {
    }

    public static void emitInit()
    {
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct _Anonymous_e__Union
    {
        // if emitSimpleStkUsed==true
        [FieldOffset(0)]
        public _u1_e__Struct u1;

        // if emitSimpleStkUsed==false
        [FieldOffset(0)]
        public _u2_e__Struct u2;

        public struct _u1_e__Struct
        {
            /// <summary>bit per pushed dword (if it fits. Lowest bit &lt;==&gt; last pushed arg)</summary>
            public uint emitSimpleStkMask;

            /// <summary>byref qualifier for emitSimpleStkMask</summary>
            public uint emitSimpleByrefStkMask;
        }

        public struct _u2_e__Struct
        {
            /// <summary>small local table to avoid malloc</summary>
            public emitArgTrackLclInlineArray emitArgTrackLcl;

            /// <summary>base of the argument tracking stack</summary>
            public unsafe byte* emitArgTrackTab;

            /// <summary>top  of the argument tracking stack</summary>
            public unsafe byte* emitArgTrackTop;

            /// <summary>count of pending arg records (stk-depth for frameless methods, gc ptrs on stk for framed methods)</summary>
            public ushort emitGcArgTrackCnt;
        }

        [InlineArray(16)]
        public struct emitArgTrackLclInlineArray
        {
            public byte e0;
        }
    }
}
