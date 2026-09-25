// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_SIMD_R_R_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            if (IsAvxCommutative(ins) && (instOptions == INS_OPTS_NONE))
            {
                if (!IsExtendedReg(op1Reg) && IsExtendedReg(op2Reg))
                {
                    // We have a VEX encoded commutative instruction in which
                    // case we want to try to put the non-extended register as
                    // op2 since this may allow the 2-byte VEX prefix to be used.
                    (op1Reg, op2Reg) = (op2Reg, op1Reg);
                }
            }

            emitIns_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            // Ensure we aren't overwriting op2.
            assert((op2Reg != targetReg) || (op1Reg == targetReg));
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);

            if (IsMovInstruction(ins))
            {
                _ = emitIns_Mov(ins, attr, targetReg, op2Reg, canSkip: false);
            }
            else
            {
                emitIns_R_R(ins, attr, targetReg, op2Reg);
            }
        }
#endif
    }

    public void emitIns_SIMD_R_R_R_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op2Reg, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD register-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            emitIns_R_R_R_I(ins, attr, targetReg, op1Reg, op2Reg, ival, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            // Ensure we aren't overwriting op2.
            assert((op2Reg != targetReg) || (op1Reg == targetReg));
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_R_I(ins, attr, targetReg, op2Reg, ival);
        }
#endif
    }

#if TARGET_AMD64
    public bool UseSimdEncoding()
    {
        return UseVexEncodings || UseEvexEncodings;
    }

    public bool IsThreeOperandAVXInstruction(instruction ins)
    {
        if (!UseSimdEncoding())
        {
            return false;
        }

        return (prefixFlags(ins) & INS_FLAGS_Is3OperandInstructionMask) != 0;
    }

    public bool IsAvxCommutative(instruction ins)
    {
        if (!UseVexEncodings)
        {
            return false;
        }

        return (prefixFlags(ins) & INS_FLAGS_IsAvxCommutative) != 0;
    }
#endif
}
