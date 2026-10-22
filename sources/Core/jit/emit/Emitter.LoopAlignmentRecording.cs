// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private const uint MAX_XARCH_ALIGN_INSTR_SIZE = 15;
    private const uint INSTR_ENCODED_SIZE = 4;

    public bool emitEndsWithAlignInstr()
    {
        assert(emitCurIG is not null);
        return emitCurIG.endsWithAlignInstr();
    }

    public void emitConnectAlignInstrWithCurIG()
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        EnsureLoopAlignmentTargetSupported();
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(emitAlignLastGroup is not null);
        assert(emitAlignLastGroup.idaIG is not null);
        if (Configuration.DEBUG && _compiler!.verbose)
        {
            jitprintf($"Mapping 'align' instruction in IG{emitAlignLastGroup.idaIG.GetDisplayId():D2} " +
                $"to target IG{emitCurIG.GetDisplayId():D2}\n");
        }
        // Track the predecessor because the next group, not this one, will start the loop.
        emitAlignLastGroup.idaLoopHeadPredIG = emitCurIG;
        emitNxtIG();
    }

    public void emitLoopAlignment(bool isPlacedBehindJmp)
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        EnsureLoopAlignmentTargetSupported();
        assert(_compiler is not null);
        uint paddingBytes;
        if (Configuration.TARGET_XARCH)
        {
            if ((_compiler.opts.compJitAlignLoopBoundary > 16) && !_compiler.opts.compJitAlignLoopAdaptive)
            {
                paddingBytes = _compiler.opts.compJitAlignLoopBoundary;
                emitLongLoopAlign(paddingBytes, isPlacedBehindJmp);
            }
            else
            {
                emitCheckAlignFitInCurIG(1);
                paddingBytes = MAX_XARCH_ALIGN_INSTR_SIZE;
                emitLoopAlign(paddingBytes, true, isPlacedBehindJmp);
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            paddingBytes = _compiler.opts.compJitAlignLoopAdaptive
                ? (uint)_compiler.opts.compJitAlignLoopBoundary >> 1
                : _compiler.opts.compJitAlignLoopBoundary;
            emitLongLoopAlign(paddingBytes, isPlacedBehindJmp);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CorJitResult.CORJIT_SKIPPED,
        FatalJitErrorCode.LoopAlignmentIsUnsupportedOnThisTarget
    );
            return;
        }

        assert(emitLastIns is not null);
        assert(emitLastIns.idIns() == INS_align);
        if (Configuration.DEBUG && _compiler!.verbose)
        {
            assert(emitCurIG is not null);
            jitprintf($"Adding 'align' instruction of {paddingBytes} bytes in {emitLabelString(emitCurIG)}.\n");
        }
    }

    public bool emitSetLoopBackEdge(BasicBlock loopTopBlock)
    {
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(loopTopBlock.isLoopAlign);
        var dstIG = emitCodeGetCookie(loopTopBlock);
        if (dstIG is null)
        {
            if (Configuration.DEBUG && _compiler!.verbose)
            {
                jitprintf($"ALIGN: loop block BB{loopTopBlock.bbNum:D2} " +
                    "needing alignment has not been generated yet; not marking IG back edge.\n");
            }
            return false;
        }

        if (dstIG.IsAfter(emitCurIG))
        {
            if (Configuration.DEBUG && _compiler!.verbose)
            {
                jitprintf($"ALIGN: found forward branch from IG{emitCurIG.GetDisplayId():D2} " +
                    $"to IG{dstIG.GetDisplayId():D2}; not marking IG back edge.\n");
            }
            return false;
        }

        var backEdgeSet = false;
        var alignCurrentLoop = true;
        var alignLastLoop = true;
        var currLoopStart = dstIG;
        var currLoopEnd = emitCurIG;

        if ((emitLastLoopEnd is null) || emitLastLoopEnd.IsBefore(currLoopStart))
        {
            assert(emitCurIG.igLoopBackEdge is null);
            emitCurIG.igLoopBackEdge = dstIG;
            backEdgeSet = true;
            if (Configuration.DEBUG && _compiler!.verbose)
            {
                jitprintf($"** IG{currLoopEnd.GetDisplayId():D2} jumps back to " +
                    $"IG{currLoopStart.GetDisplayId():D2} forming a loop.\n");
            }
            emitLastLoopStart = currLoopStart;
            emitLastLoopEnd = currLoopEnd;
        }
        else
        {
            assert(emitLastLoopStart is not null);
            if (currLoopStart == emitLastLoopStart)
            {
                // Keep the smaller loop's alignment when the headers coincide.
            }
            else if (currLoopStart.IsBefore(emitLastLoopStart) && emitLastLoopEnd.IsBefore(currLoopEnd))
            {
                alignCurrentLoop = false;
            }
            else if (emitLastLoopStart.IsBefore(currLoopStart) && currLoopEnd.IsBefore(emitLastLoopEnd))
            {
                alignLastLoop = false;
            }
            else
            {
                // Intersecting loops should not align either header.
                alignLastLoop = false;
                alignCurrentLoop = false;
            }
        }

        if (!alignLastLoop || !alignCurrentLoop)
        {
            assert(emitLastLoopStart is not null);
            assert(emitLastLoopEnd is not null);
            var alignInstr = emitAlignList;
            var markedLastLoop = alignLastLoop;
            var markedCurrLoop = alignCurrentLoop;
            while (alignInstr is not null)
            {
                var loopHeadIG = alignInstr.loopHeadIG();
                if (!alignCurrentLoop && (loopHeadIG == dstIG))
                {
                    assert(!markedCurrLoop);
                    alignInstr.removeAlignFlags();
                    markedCurrLoop = true;
                    if (Configuration.DEBUG && _compiler!.verbose)
                    {
                        jitprintf($";; Skip alignment for current loop IG{currLoopStart.GetDisplayId():D2} ~ " +
                            $"IG{currLoopEnd.GetDisplayId():D2} because it encloses an aligned loop " +
                            $"IG{emitLastLoopStart.GetDisplayId():D2} ~ IG{emitLastLoopEnd.GetDisplayId():D2}.\n");
                    }
                }

                if (!alignLastLoop && (loopHeadIG is not null) && (loopHeadIG == emitLastLoopStart))
                {
                    assert(!markedLastLoop);
                    assert(alignInstr.idaIG is not null);
                    assert(alignInstr.idaIG.endsWithAlignInstr() || alignInstr.idaIG.hadAlignInstr());
                    alignInstr.removeAlignFlags();
                    markedLastLoop = true;
                    if (Configuration.DEBUG && _compiler!.verbose)
                    {
                        jitprintf($";; Skip alignment for aligned loop IG{emitLastLoopStart.GetDisplayId():D2} ~ " +
                            $"IG{emitLastLoopEnd.GetDisplayId():D2} because it encloses the current loop " +
                            $"IG{currLoopStart.GetDisplayId():D2} ~ IG{currLoopEnd.GetDisplayId():D2}.\n");
                    }
                }

                if (markedLastLoop && markedCurrLoop)
                {
                    break;
                }
                alignInstr = emitAlignInNextIG(alignInstr);
            }
            assert(markedLastLoop && markedCurrLoop);
        }

        return backEdgeSet;
    }

    private void emitCheckAlignFitInCurIG(uint nAlignInstr)
    {
        var instrDescSize = unchecked(nAlignInstr * ((nuint)_debugInfoSize + (nuint)DescriptorSizes.Align));
        if (unchecked(emitCurIGfreeNext + instrDescSize) >= emitCurIGfreeEndp)
        {
            emitForceNewIG = true;
        }
    }

    private void emitLoopAlign(uint paddingBytes, bool isFirstAlign, bool isPlacedBehindJmp)
    {
        EnsureLoopAlignmentTargetSupported();
        assert(emitCurIG is not null);
        var alignInstrInNewIG = emitForceNewIG;
        if (!alignInstrInNewIG)
        {
            emitCurIG.igFlags |= InsGroupFlags.HasAlign;
        }

        var id = emitNewInstrAlign();
        if (alignInstrInNewIG)
        {
            emitCurIG.igFlags |= InsGroupFlags.HasAlign;
        }
        else
        {
            assert(emitCurIG.endsWithAlignInstr());
        }

        if (Configuration.TARGET_XARCH)
        {
            assert(paddingBytes <= MAX_XARCH_ALIGN_INSTR_SIZE);
            id.idSetXarchCodeSize(paddingBytes);
        }
        else
        {
            assert(paddingBytes == INSTR_ENCODED_SIZE);
        }
        id.idaIG = emitCurIG;
        if (isFirstAlign)
        {
            id.idaLoopHeadPredIG = emitCurIG;
            emitAlignLastGroup = id;
        }
        else
        {
            id.idaLoopHeadPredIG = null;
        }
        id.isPlacedAfterJmp = isPlacedBehindJmp;
        id.idaNext = emitCurIGAlignList;
        emitCurIGsize = unchecked(emitCurIGsize + (int)paddingBytes);
        dispIns(id);
        emitCurIGAlignList = id;
    }

    private void emitLongLoopAlign(uint alignmentBoundary, bool isPlacedBehindJmp)
    {
        EnsureLoopAlignmentTargetSupported();
        uint nAlignInstr;
        uint insAlignCount;
        uint paddingBytes;
        uint lastInsAlignSize = 0;
        if (Configuration.TARGET_XARCH)
        {
            var nPaddingBytes = unchecked(alignmentBoundary - 1);
            nAlignInstr = unchecked(nPaddingBytes + (MAX_XARCH_ALIGN_INSTR_SIZE - 1)) / MAX_XARCH_ALIGN_INSTR_SIZE;
            insAlignCount = nPaddingBytes / MAX_XARCH_ALIGN_INSTR_SIZE;
            lastInsAlignSize = nPaddingBytes % MAX_XARCH_ALIGN_INSTR_SIZE;
            paddingBytes = MAX_XARCH_ALIGN_INSTR_SIZE;
        }
        else
        {
            nAlignInstr = alignmentBoundary / INSTR_ENCODED_SIZE;
            insAlignCount = nAlignInstr;
            paddingBytes = INSTR_ENCODED_SIZE;
        }
        emitCheckAlignFitInCurIG(nAlignInstr);

        var isFirstAlign = true;
        while (insAlignCount != 0)
        {
            emitLoopAlign(paddingBytes, isFirstAlign, isPlacedBehindJmp);
            insAlignCount--;
            isFirstAlign = false;
        }

        if (Configuration.TARGET_XARCH)
        {
            // Native also records the zero-sized remainder when padding is a multiple of 15.
            emitLoopAlign(lastInsAlignSize, isFirstAlign, isPlacedBehindJmp);
        }
    }

    private static void EnsureLoopAlignmentTargetSupported()
    {
        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(
        CorJitResult.CORJIT_SKIPPED,
        FatalJitErrorCode.LoopAlignmentIsUnsupportedOnThisTarget
    );
            return;
        }
    }

    private static instrDescAlign? emitAlignInNextIG(instrDescAlign alignInstr)
    {
        var alignIG = alignInstr.idaIG;
        while ((alignInstr.idaNext is not null) && (alignInstr.idaNext.idaIG == alignIG))
        {
            alignInstr = alignInstr.idaNext;
        }

        return alignInstr.idaNext;
    }
}
