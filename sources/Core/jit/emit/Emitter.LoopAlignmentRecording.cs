// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_LOOP_ALIGN
namespace RyuJitSharp;

public partial class Emitter
{
    public bool emitEndsWithAlignInstr()
    {
        assert(emitCurIG is not null);
        return emitCurIG.endsWithAlignInstr();
    }

    public void emitConnectAlignInstrWithCurIG()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Loop alignment recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(emitAlignLastGroup is not null);
        assert(emitAlignLastGroup.idaIG is not null);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"Mapping 'align' instruction in IG{emitAlignLastGroup.idaIG.GetDisplayId():D2} " +
                $"to target IG{emitCurIG.GetDisplayId():D2}\n");
        }
#endif
        // Track the predecessor because the next group, not this one, will start the loop.
        emitAlignLastGroup.idaLoopHeadPredIG = emitCurIG;
        emitNxtIG();
#endif
    }

    public void emitLoopAlignment(
#if DEBUG
        bool isPlacedBehindJmp
#endif
        )
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Loop alignment recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(_compiler is not null);
        uint paddingBytes;
        if ((_compiler.opts.compJitAlignLoopBoundary > 16) && !_compiler.opts.compJitAlignLoopAdaptive)
        {
            paddingBytes = _compiler.opts.compJitAlignLoopBoundary;
            emitLongLoopAlign(paddingBytes
#if DEBUG
                , isPlacedBehindJmp
#endif
                );
        }
        else
        {
            emitCheckAlignFitInCurIG(1);
            paddingBytes = MAX_ENCODED_SIZE;
            emitLoopAlign(paddingBytes, true
#if DEBUG
                , isPlacedBehindJmp
#endif
                );
        }

        assert(emitLastIns is not null);
        assert(emitLastIns.idIns() == INS_align);
#if DEBUG
        if (_compiler.verbose)
        {
            assert(emitCurIG is not null);
            jitprintf($"Adding 'align' instruction of {paddingBytes} bytes in {emitLabelString(emitCurIG)}.\n");
        }
#endif
#endif
    }

    public bool emitSetLoopBackEdge(BasicBlock loopTopBlock)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Loop back-edge recording requires AMD64.");
#else
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(loopTopBlock.isLoopAlign);
        var dstIG = emitCodeGetCookie(loopTopBlock);
        if (dstIG is null)
        {
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf($"ALIGN: loop block BB{loopTopBlock.bbNum:D2} " +
                    "needing alignment has not been generated yet; not marking IG back edge.\n");
            }
#endif
            return false;
        }

        if (dstIG.IsAfter(emitCurIG))
        {
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf($"ALIGN: found forward branch from IG{emitCurIG.GetDisplayId():D2} " +
                    $"to IG{dstIG.GetDisplayId():D2}; not marking IG back edge.\n");
            }
#endif
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
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf($"** IG{currLoopEnd.GetDisplayId():D2} jumps back to " +
                    $"IG{currLoopStart.GetDisplayId():D2} forming a loop.\n");
            }
#endif
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
#if DEBUG
                    if (_compiler.verbose)
                    {
                        jitprintf($";; Skip alignment for current loop IG{currLoopStart.GetDisplayId():D2} ~ " +
                            $"IG{currLoopEnd.GetDisplayId():D2} because it encloses an aligned loop " +
                            $"IG{emitLastLoopStart.GetDisplayId():D2} ~ IG{emitLastLoopEnd.GetDisplayId():D2}.\n");
                    }
#endif
                }

                if (!alignLastLoop && (loopHeadIG is not null) && (loopHeadIG == emitLastLoopStart))
                {
                    assert(!markedLastLoop);
                    assert(alignInstr.idaIG is not null);
                    assert(alignInstr.idaIG.endsWithAlignInstr() || alignInstr.idaIG.hadAlignInstr());
                    alignInstr.removeAlignFlags();
                    markedLastLoop = true;
#if DEBUG
                    if (_compiler.verbose)
                    {
                        jitprintf($";; Skip alignment for aligned loop IG{emitLastLoopStart.GetDisplayId():D2} ~ " +
                            $"IG{emitLastLoopEnd.GetDisplayId():D2} because it encloses the current loop " +
                            $"IG{currLoopStart.GetDisplayId():D2} ~ IG{currLoopEnd.GetDisplayId():D2}.\n");
                    }
#endif
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
#endif
    }

#if TARGET_AMD64
    private void emitCheckAlignFitInCurIG(uint nAlignInstr)
    {
        var instrDescSize = unchecked(nAlignInstr * ((nuint)_debugInfoSize + DescriptorSizes.Align));
        if (unchecked(emitCurIGfreeNext + instrDescSize) >= emitCurIGfreeEndp)
        {
            emitForceNewIG = true;
        }
    }

    private void emitLoopAlign(uint paddingBytes, bool isFirstAlign
#if DEBUG
        , bool isPlacedBehindJmp
#endif
        )
    {
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

        assert(paddingBytes <= MAX_ENCODED_SIZE);
        id.idCodeSize(paddingBytes);
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
#if DEBUG
        id.isPlacedAfterJmp = isPlacedBehindJmp;
#endif
        id.idaNext = emitCurIGAlignList;
        emitCurIGsize = unchecked(emitCurIGsize + (int)paddingBytes);
        dispIns(id);
        emitCurIGAlignList = id;
    }

    private void emitLongLoopAlign(uint alignmentBoundary
#if DEBUG
        , bool isPlacedBehindJmp
#endif
        )
    {
        var nPaddingBytes = unchecked(alignmentBoundary - 1);
        var nAlignInstr = unchecked(nPaddingBytes + (MAX_ENCODED_SIZE - 1)) / MAX_ENCODED_SIZE;
        var insAlignCount = nPaddingBytes / MAX_ENCODED_SIZE;
        var lastInsAlignSize = nPaddingBytes % MAX_ENCODED_SIZE;
        emitCheckAlignFitInCurIG(nAlignInstr);

        var isFirstAlign = true;
        while (insAlignCount != 0)
        {
            emitLoopAlign(MAX_ENCODED_SIZE, isFirstAlign
#if DEBUG
                , isPlacedBehindJmp
#endif
                );
            insAlignCount--;
            isFirstAlign = false;
        }

        // Native also records the zero-sized remainder when padding is a multiple of 15.
        emitLoopAlign(lastInsAlignSize, isFirstAlign
#if DEBUG
            , isPlacedBehindJmp
#endif
            );
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
#endif
}
#endif
