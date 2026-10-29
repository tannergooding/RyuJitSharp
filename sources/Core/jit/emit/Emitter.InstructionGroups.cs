// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.EmitterStatistics;

namespace RyuJitSharp;

public partial class Emitter
{
    private const InsGroupFlags IGF_PROPAGATE_MASK =
        InsGroupFlags.Prolog | InsGroupFlags.Epilog | InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog;

    private insGroup emitAllocAndLinkIG()
    {
        var ig = emitAllocIG();

        var currentIG = emitCurIG;
        if (currentIG is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupMustPrecedeTheNewGroup);
        }
        emitInsertIGAfter(currentIG, ig);

        ig.igFlags |= currentIG.igFlags & IGF_PROPAGATE_MASK;
        emitCurIG = ig;

        return ig;
    }

    private insGroup emitAllocIG()
    {
        insGroup ig = new();

        if (Configuration.EMITTER_STATS)
        {
            var size = emitNativeIGSize();
            emitTotMemAlloc = unchecked(emitTotMemAlloc + size);
            emitTotalIGcnt = unchecked(emitTotalIGcnt + 1);
            emitTotalIGsize = unchecked(emitTotalIGsize + size);
            emitSizeMethod = unchecked(emitSizeMethod + size);
        }

        if (Configuration.DEBUG)
        {
            ig.igSelf = ig;
            ig.igDataSize = 0;
        }

        emitInitIG(ig);

        return ig;
    }

    private void emitInitIG(insGroup ig)
    {
        ig.InitializeNum(unchecked((uint)emitNxtIGnum));
        emitNxtIGnum = unchecked(emitNxtIGnum + 1);

        ig.igOffs = unchecked((uint)emitCurCodeOffset);
        assert(IsCodeAligned(ig.igOffs));

        var compiler = _compiler;
        if (compiler is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupsRequireAnActiveCompiler);
        }
        ig.igFuncIdx = compiler.compCurrFuncIdx;
        ig.igFlags = InsGroupFlags.None;

        ig.igWeight = 0;
        ig.igPerfScore = 0.0;
        if (Configuration.DEBUG || Configuration.LATE_DISASM)
        {
            ig.igWeight = getCurrentBlockWeight();
        }

        ig.igData = null;
        ig.igPhData = null;
        ig.igStorageSize = 0;
        ig.igDataOffset = 0;
        ig.SavedGcVars = [];
        ig.SavedByrefRegs = 0;
        ig.igSize = 0;
        ig.igGCregs = regMask.SRBM_NONE;
        ig.igInsCnt = 0;

        if (Configuration.TARGET_XARCH)
        {
            ig.igLastIns = null;
        }

        if (Configuration.FEATURE_LOOP_ALIGN)
        {
            ig.igLoopBackEdge = null;
        }

        if (Configuration.DEBUG)
        {
            ig.lastGeneratedBlock = null;
        }
    }

    private void emitInsertIGAfter(insGroup insertAfterIG, insGroup ig)
    {
        assert(emitIGlist is not null);
        assert(emitIGlast is not null);

        ig.igNext = insertAfterIG.igNext;
        insertAfterIG.igNext = ig;

        if (Configuration.TARGET_XARCH)
        {
            ig.igPrev = insertAfterIG;
            ig.igNext?.igPrev = ig;
        }

        if (emitIGlast == insertAfterIG)
        {
            emitIGlast = ig;
        }
    }

    private weight_t getCurrentBlockWeight()
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupsRequireAnActiveCompiler);
        }

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
}
