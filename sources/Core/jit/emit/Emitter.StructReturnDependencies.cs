// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_S(instruction ins, emitAttr attr, regNumber reg, int varNum, int offset)
    {
#if TARGET_ARM
        recordArm32InsRS(ins, attr, reg, varNum, offset, out _);
#elif TARGET_RISCV64
        assert(_compiler is not null);
        assert(codeGen is not null);
        var size = EA_SIZE(attr);

#if DEBUG
        switch (ins)
        {
            case INS_lb:
            case INS_lbu:
            case INS_lh:
            case INS_lhu:
            case INS_lw:
            case INS_lwu:
            case INS_flw:
            case INS_ld:
            case INS_fld:
            {
                break;
            }
            case INS_lea:
            {
                assert(size == EA_8BYTE);
                break;
            }
            default:
            {
                NYI_RISCV64("illegal ins within emitIns_R_S!");
                return;
            }
        }
#endif

        var baseOffset = _compiler.lvaFrameAddress(varNum, out var framePointerBased);
        var imm = offset < 0
            ? unchecked((nint)(-offset - 8))
            : unchecked((nint)(baseOffset + offset));

        var frameReg = framePointerBased ? REG_FPBASE : REG_SPBASE;
        assert(offset >= 0);
        offset = offset < 0 ? unchecked(-offset - 8) : offset;

        reg = unchecked((regNumber)((uint)reg & 0x1F));
        uint code;
        if (isValidSimm12(imm))
        {
            if (ins is INS_lea)
            {
                ins = INS_addi;
            }

            code = emitInsCode(ins);
            code |= unchecked((uint)reg) << 7;
            code |= unchecked((uint)frameReg) << 15;
            code |= (unchecked((uint)imm) & 0xFFF) << 20;
        }
        else if (ins is INS_lea)
        {
            var reservedReg = codeGen.rsGetRsvdReg();
            assert(isValidSimm20(unchecked((imm + 0x800) >> 12)));
            emitIns_R_I(INS_lui, EA_PTRSIZE, reservedReg, unchecked((imm + 0x800) >> 12));

            var lowImmediate = imm & 0xFFF;
            emitIns_R_R_I(INS_addi, EA_PTRSIZE, reservedReg, reservedReg, lowImmediate);

            ins = INS_add;
            code = emitInsCode(ins);
            code |= unchecked((uint)reg) << 7;
            code |= unchecked((uint)frameReg) << 15;
            code |= unchecked((uint)reservedReg) << 20;
        }
        else
        {
            var reservedReg = codeGen.rsGetRsvdReg();
            assert(isValidSimm20(unchecked((imm + 0x800) >> 12)));
            emitIns_R_I(INS_lui, EA_PTRSIZE, reservedReg, unchecked((imm + 0x800) >> 12));
            emitIns_R_R_R(INS_add, EA_PTRSIZE, reservedReg, reservedReg, frameReg);

            var lowImmediate = imm & 0xFFF;
            code = emitInsCode(ins);
            code |= unchecked((uint)reg) << 7;
            code |= unchecked((uint)reservedReg) << 15;
            code |= (unchecked((uint)lowImmediate) & 0xFFF) << 20;
        }

        var id = emitNewInstr(attr);
        id.idReg1(reg);
        id.idIns(ins);
        id.idAddr().iiaInstrEncode = code;
        id.idAddr().iiaLclVar.initLclVarAddr(varNum, unchecked((uint)offset));
        id.idSetIsLclVar();
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Target local-stack instruction recording is not implemented.");
#endif
    }
}
#endif
