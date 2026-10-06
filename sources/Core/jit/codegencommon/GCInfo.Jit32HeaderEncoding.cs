// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER
using System;
using System.Threading;

namespace RyuJitSharp;

public partial struct GCInfo
{
    private const uint HAS_UNTRACKED = uint.MaxValue;
    private const uint HAS_VARPTR = uint.MaxValue;
    private const uint HAS_NOGCREGIONS = uint.MaxValue;
    private const uint INVALID_REV_PINVOKE_OFFSET = uint.MaxValue;
    private const uint HAS_REV_PINVOKE_FRAME_OFFSET = uint.MaxValue - 1;
    private const uint INVALID_GS_COOKIE_OFFSET = 0;
    private const uint HAS_GS_COOKIE_OFFSET = uint.MaxValue;
    private const uint INVALID_SYNC_OFFSET = 0;
    private const uint HAS_SYNC_OFFSET = uint.MaxValue;

    private const int SET_FRAMESIZE_MAX = 7;
    private const int SET_ARGCOUNT_MAX = 8;
    private const int SET_PROLOGSIZE_MAX = 16;
    private const int SET_EPILOGSIZE_MAX = 10;
    private const int SET_EPILOGCNT_MAX = 4;
    private const int SET_UNTRACKED_MAX = 3;
    private const int SET_NOGCREGIONS_MAX = 4;
    private const byte MORE_BYTES_TO_FOLLOW = 0x80;
    private const byte SET_FRAMESIZE = 0;
    private const byte SET_ARGCOUNT = SET_FRAMESIZE + SET_FRAMESIZE_MAX + 1;
    private const byte SET_PROLOGSIZE = SET_ARGCOUNT + SET_ARGCOUNT_MAX + 1;
    private const byte SET_EPILOGSIZE = SET_PROLOGSIZE + SET_PROLOGSIZE_MAX + 1;
    private const byte SET_EPILOGCNT = SET_EPILOGSIZE + SET_EPILOGSIZE_MAX + 1;
    private const byte SET_UNTRACKED = SET_EPILOGCNT + (SET_EPILOGCNT_MAX + 1) * 2;
    private const byte FIRST_FLIP = SET_UNTRACKED + SET_UNTRACKED_MAX + 1;
    private const byte FLIP_EDI_SAVED = FIRST_FLIP;
    private const byte FLIP_ESI_SAVED = FIRST_FLIP + 1;
    private const byte FLIP_EBX_SAVED = FIRST_FLIP + 2;
    private const byte FLIP_EBP_SAVED = FIRST_FLIP + 3;
    private const byte FLIP_EBP_FRAME = FIRST_FLIP + 4;
    private const byte FLIP_INTERRUPTIBLE = FIRST_FLIP + 5;
    private const byte FLIP_DOUBLE_ALIGN = FIRST_FLIP + 6;
    private const byte FLIP_SECURITY = FIRST_FLIP + 7;
    private const byte FLIP_HANDLERS = FIRST_FLIP + 8;
    private const byte FLIP_LOCALLOC = FIRST_FLIP + 9;
    private const byte FLIP_EDITNCONTINUE = FIRST_FLIP + 10;
    private const byte FLIP_VAR_PTR_TABLE_SZ = FIRST_FLIP + 11;
    private const byte FFFF_UNTRACKED_CNT = FIRST_FLIP + 12;
    private const byte FLIP_VARARGS = FIRST_FLIP + 13;
    private const byte FLIP_PROF_CALLBACKS = FIRST_FLIP + 14;
    private const byte FLIP_HAS_GS_COOKIE = FIRST_FLIP + 15;
    private const byte FLIP_SYNC = FIRST_FLIP + 16;
    private const byte FLIP_HAS_GENERICS_CONTEXT = FIRST_FLIP + 17;
    private const byte FLIP_GENERICS_CONTEXT_IS_METHODDESC = FIRST_FLIP + 18;
    private const byte FLIP_REV_PINVOKE_FRAME = FIRST_FLIP + 19;
    private const byte NEXT_OPCODE = FIRST_FLIP + 20;
    private const byte NEXT_FOUR_START = 0x50;
    private const byte NEXT_FOUR_FRAMESIZE = NEXT_FOUR_START;
    private const byte NEXT_FOUR_ARGCOUNT = NEXT_FOUR_START + 0x10;
    private const byte NEXT_THREE_PROLOGSIZE = NEXT_FOUR_START + 0x20;
    private const byte NEXT_THREE_EPILOGSIZE = NEXT_FOUR_START + 0x28;
    private const byte SET_RETURNKIND = 0;
    private const int SET_RET_KIND_MAX_V5 = 7;
    private const byte SET_NOGCREGIONS_CNT_V5 = SET_RETURNKIND + 8;
    private const byte FFFF_NOGCREGION_CNT_V5 = SET_NOGCREGIONS_CNT_V5 + SET_NOGCREGIONS_MAX + 1;
    private const int IH_MAX_PROLOG_SIZE = 51;
    private const int NO_CACHED_HEADER = -1;

