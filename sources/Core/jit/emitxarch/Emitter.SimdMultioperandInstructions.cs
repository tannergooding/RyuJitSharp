// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_SIMD_R_R_R_A(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, GenTreeIndir indir, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(Is3OpRmwInstruction(ins));
        assert(UseSimdEncoding());
        assert((op2Reg != targetReg) || (op1Reg == targetReg));

        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_A(ins, attr, targetReg, op2Reg, indir, instOptions);
#endif
    }

    public unsafe void emitIns_SIMD_R_R_R_C(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(Is3OpRmwInstruction(ins));
        assert(UseSimdEncoding());
        assert((op2Reg != targetReg) || (op1Reg == targetReg));

        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_C(ins, attr, targetReg, op2Reg, fldHnd, offs, instOptions);
#endif
    }

    public void emitIns_SIMD_R_R_R_S(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, int varx, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(Is3OpRmwInstruction(ins));
        assert(UseSimdEncoding());
        assert((op2Reg != targetReg) || (op1Reg == targetReg));

        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_S(ins, attr, targetReg, op2Reg, varx, offs, instOptions);
#endif
    }

    public void emitIns_SIMD_R_R_R_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, regNumber op3Reg, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (Is3OpRmwInstruction(ins))
        {
            assert(UseSimdEncoding());
            if (instOptions != INS_OPTS_NONE)
            {
                assert(UseEvexEncodings);
            }

            // Copying op1 must not destroy another input.
            assert((op2Reg != targetReg) || (op1Reg == targetReg));
            assert((op3Reg != targetReg) || (op1Reg == targetReg));
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_R_R(ins, attr, targetReg, op2Reg, op3Reg, instOptions);
        }
        else if (UseSimdEncoding())
        {
            assert(isSse41Blendv(ins) || isAvxBlendv(ins) || isAvx512Blendv(ins));
            switch (ins)
            {
                case INS_blendvps:
                {
                    ins = INS_vblendvps;
                    break;
                }

                case INS_blendvpd:
                {
                    ins = INS_vblendvpd;
                    break;
                }

                case INS_pblendvb:
                {
                    ins = INS_vpblendvb;
                    break;
                }

                default:
                {
                    break;
                }
            }

            emitIns_R_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, op3Reg, instOptions);
        }
        else
        {
            assert(isSse41Blendv(ins));
            assert(instOptions == INS_OPTS_NONE);
            assert((op1Reg != REG_XMM0) || (op3Reg == REG_XMM0));
            assert((op2Reg != REG_XMM0) || (op3Reg == REG_XMM0));

            // Legacy blendv uses XMM0 implicitly for the mask.
            _ = emitIns_Mov(INS_movaps, attr, REG_XMM0, op3Reg, canSkip: true);
            assert((op2Reg != targetReg) || (op1Reg == targetReg));
            // Reusing the mask's register as the destination must preserve its value.
            assert((targetReg != REG_XMM0) || (op1Reg == op3Reg));

            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_R(ins, attr, targetReg, op2Reg);
        }
#endif
    }

    public void emitIns_SIMD_R_R_A_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, GenTreeIndir indir, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            assert(isSse41Blendv(ins) || isAvxBlendv(ins) || isAvx512Blendv(ins));
            switch (ins)
            {
                case INS_blendvps:
                {
                    ins = INS_vblendvps;
                    break;
                }

                case INS_blendvpd:
                {
                    ins = INS_vblendvpd;
                    break;
                }

                case INS_pblendvb:
                {
                    ins = INS_vpblendvb;
                    break;
                }

                default:
                {
                    break;
                }
            }

            emitIns_R_R_A_R(ins, attr, targetReg, op1Reg, op3Reg, indir, instOptions);
        }
        else
        {
            assert(isSse41Blendv(ins));
            assert(instOptions == INS_OPTS_NONE);
            assert(op1Reg != REG_XMM0);

            _ = emitIns_Mov(INS_movaps, attr, REG_XMM0, op3Reg, canSkip: true);
            assert((targetReg != REG_XMM0) || (op1Reg == op3Reg));
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_A(ins, attr, targetReg, indir);
        }
#endif
    }

    public unsafe void emitIns_SIMD_R_R_C_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            assert(isSse41Blendv(ins) || isAvxBlendv(ins) || isAvx512Blendv(ins));
            switch (ins)
            {
                case INS_blendvps:
                {
                    ins = INS_vblendvps;
                    break;
                }

                case INS_blendvpd:
                {
                    ins = INS_vblendvpd;
                    break;
                }

                case INS_pblendvb:
                {
                    ins = INS_vpblendvb;
                    break;
                }

                default:
                {
                    break;
                }
            }

            emitIns_R_R_C_R(ins, attr, targetReg, op1Reg, op3Reg, fldHnd, offs, instOptions);
        }
        else
        {
            assert(isSse41Blendv(ins));
            assert(instOptions == INS_OPTS_NONE);
            assert(op1Reg != REG_XMM0);

            _ = emitIns_Mov(INS_movaps, attr, REG_XMM0, op3Reg, canSkip: true);
            assert((targetReg != REG_XMM0) || (op1Reg == op3Reg));
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_C(ins, attr, targetReg, fldHnd, offs);
        }
#endif
    }

    public void emitIns_SIMD_R_R_S_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, int varx, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            assert(isSse41Blendv(ins) || isAvxBlendv(ins) || isAvx512Blendv(ins));
            switch (ins)
            {
                case INS_blendvps:
                {
                    ins = INS_vblendvps;
                    break;
                }

                case INS_blendvpd:
                {
                    ins = INS_vblendvpd;
                    break;
                }

                case INS_pblendvb:
                {
                    ins = INS_vpblendvb;
                    break;
                }

                default:
                {
                    break;
                }
            }

            emitIns_R_R_S_R(ins, attr, targetReg, op1Reg, op3Reg, varx, offs, instOptions);
        }
        else
        {
            assert(isSse41Blendv(ins));
            assert(instOptions == INS_OPTS_NONE);
            assert(op1Reg != REG_XMM0);

            _ = emitIns_Mov(INS_movaps, attr, REG_XMM0, op3Reg, canSkip: true);
            assert((targetReg != REG_XMM0) || (op1Reg == op3Reg));
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_S(ins, attr, targetReg, varx, offs);
        }
#endif
    }

    public void emitIns_SIMD_R_R_R_A_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, GenTreeIndir indir, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(UseSimdEncoding());
        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_A_I(ins, attr, targetReg, op2Reg, indir, ival, IF_RWR_RRD_ARD_CNS, instOptions);
#endif
    }

    public unsafe void emitIns_SIMD_R_R_R_C_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, CORINFO_FIELD_HANDLE fldHnd, int offs, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(UseSimdEncoding());
        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_C_I(ins, attr, targetReg, op2Reg, fldHnd, offs, ival, instOptions);
#endif
    }

    public void emitIns_SIMD_R_R_R_S_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, int varx, int offs, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(UseSimdEncoding());
        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_S_I(ins, attr, targetReg, op2Reg, varx, offs, ival, instOptions);
#endif
    }

    public void emitIns_SIMD_R_R_R_R_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, regNumber op3Reg, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD multioperand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(UseSimdEncoding());
        _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
        emitIns_R_R_R_I(ins, attr, targetReg, op2Reg, op3Reg, ival, instOptions);
#endif
    }
}
#endif
