// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    private const byte LOONGARCH64_LD = 1;
    private const byte LOONGARCH64_ST = 2;

    private bool emitInsIsLoad(instruction ins)
    {
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & LOONGARCH64_LD) != 0);
    }

    private bool emitInsIsLoadOrStore(instruction ins)
    {
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & (LOONGARCH64_LD | LOONGARCH64_ST)) != 0);
    }

    public bool emitInsMayWriteToGCReg(instruction ins)
    {
        assert(ins != INS_invalid);

        var instructionValue = (int)ins;
        return ((instructionValue >= (int)INS_mov) && (instructionValue <= (int)INS_jirl)) ||
               (ins == INS_movfcsr2gr) ||
               (ins == INS_movcf2gr)
#if FEATURE_SIMD
               || ((instructionValue >= (int)INS_vpickve2gr_d) && (instructionValue <= (int)INS_vpickve2gr_wu))
               || ((instructionValue >= (int)INS_vpickve2gr_h) && (instructionValue <= (int)INS_vpickve2gr_bu))
               || ((instructionValue >= (int)INS_xvpickve2gr_d) && (instructionValue <= (int)INS_xvpickve2gr_wu))
#endif
               ;
    }

    public bool emitInsWritesToLclVarStackLoc(instrDesc id)
    {
        if (!id.idIsLclVar())
        {
            return false;
        }

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

    public static bool IsMovInstruction(instruction ins)
    {
        return ins is INS_mov or
            INS_fmov_s or
            INS_fmov_d or
            INS_movgr2fr_w or
            INS_movgr2fr_d or
            INS_movfr2gr_s or
            INS_movfr2gr_d;
    }
}
#endif
