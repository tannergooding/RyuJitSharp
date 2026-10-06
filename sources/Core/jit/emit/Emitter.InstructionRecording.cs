// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void dispIns(instrDesc id)
    {
#if TARGET_LOONGARCH64
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction display is not used on LoongArch64.");
#else
#if DEBUG
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        emitInsSanityCheck(id);
        assert(_compiler is not null);
        if (_compiler.opts.dspCode)
        {
            emitDispIns(id, true, false, false);
        }

#if EMIT_TRACK_STACK_DEPTH
        assert(unchecked((int)emitCurStackLvl) >= 0);
#endif
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        assert(debugInfo.idSize == (nuint)emitSizeOfInsDsc(id));
#endif
#if EMITTER_STATS
        emitIFcounts[(int)id.idInsFmt()] = unchecked(emitIFcounts[(int)id.idInsFmt()] + 1);
#endif
#endif
    }

    private void appendToCurIG(instrDesc id)
    {
#if TARGET_ARMARCH
        if (id.idIns() == INS_dmb)
        {
            emitLastMemBarrier = id;
        }
        else if (emitInsIsLoadOrStore(id.idIns()))
        {
            emitLastMemBarrier = null;
        }
#endif
        emitCurIGsize = unchecked(emitCurIGsize + (int)id.idCodeSize());
    }

#if EMITTER_STATS
    private static readonly uint[] emitIFcounts = new uint[(int)insFormat.IF_COUNT];
#endif

#if DEBUG && !TARGET_XARCH && !TARGET_ARM64 && !TARGET_WASM
    private static void emitInsSanityCheck(instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction sanity checking outside AMD64 is not ported.");
    }
#endif

#if !TARGET_XARCH && !TARGET_WASM
    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction display outside xarch is not ported.");
    }
#endif

#if TARGET_ARM
    private bool emitInsIsLoadOrStore(instruction ins)
    {
        // ARM32 instInfo uses the LD/ST bits from the native emitarm.cpp table.
        const byte LD = 2;
        const byte ST = 4;

        if (((int)ins >= 0) && ((uint)ins < (uint)CodeGen.instInfo.Length))
        {
            return (CodeGen.instInfo[(int)ins] & (LD | ST)) != 0;
        }

        return false;
    }

    private bool emitInsIsLoad(instruction ins)
    {
        const byte LD = 2;
        return ((int)ins >= 0) &&
            ((uint)ins < (uint)CodeGen.instInfo.Length) &&
            ((CodeGen.instInfo[(int)ins] & LD) != 0);
    }

    private bool emitInsIsCompare(instruction ins)
    {
        const byte CMP = 8;
        return ((int)ins >= 0) &&
            ((uint)ins < (uint)CodeGen.instInfo.Length) &&
            ((CodeGen.instInfo[(int)ins] & CMP) != 0);
    }

    private bool emitInsWritesToLclVarStackLoc(instrDesc id)
    {
        if (!id.idIsLclVar())
        {
            return false;
        }

        // Match the integer-store set in emitIns_S_R; float local stores use a different form.
        return id.idIns() is INS_strb or INS_strh or INS_str;
    }

    private bool emitInsMayWriteMultipleRegs(instrDesc id)
    {
        var ins = id.idIns();
        // IF_T2_E2 is the single-register POP encoding.
        return ins is INS_ldm or INS_ldmdb or INS_smlal or INS_smull or INS_umlal or INS_umull or INS_vmov_d2i ||
            ((ins == INS_pop) && (id.idInsFmt() != insFormat.IF_T2_E2));
    }

#endif
}
