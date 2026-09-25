// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_LOOP_ALIGN
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitLoopAlignAdjustments()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Loop-alignment adjustment requires AMD64.");
#else
        if (emitAlignList is null)
        {
            return;
        }

        var compiler = _compiler ?? throw new FatalJitException("Loop-alignment adjustment requires an active compiler.");
        JITDUMP("*************** In emitLoopAlignAdjustments()\n");
        JITDUMP($"compJitAlignLoopAdaptive       = {(compiler.opts.compJitAlignLoopAdaptive ? "true" : "false")}\n");
        JITDUMP($"compJitAlignLoopBoundary       = {compiler.opts.compJitAlignLoopBoundary}\n");
        JITDUMP($"compJitAlignLoopMinBlockWeight = {compiler.opts.compJitAlignLoopMinBlockWeight}\n");
#if DEBUG
        JITDUMP($"compJitAlignLoopForJcc         = {(compiler.opts.compJitAlignLoopForJcc ? "true" : "false")}\n");
#endif
        JITDUMP($"compJitAlignLoopMaxCodeSize    = {compiler.opts.compJitAlignLoopMaxCodeSize}\n");
        JITDUMP($"compJitAlignPaddingLimit       = {compiler.opts.compJitAlignPaddingLimit}\n");

        uint estimatedPaddingNeeded = compiler.opts.compJitAlignPaddingLimit;
        uint alignBytesRemoved = 0;
        var alignInstr = emitAlignList;

        while (alignInstr is not null)
        {
            assert(alignInstr.idIns() == INS_align);

            var loopHeadPredIG = alignInstr.idaLoopHeadPredIG
                ?? throw new FatalJitException("An alignment descriptor requires a loop-head predecessor.");
            var loopHeadIG = alignInstr.loopHeadIG()
                ?? throw new FatalJitException("An alignment descriptor requires a loop-head group.");
            var containingIG = alignInstr.idaIG
                ?? throw new FatalJitException("An alignment descriptor requires a containing group.");

            JITDUMP($"  Adjusting 'align' instruction in IG{containingIG.GetDisplayId():D2} " +
                $"that is targeted for IG{loopHeadIG.GetDisplayId():D2} \n");

            // Offsets through the next align group still include the cumulative padding removed so far.
            var loopIGOffset = unchecked(loopHeadIG.igOffs - alignBytesRemoved - estimatedPaddingNeeded);
            var actualPaddingNeeded = containingIG.endsWithAlignInstr()
                ? emitCalculatePaddingForLoopAlignment(loopHeadIG, loopIGOffset
#if DEBUG
                    , false, containingIG, loopHeadPredIG
#endif
                    )
                : 0u;

            assert(estimatedPaddingNeeded >= actualPaddingNeeded);
            var diff = unchecked((ushort)(estimatedPaddingNeeded - actualPaddingNeeded));

            if (diff != 0)
            {
                containingIG.igSize = unchecked((ushort)(containingIG.igSize - diff));
                alignBytesRemoved = unchecked(alignBytesRemoved + diff);
                emitTotalCodeSize = unchecked(emitTotalCodeSize - diff);

                containingIG.igFlags |= InsGroupFlags.UpdatedInstructionSize;
                if (actualPaddingNeeded == 0)
                {
                    alignInstr.removeAlignFlags();
                }

                if (compiler.opts.compJitAlignLoopAdaptive)
                {
                    assert(actualPaddingNeeded < MAX_ENCODED_SIZE);
                    alignInstr.idCodeSize(actualPaddingNeeded);
                }
                else
                {
                    var paddingToAdjust = actualPaddingNeeded;
#if DEBUG
                    var instructionsToAdjust = (compiler.opts.compJitAlignLoopBoundary + (MAX_ENCODED_SIZE - 1))
                        / MAX_ENCODED_SIZE;
#endif
                    for (var current = alignInstr;
                        (current is not null) && ReferenceEquals(current.idaIG, containingIG);
                        current = current.idaNext)
                    {
                        var newPadding = System.Math.Min(paddingToAdjust, MAX_ENCODED_SIZE);
                        current.idCodeSize(newPadding);
                        paddingToAdjust -= newPadding;
#if DEBUG
                        instructionsToAdjust--;
#endif
                    }

                    assert(paddingToAdjust == 0);
#if DEBUG
                    assert(instructionsToAdjust == 0);
#endif
                }

                JITDUMP($"Adjusted alignment for {emitLabelString(loopHeadIG)} " +
                    $"from {estimatedPaddingNeeded} to {actualPaddingNeeded}.\n");
                JITDUMP($"Adjusted size of {emitLabelString(containingIG)} " +
                    $"from {containingIG.igSize + diff} to {containingIG.igSize}.\n");
            }

            var nextAlign = emitAlignInNextIG(alignInstr);
            var adjOffIG = containingIG.igNext;
            var adjOffUptoIG = nextAlign is not null
                ? nextAlign.idaIG ?? throw new FatalJitException("An alignment descriptor requires a containing group.")
                : emitIGlast ?? throw new FatalJitException("Alignment adjustment requires a final instruction group.");

            while ((adjOffIG is not null)
                && (ReferenceEquals(adjOffIG, adjOffUptoIG) || adjOffIG.IsBefore(adjOffUptoIG)))
            {
                var previousOffset = adjOffIG.igOffs;
                adjOffIG.igOffs = unchecked(previousOffset - alignBytesRemoved);
                JITDUMP($"Adjusted offset of {emitLabelString(adjOffIG)} " +
                    $"from {previousOffset:X4} to {adjOffIG.igOffs:X4}\n");
                adjOffIG = adjOffIG.igNext;
            }

            alignInstr = nextAlign;
            if (actualPaddingNeeded > 0)
            {
                JITDUMP($"Recording last aligned IG: {emitLabelString(loopHeadPredIG)}\n");
                emitLastAlignedIg = loopHeadPredIG;
            }
        }

#if DEBUG
        emitCheckIGList();
#endif
#endif
    }
}
#endif
