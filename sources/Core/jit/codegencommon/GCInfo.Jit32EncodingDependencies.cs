// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER
using System;

namespace RyuJitSharp;

public partial struct GCInfo
{
    private unsafe struct EpilogTableWriter
    {
        internal byte* Destination;
        internal uint PreviousOffset;
    }

    private static unsafe nuint gcRecordEpilog(void* context, uint offset)
    {
        var writer = (EpilogTableWriter*)context;
        var size = encodeUDelta(writer->Destination, offset, writer->PreviousOffset);
        if (writer->Destination != null)
        {
            writer->Destination += size;
        }

        writer->PreviousOffset = offset;
        return size;
    }

    private static byte VarTypeToReturnKind(var_types type)
    {
        return type switch
        {
            TYP_REF => 1,
            TYP_BYREF => 2,
            TYP_FLOAT or TYP_DOUBLE => 3,
            _ => 0,
        };
    }

    private readonly byte getReturnKind()
    {
        ref readonly var returnType = ref Compiler.compRetTypeDesc;
        var returnRegCount = returnType.ReturnRegCount;
        if (returnRegCount == 1)
        {
            return VarTypeToReturnKind(returnType.GetReturnRegType(0));
        }

#if DEBUG
        for (byte index = 0; index < returnRegCount; index++)
        {
            assert(!varTypeIsGC(returnType.GetReturnRegType(index)));
        }
#endif

        return 0;
    }

    internal readonly bool gcIsUntrackedLocalOrNonEnregisteredArg(int varNum)
    {
        ref var variable = ref Compiler.lvaGetDesc(varNum);

        assert(!Compiler.lvaIsFieldOfDependentlyPromotedStruct(in variable));
        assert(varTypeIsGC(variable.Type));

        if (!variable.lvIsParam)
        {
            assert(!variable.lvPinned || !variable.lvTracked);
            if (variable.lvTracked || !variable.lvOnFrame)
            {
                return false;
            }
        }
        else if (!variable.lvOnFrame)
        {
            if (!Compiler.compJmpOpUsed)
            {
                return false;
            }
        }
        else if (variable.lvIsRegArg && variable.lvTracked)
        {
            return false;
        }

        return true;
    }

    internal readonly void gcCountForHeader(out uint untrackedCount, out uint varPtrTableSize,
        out uint noGCRegionCount)
    {
        var compiler = Compiler;
        untrackedCount = 0;

        for (var varNum = 0; varNum < compiler.lvaCount; varNum++)
        {
            ref var variable = ref compiler.lvaTable[varNum];
            if (compiler.lvaIsFieldOfDependentlyPromotedStruct(in variable))
            {
                continue;
            }

            if (varTypeIsGC(variable.Type))
            {
                if (!gcIsUntrackedLocalOrNonEnregisteredArg(varNum))
                {
                    continue;
                }

#if DEBUG
                if (compiler.verbose)
                {
                    var offset = variable.StackOffset;
                    jitprintf($"GCINFO: untrckd {Globals.varTypeGCstring(variable.Type)} lcl at " +
                        $"[{(_codeGen.IsFramePointerUsed ? STR_FPBASE : STR_SPBASE)}");
                    if (offset < 0)
                    {
                        jitprintf($"-0x{unchecked(-offset):X2}");
                    }
                    else if (offset > 0)
                    {
                        jitprintf($"+0x{offset:X2}");
                    }
                    jitprintf("]\n");
                }
#endif

                untrackedCount = unchecked(untrackedCount + 1);
            }
            else if (variable.Type is TYP_STRUCT && variable.lvOnFrame)
            {
                var layout = variable.Layout
                    ?? throw new InvalidOperationException("A struct local on the frame must have a layout.");
                untrackedCount = unchecked(untrackedCount + (uint)layout.GCPtrCount);
            }
        }

#if DEBUG
        assert(RegSet.tmpGetAllFree());
#endif
        for (var temp = RegSet.tmpListBeg(); temp is not null; temp = RegSet.tmpListNxt(temp))
        {
            if (!varTypeIsGC(temp.tdTempType))
            {
                continue;
            }

#if DEBUG
            if (compiler.verbose)
            {
                var offset = temp.tdTempOffs;
                jitprintf($"GCINFO: untrck {Globals.varTypeGCstring(temp.tdTempType)} Temp at " +
                    $"[{(_codeGen.IsFramePointerUsed ? STR_FPBASE : STR_SPBASE)}");
                if (offset < 0)
                {
                    jitprintf($"-0x{unchecked(-offset):X2}");
                }
                else if (offset > 0)
                {
                    jitprintf($"+0x{offset:X2}");
                }
                jitprintf("]\n");
            }
#endif

            untrackedCount = unchecked(untrackedCount + 1);
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"GCINFO: untrckVars = {untrackedCount}\n");
        }
#endif