    private static int[]? s_infoHdrLookup;

    static GCInfo()
    {
        gcInitEncoderLookupTable();
    }

    internal struct InfoHdrSmall
    {
        internal byte prologSize;
        internal byte epilogSize;
        internal byte epilogCount;
        internal byte epilogAtEnd;
        internal byte ediSaved;
        internal byte esiSaved;
        internal byte ebxSaved;
        internal byte ebpSaved;
        internal byte ebpFrame;
        internal byte interruptible;
        internal byte doubleAlign;
        internal byte security;
        internal byte handlers;
        internal byte localloc;
        internal byte editNcontinue;
        internal byte varargs;
        internal byte profCallbacks;
        internal byte genericsContext;
        internal byte genericsContextIsMethodDesc;
        internal byte returnKind;
        internal byte isAsync;
        internal ushort argCount;
        internal uint frameSize;
        internal uint untrackedCnt;
        internal uint varPtrTableSize;

        internal InfoHdrSmall(byte prologSize, byte epilogSize, byte epilogCount, byte epilogAtEnd,
            byte ediSaved, byte esiSaved, byte ebxSaved, byte ebpSaved, byte ebpFrame, byte interruptible,
            byte doubleAlign, byte security, byte handlers, byte localloc, byte editNcontinue, byte varargs,
            byte profCallbacks, byte genericsContext, byte genericsContextIsMethodDesc, byte returnKind, byte isAsync,
            ushort argCount, uint frameSize, uint untrackedCnt, uint varPtrTableSize)
        {
            this.prologSize = prologSize;
            this.epilogSize = epilogSize;
            this.epilogCount = epilogCount;
            this.epilogAtEnd = epilogAtEnd;
            this.ediSaved = ediSaved;
            this.esiSaved = esiSaved;
            this.ebxSaved = ebxSaved;
            this.ebpSaved = ebpSaved;
            this.ebpFrame = ebpFrame;
            this.interruptible = interruptible;
            this.doubleAlign = doubleAlign;
            this.security = security;
            this.handlers = handlers;
            this.localloc = localloc;
            this.editNcontinue = editNcontinue;
            this.varargs = varargs;
            this.profCallbacks = profCallbacks;
            this.genericsContext = genericsContext;
            this.genericsContextIsMethodDesc = genericsContextIsMethodDesc;
            this.returnKind = returnKind;
            this.isAsync = isAsync;
            this.argCount = argCount;
            this.frameSize = frameSize;
            this.untrackedCnt = untrackedCnt;
            this.varPtrTableSize = varPtrTableSize;
        }

        internal readonly bool IsHeaderMatch(in InfoHdr target)
        {
            var header = ToInfoHdr();
            return header.IsHeaderMatch(in target);
        }

        internal readonly InfoHdr ToInfoHdr()
        {
            return new InfoHdr
            {
                prologSize = prologSize,
                epilogSize = epilogSize,
                epilogCount = epilogCount,
                epilogAtEnd = epilogAtEnd,
                ediSaved = ediSaved,
                esiSaved = esiSaved,
                ebxSaved = ebxSaved,
                ebpSaved = ebpSaved,
                ebpFrame = ebpFrame,
                interruptible = interruptible,
                doubleAlign = doubleAlign,
                security = security,
                handlers = handlers,
                localloc = localloc,
                editNcontinue = editNcontinue,
                varargs = varargs,
                profCallbacks = profCallbacks,
                genericsContext = genericsContext,
                genericsContextIsMethodDesc = genericsContextIsMethodDesc,
                returnKind = returnKind,
                isAsync = isAsync,
                argCount = argCount,
                frameSize = frameSize,
                untrackedCnt = untrackedCnt,
                varPtrTableSize = varPtrTableSize,
                gsCookieOffset = INVALID_GS_COOKIE_OFFSET,
                syncStartOffset = INVALID_SYNC_OFFSET,
                syncEndOffset = INVALID_SYNC_OFFSET,
                revPInvokeOffset = INVALID_REV_PINVOKE_OFFSET,
                noGCRegionCnt = 0,
            };
        }
    }

