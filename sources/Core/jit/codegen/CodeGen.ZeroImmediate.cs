// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_Zero(emitAttr size, regNumber reg)
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Register zeroing requires a fixed register set.");
        }

        assert(genIsValidIntOrFakeReg(reg));

        if (Configuration.TARGET_XARCH)
        {
            Emitter.emitIns_R_R(INS_xor, size, reg, reg);
        }
        else if (Configuration.TARGET_ARM)
        {
            Emitter.emitIns_R_I(INS_mov, size, reg, 0);
        }
        else if (Configuration.TARGET_ARM64)
        {
            // ARM64 returns void; other targets return an unused emission status.
#pragma warning disable IDE0058
            Emitter.emitIns_Mov(INS_mov, size, reg, REG_ARM64_ZR, canSkip: true);
#pragma warning restore IDE0058
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            Emitter.emitIns_R_R_I(INS_ori, size, reg, REG_LOONGARCH64_R0, 0, insOpts.INS_OPTS_NONE);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            Emitter.emitIns_R_R_I(INS_addi, size, reg, REG_RISCV64_R0, 0, insOpts.INS_OPTS_NONE);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Register zeroing is not implemented for this target.");
        }

        _regSet.verifyRegUsed(reg);
    }
}
