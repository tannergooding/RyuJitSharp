// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitLoopAlignAdjustments()
    {
        if (emitAlignList is null)
        {
            return;
        }

        EnsureLoopAlignmentTargetSupported();
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LoopAlignmentAdjustmentMissingCompiler);
            return;
        }
        JITDUMP("*************** In emitLoopAlignAdjustments()\n");
        JITDUMP($"compJitAlignLoopAdaptive       = {(compiler.opts.compJitAlignLoopAdaptive ? "true" : "false")}\n");
        JITDUMP($"compJitAlignLoopBoundary       = {compiler.opts.compJitAlignLoopBoundary}\n");
        JITDUMP($"compJitAlignLoopMinBlockWeight = {compiler.opts.compJitAlignLoopMinBlockWeight}\n");
        if (Configuration.DEBUG)
        {
            JITDUMP($"compJitAlignLoopForJcc         = {(compiler.opts.compJitAlignLoopForJcc ? "true" : "false")}\n");
        }
        JITDUMP($"compJitAlignLoopMaxCodeSize    = {compiler.opts.compJitAlignLoopMaxCodeSize}\n");
        JITDUMP($"compJitAlignPaddingLimit       = {compiler.opts.compJitAlignPaddingLimit}\n");

        uint estimatedPaddingNeeded = compiler.opts.compJitAlignPaddingLimit;
        uint alignBytesRemoved = 0;
        var alignInstr = emitAlignList;

        while (alignInstr is not null)
        {
            assert(alignInstr.idIns() == INS_align);

            var loopHeadPredIG = alignInstr.idaLoopHeadPredIG;
            if (loopHeadPredIG is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.AlignmentDescriptorMissingLoopHeadPredecessor);
                return;
            }
            var loopHeadIG = alignInstr.loopHeadIG();
            if (loopHeadIG is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.AlignmentDescriptorMissingLoopHeadGroup);
                return;
            }
            var containingIG = alignInstr.idaIG;
            if (containingIG is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.AlignmentDescriptorMissingContainingGroup);
                return;
            }

            JITDUMP($"  Adjusting 'align' instruction in IG{containingIG.GetDisplayId():D2} " +
                $"that is targeted for IG{loopHeadIG.GetDisplayId():D2} \n");

            // Offsets through the next align group still include the cumulative padding removed so far.
            var loopIGOffset = unchecked(loopHeadIG.igOffs - alignBytesRemoved - estimatedPaddingNeeded);
            var actualPaddingNeeded = containingIG.endsWithAlignInstr()
                ? emitCalculatePaddingForLoopAlignment(loopHeadIG, loopIGOffset, false, containingIG, loopHeadPredIG)
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

                if (Configuration.TARGET_XARCH && compiler.opts.compJitAlignLoopAdaptive)
                {
                    assert(actualPaddingNeeded < MAX_XARCH_ALIGN_INSTR_SIZE);
                    alignInstr.idSetXarchCodeSize(actualPaddingNeeded);
                }
                else
                {
                    var paddingToAdjust = actualPaddingNeeded;
                    int instructionsToAdjust;
                    if (Configuration.DEBUG)
                    {
                        if (Configuration.TARGET_XARCH)
                        {
                            instructionsToAdjust = unchecked((int)((compiler.opts.compJitAlignLoopBoundary
                                + (MAX_XARCH_ALIGN_INSTR_SIZE - 1)) / MAX_XARCH_ALIGN_INSTR_SIZE));
                        }
                        else
                        {
                            var arm64InstructionsToAdjust = unchecked((ushort)((compiler.opts.compJitAlignLoopBoundary >> 1)
                                / INSTR_ENCODED_SIZE));
                            if (!compiler.opts.compJitAlignLoopAdaptive)
                            {
                                arm64InstructionsToAdjust = unchecked((ushort)(compiler.opts.compJitAlignLoopBoundary
                                    / INSTR_ENCODED_SIZE));
                            }
                            instructionsToAdjust = arm64InstructionsToAdjust;
                        }
                    }
                    for (var current = alignInstr;
                        (current is not null) && ReferenceEquals(current.idaIG, containingIG);
                        current = current.idaNext)
                    {
                        uint newPadding;
                        if (Configuration.TARGET_XARCH)
                        {
                            newPadding = System.Math.Min(paddingToAdjust, MAX_XARCH_ALIGN_INSTR_SIZE);
                            current.idSetXarchCodeSize(newPadding);
                        }
                        else
                        {
                            newPadding = System.Math.Min(paddingToAdjust, INSTR_ENCODED_SIZE);
                            if (Configuration.TARGET_ARMARCH && newPadding == 0)
                            {
                                current.idInsOpt(INS_OPTS_NONE);
                            }
                        }
                        paddingToAdjust -= newPadding;
                        if (Configuration.DEBUG)
                        {
                            if (Configuration.TARGET_XARCH)
                            {
                                instructionsToAdjust--;
                            }
                            else
                            {
                                instructionsToAdjust = unchecked((ushort)(instructionsToAdjust - 1));
                            }
                        }
                    }

                    assert(paddingToAdjust == 0);
                    if (Configuration.DEBUG)
                    {
                        assert(instructionsToAdjust == 0);
                    }
                }

                JITDUMP($"Adjusted alignment for {emitLabelString(loopHeadIG)} " +
                    $"from {estimatedPaddingNeeded} to {actualPaddingNeeded}.\n");
                JITDUMP($"Adjusted size of {emitLabelString(containingIG)} " +
                    $"from {containingIG.igSize + diff} to {containingIG.igSize}.\n");
            }

            var nextAlign = emitAlignInNextIG(alignInstr);
            var adjOffIG = containingIG.igNext;
            insGroup? adjOffUptoIG;
            if (nextAlign is not null)
            {
                adjOffUptoIG = nextAlign.idaIG;
                if (adjOffUptoIG is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.AlignmentDescriptorMissingContainingGroup
                    );
                    return;
                }
            }
            else
            {
                adjOffUptoIG = emitIGlast;
                if (adjOffUptoIG is null)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.AlignmentAdjustmentRequiresAFinalInstructionGroup
    );
                    return;
                }
            }

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

        emitCheckIGList();
    }
}
