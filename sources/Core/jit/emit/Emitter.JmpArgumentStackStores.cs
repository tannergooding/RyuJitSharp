// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_S_R(instruction ins, emitAttr attr, regNumber reg, int varNum, int offset)
    {
#if TARGET_ARM
        recordArm32InsSR(ins, attr, reg, varNum, offset);
#elif TARGET_LOONGARCH64
        emitInsSRLoongArch64(ins, attr, reg, varNum, offset);
#elif TARGET_RISCV64
        emitIns_S_R_R(ins, attr, reg, REG_NA, varNum, offset);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Target local-stack store recording is not implemented.");
#endif
    }

#if TARGET_RISCV64
    public void emitIns_S_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber tmpReg,
        int varNum, int offset)
    {
        assert(_compiler is not null);
        assert(codeGen is not null);
        var reservedReg = codeGen.rsGetRsvdReg();
        assert(tmpReg != reservedReg);

#if DEBUG
        switch (ins)
        {
            case INS_sd:
            case INS_sw:
            case INS_sh:
            case INS_sb:
            case INS_fsd:
            case INS_fsw:
            {
                break;
            }
            default:
            {
                NYI_RISCV64("illegal ins within emitIns_S_R_R!");
                return;
            }
        }
#endif

        var baseOffset = _compiler.lvaFrameAddress(varNum, out var framePointerBased);
        var frameReg = framePointerBased ? REG_FPBASE : REG_SPBASE;
        var reg2 = tmpReg == REG_NA ? frameReg : tmpReg;
        var imm = tmpReg == REG_NA
            ? unchecked((nint)(baseOffset + offset))
            : offset;
        assert(reg2 != REG_NA && reg2 != reservedReg);

        if (!isValidSimm12(imm))
        {
            // Use the reserved register for the large frame displacement without
            // consuming the caller's temporary-register hint.
            assert(isValidSimm20(unchecked((imm + 0x800) >> 12)));
            emitIns_R_I(INS_lui, EA_PTRSIZE, reservedReg, unchecked((imm + 0x800) >> 12));
            emitIns_R_R_R(INS_add, EA_PTRSIZE, reservedReg, reservedReg, reg2);

            imm &= 0xfff;
            reg2 = reservedReg;
        }

        var id = emitNewInstr(attr);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idIns(ins);

        assert(isGeneralRegister(reg2));
        var code = emitInsCode(ins);
        code |= (unchecked((uint)reg1) & 0x1f) << 20;
        code |= unchecked((uint)reg2) << 15;
        code |= ((unchecked((uint)(imm >> 5)) & 0x7f) << 25) |
                ((unchecked((uint)imm) & 0x1f) << 7);

        id.idAddr().iiaInstrEncode = code;
        id.idAddr().iiaLclVar.initLclVarAddr(varNum, unchecked((uint)offset));
        id.idSetIsLclVar();
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }
#endif
}
#endif
