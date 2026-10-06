// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private void recordArm32InsRS(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs,
        out regNumber baseRegUsed)
    {
        assert(_compiler is not null);

        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Load() to select the correct instruction.");
        }

        switch (ins)
        {
            case INS_add:
            case INS_ldr:
            case INS_ldrh:
            case INS_ldrb:
            case INS_ldrsh:
            case INS_ldrsb:
            case INS_vldr:
            case INS_vmov:
            case INS_movw:
            case INS_movt:
            {
                break;
            }

            case INS_lea:
            {
                ins = INS_add;
                break;
            }

            default:
            {
                NYI("emitIns_R_S");
                baseRegUsed = REG_NA;
                return;
            }
        }

        var fmt = IF_NONE;
        const insFlags sf = INS_FLAGS_NOT_SET;
        var frameOffset = _compiler.lvaFrameAddress(
            varx,
            _compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT,
            out var reg2,
            offs,
            CodeGen.instIsFP(ins));
        var disp = unchecked(frameOffset + offs);
        var undisp = unsigned_abs(disp);
        baseRegUsed = reg2;

        if (CodeGen.instIsFP(ins))
        {
            if (undisp <= 0x03fc)
            {
                fmt = IF_T2_VLDST;
            }
            else
            {
                var rsvdReg = codeGen.rsGetRsvdReg();
                recordArm32InsGenStackOffset(rsvdReg, varx, offs, isFloatUsage: true, out baseRegUsed);
                recordArm32InsRR(INS_add, EA_4BYTE, rsvdReg, baseRegUsed, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                recordArm32InsRRI(ins, attr, reg1, rsvdReg, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                return;
            }
        }
        else if (emitInsIsLoadOrStoreForCodeGen(ins))
        {
            if (isLowRegister(reg1) && (reg2 == REG_SP) && (ins == INS_ldr) &&
                ((disp & 0x03fc) == disp))
            {
                fmt = IF_T1_J2;
            }
            else if ((disp >= 0) && (disp <= 0x0fff))
            {
                fmt = IF_T2_K1;
            }
            else if (undisp <= 0x0ff)
            {
                fmt = IF_T2_H0;
            }
            else
            {
                var rsvdReg = codeGen.rsGetRsvdReg();
                recordArm32InsGenStackOffset(rsvdReg, varx, offs, isFloatUsage: false, out baseRegUsed);
                fmt = IF_T2_E0;
                assert(baseRegUsed == reg2);
            }
        }
        else if (ins == INS_add)
        {
            if (isLowRegister(reg1) && (reg2 == REG_SP) && ((disp & 0x03fc) == disp))
            {
                fmt = IF_T1_J2;
            }
            else if (undisp <= 0x0fff)
            {
                if (disp < 0)
                {
                    ins = INS_sub;
                    disp = unchecked(-disp);
                }

                ins = ins == INS_add ? INS_addw : INS_subw;
                fmt = IF_T2_M0;
            }
            else
            {
                var rsvdReg = codeGen.rsGetRsvdReg();
                recordArm32InsGenStackOffset(rsvdReg, varx, offs, isFloatUsage: false, out baseRegUsed);
                assert(baseRegUsed == reg2);
                recordArm32InsRRR(ins, attr, reg1, reg2, rsvdReg, INS_FLAGS_DONT_CARE);
                return;
            }
        }
        else if (ins is INS_movw or INS_movt)
        {
            fmt = IF_T2_N;
        }

        assert((fmt == IF_T1_J2) || (fmt == IF_T2_E0) || (fmt == IF_T2_H0) || (fmt == IF_T2_K1) ||
            (fmt == IF_T2_L0) || (fmt == IF_T2_N) || (fmt == IF_T2_VLDST) || (fmt == IF_T2_M0));
        assert(sf != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrCns(attr, disp);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsSize(emitInsSize(fmt));
        id.idInsFlags(sf);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
        id.idSetIsLclVar();
        if (reg2 == REG_FPBASE)
        {
            id.idSetIsLclFPBase();
        }

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif

        dispIns(id);
        appendToCurIG(id);
    }

    private void recordArm32InsGenStackOffset(regNumber reg, int varx, int offs, bool isFloatUsage,
        out regNumber baseRegUsed)
    {
        assert(_compiler is not null);
        var frameOffset = _compiler.lvaFrameAddress(
            varx,
            _compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT,
            out _,
            offs,
            isFloatUsage);
        var disp = unchecked(frameOffset + offs);

        recordArm32InsRS(INS_movw, EA_4BYTE, reg, varx, offs, out baseRegUsed);
        if ((disp & 0xffff) != disp)
        {
            recordArm32InsRS(INS_movt, EA_4BYTE, reg, varx, offs, out var movtBaseReg);
            assert(baseRegUsed == movtBaseReg);
        }
    }

    private void recordArm32InsSR(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs)
    {
        assert(_compiler is not null);

        if (ins == INS_mov)
        {
            assert(false, "Please use ins_Store() to select the correct instruction.");
        }

        switch (ins)
        {
            case INS_str:
            case INS_strh:
            case INS_strb:
            case INS_vstr:
            {
                break;
            }

            default:
            {
                NYI("emitIns_S_R");
                return;
            }
        }

        var fmt = IF_NONE;
        const insFlags sf = INS_FLAGS_NOT_SET;
        var frameOffset = _compiler.lvaFrameAddress(
            varx,
            _compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT,
            out var reg2,
            offs,
            CodeGen.instIsFP(ins));
        var disp = unchecked(frameOffset + offs);
        var undisp = unsigned_abs(disp);

        if (CodeGen.instIsFP(ins))
        {
            if (undisp <= 0x03fc)
            {
                fmt = IF_T2_VLDST;
            }
            else
            {
                var rsvdReg = codeGen.rsGetRsvdReg();
                recordArm32InsGenStackOffset(rsvdReg, varx, offs, isFloatUsage: true, out var baseRegUsed);
                recordArm32InsRR(INS_add, EA_4BYTE, rsvdReg, baseRegUsed, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                recordArm32InsRRI(ins, attr, reg1, rsvdReg, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                return;
            }
        }
        else if (isLowRegister(reg1) && (reg2 == REG_SP) && (ins == INS_str) &&
            ((disp & 0x03fc) == disp))
        {
            fmt = IF_T1_J2;
        }
        else if ((disp >= 0) && (disp <= 0x0fff))
        {
            fmt = IF_T2_K1;
        }
        else if (undisp <= 0x0ff)
        {
            fmt = IF_T2_H0;
        }
        else
        {
            var rsvdReg = codeGen.rsGetRsvdReg();
            recordArm32InsGenStackOffset(rsvdReg, varx, offs, isFloatUsage: false, out var baseRegUsed);
            fmt = IF_T2_E0;
            assert(baseRegUsed == reg2);
        }

        assert((fmt == IF_T1_J2) || (fmt == IF_T2_E0) || (fmt == IF_T2_H0) ||
            (fmt == IF_T2_VLDST) || (fmt == IF_T2_K1));
        assert(sf != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrCns(attr, disp);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsSize(emitInsSize(fmt));
        id.idInsFlags(sf);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
        id.idSetIsLclVar();
        if (reg2 == REG_FPBASE)
        {
            id.idSetIsLclFPBase();
        }

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
