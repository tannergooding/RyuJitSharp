// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public bool arm_Valid_Imm_For_Instr(instruction ins, int imm, insFlags flags)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32ImmediateValidationIsNotSupportedForThisTarget
    );
            return default!;
        }

        return validImmForInstr(ins, imm, flags);
    }

    public bool arm_Valid_Imm_For_Add(int imm, insFlags flags)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32ImmediateValidationIsNotSupportedForThisTarget
    );
            return default!;
        }

        return RyuJitSharp.Emitter.emitIns_valid_imm_for_add(imm, flags);
    }

    public bool arm_Valid_Imm_For_Add_SP(int imm)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32StackPointerImmediateValidationIsNotSupportedForThisTarget
    );
            return default!;
        }

        return RyuJitSharp.Emitter.emitIns_valid_imm_for_add_sp(imm);
    }

    private void instGen_Set_Reg_To_ZeroArm32(emitAttr size, regNumber reg, insFlags flags)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32ZeroMaterializationIsNotSupportedForThisTarget
    );
            return;
        }

        assert(genIsValidIntOrFakeReg(reg));

        Emitter.emitIns_R_I(INS_mov, size, reg, 0, flags);
        _regSet.verifyRegUsed(reg);
    }

    public unsafe void genMov32RelocatableImmediate(emitAttr size, nuint address, regNumber reg)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32RelocationMaterializationIsNotSupportedForThisTarget
    );
            return;
        }

        assert(EA_IS_RELOC(size));

        Emitter.emitIns_MovRelocatableImmediate(INS_movw, size, reg, address);
        Emitter.emitIns_MovRelocatableImmediate(INS_movt, size, reg, address);

        if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS))
        {
            Emitter.emitIns_R_R_R(INS_add, size, reg, reg, REG_ARM32_PC);
        }
    }

    public unsafe void genMov32RelocatableDataLabel(uint value, regNumber reg)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32DataLabelMaterializationIsNotSupportedForThisTarget
    );
            return;
        }

        Emitter.emitIns_R_D(INS_movw, EA_HANDLE_CNS_RELOC, value, reg);
        Emitter.emitIns_R_D(INS_movt, EA_HANDLE_CNS_RELOC, value, reg);

        if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS))
        {
            Emitter.emitIns_R_R_R(INS_add, EA_HANDLE_CNS_RELOC, reg, reg, REG_ARM32_PC);
        }
    }
}
