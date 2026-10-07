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
#if TARGET_ARM
    private regMaskTP genPrespilledUnmappedRegs()
    {
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
#endif

#if TARGET_RISCV64
    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion = false)
    {
        var size = EA_SIZE(attr);
        assert(tmpReg != reg2);

#if DEBUG
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
#endif

        var immediateFits = Emitter.isValidSimm12(imm);
        if (immediateFits)
        {
            Emitter.emitIns_R_R_I(ins, attr, reg1, reg2, imm);
        }
        else
        {
            assert(tmpReg != REG_NA);
            assert(!EA_IS_RELOC(size));

            Emitter.emitLoadImmediate(true, size, tmpReg, imm);
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
#endif
}
