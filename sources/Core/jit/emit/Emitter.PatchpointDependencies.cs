// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R(instruction ins, emitAttr attr, regNumber reg)
    {
        if (Configuration.TARGET_XARCH)
        {
            emitIns_R(ins, attr, reg, INS_OPTS_NONE);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            recordArm32InsR(ins, attr, reg);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Target register instruction recording is not implemented.");
    }

    public void emitIns_R_R_I(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        nint imm,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_RISCV64)
        {
            emitIns_R_R_I_RiscV(ins, attr, reg1, reg2, imm, instOptions);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitIns_R_R_I(ins, attr, reg1, reg2, unchecked((int)imm), INS_OPTS_NONE);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Target two-register-immediate instruction recording is not implemented.");
    }

    public void emitIns_R_I_I(
        instruction ins,
        emitAttr attr,
        regNumber reg,
        nint imm1,
        nint imm2,
        insOpts instOptions = INS_OPTS_NONE,
        nuint targetHandle = 0,
        GenTreeFlags gtFlags = GTF_EMPTY)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_I_I_Arm64(ins, attr, reg, imm1, imm2, instOptions, targetHandle, gtFlags);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            emitIns_R_I_I_RiscV(ins, attr, reg, imm1, imm2);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Target register-immediate instruction recording is not implemented.");
    }

    public void emitLoadImmediateAddress(emitAttr attr, regNumber reg, nint immediate)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Address constant materialization is not implemented for this target.");
        }

        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 address constant materialization is not ported.");
    }
}
