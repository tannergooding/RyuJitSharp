// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    private const int EMIT_MAX_IG_INS_COUNT = 256;

#if EMITTER_STATS
    private static uint emitTotalInsCnt;
    private static uint emitTotalIDescCnt;
    private static uint emitTotalIDescSmallCnt;
    private static uint emitTotalIDescJmpCnt;
    private static uint emitTotalIDescLblCnt;
    private static uint emitTotalIDescAlignCnt;
#endif

    private void emitRecordMemAllocation(nuint size)
    {
        assert((size % sizeof(int)) == 0);

#if EMITTER_STATS
        emitTotMemAlloc = unchecked(emitTotMemAlloc + size);
#endif
    }

    private byte[] emitGetMem(nuint size)
    {
        emitRecordMemAllocation(size);

        return System.GC.AllocateUninitializedArray<byte>(checked((int)size));
    }

    private T emitGetMem<T>(nuint size)
        where T : class, new()
    {
        emitRecordMemAllocation(size);

        return new T();
    }

    private T emitAllocAnyInstr<T>(nuint size, emitAttr opsz)
        where T : instrDesc, new()
    {
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(emitCurIGfreeBase is not null);

        if (Configuration.DEBUG && _compiler.compStressCompile(Compiler.compStressArea.STRESS_EMITTER, 1)
            && (emitCurIGinsCnt != 0) && !emitCurIG.endsWithAlignInstr())
        {
            emitNxtIG(extend: true);
        }

        assert(IsCodeAligned(emitCurIGsize));
        var fullSize = size + (nuint)_debugInfoSize;

        if (((emitCurIGfreeNext + fullSize) >= emitCurIGfreeEndp) || emitForceNewIG
            || (emitCurIGinsCnt >= (EMIT_MAX_IG_INS_COUNT - 1)))
        {
            if (emitCurIGnonEmpty())
            {
                emitNxtIG(extend: true);
            }
            else if (emitNoGCIG)
            {
                emitCurIG.igFlags |= InsGroupFlags.NoGCInterrupt;
            }
            else
            {
                emitCurIG.igFlags &= ~InsGroupFlags.NoGCInterrupt;
            }
        }

        var id = new T();
        emitLastIns = id;
        if (Configuration.TARGET_XARCH)
        {
            emitCurIG.igLastIns = id;
        }
#if EMIT_BACKWARDS_NAVIGATION
        else
        {
            emitCurIG.igLastIns = id;
        }
#endif
        assert(size >= (nuint)nint.Size);
        if (Configuration.TARGET_XARCH)
        {
            id.idSetPrevSize(unchecked((uint)emitLastInsFullSize));
            emitLastInsFullSize = (int)fullSize;
        }
#if EMIT_BACKWARDS_NAVIGATION
        else
        {
            id.idSetPrevSize(unchecked((uint)emitLastInsFullSize));
            emitLastInsFullSize = (int)fullSize;
        }
#endif
        emitLastInsIG = emitCurIG;

        id.StorageGroup = emitCurIG;
        id.StorageIndex = emitCurIGfreeBase.Count;
        id.StorageOffset = emitCurIGfreeNext + (nuint)_debugInfoSize;
        id.StorageSize = fullSize;
        emitCurIGfreeBase.Add(id);
        emitCurIGfreeNext += fullSize;

        assert(id.idReg1() == 0);
        assert(id.idReg2() == 0);
        if (Configuration.TARGET_XARCH)
        {
            assert(id.idCodeSize() == 0);
        }
        emitInsCount = unchecked(emitInsCount + 1);

        if (_debugInfoSize > 0)
        {
            var info = emitGetMem<instrDescDebugInfo>(unchecked((nuint)DescriptorSizes.DebugInfo));
            info.idNum = unchecked((uint)emitInsCount);
            info.idSize = size;
            id.idDebugOnlyInfo(info);
        }

        if (EA_IS_GCREF(opsz))
        {
            id.idGCref(GCT_GCREF);
            id.idOpSize(EA_PTRSIZE);
        }
        else if (EA_IS_BYREF(opsz))
        {
            id.idGCref(GCT_BYREF);
            id.idOpSize(EA_PTRSIZE);
        }
        else
        {
            id.idGCref(GCT_NONE);
            id.idOpSize(EA_SIZE(opsz));
        }

        if (EA_IS_DSP_RELOC(opsz) &&
            (Configuration.TARGET_AMD64 || _compiler.opts.compReloc))
        {
            id.idSetIsDspReloc();
        }

        if (EA_IS_CNS_RELOC(opsz) && _compiler.opts.compReloc)
        {
            id.idSetIsCnsReloc();
        }

#if EMITTER_STATS
        emitTotalInsCnt = unchecked(emitTotalInsCnt + 1);
#endif
        emitCurIGinsCnt++;

#if DEBUG
        if (_compiler.compCurBB != emitCurIG.lastGeneratedBlock)
        {
            assert(_compiler.compCurBB is not null);
            emitCurIG.igBlocks.Add(_compiler.compCurBB);
            emitCurIG.lastGeneratedBlock = _compiler.compCurBB;

            if (_compiler.verbose)
            {
                jitprintf($"Mapped {Globals.FMT_BB(_compiler.compCurBB.bbNum)} to {emitLabelString(emitCurIG)}\n");
            }
        }
#endif

        return id;
    }

    private instrDescBasic emitAllocInstr(emitAttr attr)
    {
#if EMITTER_STATS
        emitTotalIDescCnt = unchecked(emitTotalIDescCnt + 1);
#endif

        return emitAllocAnyInstr<instrDescBasic>(unchecked((nuint)INSTR_DESC_SIZE), attr);
    }

    private instrDescJmp emitAllocInstrJmp()
    {
#if EMITTER_STATS
        emitTotalIDescJmpCnt = unchecked(emitTotalIDescJmpCnt + 1);
#endif

        return emitAllocAnyInstr<instrDescJmp>(unchecked((nuint)DescriptorSizes.Jump), EA_1BYTE);
    }

    private instrDescLbl emitAllocInstrLbl()
    {
        if (!(Configuration.TARGET_ARM || Configuration.TARGET_XARCH))
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Label instruction descriptors are not supported for this target.");
        }

