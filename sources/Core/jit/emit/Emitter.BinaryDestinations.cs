// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_AR(instruction ins, emitAttr attr, regNumber reg, regNumber baseReg, int displacement)
    {
#if TARGET_ARM
        recordArm32InsRAR(ins, attr, reg, baseReg, displacement);
#elif TARGET_ARM64
        NYI("emitIns_R_AR");
#elif TARGET_RISCV64
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 register-address instruction recording is not ported.");
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Register/address instruction recording requires xarch.");
#else
        emitIns_R_ARX(ins, attr, reg, baseReg, REG_NA, 1, displacement);
#endif
    }

    public regNumber emitIns_BASE_R_R_RM(instruction ins, emitAttr attr, regNumber targetReg,
        GenTree tree, GenTree regOp, GenTree rmOp)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Non-destructive binary instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(regOp.IsUsedFromReg);
        var useNdd = DoJitUseApxNDD(ins);
        if (emitIns_Mov(INS_mov, attr, targetReg, regOp.RegNum, canSkip: true, useNdd) && useNdd)
        {
            return emitInsBinary(ins, attr, regOp, rmOp, targetReg);
        }

        return emitInsBinary(ins, attr, tree, rmOp);
#endif
    }
}
