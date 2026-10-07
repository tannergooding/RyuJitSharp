// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insSvePattern;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPoisonFrame(regMaskTP regLiveIn)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Frame poisoning is not implemented for Wasm.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(_compiler.compShouldPoisonFrame());
#if TARGET_XARCH
        var poisonValReg = REG_EAX;
        assert((regLiveIn & (RBM_EDI | RBM_ECX | RBM_EAX)) == RBM_NONE);
#else
        var poisonValReg = REG_SCRATCH;
        assert((regLiveIn & (regMaskTP.CreateFromRegNum(REG_SCRATCH, REG_SCRATCH.SingleTypeMask)
            | regMaskTP.CreateFromRegNum(REG_ARG_0, REG_ARG_0.SingleTypeMask)
            | regMaskTP.CreateFromRegNum(REG_ARG_1, REG_ARG_1.SingleTypeMask)
            | regMaskTP.CreateFromRegNum(REG_ARG_2, REG_ARG_2.SingleTypeMask))) == RBM_NONE);
#endif
#if TARGET_64BIT
        var poisonVal = unchecked((nint)0xCDCDCDCDCDCDCDCDUL);
#else
        var poisonVal = unchecked((nint)(int)0xCDCDCDCD);
#endif

        var hasPoisonImm = false;
        for (var varNum = 0; varNum < _compiler.info.compLocalsCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            if (varDsc.lvIsParam || varDsc.lvMustInit || !varDsc.IsAddressExposed)
            {
                continue;
            }
            assert(varDsc.lvOnFrame);
#if TARGET_ARM64
            if (_compiler.lvaIsUnknownSizeLocal(varNum))
            {
                genPoisonUnknownSizeVariable(varNum, unchecked((sbyte)poisonVal));
                continue;
            }
#endif
            var size = unchecked((uint)_compiler.lvaLclStackHomeSize(varNum));

            // Preserve the native quotient threshold, not an estimated store count.
            if ((size / TARGET_POINTER_SIZE) > 16)
            {
#if TARGET_XARCH
                Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, REG_EDI, varNum, 0);
                assert((size % 4) == 0);
                instGen_Set_Reg_To_Imm(EA_4BYTE, REG_ECX, (nint)(size / 4));
                if (!hasPoisonImm)
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, REG_EAX, poisonVal);
                    hasPoisonImm = true;
                }
                instGen(INS_r_stosd);
#else
                Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, REG_ARG_0, varNum, 0);
                instGen_Set_Reg_To_Imm(EA_4BYTE, REG_ARG_1, unchecked((sbyte)poisonVal));
#if TARGET_64BIT
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, REG_ARG_2, unchecked((nint)size));
#else
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, REG_ARG_2, unchecked((nint)(int)size));
#endif
                genEmitHelperCall(CORINFO_HELP_NATIVE_MEMSET, 0, EA_UNKNOWN);
                hasPoisonImm = false;
#endif
            }
            else
            {
                if (!hasPoisonImm)
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, poisonValReg, poisonVal);
                    hasPoisonImm = true;
                }

#if TARGET_64BIT
                var addr = _compiler.lvaFrameAddress(varNum, out _);
#else
                var addr = 0;
#endif
                var end = unchecked(addr + (int)size);
                for (var offs = addr; offs < end;)
                {
#if TARGET_64BIT
                    if (((offs % 8) == 0) && (unchecked(end - offs) >= 8))
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_LONG), EA_8BYTE, REG_SCRATCH, varNum,
                            unchecked(offs - addr));
                        offs = unchecked(offs + 8);
                        continue;
                    }
#endif

                    assert(((offs % 4) == 0) && (unchecked(end - offs) >= 4));
                    Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, REG_SCRATCH, varNum,
                        unchecked(offs - addr));
                    offs = unchecked(offs + 4);
                }
            }
        }
#endif
    }

#if TARGET_ARM64
    private void genPoisonUnknownSizeVariable(int varNum, sbyte poisonVal)
    {
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(varNum >= 0);
        assert(varDsc.IsAddressExposed);

        if (varDsc.Type is TYP_SIMD)
        {
            Emitter.emitIns_R_I(INS_sve_mov, EA_SCALABLE, REG_SCRATCH_V, poisonVal, INS_OPTS_SCALABLE_B);
            Emitter.emitIns_S_R(INS_sve_str, EA_SCALABLE, REG_SCRATCH_V, varNum, 0);
        }
        else
        {
            assert(varDsc.Type is TYP_MASK);

            const ulong vectorPoisonVal = 0xffff0000ffff00ffUL;
            instGen_Set_Reg_To_Imm(EA_8BYTE, REG_SCRATCH, unchecked((nint)vectorPoisonVal));
            Emitter.emitIns_R_R(INS_sve_dup, EA_8BYTE, REG_SCRATCH_V, REG_SCRATCH, INS_OPTS_SCALABLE_D);
            Emitter.emitIns_R_PATTERN(INS_sve_ptrue, EA_SCALABLE, REG_SCRATCH_P, INS_OPTS_SCALABLE_B,
                SVE_PATTERN_ALL);
            Emitter.emitIns_R_R_R_I(INS_sve_cmpne, EA_SCALABLE, REG_SCRATCH_P, REG_SCRATCH_P, REG_SCRATCH_V,
                0, INS_OPTS_SCALABLE_B);
            Emitter.emitIns_S_R(INS_sve_str, EA_SCALABLE, REG_SCRATCH_P, varNum, 0);
        }
    }
#endif
}
