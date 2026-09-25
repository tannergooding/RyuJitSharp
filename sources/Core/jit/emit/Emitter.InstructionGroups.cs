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

#if DEBUG
        ig.igSelf = ig;
        ig.igDataSize = 0;
#endif

        emitInitIG(ig);

        return ig;
    }

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
        ig.igSize = 0;
        ig.igGCregs = regMask.SRBM_NONE;
        ig.igInsCnt = 0;

#if TARGET_XARCH
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

#if TARGET_XARCH
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
