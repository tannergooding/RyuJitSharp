// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    private const InsGroupFlags IGF_PROPAGATE_MASK =
        InsGroupFlags.Prolog | InsGroupFlags.Epilog | InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog;

#if EMITTER_STATS
    private static uint emitTotalIGcnt;
#endif

    private insGroup emitAllocAndLinkIG()
    {
        var ig = emitAllocIG();

        var currentIG = emitCurIG ?? throw new InvalidOperationException("An instruction group must precede the new group.");
        emitInsertIGAfter(currentIG, ig);

        ig.igFlags |= currentIG.igFlags & IGF_PROPAGATE_MASK;
        emitCurIG = ig;

        return ig;
    }

    private insGroup emitAllocIG()
    {
        insGroup ig = new();

#if EMITTER_STATS
        var size = emitNativeIGSize();
        emitTotMemAlloc = unchecked(emitTotMemAlloc + size);
        emitTotalIGcnt = unchecked(emitTotalIGcnt + 1);
        emitTotalIGsize = unchecked(emitTotalIGsize + size);
        emitSizeMethod = unchecked(emitSizeMethod + size);
#endif

#if DEBUG
        ig.igSelf = ig;
        ig.igDataSize = 0;
#endif

        emitInitIG(ig);

        return ig;
    }

#if EMITTER_STATS
    private static nuint emitNativeIGSize()
    {
#if (TARGET_AMD64 || TARGET_ARM64) && FEATURE_LOOP_ALIGN && EMIT_TRACK_STACK_DEPTH && REGMASK_BITS_64 && !EMIT_BACKWARDS_NAVIGATION
        // emit.h insGroup: the Debug jitstd::list holds five pointer-sized words; the remaining
        // fields occupy 96 bytes in Debug and 56 bytes in Release (72 with late disassembly).
#if DEBUG
        return 136;
#elif LATE_DISASM
        return 72;
#else
        return 56;
#endif
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Native instruction-group size is not yet ported for these emitter statistics.");
#endif
    }
#endif

    private void emitInitIG(insGroup ig)
    {
        ig.InitializeNum(unchecked((uint)emitNxtIGnum));
        emitNxtIGnum = unchecked(emitNxtIGnum + 1);

        ig.igOffs = unchecked((uint)emitCurCodeOffset);
        assert((ig.igOffs & (CODE_ALIGN - 1)) == 0);

        var compiler = _compiler ?? throw new InvalidOperationException("Instruction groups require an active compiler.");
        ig.igFuncIdx = compiler.compCurrFuncIdx;
        ig.igFlags = InsGroupFlags.None;

#if DEBUG || LATE_DISASM
        ig.igWeight = getCurrentBlockWeight();
        ig.igPerfScore = 0.0;
#endif

        ig.igData = null;
        ig.igPhData = null;
        ig.igStorageSize = 0;
        ig.igDataOffset = 0;
        ig.SavedGcVars = [];
        ig.SavedByrefRegs = 0;
        ig.igSize = 0;
        ig.igGCregs = regMask.SRBM_NONE;
        ig.igInsCnt = 0;

#if TARGET_XARCH || EMIT_BACKWARDS_NAVIGATION
        ig.igLastIns = null;
#endif

#if FEATURE_LOOP_ALIGN
        ig.igLoopBackEdge = null;
#endif

#if DEBUG
        ig.lastGeneratedBlock = null;
        ig.igBlocks = [];
#endif
    }

    private void emitInsertIGAfter(insGroup insertAfterIG, insGroup ig)
    {
        assert(emitIGlist is not null);
        assert(emitIGlast is not null);

        ig.igNext = insertAfterIG.igNext;
        insertAfterIG.igNext = ig;

#if TARGET_XARCH || EMIT_BACKWARDS_NAVIGATION
        ig.igPrev = insertAfterIG;
        ig.igNext?.igPrev = ig;
#endif

        if (emitIGlast == insertAfterIG)
        {
            emitIGlast = ig;
        }
    }

#if DEBUG || LATE_DISASM
    private weight_t getCurrentBlockWeight()
    {
        var compiler = _compiler ?? throw new InvalidOperationException("Instruction groups require an active compiler.");

        if (compiler.compCurBB is not null)
        {
            return compiler.compCurBB.getBBWeight(compiler);
        }
        else if (emitCurIG is not null)
        {
            assert((emitCurIG.igFlags & IGF_PROPAGATE_MASK) != 0);
            return emitCurIG.igWeight;
        }
        else
        {
            return BB_UNITY_WEIGHT;
        }
    }
#endif
}
