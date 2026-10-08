// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

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

#if DEBUG && !TARGET_XARCH && !TARGET_ARM && !TARGET_ARM64 && !TARGET_WASM
    private static void emitInsSanityCheck(instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction sanity checking outside AMD64 is not ported.");
    }
#endif

#if !TARGET_XARCH && !TARGET_WASM && !TARGET_ARM
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

    private bool emitInsMayWriteToGCReg(instrDesc id)
    {
        var ins = id.idIns();

        switch (id.idInsFmt())
        {
            case IF_T1_C:
            case IF_T1_D0:
            case IF_T1_E:
            case IF_T1_G:
            case IF_T1_H:
            case IF_T1_J0:
            case IF_T1_J1:
            case IF_T1_J2:
            case IF_T1_J3:
            case IF_T2_C0:
            case IF_T2_C1:
            case IF_T2_C2:
            case IF_T2_C3:
            case IF_T2_C4:
            case IF_T2_C5:
            case IF_T2_C6:
            case IF_T2_C10:
            case IF_T2_D0:
            case IF_T2_D1:
            case IF_T2_F1:
            case IF_T2_F2:
            case IF_T2_L0:
            case IF_T2_L1:
            case IF_T2_M0:
            case IF_T2_M1:
            case IF_T2_N:
            case IF_T2_N1:
            case IF_T2_N2:
            case IF_T2_N3:
            case IF_T2_VFP3:
            case IF_T2_VFP2:
            case IF_T2_VLDST:
            case IF_T2_E0:
            case IF_T2_E1:
            case IF_T2_E2:
            case IF_T2_G0:
            case IF_T2_G1:
            case IF_T2_H0:
            case IF_T2_H1:
            case IF_T2_K1:
            case IF_T2_K4:
            {
                return ins is not (
                    INS_str or INS_strb or INS_strh or INS_strd or INS_strex or INS_strexb or INS_strexd or INS_strexh or
                    INS_push or INS_cmp or INS_cmn or INS_tst or INS_teq);
            }

            case IF_T2_VMOVS:
            {
                // Integer-to-float moves read integer registers; float-to-integer moves can overwrite GC registers.
                assert(id.idGCref() == GCInfo.GCtype.GCT_NONE);
                return ins == INS_vmov_f2i;
            }

            case IF_T2_VMOVD:
            {
                // Integer-to-double moves read integer registers; double-to-integer moves can overwrite GC registers.
                assert(id.idGCref() == GCInfo.GCtype.GCT_NONE);
                return ins == INS_vmov_d2i;
            }

            default:
            {
                return false;
            }
        }
    }

#endif
}
