// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insSvePattern;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPoisonFrame(regMaskTP regLiveIn)
    {
        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Frame poisoning is not implemented for Wasm.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        assert(_compiler.compShouldPoisonFrame());
        var poisonValReg = Configuration.TARGET_XARCH
            ? Configuration.TARGET_X86 ? REG_X86_EAX : REG_X64_RAX
            : TargetRuntimeMetadata.ScratchRegister;
        if (Configuration.TARGET_XARCH)
        {
            var poisonMask = Configuration.TARGET_X86
                ? new regMaskTP(SRBM_X86_EDI | SRBM_X86_ECX | SRBM_X86_EAX)
                : new regMaskTP(SRBM_X64_RDI | SRBM_X64_RCX | SRBM_X64_RAX);
            assert((regLiveIn & poisonMask) == RBM_NONE);
        }
        else
        {
            assert((regLiveIn & (regMaskTP.CreateFromRegNum(TargetRuntimeMetadata.ScratchRegister, TargetRuntimeMetadata.ScratchRegister.SingleTypeMask)
                | regMaskTP.CreateFromRegNum(TargetRuntimeMetadata.FirstIntegerArgumentRegister, TargetRuntimeMetadata.FirstIntegerArgumentRegister.SingleTypeMask)
                | regMaskTP.CreateFromRegNum(TargetRuntimeMetadata.SecondIntegerArgumentRegister, TargetRuntimeMetadata.SecondIntegerArgumentRegister.SingleTypeMask)
                | regMaskTP.CreateFromRegNum(
                    TargetRuntimeMetadata.ThirdIntegerArgumentRegister,
                    TargetRuntimeMetadata.ThirdIntegerArgumentRegister.SingleTypeMask))) == RBM_NONE);
        }

        var poisonVal = Configuration.TARGET_64BIT
            ? unchecked((nint)0xCDCDCDCDCDCDCDCDUL)
            : unchecked((nint)(int)0xCDCDCDCD);
        var targetPointerSize = Configuration.TARGET_64BIT ? sizeof(long) : sizeof(int);

        var hasPoisonImm = false;
        for (var varNum = 0; varNum < _compiler.info.compLocalsCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            if (varDsc.lvIsParam || varDsc.lvMustInit || !varDsc.IsAddressExposed)
            {
                continue;
            }
            assert(varDsc.lvOnFrame);
            if (Configuration.TARGET_ARM64 && _compiler.lvaLocalIsOnUnknownSizeFrame(varNum))
            {
                genPoisonUnknownSizeVariable(varNum, unchecked((sbyte)poisonVal));
                continue;
            }

            var size = unchecked((uint)_compiler.lvaLclStackHomeSize(varNum));

            // Preserve the native quotient threshold, not an estimated store count.
            if ((size / targetPointerSize) > 16)
            {
                if (Configuration.TARGET_XARCH)
                {
                    var diReg = Configuration.TARGET_X86 ? REG_X86_EDI : REG_X64_RDI;
                    var cxReg = Configuration.TARGET_X86 ? REG_X86_ECX : REG_X64_RCX;
                    Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, diReg, varNum, 0);
                    assert((size % 4) == 0);
                    instGen_Set_Reg_To_Imm(EA_4BYTE, cxReg, (nint)(size / 4));
                    if (!hasPoisonImm)
                    {
                        instGen_Set_Reg_To_Imm(EA_PTRSIZE, poisonValReg, poisonVal);
                        hasPoisonImm = true;
                    }
                    instGen(INS_r_stosd);
                }
                else
                {
                    Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.FirstIntegerArgumentRegister, varNum, 0);
                    instGen_Set_Reg_To_Imm(EA_4BYTE, TargetRuntimeMetadata.SecondIntegerArgumentRegister, unchecked((sbyte)poisonVal));
                    var sizeArgument = Configuration.TARGET_64BIT
                        ? unchecked((nint)size)
                        : unchecked((nint)(int)size);
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, TargetRuntimeMetadata.ThirdIntegerArgumentRegister, sizeArgument);
                    genEmitHelperCall(CORINFO_HELP_NATIVE_MEMSET, 0, EA_UNKNOWN);
                    hasPoisonImm = false;
                }
            }
            else
            {
                if (!hasPoisonImm)
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, poisonValReg, poisonVal);
                    hasPoisonImm = true;
                }

                nint addr;
                if (Configuration.TARGET_64BIT)
                {
                    addr = _compiler.lvaFrameAddress(varNum, out _);
                }
                else
                {
                    addr = 0;
                }

                var end = unchecked(addr + (int)size);
                for (var offs = addr; offs < end;)
                {
                    if (Configuration.TARGET_64BIT && ((offs % 8) == 0) && (unchecked(end - offs) >= 8))
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_LONG), EA_8BYTE, poisonValReg, varNum,
                            unchecked((int)(offs - addr)));
                        offs = unchecked(offs + 8);
                        continue;
                    }

                    assert(((offs % 4) == 0) && (unchecked(end - offs) >= 4));
                    Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, poisonValReg, varNum,
                        unchecked((int)(offs - addr)));
                    offs = unchecked(offs + 4);
                }
            }
        }
    }

    private void genPoisonUnknownSizeVariable(int varNum, sbyte poisonVal)
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Unknown-size frame poisoning requires ARM64.");
            return;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(varNum >= 0);
        assert(varDsc.IsAddressExposed);

        if (varDsc.Type is TYP_SIMD_ARM64)
        {
            Emitter.emitIns_R_I(INS_sve_mov, EA_SCALABLE_ARM64, REG_ARM64_V9, poisonVal, INS_OPTS_SCALABLE_B);
            Emitter.emitIns_S_R(INS_sve_str, EA_SCALABLE_ARM64, REG_ARM64_V9, varNum, 0);
        }
        else
        {
            assert(varDsc.Type is TYP_MASK);

            const ulong vectorPoisonVal = 0xffff0000ffff00ffUL;
            instGen_Set_Reg_To_Imm(EA_8BYTE, TargetRuntimeMetadata.ScratchRegister, unchecked((nint)vectorPoisonVal));
            Emitter.emitIns_R_R(INS_sve_dup, EA_8BYTE, REG_ARM64_V9, TargetRuntimeMetadata.ScratchRegister, INS_OPTS_SCALABLE_D);
            Emitter.emitIns_R_PATTERN(INS_sve_ptrue, EA_SCALABLE_ARM64, REG_ARM64_P4, INS_OPTS_SCALABLE_B,
                SVE_PATTERN_ALL);
            Emitter.emitIns_R_R_R_I(INS_sve_cmpne, EA_SCALABLE_ARM64, REG_ARM64_P4, REG_ARM64_P4, REG_ARM64_V9,
                0, INS_OPTS_SCALABLE_B);
            Emitter.emitIns_S_R(INS_sve_str, EA_SCALABLE_ARM64, REG_ARM64_P4, varNum, 0);
        }
    }
}