    internal struct InfoHdr
    {
        internal byte prologSize;
        internal byte epilogSize;
        internal byte epilogCount;
        internal byte epilogAtEnd;
        internal byte ediSaved;
        internal byte esiSaved;
        internal byte ebxSaved;
        internal byte ebpSaved;
        internal byte ebpFrame;
        internal byte interruptible;
        internal byte doubleAlign;
        internal byte security;
        internal byte handlers;
        internal byte localloc;
        internal byte editNcontinue;
        internal byte varargs;
        internal byte profCallbacks;
        internal byte genericsContext;
        internal byte genericsContextIsMethodDesc;
        internal byte returnKind;
        internal byte isAsync;
        internal ushort argCount;
        internal uint frameSize;
        internal uint untrackedCnt;
        internal uint varPtrTableSize;
        internal uint gsCookieOffset;
        internal uint syncStartOffset;
        internal uint syncEndOffset;
        internal uint revPInvokeOffset;
        internal uint noGCRegionCnt;

        internal readonly bool IsHeaderMatch(in InfoHdr target)
        {
            assert(target.untrackedCnt != HAS_UNTRACKED);
            assert(target.varPtrTableSize != HAS_VARPTR);
            assert(target.gsCookieOffset != HAS_GS_COOKIE_OFFSET);
            assert(target.syncStartOffset != HAS_SYNC_OFFSET);
            assert(target.revPInvokeOffset != HAS_REV_PINVOKE_FRAME_OFFSET);
            assert(target.noGCRegionCnt != HAS_NOGCREGIONS);

            if ((prologSize != target.prologSize) ||
                (epilogSize != target.epilogSize) ||
                (epilogCount != target.epilogCount) ||
                (epilogAtEnd != target.epilogAtEnd) ||
                (ediSaved != target.ediSaved) ||
                (esiSaved != target.esiSaved) ||
                (ebxSaved != target.ebxSaved) ||
                (ebpSaved != target.ebpSaved) ||
                (ebpFrame != target.ebpFrame) ||
                (interruptible != target.interruptible) ||
                (doubleAlign != target.doubleAlign) ||
                (security != target.security) ||
                (handlers != target.handlers) ||
                (localloc != target.localloc) ||
                (editNcontinue != target.editNcontinue) ||
                (varargs != target.varargs) ||
                (profCallbacks != target.profCallbacks) ||
                (genericsContext != target.genericsContext) ||
                (genericsContextIsMethodDesc != target.genericsContextIsMethodDesc) ||
                (returnKind != target.returnKind) ||
                (isAsync != target.isAsync) ||
                (argCount != target.argCount) ||
                (frameSize != target.frameSize))
            {
                return false;
            }

            if ((untrackedCnt != target.untrackedCnt) &&
                ((target.untrackedCnt <= SET_UNTRACKED_MAX) || (untrackedCnt != HAS_UNTRACKED)))
            {
                return false;
            }

            if ((varPtrTableSize != target.varPtrTableSize) &&
                ((varPtrTableSize == 0) != (target.varPtrTableSize == 0)))
            {
                return false;
            }

            if ((gsCookieOffset == INVALID_GS_COOKIE_OFFSET) !=
                (target.gsCookieOffset == INVALID_GS_COOKIE_OFFSET))
            {
                return false;
            }

            if ((syncStartOffset == INVALID_SYNC_OFFSET) !=
                (target.syncStartOffset == INVALID_SYNC_OFFSET))
            {
                return false;
            }

            if ((revPInvokeOffset == INVALID_REV_PINVOKE_OFFSET) !=
                (target.revPInvokeOffset == INVALID_REV_PINVOKE_OFFSET))
            {
                return false;
            }

            if ((noGCRegionCnt != target.noGCRegionCnt) &&
                ((target.noGCRegionCnt <= SET_NOGCREGIONS_MAX) || (noGCRegionCnt != HAS_NOGCREGIONS)))
            {
                return false;
            }

            return true;
        }
    }

