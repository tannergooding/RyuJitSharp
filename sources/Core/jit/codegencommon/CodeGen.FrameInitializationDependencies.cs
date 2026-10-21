// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private regMaskTP genPrespilledUnmappedRegs()
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Prespilled unmapped registers are only supported on ARM.");
            return default!;
        }

        var regs = _regSet.rsMaskPreSpillRegs(false);

        if (_compiler._paramRegLocalMappings is not null)
        {
            foreach (var mapping in _compiler._paramRegLocalMappings)
            {
                var registerMask = new regMaskTP(unchecked((regMask)(
                    (ulong)mapping.RegisterSegment.RegisterMask << mapping.RegisterSegment.RegisterMaskBase)));
                regs &= ~registerMask;
            }
        }

        return regs;
    }

    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion = false)
    {
        if (Configuration.TARGET_RISCV64)
        {
            return genInstrWithConstantRiscV64(ins, attr, reg1, reg2, imm, tmpReg, inUnwindRegion);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return genInstrWithConstantLoongArch64(ins, attr, reg1, reg2, imm, tmpReg, inUnwindRegion);
        }

        if (Configuration.TARGET_ARM64)
        {
            return genInstrWithConstantArm64(ins, attr, reg1, reg2, imm, tmpReg, inUnwindRegion);
        }

        if (Configuration.TARGET_ARM)
        {
            if (inUnwindRegion)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "Unwind-region immediate materialization is not implemented for ARM.");
                return default!;
            }
            return genInstrWithConstantArm(ins, attr, reg1, reg2, imm, tmpReg, INS_FLAGS_DONT_CARE);
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "Immediate materialization is not implemented for this target.");
        return default!;
    }

    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, insFlags flags)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Instruction flags for immediate materialization are only supported on ARM.");
            return default!;
        }

        return genInstrWithConstantArm(ins, attr, reg1, reg2, imm, tmpReg, flags);
    }

    private bool genInstrWithConstantRiscV64(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion = false)
    {
        var size = EA_SIZE(attr);
        assert(tmpReg != reg2);

        if (Configuration.DEBUG)
        {
            switch (ins)
            {
                case INS_addi:
                case INS_sb:
                case INS_sh:
                case INS_sw:
                case INS_fsw:
                case INS_sd:
                case INS_fsd:
                case INS_lb:
                case INS_lh:
                case INS_lw:
                case INS_flw:
                case INS_ld:
                case INS_fld:
                case INS_lbu:
                case INS_lhu:
                case INS_lwu:
                {
                    break;
                }

                default:
                {
                    assert(false, conditionExpression: "!\"Unexpected instruction in genInstrWithConstant\"");
                    break;
                }
            }
        }

        var immediateFits = Emitter.isValidSimm12(imm);
        if (immediateFits)
        {
            Emitter.emitIns_R_R_I(ins, attr, reg1, reg2, imm);
        }
        else
        {
            assert(tmpReg != REG_NA);
            assert(!EA_IS_RELOC(size));

            _ = Emitter.emitLoadImmediate(true, size, tmpReg, imm);
            _regSet.verifyRegUsed(tmpReg);

            if (inUnwindRegion)
            {
                _compiler.unwindPadding();
            }

            if (ins == INS_addi)
            {
                Emitter.emitIns_R_R_R(INS_add, attr, reg1, reg2, tmpReg);
            }
            else
            {
                Emitter.emitIns_R_R_R(INS_add, attr, tmpReg, reg2, tmpReg);
                Emitter.emitIns_R_R_I(ins, attr, reg1, tmpReg, 0);
            }
        }

        return immediateFits;
    }
}