        varPtrTableSize = 0;
        for (var variable = gcVarPtrList; variable is not null; variable = variable.vpdNext)
        {
            if (variable.vpdBegOfs == variable.vpdEndOfs)
            {
                continue;
            }

            varPtrTableSize = unchecked(varPtrTableSize + 1);
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"GCINFO: trackdLcls = {varPtrTableSize}\n");
        }
#endif

        var noGCRegions = 0u;
        if (_codeGen.Interruptible)
        {
            var lastEndOffset = uint.MaxValue;
            _ = _codeGen.Emitter.emitGenNoGCLst((_, offset, size, _, _) =>
            {
                if (lastEndOffset != offset)
                {
                    noGCRegions = unchecked(noGCRegions + 1);
                }

                lastEndOffset = unchecked(offset + size);
                return true;
            }, skipMainPrologsAndEpilogs: true);
        }

        noGCRegionCount = noGCRegions;
    }

    internal unsafe nuint gcInfoBlockHdrSave(byte* destination, int write,
        uint codeSize, uint prologSize, uint epilogSize, ref InfoHdr header, ref int cached)
    {
#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf("*************** In gcInfoBlockHdrSave()\n");
        }
#endif
        nuint size = 0;

#if VERIFY_GC_TABLES
        *(ushort*)destination = 0xFEEF;
        size += sizeof(ushort);
        destination += sizeof(ushort);
#endif

#if DEBUG
        if (Compiler.verbose && (write != 0))
        {
            jitprintf($"GCINFO: methodSize = {codeSize:X4}\n");
            jitprintf($"GCINFO: prologSize = {prologSize:X4}\n");
            jitprintf($"GCINFO: epilogSize = {epilogSize:X4}\n");
        }
#endif

        var methodSize = encodeUnsigned(destination, codeSize);
        size += methodSize;
        if (write != 0)
        {
            destination += methodSize;
        }

        var compiler = Compiler;
        var emitter = _codeGen.Emitter;
        if (write == 0)
        {
            header = default;
            cached = NO_CACHED_HEADER;
        }

        assert(prologSize <= byte.MaxValue);
        header.prologSize = unchecked((byte)prologSize);
        assert(epilogSize <= byte.MaxValue);
        header.epilogSize = unchecked((byte)epilogSize);

        var epilogCount = emitter.emitGetEpilogCnt();
        header.epilogCount = unchecked((byte)(epilogCount & 0x07));
        if (header.epilogCount != epilogCount)
        {
            IMPL_LIMITATION("emitGetEpilogCnt() does not fit in InfoHdr::epilogCount");
        }

        header.epilogAtEnd = emitter.emitHasEpilogEnd() ? (byte)1 : (byte)0;

        if (RegSet.rsRegsModified(RBM_EDI))
        {
            header.ediSaved = 1;
        }
        if (RegSet.rsRegsModified(RBM_ESI))
        {
            header.esiSaved = 1;
        }
        if (RegSet.rsRegsModified(RBM_EBX))
        {
            header.ebxSaved = 1;
        }

        header.interruptible = _codeGen.Interruptible ? (byte)1 : (byte)0;
        if (!_codeGen.IsFramePointerUsed)
        {
#if DOUBLE_ALIGN
            if (compiler.genDoubleAlign)
            {
                header.ebpSaved = 1;
                assert(!RegSet.rsRegsModified(RBM_EBP));
            }
#endif
            if (RegSet.rsRegsModified(RBM_EBP))
            {
                header.ebpSaved = 1;
            }
        }
        else
        {
            header.ebpSaved = 1;
            header.ebpFrame = 1;
        }

