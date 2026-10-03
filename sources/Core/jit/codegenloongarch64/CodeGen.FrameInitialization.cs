// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion = false)
    {
        var size = EA_SIZE(attr);
        assert(tmpReg != reg2);

#if DEBUG
        switch (ins)
        {
            case INS_addi_d:
            case INS_st_b:
            case INS_st_h:
            case INS_st_w:
            case INS_fst_s:
            case INS_st_d:
            case INS_fst_d:
            case INS_ld_b:
            case INS_ld_bu:
            case INS_ld_h:
            case INS_ld_hu:
            case INS_ld_w:
            case INS_fld_s:
            case INS_ld_d:
            case INS_fld_d:
            {
                break;
            }

            default:
            {
                assert(false, "!\"Unexpected instruction in genInstrWithConstant\"");
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

            Emitter.emitIns_I_la(size, tmpReg, imm);
            _regSet.verifyRegUsed(tmpReg);
            if (inUnwindRegion)
            {
                _compiler.unwindPadding();
            }

            if (ins == INS_addi_d)
            {
                Emitter.emitIns_R_R_R(INS_add_d, attr, reg1, reg2, tmpReg);
            }
            else
            {
                Emitter.emitIns_R_R_R(INS_add_d, attr, tmpReg, reg2, tmpReg);
                Emitter.emitIns_R_R_I(ins, attr, reg1, tmpReg, 0);
            }
        }

        return immediateFits;
    }
}
#endif
