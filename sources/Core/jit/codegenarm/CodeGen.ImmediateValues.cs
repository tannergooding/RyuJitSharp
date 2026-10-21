// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_ImmArm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
        )
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM32 immediate materialization requires ARM32.");
            return;
        }

        assert(!genIsValidFloatReg(reg));

        if (!_compiler.opts.compReloc)
        {
            size = EA_SIZE(size);
        }

        if (EA_IS_RELOC(size))
        {
            // The relocation address is host-sized; only nonrelocatable immediates narrow below.
            genMov32RelocatableImmediate(size, unchecked((nuint)imm), reg);
        }
        else if (imm == 0)
        {
            instGen_Set_Reg_To_ZeroArm32(size, reg, flags);
        }
        else
        {
            var val32 = unchecked((int)imm);
            if (validImmForMov(val32))
            {
                Emitter.emitIns_R_I(INS_mov, size, reg, val32, flags);
            }
            else
            {
                var immLo16 = val32 & 0xffff;
                var immHi16 = (val32 >> 16) & 0xffff;

                assert(validImmForMov(immLo16));
                assert(immHi16 != 0);

                Emitter.emitIns_R_I(INS_movw, size, reg, immLo16);

                // For -32768 through -1, a low register can sign-extend the
                // low halfword with SXTH instead of loading an all-ones high halfword.
                if (RyuJitSharp.Emitter.isLowRegister(reg) && (immHi16 == 0xffff) && ((immLo16 & 0x8000) == 0x8000))
                {
                    _ = Emitter.emitIns_Mov(INS_sxth, EA_4BYTE, reg, reg, canSkip: false);
                }
                else
                {
                    Emitter.emitIns_R_I(INS_movt, size, reg, immHi16);
                }

                if (flags == INS_FLAGS_SET)
                {
                    _ = Emitter.emitIns_Mov(INS_mov, size, reg, reg, canSkip: false, INS_FLAGS_SET);
                }
            }
        }

        _regSet.verifyRegUsed(reg);
    }
}