    private static void GetInfoHdr(int index, out InfoHdr header)
    {
        header = s_infoHdrShortcut[index].ToInfoHdr();
    }

    private static bool InitNeeded3(uint current, uint target, uint max, out uint hint)
    {
        assert(current != target);

        var value = target;
        while (value > max)
        {
            var next = value & 0x07;
            value >>= 3;
            if (value == current)
            {
                hint = next;
                return false;
            }
        }

        hint = value;
        return true;
    }

    private static bool InitNeeded4(uint current, uint target, uint max, out uint hint)
    {
        assert(current != target);

        var value = target;
        while (value > max)
        {
            var next = value & 0x0F;
            value >>= 4;
            if (value == current)
            {
                hint = next;
                return false;
            }
        }

        hint = value;
        return true;
    }

    private static int BigEncoding3(uint current, uint target, uint max)
    {
        assert(current != target);

        var value = target;
        var count = 0;
        while (value > max)
        {
            value >>= 3;
            if (value == current)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int BigEncoding4(uint current, uint target, uint max)
    {
        assert(current != target);

        var value = target;
        var count = 0;
        while (value > max)
        {
            value >>= 4;
            if (value == current)
            {
                break;
            }

            count++;
        }

        return count;
    }

    internal static byte EncodeHeaderNext(in InfoHdr header, ref InfoHdr state, out byte codeSet)
    {
        var encoding = byte.MaxValue;
        codeSet = 1;

        if (state.argCount != header.argCount)
        {
            if (header.argCount <= SET_ARGCOUNT_MAX)
            {
                state.argCount = header.argCount;
                encoding = unchecked((byte)(SET_ARGCOUNT + header.argCount));
                goto Done;
            }

            if (InitNeeded4(state.argCount, header.argCount, SET_ARGCOUNT_MAX, out var hint))
            {
                assert(hint <= SET_ARGCOUNT_MAX);
                state.argCount = unchecked((ushort)hint);
                encoding = unchecked((byte)(SET_ARGCOUNT + hint));
                goto Done;
            }

            assert(hint <= 0x0F);
            state.argCount = unchecked((ushort)((state.argCount << 4) + hint));
            encoding = unchecked((byte)(NEXT_FOUR_ARGCOUNT + hint));
            goto Done;
        }

        if (state.frameSize != header.frameSize)
        {
            if (header.frameSize <= SET_FRAMESIZE_MAX)
            {
                state.frameSize = header.frameSize;
                encoding = unchecked((byte)(SET_FRAMESIZE + header.frameSize));
                goto Done;
            }

            if (InitNeeded4(state.frameSize, header.frameSize, SET_FRAMESIZE_MAX, out var hint))
            {
                assert(hint <= SET_FRAMESIZE_MAX);
                state.frameSize = hint;
                encoding = unchecked((byte)(SET_FRAMESIZE + hint));
                goto Done;
            }

            assert(hint <= 0x0F);
            state.frameSize = unchecked((state.frameSize << 4) + hint);
            encoding = unchecked((byte)(NEXT_FOUR_FRAMESIZE + hint));
            goto Done;
        }

        if ((state.epilogCount != header.epilogCount) || (state.epilogAtEnd != header.epilogAtEnd))
        {
            if (header.epilogCount > SET_EPILOGCNT_MAX)
            {
                IMPL_LIMITATION("More than SET_EPILOGCNT_MAX epilogs");
            }

            state.epilogCount = header.epilogCount;
            state.epilogAtEnd = header.epilogAtEnd;
            encoding = unchecked((byte)(SET_EPILOGCNT + header.epilogCount * 2));
            if (header.epilogAtEnd != 0)
            {
                encoding++;
            }

            goto Done;
        }

        if (state.varPtrTableSize != header.varPtrTableSize)
        {
            assert((state.varPtrTableSize == 0) || (state.varPtrTableSize == HAS_VARPTR));

            if (state.varPtrTableSize == 0)
            {
                state.varPtrTableSize = HAS_VARPTR;
                encoding = FLIP_VAR_PTR_TABLE_SZ;
                goto Done;
            }
            else if (header.varPtrTableSize == 0)
            {
                state.varPtrTableSize = 0;
                encoding = FLIP_VAR_PTR_TABLE_SZ;
                goto Done;
            }
        }

        if (state.untrackedCnt != header.untrackedCnt)
        {
            assert((state.untrackedCnt <= SET_UNTRACKED_MAX) || (state.untrackedCnt == HAS_UNTRACKED));

            if (header.untrackedCnt <= SET_UNTRACKED_MAX)
            {
                state.untrackedCnt = header.untrackedCnt;
                encoding = unchecked((byte)(SET_UNTRACKED + header.untrackedCnt));
                goto Done;
            }
            else if (state.untrackedCnt != HAS_UNTRACKED)
            {
                state.untrackedCnt = HAS_UNTRACKED;
                encoding = FFFF_UNTRACKED_CNT;
                goto Done;
            }
        }

        if (state.epilogSize != header.epilogSize)
        {
            if (header.epilogSize <= SET_EPILOGSIZE_MAX)
            {
                state.epilogSize = header.epilogSize;
                encoding = unchecked((byte)(SET_EPILOGSIZE + header.epilogSize));
                goto Done;
            }

            if (InitNeeded3(state.epilogSize, header.epilogSize, SET_EPILOGSIZE_MAX, out var hint))
            {
                assert(hint <= SET_EPILOGSIZE_MAX);
                state.epilogSize = unchecked((byte)hint);
                encoding = unchecked((byte)(SET_EPILOGSIZE + hint));
                goto Done;
            }

            assert(hint <= 0x07);
            state.epilogSize = unchecked((byte)((state.epilogSize << 3) + hint));
            encoding = unchecked((byte)(NEXT_THREE_EPILOGSIZE + hint));
            goto Done;
        }

        if (state.prologSize != header.prologSize)
        {
            if (header.prologSize <= SET_PROLOGSIZE_MAX)
            {
                state.prologSize = header.prologSize;
                encoding = unchecked((byte)(SET_PROLOGSIZE + header.prologSize));
                goto Done;
            }

            assert(SET_PROLOGSIZE_MAX > 15);
            if (InitNeeded3(state.prologSize, header.prologSize, 15, out var hint))
            {
                assert(hint <= 15);
                state.prologSize = unchecked((byte)hint);
                encoding = unchecked((byte)(SET_PROLOGSIZE + hint));
                goto Done;
            }

            assert(hint <= 0x07);
            state.prologSize = unchecked((byte)((state.prologSize << 3) + hint));
            encoding = unchecked((byte)(NEXT_THREE_PROLOGSIZE + hint));
            goto Done;
        }

        if (state.ediSaved != header.ediSaved)
        {
            state.ediSaved = header.ediSaved;
            encoding = FLIP_EDI_SAVED;
            goto Done;
        }

        if (state.esiSaved != header.esiSaved)
        {
            state.esiSaved = header.esiSaved;
            encoding = FLIP_ESI_SAVED;
            goto Done;
        }

        if (state.ebxSaved != header.ebxSaved)
        {
            state.ebxSaved = header.ebxSaved;
            encoding = FLIP_EBX_SAVED;
            goto Done;
        }

        if (state.ebpSaved != header.ebpSaved)
        {
            state.ebpSaved = header.ebpSaved;
            encoding = FLIP_EBP_SAVED;
            goto Done;
        }

        if (state.ebpFrame != header.ebpFrame)
        {
            state.ebpFrame = header.ebpFrame;
            encoding = FLIP_EBP_FRAME;
            goto Done;
        }

        if (state.interruptible != header.interruptible)
        {
            state.interruptible = header.interruptible;
            encoding = FLIP_INTERRUPTIBLE;
            goto Done;
        }

#if DOUBLE_ALIGN
        if (state.doubleAlign != header.doubleAlign)
        {
            state.doubleAlign = header.doubleAlign;
            encoding = FLIP_DOUBLE_ALIGN;
            goto Done;
        }
#endif

        if (state.security != header.security)
        {
            state.security = header.security;
            encoding = FLIP_SECURITY;
            goto Done;
        }

        if (state.handlers != header.handlers)
        {
            state.handlers = header.handlers;
            encoding = FLIP_HANDLERS;
            goto Done;
        }

        if (state.localloc != header.localloc)
        {
            state.localloc = header.localloc;
            encoding = FLIP_LOCALLOC;
            goto Done;
        }

        if (state.editNcontinue != header.editNcontinue)
        {
            state.editNcontinue = header.editNcontinue;
            encoding = FLIP_EDITNCONTINUE;
            goto Done;
        }

        if (state.varargs != header.varargs)
        {
            state.varargs = header.varargs;
            encoding = FLIP_VARARGS;
            goto Done;
        }

        if (state.profCallbacks != header.profCallbacks)
        {
            state.profCallbacks = header.profCallbacks;
            encoding = FLIP_PROF_CALLBACKS;
            goto Done;
        }

        if (state.genericsContext != header.genericsContext)
        {
            state.genericsContext = header.genericsContext;
            encoding = FLIP_HAS_GENERICS_CONTEXT;
            goto Done;
        }

        if (state.genericsContextIsMethodDesc != header.genericsContextIsMethodDesc)
        {
            state.genericsContextIsMethodDesc = header.genericsContextIsMethodDesc;
            encoding = FLIP_GENERICS_CONTEXT_IS_METHODDESC;
            goto Done;
        }

        if ((state.returnKind != header.returnKind) || (state.isAsync != header.isAsync))
        {
            state.returnKind = header.returnKind;
            state.isAsync = header.isAsync;
            codeSet = 2;
            encoding = unchecked((byte)(header.returnKind | (header.isAsync != 0 ? 4 : 0)));
            assert(encoding <= SET_RET_KIND_MAX_V5);
            goto Done;
        }

        if (state.gsCookieOffset != header.gsCookieOffset)
        {
            assert((state.gsCookieOffset == INVALID_GS_COOKIE_OFFSET) ||
                (state.gsCookieOffset == HAS_GS_COOKIE_OFFSET));

            if (state.gsCookieOffset == INVALID_GS_COOKIE_OFFSET)
            {
                state.gsCookieOffset = HAS_GS_COOKIE_OFFSET;
                encoding = FLIP_HAS_GS_COOKIE;
                goto Done;
            }
            else if (header.gsCookieOffset == INVALID_GS_COOKIE_OFFSET)
            {
                state.gsCookieOffset = INVALID_GS_COOKIE_OFFSET;
                encoding = FLIP_HAS_GS_COOKIE;
                goto Done;
            }
        }

        if (state.syncStartOffset != header.syncStartOffset)
        {
            assert((state.syncStartOffset == INVALID_SYNC_OFFSET) || (state.syncStartOffset == HAS_SYNC_OFFSET));

            if (state.syncStartOffset == INVALID_SYNC_OFFSET)
            {
                state.syncStartOffset = HAS_SYNC_OFFSET;
                encoding = FLIP_SYNC;
                goto Done;
            }
            else if (header.syncStartOffset == INVALID_SYNC_OFFSET)
            {
                state.syncStartOffset = INVALID_SYNC_OFFSET;
                encoding = FLIP_SYNC;
                goto Done;
            }
        }

        if (state.revPInvokeOffset != header.revPInvokeOffset)
        {
            assert((state.revPInvokeOffset == INVALID_REV_PINVOKE_OFFSET) ||
                (state.revPInvokeOffset == HAS_REV_PINVOKE_FRAME_OFFSET));

            if (state.revPInvokeOffset == INVALID_REV_PINVOKE_OFFSET)
            {
                state.revPInvokeOffset = HAS_REV_PINVOKE_FRAME_OFFSET;
                encoding = FLIP_REV_PINVOKE_FRAME;
                goto Done;
            }
            else if (header.revPInvokeOffset == INVALID_REV_PINVOKE_OFFSET)
            {
                state.revPInvokeOffset = INVALID_REV_PINVOKE_OFFSET;
                encoding = FLIP_REV_PINVOKE_FRAME;
                goto Done;
            }
        }

        if (state.noGCRegionCnt != header.noGCRegionCnt)
        {
            assert((state.noGCRegionCnt <= SET_NOGCREGIONS_MAX) || (state.noGCRegionCnt == HAS_NOGCREGIONS));

            if (header.noGCRegionCnt <= SET_NOGCREGIONS_MAX)
            {
                state.noGCRegionCnt = header.noGCRegionCnt;
                codeSet = 2;
                encoding = unchecked((byte)(SET_NOGCREGIONS_CNT_V5 + header.noGCRegionCnt));
                goto Done;
            }
            else if (state.noGCRegionCnt != HAS_NOGCREGIONS)
            {
                state.noGCRegionCnt = HAS_NOGCREGIONS;
                codeSet = 2;
                encoding = FFFF_NOGCREGION_CNT_V5;
                goto Done;
            }
        }

    Done:
        assert(encoding < MORE_BYTES_TO_FOLLOW);
        if (!state.IsHeaderMatch(in header))
        {
            encoding |= MORE_BYTES_TO_FOLLOW;
        }

        return encoding;
    }

    private static int MeasureDistance(in InfoHdr header, in InfoHdrSmall candidate, int closeness)
    {
        var distance = 0;

        if (candidate.untrackedCnt != header.untrackedCnt)
        {
            if (header.untrackedCnt > SET_UNTRACKED_MAX)
            {
                if (candidate.untrackedCnt != HAS_UNTRACKED)
                {
                    distance++;
                }
            }
            else
            {
                distance++;
            }

            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.varPtrTableSize != header.varPtrTableSize)
        {
            if (header.varPtrTableSize != 0)
            {
                if (candidate.varPtrTableSize != HAS_VARPTR)
                {
                    distance++;
                }
            }
            else
            {
                assert(candidate.varPtrTableSize == HAS_VARPTR);
                distance++;
            }

            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.frameSize != header.frameSize)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }

            if (header.frameSize > SET_FRAMESIZE_MAX)
            {
                distance += BigEncoding4(candidate.frameSize, header.frameSize, SET_FRAMESIZE_MAX);
                if (distance >= closeness)
                {
                    return distance;
                }
            }
        }

        if (candidate.argCount != header.argCount)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }

