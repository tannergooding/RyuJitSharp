// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
    private static uint emitTotalIGmcnt;
#endif

    public void emitBegFN(bool hasFramePtr
#if DEBUG
        , bool chkAlign
#endif
        )
    {
        assert(_compiler is not null);
        emitCurIGfreeBase = null;
        emitIGbuffSize = 0;

#if FEATURE_LOOP_ALIGN
        emitLastAlignedIg = null;
        emitLastLoopStart = null;
        emitLastLoopEnd = null;
#endif

        emitHasFramePtr = hasFramePtr;
#if DEBUG
        emitChkAlign = chkAlign;
#endif
        emitPrologEndPos.Init();
        emitEpilogSize = 0;
        emitEpilogCnt = 0;
#if TARGET_XARCH
        emitExitSeqBegLoc.Init();
        emitExitSeqSize = int.MaxValue;
#endif
        emitPlaceholderList = null;
        emitPlaceholderLast = null;

#if JIT32_GCENCODER
        emitEpilogList = null;
        emitEpilogLast = null;
#endif

        emitJumpList = null;
        emitJumpLast = null;
        emitFixedSizeJumpList = null;
        emitCurIGjmpList = null;
        emitFwdJumps = false;
        emitNoGCRequestCount = 0;
        emitNoGCIG = false;
        emitLastSavedIGWasNoGC = false;
        emitForceNewIG = false;
        emitContainsRemovableJmpCandidates = false;

#if FEATURE_LOOP_ALIGN
        emitAlignList = null;
        emitAlignLastGroup = null;
        emitAlignLast = null;
        emitCurIGAlignList = null;
#endif

        assert(VarSetOps.IsEmpty(_compiler, emitThisGCrefVars));
        assert(VarSetOps.IsEmpty(_compiler, emitInitGCrefVars));
        assert(VarSetOps.IsEmpty(_compiler, emitPrevGCrefVars));
        emitThisGCrefRegs = regMask.SRBM_NONE;
        emitInitGCrefRegs = regMask.SRBM_NONE;
        emitPrevGCrefRegs = regMask.SRBM_NONE;
        emitThisByrefRegs = regMask.SRBM_NONE;
        emitInitByrefRegs = regMask.SRBM_NONE;
        emitPrevByrefRegs = regMask.SRBM_NONE;
        emitForceStoreGCState = false;
        emitAddedLabel = false;

#if DEBUG
        emitIssuing = false;
#endif
        emitGCrFrameOffsMin = 0;
        emitGCrFrameOffsMax = 0;
        emitGCrFrameOffsCnt = 0;
#if DEBUG
        emitGCrFrameLiveTab = null;
#endif
        emitIGlist = null;
        emitIGlast = null;
        emitCurCodeOffset = 0;
        emitFirstColdIG = null;
        emitTotalCodeSize = 0;

#if TARGET_LOONGARCH64
        emitCounts_INS_OPTS_J = 0;
#endif

#if EMITTER_STATS
        emitTotalIGmcnt = unchecked(emitTotalIGmcnt + 1);
        emitSizeMethod = 0;
        emitCurPrologInsCnt = 0;
        emitCurPrologIGSize = 0;
#endif

        emitInsCount = 0;
        emitCurStackLvl = 0;

#if EMIT_TRACK_STACK_DEPTH
        emitMaxStackDepth = 0;
        emitCntStackDepth = sizeof(int);
#endif
        emitNxtIGnum = 1;
        var ig = emitAllocIG();
        emitIGlist = ig;
        emitIGlast = ig;
        emitCurIG = ig;
        ig.igFlags |= InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead;
        emitLastIns = null;
        emitLastInsIG = null;

#if TARGET_ARMARCH
        emitLastMemBarrier = null;
#endif

        ig.igNext = null;

#if TARGET_XARCH || EMIT_BACKWARDS_NAVIGATION
        emitLastInsFullSize = 0;
        ig.igPrev = null;
#endif

        emitNewIG();
        assert(emitCurIG is not null);
        emitCurIG.igFlags &= ~IGF_PROPAGATE_MASK;
    }
}
