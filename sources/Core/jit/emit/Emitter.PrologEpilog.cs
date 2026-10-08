// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insGroupPlaceholderType;

namespace RyuJitSharp;

public partial class Emitter
{
#if !TARGET_AMD64
    internal void RequireSupportedInstructionRecording()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction recording outside AMD64 is not ported.");
    }
#endif

    public VARSET_TP InitGCrefVars => emitInitGCrefVars;

    public regMaskTP InitGCrefRegs => new(emitInitGCrefRegs);

    public regMaskTP InitByrefRegs => new(emitInitByrefRegs);

    public void emitGeneratePrologEpilog()
    {
#if DEBUG
        var prologCount = 0;
        var epilogCount = 0;
        var funcletPrologCount = 0;
        var funcletEpilogCount = 0;
#endif
        for (var placeholder = emitPlaceholderList; placeholder is not null;)
        {
            assert((placeholder.igFlags & InsGroupFlags.Placeholder) != 0);
            var data = placeholder.igPhData;
            assert(data is not null);
            // Beginning generation clears the placeholder data containing this link.
            var next = data.igPhNext;
            var block = data.igPhBB;
            assert(block is not null);
            switch (data.igPhType)
            {
                case IGPT_PROLOG:
                {
#if DEBUG
                    prologCount++;
#endif
                    break;
                }

                case IGPT_EPILOG:
                {
#if DEBUG
                    epilogCount++;
#endif
                    emitBegFnEpilog(placeholder);
                    codeGen.genFnEpilog(block);
                    emitEndFnEpilog();
                    break;
                }

                case IGPT_FUNCLET_PROLOG:
                {
#if DEBUG
                    funcletPrologCount++;
#endif
                    emitBegFuncletProlog(placeholder);
                    codeGen.genFuncletProlog(block);
                    emitEndFuncletProlog();
                    break;
                }

                case IGPT_FUNCLET_EPILOG:
                {
#if DEBUG
                    funcletEpilogCount++;
#endif
                    emitBegFuncletEpilog(placeholder);
                    codeGen.genFuncletEpilog(block);
                    emitEndFuncletEpilog();
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
            placeholder = next;
        }
#if DEBUG
        assert(_compiler is not null);
        if (_compiler.verbose)
        {
            jitprintf($"{prologCount} prologs, {epilogCount} epilogs");
            jitprintf($", {funcletPrologCount} funclet prologs, {funcletEpilogCount} funclet epilogs\n");
            assert(funcletPrologCount == _compiler.ehFuncletCount());
        }
#endif
    }

    public void emitStartPrologEpilogGeneration()
    {
        if (emitCurIG is not null)
        {
            _ = emitSavIG(emitAdd: false);
        }
    }

    public void emitFinishPrologEpilogGeneration()
    {
        emitRecomputeIGoffsets();
        emitCurIG = null;
    }

    private void emitRecomputeIGoffsets()
    {
        uint offset = 0;
        for (var group = emitIGlist; group is not null; group = group.igNext)
        {
            group.igOffs = offset;
            assert(IsCodeAligned(group.igOffs));
            offset = unchecked(offset + group.igSize);
            assert(offset >= group.igOffs);
        }
        emitTotalCodeSize = unchecked((int)offset);
#if DEBUG
        emitCheckIGList();
#endif
    }

    public void emitBegProlog()
    {
        assert(_compiler is not null);
#if EMIT_TRACK_STACK_DEPTH
        emitCntStackDepth = 0;
        assert(emitCurStackLvl == 0);
#endif
        emitNoGCRequestCount = 1;
        emitNoGCIG = true;
        emitForceNewIG = false;
        emitGenIG(emitGetFirstPrologIG());

        VarSetOps.ClearD(_compiler, emitInitGCrefVars);
        VarSetOps.ClearD(_compiler, emitPrevGCrefVars);
        emitInitGCrefRegs = 0;
        emitPrevGCrefRegs = 0;
        emitInitByrefRegs = 0;
        emitPrevByrefRegs = 0;
    }

    public void emitMarkPrologEnd()
    {
        assert(emitGeneratingPrologOrFuncletProlog());
        emitPrologEndPos.CaptureLocation(this);
    }

    public void emitEndProlog()
    {
        assert(emitGeneratingPrologOrFuncletProlog());
        emitNoGCRequestCount = 0;
        emitNoGCIG = false;
        if (emitCurIGnonEmpty() || (emitCurIG == emitGetFirstPrologIG()))
        {
            _ = emitSavIG(emitAdd: false);
        }

#if EMIT_TRACK_STACK_DEPTH
        emitCurStackLvl = 0;
        emitCntStackDepth = sizeof(int);
#endif
    }

    private void emitBegPrologEpilog(insGroup placeholder)
    {
        assert(_compiler is not null);
        assert((placeholder.igFlags & InsGroupFlags.Placeholder) != 0);
        if (emitCurIGnonEmpty())
        {
            _ = emitSavIG(emitAdd: false);
        }

        placeholder.igFlags &= ~InsGroupFlags.Placeholder;
        emitNoGCRequestCount = 1;
        emitNoGCIG = true;
        emitForceNewIG = false;

        var data = placeholder.igPhData;
        assert(data is not null);
        VarSetOps.Assign(_compiler, ref emitPrevGCrefVars, data.igPhPrevGCrefVars);
        emitPrevGCrefRegs = (regMask)data.igPhPrevGCrefRegs;
        emitPrevByrefRegs = (regMask)data.igPhPrevByrefRegs;
        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, data.igPhInitGCrefVars);
        VarSetOps.Assign(_compiler, ref emitInitGCrefVars, data.igPhInitGCrefVars);
        emitThisGCrefRegs = emitInitGCrefRegs = (regMask)data.igPhInitGCrefRegs;
        emitThisByrefRegs = emitInitByrefRegs = (regMask)data.igPhInitByrefRegs;
        _compiler.compCurBB = data.igPhBB;
        placeholder.igPhData = null;

        _compiler.funSetCurrentFunc(placeholder.igFuncIdx);
        emitCurCodeOffset = unchecked((int)placeholder.igOffs);
        emitGenIG(placeholder);
#if EMIT_TRACK_STACK_DEPTH
        emitCntStackDepth = 0;
        assert(emitCurStackLvl == 0);
#endif
    }

    private void emitEndPrologEpilog()
    {
        emitNoGCRequestCount = 0;
        emitNoGCIG = false;
        if (emitCurIGnonEmpty())
        {
            _ = emitSavIG(emitAdd: false);
        }
        assert(emitCurIGsize <= MAX_PLACEHOLDER_IG_SIZE);

#if EMIT_TRACK_STACK_DEPTH
        emitCurStackLvl = 0;
        emitCntStackDepth = sizeof(int);
#endif
    }

    public void emitBegFnEpilog(insGroup placeholder)
    {
        emitEpilogCnt++;
        emitBegPrologEpilog(placeholder);

#if JIT32_GCENCODER
        var epilog = new EpilogList();
        if (emitEpilogLast is not null)
        {
            emitEpilogLast.elNext = epilog;
        }
        else
        {
            emitEpilogList = epilog;
        }

        emitEpilogLast = epilog;
#endif
    }

    public void emitEndFnEpilog()
    {
        emitEndPrologEpilog();

#if JIT32_GCENCODER
        assert(emitEpilogLast is not null);
        var epilogBegin = emitEpilogLast.elLoc.CodeOffset(this);
        var exitSequenceBegin = emitExitSeqBegLoc.CodeOffset(this);
        var epilogSize = unchecked(exitSequenceBegin - epilogBegin);
        assert((emitEpilogSize == 0) || (unchecked((uint)emitEpilogSize) == epilogSize));
        emitEpilogSize = unchecked((int)epilogSize);

        assert(emitCurIG is not null);
        var epilogEnd = emitCodeOffset(emitCurIG, emitCurOffset());
        assert(exitSequenceBegin != epilogEnd);
        var exitSequenceSize = unchecked(epilogEnd - exitSequenceBegin);
        if (exitSequenceSize < unchecked((uint)emitExitSeqSize))
        {
            assert((emitEpilogCnt == 1) || (unchecked((uint)emitExitSeqSize - exitSequenceSize) <= 5u));
            emitExitSeqSize = unchecked((int)exitSequenceSize);
        }
#endif
    }

    public uint emitGetEpilogCnt()
    {
        return unchecked((uint)emitEpilogCnt);
    }

    public void emitBegFuncletProlog(insGroup placeholder)
    {
        emitBegPrologEpilog(placeholder);
    }

    public void emitEndFuncletProlog()
    {
        emitEndPrologEpilog();
    }

    public void emitBegFuncletEpilog(insGroup placeholder)
    {
        emitBegPrologEpilog(placeholder);
    }

    public void emitEndFuncletEpilog()
    {
        emitEndPrologEpilog();
    }

#if JIT32_GCENCODER
    public void emitStartEpilog()
    {
        assert(emitEpilogLast is not null);
        emitEpilogLast.elLoc.CaptureLocation(this);
    }

    public bool emitHasEpilogEnd()
    {
        if (emitEpilogCnt == 1)
        {
            assert(emitIGlast is not null);
            return (emitIGlast.igFlags & InsGroupFlags.Epilog) != 0;
        }

        return false;
    }
#endif

#if TARGET_XARCH
    public void emitStartExitSeq()
    {
        assert(emitGeneratingEpilogOrFuncletEpilog());
        emitExitSeqBegLoc.CaptureLocation(this);
    }
#endif

    public void emitSetFrameRangeGCRs(int offsLo, int offsHi)
    {
        assert(emitGeneratingPrologOrFuncletProlog());
        assert(offsHi > offsLo);
#if DEBUG
        assert(_compiler is not null);
        if (_compiler.verbose)
        {
            var count = unchecked((uint)(offsHi - offsLo)) / TARGET_POINTER_SIZE;
            jitprintf($"{count} tracked GC refs are at stack offsets ");
            if (offsLo >= 0)
            {
                jitprintf($" {offsLo:X4} ...  {offsHi:X4}\n");
                assert(offsHi >= 0);
            }
            else
            {
#if TARGET_ARM && PROFILING_SUPPORTED
                if (!_compiler.compIsProfilerHookNeeded)
                {
#endif
#if TARGET_AMD64
                jitprintf($"-{unchecked(-offsLo):X4} ... {offsHi:X4}\n");
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
                if (offsHi < 0)
                {
                    jitprintf($"-{unchecked(-offsLo):X4} ... -{unchecked(-offsHi):X4}\n");
                }
                else
                {
                    jitprintf($"-{unchecked(-offsLo):X4} ... {offsHi:X4}\n");
                }
#else
                jitprintf($"-{unchecked(-offsLo):X4} ... -{unchecked(-offsHi):X4}\n");
                assert(offsHi <= 0);
#endif
#if TARGET_ARM && PROFILING_SUPPORTED
                }
                else if (offsHi < 0)
                {
                    jitprintf($"-{unchecked(-offsLo):X4} ... -{unchecked(-offsHi):X4}\n");
                }
                else
                {
                    jitprintf($"-{unchecked(-offsLo):X4} ... {offsHi:X4}\n");
                }
#endif
            }
        }
#endif
        assert(((offsHi - offsLo) % TARGET_POINTER_SIZE) == 0);
        assert((offsLo % TARGET_POINTER_SIZE) == 0);
        assert((offsHi % TARGET_POINTER_SIZE) == 0);
        emitGCrFrameOffsMin = offsLo;
        emitGCrFrameOffsMax = offsHi;
        emitGCrFrameOffsCnt = (offsHi - offsLo) / TARGET_POINTER_SIZE;
    }

    public bool emitIsWithinFrameRangeGCRs(int offs)
    {
        return (offs >= emitGCrFrameOffsMin) && (offs < emitGCrFrameOffsMax);
    }
}
