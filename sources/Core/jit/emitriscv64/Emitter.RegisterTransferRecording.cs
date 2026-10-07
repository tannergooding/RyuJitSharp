// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
    {
        var code = emitInsCode(ins);

        if (ins is INS_mov or INS_sext_w or INS_not || (INS_clz <= ins && ins <= INS_rev8))
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= (uint)reg1 << 7;
            code |= (uint)reg2 << 15;
        }
        else if (ins is INS_fmv_x_d or INS_fmv_x_w or INS_fclass_s or INS_fclass_d)
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isFloatReg(reg2));
            code |= (uint)reg1 << 7;
            code |= ((uint)reg2 & 0x1F) << 15;
        }
        else if (ins is INS_fcvt_w_s or INS_fcvt_wu_s or INS_fcvt_w_d or INS_fcvt_wu_d or
            INS_fcvt_l_s or INS_fcvt_lu_s or INS_fcvt_l_d or INS_fcvt_lu_d)
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isFloatReg(reg2));
            code |= (uint)reg1 << 7;
            code |= ((uint)reg2 & 0x1F) << 15;
            code |= 0x1u << 12;
        }
        else if (ins is INS_fmv_w_x or INS_fmv_d_x)
        {
            assert(isFloatReg(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= ((uint)reg1 & 0x1F) << 7;
            code |= (uint)reg2 << 15;
        }
        else if (ins is INS_fcvt_s_w or INS_fcvt_s_wu or INS_fcvt_d_w or INS_fcvt_d_wu or
            INS_fcvt_s_l or INS_fcvt_s_lu or INS_fcvt_d_l or INS_fcvt_d_lu)
        {
            assert(isFloatReg(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= ((uint)reg1 & 0x1F) << 7;
            code |= (uint)reg2 << 15;

            if (ins is not INS_fcvt_d_w and not INS_fcvt_d_wu)
            {
                // Use the dynamic rounding mode; fcvt.d.w[u] is always exact.
                code |= 0x7u << 12;
            }
        }
        else if (ins is INS_fcvt_s_d or INS_fcvt_d_s or INS_fsqrt_s or INS_fsqrt_d)
        {
            assert(isFloatReg(reg1));
            assert(isFloatReg(reg2));
            code |= ((uint)reg1 & 0x1F) << 7;
            code |= ((uint)reg2 & 0x1F) << 15;

            if (ins is not INS_fcvt_d_s)
            {
                // fcvt.d.s is exact; the other conversions use the dynamic rounding mode.
                code |= 0x7u << 12;
            }
        }
        else
        {
            throw new FatalJitException(
                CORJIT_SKIPPED, "Illegal instruction within RISC-V two-register instruction recording.");
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaInstrEncode = code;
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