#if DOUBLE_ALIGN
        header.doubleAlign = compiler.genDoubleAlign ? (byte)1 : (byte)0;
#endif

        header.security = 0;
        header.handlers = compiler.compHndBBtabCount != 0 ? (byte)1 : (byte)0;
        header.localloc = compiler.compLocallocUsed ? (byte)1 : (byte)0;
        header.varargs = compiler.info.compIsVarArgs ? (byte)1 : (byte)0;
        header.profCallbacks = compiler.info.compProfilerCallback ? (byte)1 : (byte)0;
        header.editNcontinue = compiler.opts.compDbgEnC ? (byte)1 : (byte)0;
        header.genericsContext = compiler.lvaReportParamTypeArg() ? (byte)1 : (byte)0;
        header.genericsContextIsMethodDesc =
            (header.genericsContext != 0) &&
            ((compiler.info.compMethodInfo->options & CORINFO_GENERICS_CTXT_FROM_METHODDESC) != 0)
                ? (byte)1
                : (byte)0;

        header.returnKind = getReturnKind();
        header.isAsync = compiler.compIsAsync ? (byte)1 : (byte)0;
        assert(header.returnKind <= SET_RET_KIND_MAX_V5);

        header.gsCookieOffset = INVALID_GS_COOKIE_OFFSET;
        if (compiler.NeedsGSSecurityCookie)
        {
            assert(compiler.lvaGSSecurityCookie != BAD_VAR_NUM);
            var stackOffset = compiler.lvaGetDesc(compiler.lvaGSSecurityCookie).StackOffset;
            header.gsCookieOffset = _codeGen.IsFramePointerUsed
                ? unchecked((uint)-stackOffset)
                : unchecked((uint)stackOffset);
            assert(header.gsCookieOffset != INVALID_GS_COOKIE_OFFSET);
        }

        header.syncStartOffset = INVALID_SYNC_OFFSET;
        header.syncEndOffset = INVALID_SYNC_OFFSET;
        if ((compiler.info.compFlags & CORINFO_FLG_SYNCH) != 0)
        {
            header.syncStartOffset = 1;
            header.syncEndOffset = 1;
        }

        header.revPInvokeOffset = INVALID_REV_PINVOKE_OFFSET;
        if (compiler.opts.IsReversePInvoke)
        {
            assert(compiler.lvaReversePInvokeFrameVar != BAD_VAR_NUM);
            var stackOffset = compiler.lvaGetDesc(compiler.lvaReversePInvokeFrameVar).StackOffset;
            header.revPInvokeOffset = _codeGen.IsFramePointerUsed
                ? unchecked((uint)-stackOffset)
                : unchecked((uint)stackOffset);
            assert(header.revPInvokeOffset != INVALID_REV_PINVOKE_OFFSET);
        }

        var argCount = compiler.lvaParameterStackSize / REGSIZE_BYTES;
        assert(argCount <= ushort.MaxValue);
        header.argCount = unchecked((ushort)argCount);

        header.frameSize = unchecked((uint)(compiler.compLclFrameSize / sizeof(int)));

        if (write == 0)
        {
            gcCountForHeader(out header.untrackedCnt, out header.varPtrTableSize, out header.noGCRegionCnt);
        }

        var more = 0;
        var headerEncoding = EncodeHeaderFirst(in header, out var state, ref more, ref cached);
        size++;
        if (write != 0)
        {
            *destination++ = headerEncoding;
            var encoding = headerEncoding;
            byte codeSet = 1;
            while ((encoding & MORE_BYTES_TO_FOLLOW) != 0)
            {
                encoding = EncodeHeaderNext(in header, ref state, out codeSet);
                assert((codeSet is 1 or 2));
                if (codeSet == 2)
                {
                    *destination++ = NEXT_OPCODE | MORE_BYTES_TO_FOLLOW;
                    size++;
                }

                *destination++ = encoding;
                size++;
            }
        }
        else
        {
            size += unchecked((nuint)more);
        }

        if (header.untrackedCnt > SET_UNTRACKED_MAX)
        {
            var encodedSize = encodeUnsigned(write != 0 ? destination : null, header.untrackedCnt);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }
        }

        if (header.varPtrTableSize != 0)
        {
            var encodedSize = encodeUnsigned(write != 0 ? destination : null, header.varPtrTableSize);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }
        }

        if (header.gsCookieOffset != INVALID_GS_COOKIE_OFFSET)
        {
            assert((write == 0) || (state.gsCookieOffset == HAS_GS_COOKIE_OFFSET));
            var encodedSize = encodeUnsigned(write != 0 ? destination : null, header.gsCookieOffset);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }
        }

        if (header.syncStartOffset != INVALID_SYNC_OFFSET)
        {
            assert((write == 0) || (state.syncStartOffset == HAS_SYNC_OFFSET));
            var encodedSize = encodeUnsigned(write != 0 ? destination : null, header.syncStartOffset);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }

            encodedSize = encodeUnsigned(write != 0 ? destination : null, header.syncEndOffset);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }
        }

        if (header.revPInvokeOffset != INVALID_REV_PINVOKE_OFFSET)
        {
            assert((write == 0) || (state.revPInvokeOffset == HAS_REV_PINVOKE_FRAME_OFFSET));
            var encodedSize = encodeUnsigned(write != 0 ? destination : null, header.revPInvokeOffset);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }
        }

        if (header.noGCRegionCnt > SET_NOGCREGIONS_MAX)
        {
            var encodedSize = encodeUnsigned(write != 0 ? destination : null, header.noGCRegionCnt);
            size += encodedSize;
            if (write != 0)
            {
                destination += encodedSize;
            }
        }

        if (header.epilogCount != 0 && ((header.epilogAtEnd == 0) || (header.epilogCount != 1)))
        {
#if VERIFY_GC_TABLES
            *(ushort*)destination = 0xFACE;
            size += sizeof(ushort);
            destination += sizeof(ushort);
#endif

            var writer = new EpilogTableWriter
            {
                Destination = write != 0 ? destination : null,
                PreviousOffset = 0,
            };
            var epilogTableSize = emitter.emitGenEpilogLst(gcRecordEpilog, &writer);
            size += epilogTableSize;
            if (write != 0)
            {
                destination += (nint)epilogTableSize;
            }
        }

