// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private const int SC_IG_BUFFER_NUM_SMALL_DESCS = 14;
    private const int SC_IG_BUFFER_NUM_LARGE_DESCS = 50;
#endif

    private void emitGenIG(insGroup ig)
    {
#if !TARGET_AMD64 || EMITTER_STATS
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction-group storage requires AMD64 without emitter allocation statistics.");
#else
        emitCurIG = ig;

#if EMIT_TRACK_STACK_DEPTH
        ig.igStkLvl = unchecked((uint)emitCurStackLvl);

        if (ig.igStkLvl != emitCurStackLvl)
        {
            IMPL_LIMITATION("Too many arguments pushed on stack");
        }
#endif

        if (emitNoGCIG)
        {
            ig.igFlags |= InsGroupFlags.NoGCInterrupt;
        }

        emitCurIGinsCnt = 0;
        emitCurIGsize = 0;
        assert(emitCurIGjmpList is null);
#if FEATURE_LOOP_ALIGN
        assert(emitCurIGAlignList is null);
#endif

        if (emitCurIGfreeBase is null)
        {
            emitIGbuffSize = (nuint)((SC_IG_BUFFER_NUM_SMALL_DESCS * (SMALL_IDSC_SIZE + _debugInfoSize))
                + (SC_IG_BUFFER_NUM_LARGE_DESCS * (INSTR_DESC_SIZE + _debugInfoSize)));
            emitCurIGfreeBase = [];
            emitCurIGfreeEndp = emitIGbuffSize;
        }

        emitCurIGfreeBase.Clear();
        emitCurIGfreeNext = 0;
        emitLastInsFullSize = 0;
#endif
    }

    private void emitNewIG()
    {
#if !TARGET_AMD64 || EMITTER_STATS
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction-group storage requires AMD64 without emitter allocation statistics.");
#else
        var ig = emitAllocAndLinkIG();
        emitGenIG(ig);

#if DEBUG
        assert(_compiler is not null);

        if (_compiler.verbose)
        {
            jitprintf("Created:\n      ");
            emitDispIG(ig, displayFunc: false, displayInstructions: false, displayLocation: false);
        }
#endif
#endif
    }

    private bool emitHasLastIns()
    {
        return emitLastIns is not null;
    }

    private insGroup emitSavIG(bool emitAdd)
    {
#if !TARGET_AMD64 || EMITTER_STATS
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction-group storage requires AMD64 without emitter allocation statistics.");
#else
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(emitCurIGfreeBase is not null);
        assert(emitCurIGfreeNext <= emitCurIGfreeEndp);

        var ig = emitCurIG;
        var sz = emitCurIGfreeNext;
        var alignmentMask = (nuint)(nint.Size - 1);
        var gs = (sz + alignmentMask) & ~alignmentMask;

        if ((ig.igFlags & InsGroupFlags.Extend) == 0)
        {
            if (emitForceStoreGCState || !VarSetOps.Equal(_compiler, emitPrevGCrefVars, emitInitGCrefVars))
            {
                ig.igFlags |= InsGroupFlags.GCVars;
                gs += (nuint)nint.Size;
            }

            // The pinned emitter always records byrefs, even when the previous mask matches.
            ig.igFlags |= InsGroupFlags.ByrefRegs;
            gs += sizeof(uint);
        }

        ig.igStorageSize = gs;
        ig.igDataOffset = 0;

        if ((ig.igFlags & InsGroupFlags.ByrefRegs) != 0)
        {
            ig.SavedByrefRegs = unchecked((uint)emitInitByrefRegs);
            ig.igDataOffset += sizeof(uint);
        }

        if ((ig.igFlags & InsGroupFlags.GCVars) != 0)
        {
            VarSetOps.AssignNoCopy(_compiler, ref ig.SavedGcVars, VarSetOps.MakeEmpty(_compiler));
            VarSetOps.Assign(_compiler, ref ig.SavedGcVars, emitInitGCrefVars);
            ig.igDataOffset += (nuint)nint.Size;
        }

        assert((ig.igFlags & InsGroupFlags.Placeholder) == 0);
        ig.igData = [.. emitCurIGfreeBase];
#if DEBUG
        ig.igDataSize = sz;
#endif

        noway_assert(unchecked((byte)emitCurIGinsCnt) == emitCurIGinsCnt);
        noway_assert(unchecked((ushort)emitCurIGsize) == emitCurIGsize);
        ig.igInsCnt = unchecked((byte)emitCurIGinsCnt);
        ig.igSize = unchecked((ushort)emitCurIGsize);

        if (ig.igSize != 0)
        {
            emitLastSavedIGWasNoGC = (ig.igFlags & InsGroupFlags.NoGCInterrupt) != 0;
        }

        emitCurCodeOffset = unchecked(emitCurCodeOffset + emitCurIGsize);
        assert((emitCurCodeOffset & (CODE_ALIGN - 1)) == 0);

        if ((ig.igFlags & InsGroupFlags.Extend) == 0)
        {
            ig.igGCregs = emitInitGCrefRegs;
        }

        if (!emitAdd)
        {
            VarSetOps.Assign(_compiler, ref emitPrevGCrefVars, emitThisGCrefVars);
            emitPrevGCrefRegs = emitThisGCrefRegs;
            emitPrevByrefRegs = emitThisByrefRegs;

            if (emitAddedLabel)
            {
                emitForceStoreGCState = false;
                emitAddedLabel = false;
            }
        }

#if DEBUG
        if (_compiler.opts.dspCode)
        {
            if (_compiler.verbose)
            {
                jitprintf("Saved:\n      ");
                emitDispIG(ig, displayFunc: false, displayInstructions: false, displayLocation: false);
            }
            else
            {
                jitprintf($"      {emitLabelString(ig)}:        ; funclet={ig.igFuncIdx:D2}\n");
            }
        }
#endif

#if FEATURE_LOOP_ALIGN
        if (emitCurIGAlignList is not null)
        {
            instrDescAlign? list = null;
            instrDescAlign? last = null;

            do
            {
                var oa = emitCurIGAlignList;
                emitCurIGAlignList = oa.idaNext;
                var na = emitSavedDescriptor(ig, oa, sz);

                assert(na.idaIG == ig);
                assert(na.idIns() == oa.idIns());
                assert(na.idaNext == oa.idaNext);
                assert(na.idIns() == INS_align);

                na.idaNext = list;
                list = na;
                last ??= na;
            }
            while (emitCurIGAlignList is not null);

            assert(last is not null);

            if (emitAlignList is null)
            {
                assert(emitAlignLast is null);
                last.idaNext = emitAlignList;
                emitAlignList = list;
            }
            else
            {
                assert(emitAlignLast is not null);
                last.idaNext = null;
                emitAlignLast.idaNext = list;
            }

            emitAlignLast = last;
            emitAlignLastGroup = list;
        }
#endif

        if (emitCurIGjmpList is not null)
        {
            instrDescJmp? list = null;
            instrDescJmp? last = null;

            do
            {
                var oj = emitCurIGjmpList;
                emitCurIGjmpList = oj.idjNext;
                var nj = emitSavedDescriptor(ig, oj, sz);

                assert(nj.idjIG == ig);
                assert(nj.idIns() == oj.idIns());
                assert(nj.idjNext == oj.idjNext);
                assert((last is null) || (last.idjOffs > nj.idjOffs));

                if ((ig.igFlags & IGF_PROPAGATE_MASK) != 0)
                {
                    nj.idjNext = emitFixedSizeJumpList;
                    emitFixedSizeJumpList = nj;
                    assert(nj.idjShort);
                    continue;
                }

                nj.idjNext = list;
                list = nj;
                last ??= nj;
            }
            while (emitCurIGjmpList is not null);

            if (last is not null)
            {
                if (emitJumpList is null)
                {
                    last.idjNext = emitJumpList;
                    emitJumpList = list;
                }
                else
                {
                    assert(emitJumpLast is not null);
                    last.idjNext = null;
                    emitJumpLast.idjNext = list;
                }

                emitJumpLast = last;
            }
        }

        assert(emitHasLastIns() == (emitLastInsIG is not null));

        if ((emitLastIns is not null) && (sz != 0))
        {
            assert(emitLastInsIG == emitCurIG);

            if (emitLastIns.idIns() == INS_jmp)
            {
                ig.igFlags |= InsGroupFlags.HasRemovableJump;
            }

            emitLastIns = emitSavedDescriptor(ig, emitLastIns, sz);
            emitLastInsIG = ig;
            ig.igLastIns = emitLastIns;
        }

        emitCurIGfreeBase.Clear();
        emitCurIGfreeNext = 0;

        return ig;
#endif
    }

    private static T emitSavedDescriptor<T>(insGroup ig, T descriptor, nuint size)
        where T : instrDesc
    {
        assert(descriptor.StorageGroup == ig);
        assert(descriptor.StorageOffset < size);
        assert(ig.igData is not null);
        T saved = (T)ig.igData[descriptor.StorageIndex];
        assert(saved == descriptor);
        assert(saved.StorageOffset == descriptor.StorageOffset);

        return saved;
    }
}
