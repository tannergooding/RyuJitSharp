// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using GCtype = RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitRecordGCcall(byte* codePos, byte callInstrSize)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "GC call output requires Windows AMD64.");
#else
#if DEBUG
        assert(emitIssuing);
#endif
        assert(!emitFullGCinfo);
        var offs = emitCurCodeOffs(codePos);

#if DEBUG
        var compiler = _compiler ?? throw new FatalJitException("GC call output requires an active compiler.");
        if (compiler.verbose)
        {
            jitprintf($"; Call at {unchecked(offs - callInstrSize):X4} [stk={emitCurStackLvl}], GCvars=");
            emitDispVarSet();
            jitprintf(", gcrefRegs=");
            printRegMaskInt(new regMaskTP(emitThisGCrefRegs));
            emitDispRegSet(new regMaskTP(emitThisGCrefRegs));
            jitprintf(", byrefRegs=");
            printRegMaskInt(new regMaskTP(emitThisByrefRegs));
            emitDispRegSet(new regMaskTP(emitThisByrefRegs));
            jitprintf("\n");
        }
#endif

#if EMIT_TRACK_STACK_DEPTH
        noway_assert((uint)emitCurStackLvl / sizeof(uint) <= ushort.MaxValue);
#endif

        if (emitSimpleStkUsed)
        {
            gcInfo.gcCallDescAppend(offs, callInstrSize, emitThisGCrefRegs, emitThisByrefRegs, 0,
                unchecked((uint)u1.emitSimpleStkMask), unchecked((uint)u1.emitSimpleByrefStkMask));
            return;
        }

        var argCount = u2.emitGcArgTrackCnt;
        gcInfo.gcCallDescAppend(offs, callInstrSize, emitThisGCrefRegs, emitThisByrefRegs, argCount, 0, 0);
        if (argCount == 0)
        {
            return;
        }

        var argTable = gcInfo.gcCallDescAllocArgTable(argCount);
        var gcArgs = 0;
        var stkLvl = (uint)emitCurStackLvl / sizeof(int);
        for (var i = 0u; i < stkLvl; i++)
        {
            var gcType = (GCtype)u2.emitArgTrackTab[stkLvl - i - 1];
            if (emitStackNeedsGC(gcType))
            {
                noway_assert(gcArgs < argTable.Length);
                argTable[gcArgs] = unchecked(i * (uint)TARGET_POINTER_SIZE);
                if (gcType == GCT_BYREF)
                {
                    argTable[gcArgs] |= byref_OFFSET_FLAG;
                }
                gcArgs++;
            }
        }
        assert(gcArgs == argCount);
#endif
    }

#if DEBUG && TARGET_AMD64
    private string emitGetFrameReg()
    {
        return emitHasFramePtr ? STR_FPBASE : STR_SPBASE;
    }

    private void emitDispVarSet()
    {
        var spaced = false;
        var live = emitGCrFrameLiveTab;
        for (var i = 0; i < emitGCrFrameOffsCnt; i++)
        {
            assert(live is not null && i < live.Length);
            if (live[i] is null)
            {
                continue;
            }

            if (spaced)
            {
                jitprintf(" ");
            }
            spaced = true;

            var offset = unchecked(emitGCrFrameOffsMin + i * TARGET_POINTER_SIZE);
            jitprintf($"[{emitGetFrameReg()}");
            if (offset < 0)
            {
                jitprintf($"-0x{unchecked(-offset):X2}");
            }
            else if (offset > 0)
            {
                jitprintf($"+0x{offset:X2}");
            }
            jitprintf("]");
        }

        if (!spaced)
        {
            jitprintf("none");
        }
    }
#endif
}
