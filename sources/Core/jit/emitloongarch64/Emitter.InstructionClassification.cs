// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private const byte LOONGARCH64_LD = 1;
    private const byte LOONGARCH64_ST = 2;

    private bool emitInsIsLoad(instruction ins)
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LoongArch64 instruction classification requires LoongArch64.");
        }

        // Pseudo instructions such as LEA are not represented in the instruction table.
        return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
               (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length &&
               (((byte)CodeGen.instInfo[nativeOrdinal] & LOONGARCH64_LD) != 0);
    }

    private bool emitInsIsLoadOrStore(instruction ins)
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LoongArch64 instruction classification requires LoongArch64.");
        }

        // Pseudo instructions such as LEA are not represented in the instruction table.
        return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
               (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length &&
               (((byte)CodeGen.instInfo[nativeOrdinal] & (LOONGARCH64_LD | LOONGARCH64_ST)) != 0);
    }

    public bool emitInsMayWriteToGCReg(instruction ins)
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LoongArch64 GC-register classification requires LoongArch64.");
        }

        assert(ins != INS_invalid);

        // Register-writing ranges follow the instruction groupings in instrsloongarch64.h.
        var writesToGCReg = InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_mov, INS_jirl) ||
                            (ins == INS_movfcsr2gr) ||
                            (ins == INS_movcf2gr);

        if (Configuration.FEATURE_SIMD)
        {
            writesToGCReg |= InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vpickve2gr_d, INS_vpickve2gr_wu) ||
                             InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vpickve2gr_h, INS_vpickve2gr_bu) ||
                             InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvpickve2gr_d, INS_xvpickve2gr_wu);
        }

        return writesToGCReg;
    }

    public bool emitInsWritesToLclVarStackLoc(instrDesc id)
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LoongArch64 stack-store classification requires LoongArch64.");
        }

        if (!id.idIsLclVar())
        {
            return false;
        }

        // This list mirrors stores accepted by emitIns_S_R and excludes floating-point locals.
        return id.idIns() switch
        {
            INS_st_d or
            INS_st_w or
            INS_st_b or
            INS_st_h or
            INS_stptr_d or
            INS_stx_d or
            INS_stx_w or
            INS_stx_b or
            INS_stx_h => true,
            _ => false,
        };
    }
}
