// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private void recordArm32InsRAR(instruction ins, emitAttr attr, regNumber ireg, regNumber reg, int offs)
    {
        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Load() to select the correct instruction.");
        }

        if (ins == INS_lea)
        {
            if (emitIns_valid_imm_for_add(offs, INS_FLAGS_DONT_CARE))
            {
                recordArm32InsRRI(INS_add, attr, ireg, reg, offs, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            }
            else
            {
                assert(false, "emitIns_R_AR immediate does not fit in the instruction.");
                NYI("emitIns_R_AR immediate");
            }

            return;
        }

        if (emitInsIsLoad(ins))
        {
            recordArm32InsRRI(ins, attr, ireg, reg, offs, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            return;
        }

        if ((ins is INS_mov or INS_ldr) && (EA_SIZE(attr) == EA_4BYTE))
        {
            recordArm32InsRRI(INS_ldr, attr, ireg, reg, offs, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            return;
        }

        if (ins == INS_vldr)
        {
            recordArm32InsRRI(ins, attr, ireg, reg, offs, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            return;
        }

        NYI("emitIns_R_AR");
    }

    private void recordArm32InsAR_R(instruction ins, emitAttr attr, regNumber ireg, regNumber reg, int offs)
    {
        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Store() to select the correct instruction.");
        }

        recordArm32InsRRI(ins, attr, ireg, reg, offs, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
    }

    private void recordArm32InsRARR(instruction ins, emitAttr attr, regNumber ireg, regNumber reg,
        regNumber rg2, int disp)
    {
        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Load() to select the correct instruction.");
        }

        if (ins == INS_lea)
        {
            recordArm32InsRRR(INS_add, attr, ireg, reg, rg2, INS_FLAGS_DONT_CARE);
            if (disp != 0)
            {
                recordArm32InsRRI(INS_add, attr, ireg, ireg, disp, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            }

            return;
        }

        if (emitInsIsLoad(ins) && (disp == 0))
        {
            recordArm32InsRRRImm(
                ins, attr, ireg, reg, rg2, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            return;
        }

        assert(false, "emitIns_R_ARR received an unexpected instruction or displacement.");
        NYI("emitIns_R_ARR");
    }

    private void recordArm32InsARRR(instruction ins, emitAttr attr, regNumber ireg, regNumber reg,
        regNumber rg2, int disp)
    {
        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Store() to select the correct instruction.");
        }

        if (!emitInsIsStore(ins))
        {
            assert(false, "emitIns_ARR_R received an unexpected instruction.");
            NYI("emitIns_ARR_R");
            return;
        }

        if (disp == 0)
        {
            recordArm32InsRRR(ins, attr, ireg, reg, rg2, INS_FLAGS_DONT_CARE);
        }
        else
        {
            recordArm32InsRRR(INS_add, attr, ireg, reg, rg2, INS_FLAGS_DONT_CARE);
            recordArm32InsRRI(ins, attr, ireg, ireg, disp, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
        }
    }

    private void recordArm32InsRARX(instruction ins, emitAttr attr, regNumber ireg, regNumber reg,
        regNumber rg2, uint mul, int disp)
    {
        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Load() to select the correct instruction.");
        }

        var shift = unchecked((int)genLog2(mul));
        if ((ins != INS_lea) && !emitInsIsLoad(ins))
        {
            assert(false, "emitIns_R_ARX received an unexpected instruction.");
            NYI("emitIns_R_ARX");
            return;
        }

        if (ins == INS_lea)
        {
            ins = INS_add;
        }

        if (disp == 0)
        {
            recordArm32InsRRRImm(ins, attr, ireg, reg, rg2, shift, INS_FLAGS_DONT_CARE, INS_OPTS_LSL);
            return;
        }

        var useForm2 = false;
        var mustUseForm1 = (unchecked((uint)disp) % mul) != 0 || (reg == ireg);
        if (!mustUseForm1 &&
            (reg >= REG_R8) && (ireg < REG_R8) && (rg2 < REG_R8) && ((disp >> shift) <= 7))
        {
            useForm2 = true;
        }

        if (useForm2)
        {
            recordArm32InsRRI(
                INS_add, EA_4BYTE, ireg, rg2, disp >> shift, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            recordArm32InsRRRImm(
                ins, attr, ireg, reg, ireg, shift, INS_FLAGS_NOT_SET, INS_OPTS_LSL);
        }
        else
        {
            recordArm32InsRRRImm(
                INS_add, attr, ireg, reg, rg2, shift, INS_FLAGS_NOT_SET, INS_OPTS_LSL);
            recordArm32InsRRI(ins, attr, ireg, ireg, disp, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
        }
    }
}
#endif