#if EMITTER_STATS
        emitTotalIDescLblCnt = unchecked(emitTotalIDescLblCnt + 1);
#endif

        return emitAllocAnyInstr<instrDescLbl>(unchecked((nuint)DescriptorSizes.Label), EA_4BYTE);
    }

    private instrDescAlign emitAllocInstrAlign()
    {
#if EMITTER_STATS
        emitTotalIDescAlignCnt = unchecked(emitTotalIDescAlignCnt + 1);
#endif

        return emitAllocAnyInstr<instrDescAlign>((nuint)DescriptorSizes.Align, EA_1BYTE);
    }

    private instrDescBasic emitNewInstrSmall(emitAttr attr)
    {
        var id = emitAllocAnyInstr<instrDescBasic>(SMALL_IDSC_SIZE, attr);
        id.idSetIsSmallDsc();

#if EMITTER_STATS
        emitTotalIDescSmallCnt = unchecked(emitTotalIDescSmallCnt + 1);
#endif

        return id;
    }

    private instrDescBasic emitNewInstr(emitAttr attr)
    {
        // This always allocates a full instruction descriptor.
        return emitAllocInstr(attr);
    }

    private instrDescJmp emitNewInstrJmp()
    {
        return emitAllocInstrJmp();
    }

    private instrDescLbl emitNewInstrLbl()
    {
        return emitAllocInstrLbl();
    }

    private instrDescAlign emitNewInstrAlign()
    {
        var id = emitAllocInstrAlign();
        id.idIns(instruction.INS_align);
        if (Configuration.TARGET_ARM64)
        {
            id.idInsFmt(insFormat.IF_ARM64_SN_0A);
            id.idInsOpt(INS_OPTS_ALIGN);
        }

        return id;
    }
}
