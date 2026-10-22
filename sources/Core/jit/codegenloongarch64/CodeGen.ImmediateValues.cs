// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
        )
    {
        if (Configuration.TARGET_ARM)
        {
            instGen_Set_Reg_To_ImmArm(size, reg, imm, flags, targetHandle, gtFlags);
            return;
        }

        if (Configuration.TARGET_XARCH)
        {
            instGen_Set_Reg_To_ImmXarch(size, reg, imm, flags, targetHandle, gtFlags);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            instGen_Set_Reg_To_ImmArm64(size, reg, imm, flags, targetHandle, gtFlags);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            assert(genIsValidIntReg(reg));
            if (EA_IS_RELOC(size))
            {
                Emitter.emitIns_R_AI(INS_addi, size, reg, reg, imm);
            }
            else
            {
                _ = Emitter.emitLoadImmediate(true, size, reg, imm);
            }

            _regSet.verifyRegUsed(reg);
            return;
        }

        if (!Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ImmediateMaterializationUsesASeparateTargetImplementation
    );
            return;
        }

        if (!_compiler.opts.compReloc)
        {
            size = EA_SIZE(size);
        }

        if (EA_IS_RELOC(size))
        {
            assert(genIsValidIntReg(reg));
            Emitter.emitIns_R_AI(INS_bl, size, reg, imm, targetHandle, gtFlags);
        }
        else
        {
            Emitter.emitIns_I_la(size, reg, imm);
        }

        _regSet.verifyRegUsed(reg);
    }
}
