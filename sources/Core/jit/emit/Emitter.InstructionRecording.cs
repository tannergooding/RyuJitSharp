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
        if (Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Instruction display is not used on LoongArch64.");
        }

        if (Configuration.DEBUG)
        {
            if (Configuration.TARGET_AMD64)
            {
                RequireSupportedInstructionRecording();
            }

            emitInsSanityCheck(id);
            assert(_compiler is not null);
            if (_compiler.opts.dspCode)
            {
                emitDispIns(id, true, false, false);
            }
        }

        if (Configuration.DEBUG)
        {
            if (Configuration.EMIT_TRACK_STACK_DEPTH)
            {
                assert(unchecked((int)emitCurStackLvl) >= 0);
            }

            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            assert(debugInfo.idSize == (nuint)emitSizeOfInsDsc(id));
        }
        if (Configuration.EMITTER_STATS)
        {
            emitIFcounts[(int)id.idInsFmt()] = unchecked(emitIFcounts[(int)id.idInsFmt()] + 1);
        }
    }

    private void appendToCurIG(instrDesc id)
    {
        if (Configuration.TARGET_ARM || Configuration.TARGET_ARMARCH)
        {
            if (id.idIns() == INS_dmb)
            {
                emitLastMemBarrier = id;
            }
            else if (emitInsIsArmLoadOrStore(id.idIns()))
            {
                emitLastMemBarrier = null;
            }
        }

        emitCurIGsize = unchecked(emitCurIGsize + (int)id.idCodeSize());
    }

    private bool emitInsIsLoadOrStoreArm32(instruction ins)
    {
        const byte LD = 2;
        const byte ST = 4;

        return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)s_armInstructionLoadStoreFlags.Length &&
            (s_armInstructionLoadStoreFlags[nativeOrdinal] & (LD | ST)) != 0;
    }

    private bool emitInsIsArmLoadOrStore(instruction ins)
    {
        if (Configuration.TARGET_ARM)
        {
            return emitInsIsLoadOrStoreArm32(ins);
        }

        if (Configuration.TARGET_ARM64)
        {
            return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
                (uint)nativeOrdinal < (uint)s_arm64InstructionLoadStoreFlags.Length &&
                s_arm64InstructionLoadStoreFlags[nativeOrdinal] != 0;
        }

        throw new FatalJitException(CORJIT_SKIPPED,
            "Load/store classification is only supported on ARM32 and ARM64.");
    }

    private static readonly uint[] emitIFcounts =
        Configuration.EMITTER_STATS ? new uint[emitFmtToOps.Length] : null!;

    private void emitInsSanityCheck(instrDesc id)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        if (Configuration.TARGET_XARCH)
        {
            emitInsSanityCheckXarch(id);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            emitInsSanityCheckArm32(id);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitInsSanityCheckArm64(id);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            emitInsSanityCheckRiscV64(id);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            emitInsSanityCheckWasm(id);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED,
            "Instruction sanity checking is not implemented for this target.");
    }

    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        if (Configuration.TARGET_XARCH)
        {
            emitDispInsXarch(id, isNew, doffs, asmfm, offset, code, size, ig);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            emitDispInsArm32(id, isNew, doffs, asmfm, offset, code, size, ig);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitDispInsArm64(id, isNew, doffs, asmfm, offset, code, size, ig);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            emitDispInsRiscV64(id, isNew, doffs, asmfm, offset, code, size, ig);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitDispInsLoongArch64(id, offset, ig);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            emitDispInsWasm(id, isNew, doffs, asmfm, offset, code, size, ig);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Instruction display is not implemented for this target.");
    }

    private bool emitInsIsLoadArm32(instruction ins)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 instruction classification is only supported on ARM32.");
        }

        return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)s_armInstructionLoadStoreFlags.Length &&
            ((s_armInstructionLoadStoreFlags[nativeOrdinal] & 2) != 0);
    }

    private bool emitInsIsCompareArm32(instruction ins)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 instruction classification is only supported on ARM32.");
        }

        return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)s_armInstructionCompareFlags.Length &&
            (s_armInstructionCompareFlags[nativeOrdinal] != 0);
    }

    private bool emitInsWritesToLclVarStackLocArm32(instrDesc id)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 instruction classification is only supported on ARM32.");
        }

        if (!id.idIsLclVar())
        {
            return false;
        }

        // Match the integer-store set in emitIns_S_R; float local stores use a different form.
        return id.idIns() is INS_strb or INS_strh or INS_str;
    }

    private bool emitInsMayWriteMultipleRegsArm32(instrDesc id)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 instruction classification is only supported on ARM32.");
        }

        var ins = id.idIns();
        // IF_T2_E2 is the single-register POP encoding.
        return ins is INS_ldm or INS_ldmdb or INS_smlal or INS_smull or INS_umlal or INS_umull or INS_vmov_d2i ||
            ((ins == INS_pop) && (id.idInsFmt() != insFormat.IF_ARM32_T2_E2));
    }

    private bool emitInsMayWriteToGCRegArm32(instrDesc id)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 instruction classification is only supported on ARM32.");
        }

        var ins = id.idIns();

        switch (id.idInsFmt())
        {
            case IF_ARM32_T1_C:
            case IF_ARM32_T1_D0:
            case IF_ARM32_T1_E:
            case IF_ARM32_T1_G:
            case IF_ARM32_T1_H:
            case IF_ARM32_T1_J0:
            case IF_ARM32_T1_J1:
            case IF_ARM32_T1_J2:
            case IF_ARM32_T1_J3:
            case IF_ARM32_T2_C0:
            case IF_ARM32_T2_C1:
            case IF_ARM32_T2_C2:
            case IF_ARM32_T2_C3:
            case IF_ARM32_T2_C4:
            case IF_ARM32_T2_C5:
            case IF_ARM32_T2_C6:
            case IF_ARM32_T2_C10:
            case IF_ARM32_T2_D0:
            case IF_ARM32_T2_D1:
            case IF_ARM32_T2_F1:
            case IF_ARM32_T2_F2:
            case IF_ARM32_T2_L0:
            case IF_ARM32_T2_L1:
            case IF_ARM32_T2_M0:
            case IF_ARM32_T2_M1:
            case IF_ARM32_T2_N:
            case IF_ARM32_T2_N1:
            case IF_ARM32_T2_N2:
            case IF_ARM32_T2_N3:
            case IF_ARM32_T2_VFP3:
            case IF_ARM32_T2_VFP2:
            case IF_ARM32_T2_VLDST:
            case IF_ARM32_T2_E0:
            case IF_ARM32_T2_E1:
            case IF_ARM32_T2_E2:
            case IF_ARM32_T2_G0:
            case IF_ARM32_T2_G1:
            case IF_ARM32_T2_H0:
            case IF_ARM32_T2_H1:
            case IF_ARM32_T2_K1:
            case IF_ARM32_T2_K4:
            {
                return ins is not (
                    INS_str or INS_strb or INS_strh or INS_strd or INS_strex or INS_strexb or INS_strexd or INS_strexh or
                    INS_push or INS_cmp or INS_cmn or INS_tst or INS_teq);
            }

            case IF_ARM32_T2_VMOVS:
            {
                // Integer-to-float moves read integer registers; float-to-integer moves can overwrite GC registers.
                assert(id.idGCref() == GCInfo.GCtype.GCT_NONE);
                return ins == INS_vmov_f2i;
            }

            case IF_ARM32_T2_VMOVD:
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
}
