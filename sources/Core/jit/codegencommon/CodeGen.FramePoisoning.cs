// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPoisonFrame(regMaskTP regLiveIn)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Frame poisoning requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(_compiler.compShouldPoisonFrame());
        var poisonValReg = REG_EAX;
        assert((regLiveIn & (RBM_EDI | RBM_ECX | RBM_EAX)) == RBM_NONE);
        var poisonVal = unchecked((nint)0xCDCDCDCDCDCDCDCDUL);

        // REP STOSD preserves EAX, so all selected locals can share one load.
        var hasPoisonImm = false;
        for (var varNum = 0; varNum < _compiler.info.compLocalsCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            if (varDsc.lvIsParam || varDsc.lvMustInit || !varDsc.IsAddressExposed)
            {
                continue;
            }
            assert(varDsc.lvOnFrame);
            var size = unchecked((uint)_compiler.lvaLclStackHomeSize(varNum));

            // Preserve the native quotient threshold, not an estimated store count.
            if ((size / TARGET_POINTER_SIZE) > 16)
            {
                Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, REG_EDI, varNum, 0);
                assert((size % 4) == 0);
                instGen_Set_Reg_To_Imm(EA_4BYTE, REG_ECX, (nint)(size / 4));
                if (!hasPoisonImm)
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, REG_EAX, poisonVal);
                    hasPoisonImm = true;
                }
                instGen(INS_r_stosd);
            }
            else
            {
                if (!hasPoisonImm)
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, poisonValReg, poisonVal);
                    hasPoisonImm = true;
                }

                var addr = _compiler.lvaFrameAddress(varNum, out _);
                var end = unchecked(addr + (int)size);
                for (var offs = addr; offs < end;)
                {
                    if (((offs % 8) == 0) && (unchecked(end - offs) >= 8))
                    {
                        Emitter.emitIns_S_R(ins_Store(TYP_LONG), EA_8BYTE, REG_SCRATCH, varNum,
                            unchecked(offs - addr));
                        offs = unchecked(offs + 8);
                        continue;
                    }

                    assert(((offs % 4) == 0) && (unchecked(end - offs) >= 4));
                    Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, REG_SCRATCH, varNum,
                        unchecked(offs - addr));
                    offs = unchecked(offs + 4);
                }
            }
        }
#endif
    }
}
