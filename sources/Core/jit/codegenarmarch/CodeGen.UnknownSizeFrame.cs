// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genUnknownSizeFrame()
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Unknown-size frame generation requires SIMD-enabled ARM64.");
        }

        assert(_compiler.compLocallocUsed && _compiler.compUsesUnknownSizeFrame);
#if DEBUG
        assert(_compiler.unkSizeFrame.isFinalized);
#endif
        var totalVectorCount = _compiler.unkSizeFrame.FrameSizeInVectors();

        // SVE local offsets are based at the top of the UnknownSizeFrame.
        // TODO-SVE: Centering the frame could reduce address computations because
        // the indexed immediate is signed 9-bit.
        Emitter.emitIns_MovArm64Frame(EA_8BYTE, REG_ARM64_R19, REG_ARM64_SP);

        if ((totalVectorCount > 0) && (totalVectorCount <= 32))
        {
            Emitter.emitIns_R_R_IArm64Frame(
                INS_sve_addvl, EA_8BYTE, REG_ARM64_SP, REG_ARM64_SP, -(long)totalVectorCount);
        }
        else
        {
            assert(totalVectorCount != 0);
            var rsvd = rsGetRsvdRegArm64();

            instGen_Set_UnknownSizeFrame_CountArm64(rsvd, totalVectorCount);
            Emitter.emitIns_R_IArm64Frame(INS_sve_rdvl, EA_8BYTE, REG_ARM64_R9, 1);
            Emitter.emitIns_R_R_R_RArm64Frame(INS_msub, EA_8BYTE, REG_ARM64_SP, rsvd, REG_ARM64_R9, REG_ARM64_SP);
        }
    }

    // The stack pointer must still point to the end of the frame established by genUnknownSizeFrame.
    public void genZeroInitializeUnknownSizeFrame()
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Unknown-size frame initialization requires SIMD-enabled ARM64.");
        }

        assert(_compiler.compUsesUnknownSizeFrame);

        var vectorCount = _compiler.unkSizeFrame.FrameSizeInVectors();
        assert(vectorCount > 0);

        // z9 <== {0, 0, ...}
        Emitter.emitIns_R_IArm64Frame(INS_sve_mov, EA_SCALABLE_ARM64, REG_ARM64_V9, 0, INS_OPTS_SCALABLE_B);

        // For small vector counts, emit an unrolled loop of vector stores.
        // Unrolling to a maximum of 5 stores optimizes for code size rather than performance.
        // TODO-SVE: Does unrolling further improve performance?
        if (vectorCount <= 5)
        {
            for (var index = 0u; index < vectorCount; index++)
            {
                // str z9, [sp, #i MUL VL]
                Emitter.emitIns_R_R_IArm64Frame(INS_sve_str, EA_SCALABLE_ARM64, REG_ARM64_V9, REG_ARM64_SP, index);
            }
        }
        else
        {
            // $cursor <== x19
            Emitter.emitIns_MovArm64Frame(EA_BYREF_FLG | EA_8BYTE, REG_ARM64_R9, REG_ARM64_R19);
            var loop = genCreateTempLabel();

            // loop:
            genDefineInlineTempLabel(loop);

            // addvl $cursor, $cursor, #-1
            Emitter.emitIns_R_R_IArm64Frame(INS_sve_addvl, EA_8BYTE, REG_ARM64_R9, REG_ARM64_R9, -1);

            // str z9, [$cursor]
            Emitter.emitIns_R_R_IArm64Frame(INS_sve_str, EA_SCALABLE_ARM64, REG_ARM64_V9, REG_ARM64_R9, 0);

            // cmp sp, $cursor
            Emitter.emitIns_R_R_IArm64Frame(INS_cmp, EA_8BYTE, REG_ARM64_SP, REG_ARM64_R9, 0, INS_OPTS_UXTX);

            // b.ne loop
            Emitter.emitIns_JArm64Frame(INS_bne, loop);
        }
    }

    private void instGen_Set_UnknownSizeFrame_CountArm64(regNumber reg, uint count)
    {
        if (count == 0)
        {
            Emitter.emitIns_MovArm64Frame(EA_8BYTE, reg, REG_ARM64_ZR);
        }
        else if (TargetCostEstimation.IsArm64MoveImmediate(count, EA_8BYTE))
        {
            Emitter.emitIns_R_IArm64Frame(INS_mov, EA_8BYTE, reg, count);
        }
        else
        {
            // MOVN fills untouched halfwords with ones; MOVZ fills them with zeroes.
            // Prefer MOVZ when both choices omit the same number of halfwords.
            var preferMovn = 0;
            for (var shift = 48; shift >= 0; shift -= 16)
            {
                var halfword = unchecked((ushort)((ulong)count >> shift));
                if (halfword == ushort.MaxValue)
                {
                    preferMovn++;
                }
                else if (halfword == 0)
                {
                    preferMovn--;
                }
            }

            var ins = preferMovn > 0 ? INS_movn : INS_movz;
            var skipValue = preferMovn > 0 ? ushort.MaxValue : (ushort)0;
            for (var shift = 0; shift < 64; shift += 16)
            {
                var halfword = unchecked((ushort)((ulong)count >> shift));
                if (halfword != skipValue)
                {
                    if (ins == INS_movn)
                    {
                        halfword = unchecked((ushort)~halfword);
                    }

                    Emitter.emitIns_MovHalfwordArm64Frame(ins, reg, halfword, shift);
                    ins = INS_movk;
                }
            }

            assert(ins == INS_movk);
        }

        _regSet.verifyRegUsed(reg);
    }
}
