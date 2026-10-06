// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsLoadStoreOp(instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir)
    {
        if (indir.IsUnaligned)
        {
            if (indir.Oper is GT_STOREIND)
            {
                var type = indir.AsStoreInd().Data.Type;
                if (type is TYP_FLOAT)
                {
                    var tmpReg = codeGen.InternalRegisters.GetSingle(indir);
                    _ = emitIns_Mov(INS_vmov_f2i, EA_4BYTE, tmpReg, dataReg, canSkip: false);
                    emitInsLoadStoreOp(INS_str, EA_4BYTE, tmpReg, indir, 0);
                    return;
                }
                else if (type is TYP_DOUBLE)
                {
                    var tmpReg1 = codeGen.InternalRegisters.Extract(indir);
                    var tmpReg2 = codeGen.InternalRegisters.GetSingle(indir);
                    emitIns_R_R_R(INS_vmov_d2i, EA_8BYTE, tmpReg1, tmpReg2, dataReg);
                    emitInsLoadStoreOp(INS_str, EA_4BYTE, tmpReg1, indir, 0);
                    emitInsLoadStoreOp(INS_str, EA_4BYTE, tmpReg2, indir, 4);
                    return;
                }
            }
            else if (indir.Oper is GT_IND)
            {
                var type = indir.Type;
                if (type is TYP_FLOAT)
                {
                    var tmpReg = codeGen.InternalRegisters.GetSingle(indir);
                    emitInsLoadStoreOp(INS_ldr, EA_4BYTE, tmpReg, indir, 0);
                    _ = emitIns_Mov(INS_vmov_i2f, EA_4BYTE, dataReg, tmpReg, canSkip: false);
                    return;
                }
                else if (type is TYP_DOUBLE)
                {
                    var tmpReg1 = codeGen.InternalRegisters.Extract(indir);
                    var tmpReg2 = codeGen.InternalRegisters.GetSingle(indir);
                    emitInsLoadStoreOp(INS_ldr, EA_4BYTE, tmpReg1, indir, 0);
                    emitInsLoadStoreOp(INS_ldr, EA_4BYTE, tmpReg2, indir, 4);
                    emitIns_R_R_R(INS_vmov_i2d, EA_8BYTE, dataReg, tmpReg1, tmpReg2);
                    return;
                }
            }
        }

        emitInsLoadStoreOp(ins, attr, dataReg, indir, 0);
    }

    private void emitInsLoadStoreOp(instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir, int offset)
    {
        var addr = indir.Addr;

        if (addr.IsContained)
        {
            assert(addr.Oper is GT_LCL_ADDR or GT_LEA);

            var lsl = 0;

            if (addr.Oper is GT_LEA)
            {
                var addrMode = addr.AsAddrMode();
                offset = unchecked(offset + addrMode.Offset);
                if (addrMode.Scale > 0)
                {
                    assert(isPow2(addrMode.Scale));
                    lsl = unchecked((int)genLog2(addrMode.Scale));
                }
            }

            var memBase = indir.Base ??
                throw new FatalJitException(CORJIT_INTERNALERROR, "Contained ARM32 indirect has no base register.");

            if (indir.HasIndex)
            {
                assert(addr.Oper is GT_LEA);

                var index = indir.Index;
                if (offset != 0)
                {
                    var tmpReg = codeGen.InternalRegisters.GetSingle(indir);

                    // Preserve GC address tracking when computing a partial address in a temporary register.
                    var lea = addr.AsAddrMode();
                    var leaBasePartialAddrAttr = lea.Type is TYP_REF or TYP_BYREF ? EA_BYREF : EA_PTRSIZE;

                    if (emitIns_valid_imm_for_add(offset, INS_FLAGS_DONT_CARE))
                    {
                        if (lsl > 0)
                        {
                            emitIns_R_R_R_I(INS_add, leaBasePartialAddrAttr, tmpReg, memBase.RegNum,
                                index.RegNum, lsl, INS_FLAGS_DONT_CARE, INS_OPTS_LSL);
                        }
                        else
                        {
                            emitIns_R_R_R(INS_add, leaBasePartialAddrAttr, tmpReg, memBase.RegNum, index.RegNum);
                        }

                        noway_assert(emitInsIsLoad(ins) || (tmpReg != dataReg));
                        emitIns_R_R_I(ins, attr, dataReg, tmpReg, offset,
                            INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                    }
                    else
                    {
                        codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, offset);
                        emitIns_R_R_R(INS_add, leaBasePartialAddrAttr, tmpReg, tmpReg, memBase.RegNum);

                        noway_assert(emitInsIsLoad(ins) || (tmpReg != dataReg));
                        noway_assert(tmpReg != index.RegNum);
                        emitIns_R_R_R_I(ins, attr, dataReg, tmpReg, index.RegNum, lsl,
                            INS_FLAGS_DONT_CARE, INS_OPTS_LSL);
                    }
                }
                else if (lsl > 0)
                {
                    emitIns_R_R_R_I(ins, attr, dataReg, memBase.RegNum, index.RegNum, lsl,
                        INS_FLAGS_DONT_CARE, INS_OPTS_LSL);
                }
                else
                {
                    emitIns_R_R_R(ins, attr, dataReg, memBase.RegNum, index.RegNum);
                }
            }
            else if (addr.Oper is GT_LCL_ADDR)
            {
                var varNode = addr.AsLclVarCommon();
                if (emitInsIsStore(ins))
                {
                    emitIns_S_R(ins, attr, dataReg, varNode.LclNum, varNode.LclOffs);
                }
                else
                {
                    emitIns_R_S(ins, attr, dataReg, varNode.LclNum, varNode.LclOffs);
                }
            }
            else if (emitIns_valid_imm_for_ldst_offset(offset, attr))
            {
                emitIns_R_R_I(ins, attr, dataReg, memBase.RegNum, offset,
                    INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            }
            else
            {
                var tmpReg = codeGen.InternalRegisters.GetSingle(indir);
                codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, offset);
                emitIns_R_R_R(ins, attr, dataReg, memBase.RegNum, tmpReg);
            }
        }
        else
        {
#if DEBUG
            if (addr.Oper is GT_LCL_ADDR)
            {
                assert(_compiler is not null);
                ref var varDsc = ref _compiler.lvaGetDesc(addr.AsLclVarCommon().LclNum);
                assert(!varDsc.lvTracked);
            }
#endif

            if (offset != 0)
            {
                assert(emitIns_valid_imm_for_add(offset, INS_FLAGS_DONT_CARE));
                emitIns_R_R_I(ins, attr, dataReg, addr.RegNum, offset,
                    INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            }
            else
            {
                emitIns_R_R(ins, attr, dataReg, addr.RegNum);
            }
        }
    }

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
