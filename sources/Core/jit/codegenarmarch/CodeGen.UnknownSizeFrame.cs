// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genUnknownSizeFrame()
    {
        assert(_compiler.compLocallocUsed && _compiler.compUsesUnknownSizeFrame);
#if DEBUG
        assert(_compiler.unkSizeFrame.isFinalized);
#endif
        var totalVectorCount = _compiler.unkSizeFrame.FrameSizeInVectors();

        // SVE local offsets are based at the top of the UnknownSizeFrame.
        // TODO-SVE: Centering the frame could reduce address computations because
        // the indexed immediate is signed 9-bit.
        inst_Mov(TYP_I_IMPL, REG_UNKBASE, REG_SP, canSkip: false);

        if ((totalVectorCount > 0) && (totalVectorCount <= 32))
        {
            Emitter.emitIns_R_R_I(
                INS_sve_addvl, EA_8BYTE, REG_SP, REG_SP, -(nint)totalVectorCount);
        }
        else
        {
            assert(totalVectorCount != 0);
            var rsvd = rsGetRsvdReg();

            instGen_Set_Reg_To_Imm(EA_8BYTE, rsvd, (nint)totalVectorCount);
            Emitter.emitIns_R_I(INS_sve_rdvl, EA_8BYTE, REG_SCRATCH, 1);
            Emitter.emitIns_R_R_R_R(INS_msub, EA_8BYTE, REG_SP, rsvd, REG_SCRATCH, REG_SP);
        }
    }

    // The stack pointer must still point to the end of the frame established by genUnknownSizeFrame.
    public void genZeroInitializeUnknownSizeFrame()
    {
        assert(_compiler.compUsesUnknownSizeFrame);

        var vectorCount = _compiler.unkSizeFrame.FrameSizeInVectors();
        assert(vectorCount > 0);

        // z9 <== {0, 0, ...}
        Emitter.emitIns_R_I(INS_sve_mov, EA_SCALABLE, REG_SCRATCH_V, 0, INS_OPTS_SCALABLE_B);

        // For small vector counts, emit an unrolled loop of vector stores.
        // Unrolling to a maximum of 5 stores optimizes for code size rather than performance.
        // TODO-SVE: Does unrolling further improve performance?
        if (vectorCount <= 5)
        {
            for (var index = 0u; index < vectorCount; index++)
            {
                // str z9, [sp, #i MUL VL]
                Emitter.emitIns_R_R_I(INS_sve_str, EA_SCALABLE, REG_SCRATCH_V, REG_SP, (nint)index);
            }
        }
        else
        {
            // $cursor <== x19
            inst_Mov(TYP_BYREF, REG_SCRATCH, REG_UNKBASE, canSkip: false);
            var loop = genCreateTempLabel();

            // loop:
            genDefineInlineTempLabel(loop);

            // addvl $cursor, $cursor, #-1
            Emitter.emitIns_R_R_I(INS_sve_addvl, EA_8BYTE, REG_SCRATCH, REG_SCRATCH, -1);

            // str z9, [$cursor]
            Emitter.emitIns_R_R(INS_sve_str, EA_SCALABLE, REG_SCRATCH_V, REG_SCRATCH);

            // cmp sp, $cursor
            Emitter.emitIns_R_R(INS_cmp, EA_8BYTE, REG_SP, REG_SCRATCH, INS_OPTS_UXTX);

            // b.ne loop
            Emitter.emitIns_J(INS_bne, loop);
        }
    }
}
#endif
