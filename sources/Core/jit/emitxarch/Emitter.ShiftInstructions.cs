// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsRMW(instruction ins, emitAttr attr, GenTreeStoreInd storeInd, GenTree src)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Read-modify-write instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var addr = storeInd.Addr;
        if (addr.Oper is GT_RELOAD or GT_COPY)
        {
            addr = addr.AsOp().Op1;
            assert(addr.Oper is not GT_RELOAD and not GT_COPY);
        }
        assert((addr.Oper is GT_LCL_VAR or GT_LEA or GT_CNS_INT) || addr.IsLclVarAddr);

        instrDesc id;
        uint sz;
        var offset = storeInd.Offset;

        if (src.IsContainedIntOrIImmed)
        {
            var intConst = src.AsIntConCommon();
            var iconVal = unchecked((int)intConst.IconValue);
            switch (ins)
            {
                case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
                {
                    iconVal &= 0x7F;
                    break;
                }

                default:
                {
                    break;
                }
            }

            if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
            {
                var lclVar = addr.AsLclFld();
                emitIns_S_I(ins, attr, lclVar.LclNum, unchecked((int)lclVar.LclOffs), iconVal);
                return;
            }
            else
            {
                id = emitNewInstrAmdCns(attr, offset, iconVal);
                emitHandleMemOp(storeInd, id, emitInsModeFormat(ins, IF_ARD_CNS), ins);
                id.idIns(ins);
                sz = emitInsSizeAM(id, insCodeMI(ins), iconVal);
            }
        }
        else
        {
            assert(!src.IsContained);

            if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
            {
                var lclVar = addr.AsLclFld();
                emitIns_S_R(ins, attr, src.RegNum, lclVar.LclNum, unchecked((int)lclVar.LclOffs));
                return;
            }

            id = emitNewInstrAmd(attr, offset);
            emitHandleMemOp(storeInd, id, emitInsModeFormat(ins, IF_ARD_RRD), ins);
            id.idReg1(src.RegNum);
            id.idIns(ins);
            sz = emitInsSizeAM(id, insCodeMR(ins));
        }

        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitInsRMW(instruction ins, emitAttr attr, GenTreeStoreInd storeInd)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Unary read-modify-write instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var addr = storeInd.Addr;
        if (addr.Oper is GT_RELOAD or GT_COPY)
        {
            addr = addr.AsOp().Op1;
            assert(addr.Oper is not GT_RELOAD and not GT_COPY);
        }
        assert((addr.Oper is GT_LCL_VAR or GT_LEA or GT_CNS_INT) || addr.IsLclVarAddr);

        var offset = storeInd.Offset;
        if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
        {
            var lclVar = addr.AsLclFld();
            emitIns_S(ins, attr, lclVar.LclNum, unchecked((int)lclVar.LclOffs));
            return;
        }

        var id = emitNewInstrAmd(attr, offset);
        emitHandleMemOp(storeInd, id, emitInsModeFormat(ins, IF_ARD), ins);
        id.idIns(ins);
        var sz = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_ARX(instruction ins, emitAttr attr, regNumber reg, regNumber @base,
        regNumber index, uint scale, int disp)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed-address instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_64BYTE) && (reg != REG_NA));
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg));

        if ((ins == INS_lea) && (reg == @base) && (index == REG_NA) && (disp == 0))
        {
            return;
        }

        var id = emitNewInstrAmd(attr, disp);
        var fmt = emitInsModeFormat(ins, IF_RRD_ARD);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(reg);
        id.idAddr().iiaAddrMode.amBaseReg = @base;
        id.idAddr().iiaAddrMode.amIndxReg = index;
        id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeScale(scale);

        assert(emitGetInsAmdAny(id) == disp);
        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_BASE_R_R_I(instruction ins, emitAttr attr, regNumber op1Reg, regNumber op2Reg, int ival)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Base register-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (DoJitUseApxNDD(ins) && (op1Reg != op2Reg))
        {
            if (IsShiftInstruction(ins) && (ival == 1))
            {
                emitIns_R_R(ins, attr, op1Reg, op2Reg, INS_OPTS_EVEX_nd);
            }
            else
            {
                emitIns_R_R_I(ins, attr, op1Reg, op2Reg, ival, INS_OPTS_EVEX_nd);
            }
        }
        else
        {
            _ = emitIns_Mov(INS_mov, attr, op1Reg, op2Reg, canSkip: true);
            if (IsShiftInstruction(ins) && (ival == 1))
            {
                emitIns_R(ins, attr, op1Reg);
            }
            else
            {
                emitIns_R_I(ins, attr, op1Reg, ival);
            }
        }
#endif
    }

#if TARGET_AMD64
    public static bool IsShiftInstruction(instruction ins)
    {
        return ins is INS_rcl_1 or INS_rcr_1 or INS_rol_1 or INS_ror_1 or INS_shl_1 or INS_shr_1 or INS_sar_1
            or INS_rcl or INS_rcr or INS_rol or INS_ror or INS_shl or INS_shr or INS_sar
            or INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N;
    }
#endif
}
