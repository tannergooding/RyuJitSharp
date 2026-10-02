// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe delegate void emitSplitCallbackType(void* context, emitLocation location);

    // unwind.h's FunctionLength encoding limits: ARM/RISC-V use 19 bits, ARM64/LoongArch use 20.
#if TARGET_ARM || TARGET_RISCV64
    private const uint UW_MAX_FRAGMENT_SIZE_BYTES = 1u << 19;
#else
    private const uint UW_MAX_FRAGMENT_SIZE_BYTES = 1u << 20;
#endif

    // The instruction position does not affect whether the group ends a function or funclet.
    public bool emitIsFuncEnd(emitLocation emitLoc, emitLocation? emitLocNextFragment = null)
    {
        var ig = emitLoc.GetIG();
        assert(ig is not null);

        if (emitLocNextFragment.HasValue && (ig.igNext == emitLocNextFragment.Value.GetIG()))
        {
            return true;
        }

        if (ig.igNext is null)
        {
            return true;
        }

        if ((ig.igNext.igFlags & InsGroupFlags.FuncletProlog) != 0)
        {
            return true;
        }

        if ((ig.igNext.igFlags & InsGroupFlags.Placeholder) != 0)
        {
            assert(ig.igNext.igPhData is not null);

            if (ig.igNext.igPhData.igPhType == insGroupPlaceholderType.IGPT_FUNCLET_PROLOG)
            {
                return true;
            }
        }

        return false;
    }

    // Report fragment starts, but not the initial start or the final end. Locations must be
    // group starts; null denotes the start/end of the code. Never split adjacent prolog/epilog
    // groups, even if doing so requires a fragment larger than maxSplitSize.
    public unsafe void emitSplit(emitLocation? startLoc, emitLocation? endLoc, uint maxSplitSize,
        void* context, emitSplitCallbackType callbackFunc)
    {
        var igStart = startLoc.HasValue ? startLoc.Value.GetIG() : emitIGlist;
        var igEnd = endLoc.HasValue ? endLoc.Value.GetIG() : null;
        insGroup? igPrev = null;
        var ig = igStart;
        var igLastReported = igStart;
        insGroup? igLastCandidate = null;
        uint candidateSize = 0;
        uint curSize = 0;

        void SplitIfNecessary()
        {
            if (curSize < maxSplitSize)
            {
                return;
            }

            if (igLastCandidate is null)
            {
#if DEBUG
                assert(_compiler is not null);
                if (_compiler.verbose)
                {
                    assert(ig is not null);
                    jitprintf($"emitSplit: can't split at IG{ig.GetDisplayId():D2}; we don't have a candidate to report\n");
                }
#endif
                return;
            }

            if (igLastCandidate == igLastReported)
            {
#if DEBUG
                assert(_compiler is not null);
                if (_compiler.verbose)
                {
                    jitprintf($"emitSplit: can't split at IG{igLastCandidate.GetDisplayId():D2}; we already reported it\n");
                }
#endif
                return;
            }

            // Tiny stress split sizes can encounter empty alignment groups. Every reported
            // fragment must still contain a nonzero amount of code.
            if (candidateSize == 0)
            {
#if DEBUG
                assert(_compiler is not null);
                if (_compiler.verbose)
                {
                    jitprintf($"emitSplit: can't split at IG{igLastCandidate.GetDisplayId():D2}; zero-sized candidate\n");
                }
#endif
                return;
            }

#if DEBUG
            assert(_compiler is not null);
            if (_compiler.verbose)
            {
                jitprintf($"emitSplit: split at IG{igLastCandidate.GetDisplayId():D2} is size {candidateSize:x}, "
                    + $"{(candidateSize >= maxSplitSize ? "larger" : "less")} than requested maximum size of {maxSplitSize:x}\n");
            }
#endif

            // The value handed to the callback remains valid independently of this invocation.
            var location = new emitLocation(igLastCandidate);
            callbackFunc(context, location);
            igLastReported = igLastCandidate;
            igLastCandidate = null;
            curSize = unchecked(curSize - candidateSize);
        }

        for (; (ig != igEnd) && (ig is not null); igPrev = ig, ig = ig.igNext)
        {
            SplitIfNecessary();

            // Adjacent groups with the same prolog/epilog flag might be part of the same
            // sequence. Without more information, retain the previous split candidate.
            if ((igPrev is not null)
                && ((((igPrev.igFlags & InsGroupFlags.Prolog) != 0)
                        && ((ig.igFlags & InsGroupFlags.Prolog) != 0))
                    || (((igPrev.igFlags & InsGroupFlags.Epilog) != 0)
                        && ((ig.igFlags & InsGroupFlags.Epilog) != 0))
                    || (((igPrev.igFlags & InsGroupFlags.FuncletProlog) != 0)
                        && ((ig.igFlags & InsGroupFlags.FuncletProlog) != 0))
                    || (((igPrev.igFlags & InsGroupFlags.FuncletEpilog) != 0)
                        && ((ig.igFlags & InsGroupFlags.FuncletEpilog) != 0))))
            {
                // We can't update the candidate.
            }
            else
            {
                igLastCandidate = ig;
                candidateSize = curSize;
            }

            curSize = unchecked(curSize + ig.igSize);
        }

        // A final empty alignment group must not become a zero-sized fragment. Empty groups
        // before a populated group are included in the populated group's fragment instead.
        if ((igLastCandidate is not null) && (curSize == candidateSize))
        {
            JITDUMP($"emitSplit: can't split at last candidate IG{igLastCandidate.GetDisplayId():D2} because it would create a zero-sized fragment\n");
        }
        else
        {
            SplitIfNecessary();
        }

        assert((curSize > 0) && (curSize < UW_MAX_FRAGMENT_SIZE_BYTES));
    }
}
#endif
