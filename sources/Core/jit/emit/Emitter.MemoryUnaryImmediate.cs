// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_C(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, int offs)
    {
#if TARGET_ARM64
        NYI("emitIns_C");
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Single static-field instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        uint size;
        instrDesc id;
        if ((attr & EA_OFFSET_FLG) != 0)
        {
            assert(ins == INS_push);
            size = 1 + TARGET_POINTER_SIZE;
            id = emitNewInstrDsp(EA_1BYTE, offs);
            id.idIns(ins);
            id.idInsFmt(IF_MRD_OFF);
        }
        else
        {
            var format = emitInsModeFormat(ins, IF_MRD);
            id = emitNewInstrDsp(attr, offs);
            id.idIns(ins);
            id.idInsFmt(format);
            size = emitInsSizeCV(id, insCodeMR(ins));
        }
        if (TakesRexWPrefix(id))
        {
            size += emitGetRexPrefixSize(id, ins);
        }

        id.idAddr().iiaFieldHnd = fldHnd;
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#if TARGET_X86
        emitAdjustStackDepthPushPop(ins);
#endif
#endif
    }

    public void emitIns_AR(instruction ins, emitAttr attr, regNumber baseReg, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Unary memory instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(ins is INS_prefetcht0 or INS_prefetcht1 or INS_prefetcht2 or INS_prefetchnta or INS_inc or INS_dec);
        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_ARD));
        id.idAddr().iiaAddrMode.amBaseReg = baseReg;
        id.idAddr().iiaAddrMode.amIndxReg = REG_NA;
        if ((instOptions & INS_OPTS_EVEX_NoApxPromotion) != 0)
        {
            id.idSetNoApxEvexPromotion();
        }

        var size = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }
}
