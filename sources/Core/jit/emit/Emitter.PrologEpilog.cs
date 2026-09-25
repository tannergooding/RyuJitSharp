// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insGroupPlaceholderType;

namespace RyuJitSharp;

public partial class Emitter
{
    public VARSET_TP InitGCrefVars => emitInitGCrefVars;

    public regMaskTP InitGCrefRegs => new(emitInitGCrefRegs);

    public regMaskTP InitByrefRegs => new(emitInitByrefRegs);

    public void emitGeneratePrologEpilog()
    {
        RequireSupportedInstructionRecording();
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
        RequireSupportedInstructionRecording();
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
            assert((group.igOffs & (CODE_ALIGN - 1)) == 0);
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
        RequireSupportedInstructionRecording();
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
        RequireSupportedInstructionRecording();
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
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Epilog materialization requires AMD64.");
#else
        emitEpilogCnt++;
        emitBegPrologEpilog(placeholder);
#endif
    }

    public void emitEndFnEpilog()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Epilog materialization requires AMD64.");
#else
        emitEndPrologEpilog();
#endif
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

    public void emitStartExitSeq()
    {
        assert(emitGeneratingEpilogOrFuncletEpilog());
        emitExitSeqBegLoc.CaptureLocation(this);
    }

    public void emitSetFrameRangeGCRs(int offsLo, int offsHi)
    {
        RequireSupportedInstructionRecording();
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
                jitprintf($"-{unchecked(-offsLo):X4} ... {offsHi:X4}\n");
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
}
