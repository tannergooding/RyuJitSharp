// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    private const int EMIT_MAX_IG_INS_COUNT = 256;

    private T emitAllocAnyInstr<T>(nuint size, emitAttr opsz)
        where T : instrDesc, new()
    {
#if !TARGET_AMD64 || EMITTER_STATS
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction allocation requires AMD64 without emitter allocation statistics.");
#else
        assert(_compiler is not null);
        assert(emitCurIG is not null);
        assert(emitCurIGfreeBase is not null);

#if DEBUG
        if (_compiler.compStressCompile(Compiler.compStressArea.STRESS_EMITTER, 1)
            && (emitCurIGinsCnt != 0) && !emitCurIG.endsWithAlignInstr())
        {
            emitNxtIG(extend: true);
        }
#endif

        assert((emitCurIGsize & (CODE_ALIGN - 1)) == 0);
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
        emitCurIG.igLastIns = id;
        assert(size >= (nuint)nint.Size);
        id.idSetPrevSize(unchecked((uint)emitLastInsFullSize));
        emitLastInsFullSize = (int)fullSize;
        emitLastInsIG = emitCurIG;

        id.StorageGroup = emitCurIG;
        id.StorageIndex = emitCurIGfreeBase.Count;
        id.StorageOffset = emitCurIGfreeNext + (nuint)_debugInfoSize;
        id.StorageSize = fullSize;
        emitCurIGfreeBase.Add(id);
        emitCurIGfreeNext += fullSize;

        assert(id.idReg1() == 0);
        assert(id.idReg2() == 0);
        assert(id.idCodeSize() == 0);
        emitInsCount = unchecked(emitInsCount + 1);

        if (_debugInfoSize > 0)
        {
            id.idDebugOnlyInfo(new instrDescDebugInfo
            {
                idNum = unchecked((uint)emitInsCount),
                idSize = size,
            });
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

        if (EA_IS_DSP_RELOC(opsz))
        {
            id.idSetIsDspReloc();
        }

        if (EA_IS_CNS_RELOC(opsz) && _compiler.opts.compReloc)
        {
            id.idSetIsCnsReloc();
        }

        emitCurIGinsCnt++;

#if DEBUG
        if (_compiler.compCurBB != emitCurIG.lastGeneratedBlock)
        {
            assert(_compiler.compCurBB is not null);
            emitCurIG.igBlocks.Add(_compiler.compCurBB);
            emitCurIG.lastGeneratedBlock = _compiler.compCurBB;

            if (_compiler.verbose)
            {
                jitprintf($"Mapped {_compiler.compCurBB.dspToString()} to {emitLabelString(emitCurIG)}\n");
            }
        }
#endif

        return id;
#endif
    }

    private instrDescBasic emitAllocInstr(emitAttr attr)
    {
        return emitAllocAnyInstr<instrDescBasic>(INSTR_DESC_SIZE, attr);
    }

    private instrDescJmp emitAllocInstrJmp()
    {
        return emitAllocAnyInstr<instrDescJmp>(DescriptorSizes.Jump, EA_1BYTE);
    }

#if FEATURE_LOOP_ALIGN
    private instrDescAlign emitAllocInstrAlign()
    {
        return emitAllocAnyInstr<instrDescAlign>(DescriptorSizes.Align, EA_1BYTE);
    }
#endif

    private instrDescBasic emitNewInstrSmall(emitAttr attr)
    {
        var id = emitAllocAnyInstr<instrDescBasic>(SMALL_IDSC_SIZE, attr);
        id.idSetIsSmallDsc();

        return id;
    }

    private instrDescBasic emitNewInstr(emitAttr attr)
    {
        return emitAllocInstr(attr);
    }

    private instrDescJmp emitNewInstrJmp()
    {
        return emitAllocInstrJmp();
    }

#if FEATURE_LOOP_ALIGN
    private instrDescAlign emitNewInstrAlign()
    {
        var id = emitAllocInstrAlign();
        id.idIns(instruction.INS_align);

        return id;
    }
#endif
}
