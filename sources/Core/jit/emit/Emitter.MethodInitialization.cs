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

    public void emitBegFN(bool hasFramePtr, bool chkAlign = false)
    {
        assert(_compiler is not null);
        emitCurIGfreeBase = null;
        emitIGbuffSize = 0;

        if (Configuration.FEATURE_LOOP_ALIGN)
        {
            emitLastAlignedIg = null;
            emitLastLoopStart = null;
            emitLastLoopEnd = null;
        }

        emitHasFramePtr = hasFramePtr;
#if DEBUG
        emitChkAlign = chkAlign;
#endif
        emitPrologEndPos.Init();
        emitEpilogSize = 0;
        emitEpilogCnt = 0;
        emitExitSeqBegLoc.Init();
        emitExitSeqSize = int.MaxValue;
        emitPlaceholderList = null;
        emitPlaceholderLast = null;

        emitEpilogList = null;
        emitEpilogLast = null;

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

        if (Configuration.FEATURE_LOOP_ALIGN)
        {
            emitAlignList = null;
            emitAlignLastGroup = null;
            emitAlignLast = null;
            emitCurIGAlignList = null;
        }

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
        if (Configuration.DEBUG)
        {
            emitGCrFrameLiveTab = null;
        }
        emitIGlist = null;
        emitIGlast = null;
        emitCurCodeOffset = 0;
        emitFirstColdIG = null;
        emitTotalCodeSize = 0;

        emitCounts_INS_OPTS_J = 0;

#if EMITTER_STATS
        emitTotalIGmcnt = unchecked(emitTotalIGmcnt + 1);
        emitSizeMethod = 0;
        emitCurPrologInsCnt = 0;
        emitCurPrologIGSize = 0;
#endif

        emitInsCount = 0;
        emitCurStackLvl = 0;

        if (Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            emitMaxStackDepth = 0;
            emitCntStackDepth = sizeof(int);
        }
        emitNxtIGnum = 1;
        var ig = emitAllocIG();
        emitIGlist = ig;
        emitIGlast = ig;
        emitCurIG = ig;
        ig.igFlags |= InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead;
        emitLastIns = null;
        emitLastInsIG = null;

        emitLastMemBarrier = null;

        ig.igNext = null;

        emitLastInsFullSize = 0;
        ig.igPrev = null;

        emitNewIG();
        assert(emitCurIG is not null);
        emitCurIG.igFlags &= ~IGF_PROPAGATE_MASK;
    }
}