#if DISPLAY_SIZES
        if (write != 0)
        {
            if (_codeGen.Interruptible)
            {
                Compiler.genMethodICnt = unchecked(Compiler.genMethodICnt + 1);
            }
            else
            {
                Compiler.genMethodNCnt = unchecked(Compiler.genMethodNCnt + 1);
            }
        }
#endif

        return size;
    }

    internal readonly unsafe nuint gcPtrTableSize(InfoHdr header, uint codeSize, ref nuint argTabOffset)
    {
#pragma warning disable IDE0007
        byte* temporary = stackalloc byte[17];
#pragma warning restore IDE0007
#if DEBUG
        temporary[16] = 0xAB;
#endif
        var size = gcMakeRegPtrTable(temporary, 0, header, codeSize, ref argTabOffset);
#if DEBUG
        assert(temporary[16] == 0xAB);
#endif
        return size;
    }

    internal readonly unsafe byte* gcPtrTableSave(byte* destination, InfoHdr header,
        uint codeSize, ref nuint argTabOffset)
    {
        return destination + (nint)gcMakeRegPtrTable(destination, -1, header, codeSize, ref argTabOffset);
    }

#if DUMP_GC_TABLES
    internal readonly unsafe void gcFindPtrsInFrame(void* infoBlock, void* codeBlock, uint offs)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC frame pointer dumping is not ported.");
    }

    internal readonly unsafe nuint gcInfoBlockHdrDump(byte* source, ref InfoHdr header, out uint codeSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC header decoding is not ported.");
    }

    internal readonly unsafe nuint gcDumpPtrTable(byte* source, InfoHdr header, uint codeSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table decoding is not ported.");
    }
#endif
}
#endif