            if (header.argCount > SET_ARGCOUNT_MAX)
            {
                distance += BigEncoding4(candidate.argCount, header.argCount, SET_ARGCOUNT_MAX);
                if (distance >= closeness)
                {
                    return distance;
                }
            }
        }

        if (candidate.prologSize != header.prologSize)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }

            if (header.prologSize > SET_PROLOGSIZE_MAX)
            {
                assert(SET_PROLOGSIZE_MAX > 15);
                distance += BigEncoding3(candidate.prologSize, header.prologSize, 15);
                if (distance >= closeness)
                {
                    return distance;
                }
            }
        }

        if (candidate.epilogSize != header.epilogSize)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }

            if (header.epilogSize > SET_EPILOGSIZE_MAX)
            {
                distance += BigEncoding3(candidate.epilogSize, header.epilogSize, SET_EPILOGSIZE_MAX);
                if (distance >= closeness)
                {
                    return distance;
                }
            }
        }

        if ((candidate.epilogCount != header.epilogCount) || (candidate.epilogAtEnd != header.epilogAtEnd))
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }

            if (header.epilogCount > SET_EPILOGCNT_MAX)
            {
                IMPL_LIMITATION("More than SET_EPILOGCNT_MAX epilogs");
            }
        }

        if (candidate.ediSaved != header.ediSaved)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.esiSaved != header.esiSaved)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.ebxSaved != header.ebxSaved)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.ebpSaved != header.ebpSaved)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.ebpFrame != header.ebpFrame)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.interruptible != header.interruptible)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

