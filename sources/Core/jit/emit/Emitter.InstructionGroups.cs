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

        global::RyuJitSharp.insGroup __jitExceptionResult292;
        var __jitExceptionCoalesceLeft293 = emitCurIG;
        if (__jitExceptionCoalesceLeft293 != null)
        {
            __jitExceptionResult292 = __jitExceptionCoalesceLeft293;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupMustPrecedeTheNewGroup);
            __jitExceptionResult292 = default;
        }
        var currentIG = __jitExceptionResult292;
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

        global::RyuJitSharp.Compiler __jitExceptionResult294;
        var __jitExceptionCoalesceLeft295 = _compiler;
        if (__jitExceptionCoalesceLeft295 != null)
        {
            __jitExceptionResult294 = __jitExceptionCoalesceLeft295;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupsRequireAnActiveCompiler);
            __jitExceptionResult294 = default;
        }
        var compiler = __jitExceptionResult294;
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
        global::RyuJitSharp.Compiler __jitExceptionResult296;
        var __jitExceptionCoalesceLeft297 = _compiler;
        if (__jitExceptionCoalesceLeft297 != null)
        {
            __jitExceptionResult296 = __jitExceptionCoalesceLeft297;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InstructionGroupsRequireAnActiveCompiler);
            __jitExceptionResult296 = default;
        }
        var compiler = __jitExceptionResult296;

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