#if DOUBLE_ALIGN
        if (candidate.doubleAlign != header.doubleAlign)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }
#endif

        if (candidate.security != header.security)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.handlers != header.handlers)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.localloc != header.localloc)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.editNcontinue != header.editNcontinue)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.varargs != header.varargs)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.profCallbacks != header.profCallbacks)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.genericsContext != header.genericsContext)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (candidate.genericsContextIsMethodDesc != header.genericsContextIsMethodDesc)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if ((candidate.returnKind != header.returnKind) || (candidate.isAsync != header.isAsync))
        {
            distance += 2;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (header.gsCookieOffset != INVALID_GS_COOKIE_OFFSET)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (header.syncStartOffset != INVALID_SYNC_OFFSET)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (header.revPInvokeOffset != INVALID_REV_PINVOKE_OFFSET)
        {
            distance++;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        if (header.noGCRegionCnt > 0)
        {
            distance += 2;
            if (distance >= closeness)
            {
                return distance;
            }
        }

        return distance;
    }

    internal static byte EncodeHeaderFirst(in InfoHdr header, out InfoHdr state, ref int more, ref int cached)
    {
        var lookup = Volatile.Read(ref s_infoHdrLookup)
            ?? throw new InvalidOperationException("JIT32 GC encoder lookup table has not been initialized.");
        var index = cached;

        if (index != NO_CACHED_HEADER && s_infoHdrShortcut[index].IsHeaderMatch(in header))
        {
            GetInfoHdr(index, out state);
            more = 0;
            return unchecked((byte)index);
        }

        var prologSize = header.prologSize;
        var lo = 0;
        var hi = 0;
        if (prologSize <= IH_MAX_PROLOG_SIZE)
        {
            lo = lookup[prologSize];
            hi = lookup[prologSize + 1];
            for (index = lo; index < hi; index++)
            {
                var candidate = s_infoHdrShortcut[index];
                assert(prologSize == candidate.prologSize);
                if (candidate.IsHeaderMatch(in header))
                {
                    GetInfoHdr(index, out state);
                    cached = index;
                    more = 0;
                    return unchecked((byte)index);
                }
            }
        }

        var nearest = -1;
        var closeness = 255;
        var minimumAcceptableDistance = 1;
        if (header.frameSize > SET_FRAMESIZE_MAX)
        {
            minimumAcceptableDistance++;
            if (header.frameSize > 32)
            {
                minimumAcceptableDistance++;
            }
        }

        if (header.argCount > SET_ARGCOUNT_MAX)
        {
            minimumAcceptableDistance++;
            if (header.argCount > 32)
            {
                minimumAcceptableDistance++;
            }
        }

        if (cached != NO_CACHED_HEADER)
        {
            var distance = MeasureDistance(in header, in s_infoHdrShortcut[cached], closeness);
            assert(distance > 0);
            if (distance <= minimumAcceptableDistance)
            {
                GetInfoHdr(cached, out state);
                more = distance;
                return unchecked((byte)(0x80 | cached));
            }

            closeness = distance;
            nearest = cached;
        }

        for (index = lo; index < hi; index++)
        {
            if (index == cached)
            {
                continue;
            }

            var distance = MeasureDistance(in header, in s_infoHdrShortcut[index], closeness);
            assert(distance > 0);
            if (distance <= minimumAcceptableDistance)
            {
                GetInfoHdr(index, out state);
                cached = index;
                more = distance;
                return unchecked((byte)(0x80 | index));
            }

            if (distance < closeness)
            {
                closeness = distance;
                nearest = index;
            }
        }

        var last = lookup[IH_MAX_PROLOG_SIZE + 1];
        assert(last <= s_infoHdrShortcut.Length);
        for (index = 0; index < last; index++)
        {
            if ((index == cached) || ((index >= lo) && (index < hi)))
            {
                continue;
            }

            var distance = MeasureDistance(in header, in s_infoHdrShortcut[index], closeness);
            assert(distance > 0);
            if (distance <= minimumAcceptableDistance)
            {
                GetInfoHdr(index, out state);
                cached = index;
                more = distance;
                return unchecked((byte)(0x80 | index));
            }

            if (distance < closeness)
            {
                closeness = distance;
                nearest = index;
            }
        }

        assert((nearest >= 0) && (nearest <= 127));
        GetInfoHdr(nearest, out state);
        cached = nearest;
        more = closeness;
        return unchecked((byte)(0x80 | nearest));
    }

    internal static void gcInitEncoderLookupTable()
    {
        var lookup = new int[IH_MAX_PROLOG_SIZE + 2];
        var lo = -1;
        int hi;
        var n = 0;

        for (; n < s_infoHdrShortcut.Length; n++)
        {
            var prologSize = s_infoHdrShortcut[n].prologSize;
            if (prologSize == lo)
            {
                continue;
            }

            if (prologSize < lo)
            {
                assert(prologSize == 0);
                hi = IH_MAX_PROLOG_SIZE;
            }
            else
            {
                hi = prologSize;
            }

            assert(hi <= IH_MAX_PROLOG_SIZE);
            while (lo < hi)
            {
                lookup[++lo] = n;
            }

            if (lo == IH_MAX_PROLOG_SIZE)
            {
                break;
            }
        }

        assert(lo == IH_MAX_PROLOG_SIZE);
        assert(lookup[IH_MAX_PROLOG_SIZE] < s_infoHdrShortcut.Length);

        while ((n < s_infoHdrShortcut.Length) &&
            (s_infoHdrShortcut[n].prologSize == lo))
        {
            n++;
        }

        lookup[++lo] = n;
        Volatile.Write(ref s_infoHdrLookup, lookup);
    }
}
#endif
