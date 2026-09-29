// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Numerics;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_S(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs)
    {
        assert(_compiler is not null);
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        var opt = INS_OPTS_NONE;
        var reg2 = REG_NA;
        var reg3 = REG_NA;
        var scale = 0;
        var isLdrStr = false;
        var isSimple = true;
        var useRegForImm = false;
        nint imm = 0;

        assert(offs >= 0);

        if (((varx >= 0) && _compiler.lvaIsUnknownSizeLocal(varx)) ||
            ((varx < 0) && codeGen.RegSet.tmpIsUnknownSizeTemp(varx)))
        {
            // Scalable locals and temps use VL/PL offsets from the unknown-size frame.
            assert(offs == 0);
            isSimple = false;
            reg2 = REG_UNKBASE;
            var localType = TYP_UNDEF;

            if (varx >= 0)
            {
                ref var varDsc = ref _compiler.lvaGetDesc(varx);
                imm = _compiler.unkSizeFrame.GetAddressingOffset(in varDsc);
                localType = varDsc.Type;
            }
            else
            {
                var tmpDsc = codeGen.RegSet.tmpGetNum(varx);
                imm = _compiler.unkSizeFrame.GetAddressingOffset(tmpDsc);
                localType = tmpDsc.tdTempType;
            }

            switch (ins)
            {
                case INS_lea:
                {
                    // TODO-SVE: Materializing the address of a mask local/temp is not supported upstream.
                    assert(localType != TYP_MASK);
                    if (isValidSimm(imm, 6))
                    {
                        emitIns_R_R_I(INS_sve_addvl, EA_8BYTE, reg1, REG_UNKBASE, imm);
                    }
                    else
                    {
                        // Form the address as the unknown-size frame base plus imm * VL.
                        var rsvd = codeGen.rsGetRsvdReg();
                        codeGen.instGen_Set_Reg_To_Imm(EA_8BYTE, reg1, imm);
                        emitIns_R_I(INS_sve_rdvl, EA_8BYTE, rsvd, 1);
                        emitIns_R_R_R_R(INS_madd, EA_8BYTE, reg1, reg1, rsvd, REG_UNKBASE);
                    }
                    return;
                }

                case INS_sve_ldr:
                {
                    // TODO-SVE: Upstream does not materialize large VL/PL-scaled offsets.
                    assert(isValidSimm(imm, 9));
                    fmt = isPredicateRegister(reg1) ? IF_SVE_ID_2A : IF_SVE_IE_2A;
                    break;
                }

                default:
                {
                    NYI("emitIns_R_S");
                    return;
                }
            }
        }
        else
        {
            var frameBase = _compiler.lvaFrameAddress(varx, out var FPbased);
            var disp = unchecked(frameBase + offs);
            imm = disp;
            reg2 = FPbased ? REG_FPBASE : REG_SPBASE;

            switch (ins)
            {
                case INS_strb:
                case INS_ldrb:
                case INS_ldrsb:
                {
                    scale = 0;
                    break;
                }

                case INS_strh:
                case INS_ldrh:
                case INS_ldrsh:
                {
                    scale = 1;
                    break;
                }

                case INS_ldrsw:
                {
                    scale = 2;
                    break;
                }

                case INS_str:
                case INS_ldr:
                {
                    assert(isValidGeneralDatasize(size) || isValidVectorDatasize(size));
                    scale = BitOperations.Log2((uint)EA_SIZE_IN_BYTES(size));
                    isLdrStr = true;
                    break;
                }

                case INS_lea:
                {
                    assert(size == EA_8BYTE);
                    isSimple = false;
                    scale = 0;

                    if (disp >= 0)
                    {
                        ins = INS_add;
                    }
                    else
                    {
                        ins = INS_sub;
                        imm = unchecked(-disp);
                    }

                    if (imm <= 0x0fff)
                    {
                        fmt = IF_DI_2A;
                    }
                    else
                    {
                        var rsvdReg = codeGen.rsGetRsvdReg();
                        codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, rsvdReg, imm);
                        imm = 0;
                        if (encodingZRtoSP(reg2) == REG_SP)
                        {
                            fmt = IF_DR_3C;
                            opt = INS_OPTS_LSL;
                            reg3 = rsvdReg;
                        }
                        else
                        {
                            fmt = IF_DR_3A;
                        }
                    }
                    break;
                }

                case INS_sve_ldr:
                {
                    assert(isPredicateRegister(reg1) || isVectorRegister(reg1));
                    isSimple = false;
                    size = EA_SCALABLE;
                    attr = size;
                    fmt = isPredicateRegister(reg1) ? IF_SVE_ID_2A : IF_SVE_IE_2A;

                    useRegForImm = true;
                    var rsvdReg = codeGen.rsGetRsvdReg();
                    codeGen.instGen_Set_Reg_To_Base_Plus_Imm(EA_PTRSIZE, rsvdReg, reg2, imm);
                    reg2 = rsvdReg;
                    imm = 0;
                    break;
                }

                default:
                {
                    NYI("emitIns_R_S");
                    return;
                }
            }
        }

        assert((scale >= 0) && (scale <= 4));

        if (isSimple)
        {
            nint mask = (1 << scale) - 1;

            if (imm == 0)
            {
                fmt = IF_LS_2A;
            }
            else if ((imm < 0) || ((imm & mask) != 0))
            {
                if (isValidSimm(imm, 9))
                {
                    fmt = IF_LS_2C;
                }
                else
                {
                    useRegForImm = true;
                }
            }
            else if (imm > 0)
            {
                if (((imm & mask) == 0) && ((imm >> scale) < 0x1000))
                {
                    imm >>= scale;
                    fmt = IF_LS_2B;
                }
                else
                {
                    useRegForImm = true;
                }
            }

            if (useRegForImm)
            {
                var rsvdReg = codeGen.rsGetRsvdReg();
                codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, rsvdReg, imm);
                fmt = IF_LS_3A;
            }
        }

        assert(fmt != IF_NONE);

        if (isLdrStr && _compiler.opts.OptimizationEnabled &&
            OptimizeLdrStr(ins, attr, reg1, reg2, imm, size, fmt, true, varx, offs
#if DEBUG
                , useRegForImm
#endif
                ))
        {
            return;
        }

        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(encodingSPtoZR(reg2));
        id.idReg3(reg3);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
        id.idSetIsLclVar();

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_S_S(instruction ins, emitAttr attr1, emitAttr attr2,
        regNumber reg1, regNumber reg2, int varx, int offs)
    {
        assert(_compiler is not null);
        assert((ins == INS_ldp) || (ins == INS_ldnp));
        assert(EA_8BYTE == EA_SIZE(attr1));
        assert(EA_8BYTE == EA_SIZE(attr2));
        assert(isGeneralRegisterOrZR(reg1));
        assert(isGeneralRegisterOrZR(reg2));
        assert(offs >= 0);

        var fmt = IF_LS_3B;
        const int scale = 3;
        var frameBase = _compiler.lvaFrameAddress(varx, out var FPbased);
        var disp = unchecked(frameBase + offs);
        var reg3 = FPbased ? REG_FPBASE : REG_SPBASE;

        var useRegForAdr = true;
        nint imm = disp;
        nint mask = (1 << scale) - 1;
        if (imm == 0)
        {
            useRegForAdr = false;
        }
        else
        {
            if ((imm & mask) == 0)
            {
                var immShift = imm >> scale;
                if ((immShift >= -64) && (immShift <= 63))
                {
                    fmt = IF_LS_3C;
                    useRegForAdr = false;
                    imm = immShift;
                }
            }
        }

        if (useRegForAdr)
        {
            var rsvd = codeGen.rsGetRsvdReg();
            emitIns_R_R_Imm(INS_add, EA_PTRSIZE, rsvd, reg3, imm);
            reg3 = rsvd;
            imm = 0;
        }

        reg3 = encodingSPtoZR(reg3);

#pragma warning disable CA1508 // Preserve the native format assertion after exhaustive selection.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508

        var id = emitNewInstrCns(attr1, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);

        if (EA_IS_GCREF(attr2))
        {
            id.idGCrefReg2(GCT_GCREF);
        }
        else if (EA_IS_BYREF(attr2))
        {
            id.idGCrefReg2(GCT_BYREF);
        }
        else
        {
            id.idGCrefReg2(GCT_NONE);
        }

        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
        id.idSetIsLclVar();

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_S_R(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs)
    {
        assert(_compiler is not null);
        assert(offs >= 0);
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        var scale = 0;
        var isVectorStore = false;
        var isStr = false;
        var isSimple = true;
        var useRegForImm = false;
        var reg2 = REG_NA;
        nint imm = 0;

        if (((varx >= 0) && _compiler.lvaIsUnknownSizeLocal(varx)) ||
            ((varx < 0) && codeGen.RegSet.tmpIsUnknownSizeTemp(varx)))
        {
            assert(ins == INS_sve_str);
            assert(offs == 0);
            assert(attr == EA_SCALABLE);

            reg2 = REG_UNKBASE;
            fmt = isPredicateRegister(reg1) ? IF_SVE_JG_2A : IF_SVE_JH_2A;
            isSimple = false;

            if (varx >= 0)
            {
                imm = _compiler.unkSizeFrame.GetAddressingOffset(in _compiler.lvaGetDesc(varx));
            }
            else
            {
                imm = _compiler.unkSizeFrame.GetAddressingOffset(codeGen.RegSet.tmpGetNum(varx));
            }

            // TODO-SVE: Upstream does not materialize large VL/PL-scaled offsets.
            assert(isValidSimm(imm, 9));
        }
        else
        {
            var frameBase = _compiler.lvaFrameAddress(varx, out var FPbased);
            var disp = unchecked(frameBase + offs);
            imm = disp;
            reg2 = FPbased ? REG_FPBASE : REG_SPBASE;

            switch (ins)
            {
                case INS_strb:
                {
                    scale = 0;
                    assert(isGeneralRegisterOrZR(reg1));
                    break;
                }

                case INS_strh:
                {
                    scale = 1;
                    assert(isGeneralRegisterOrZR(reg1));
                    break;
                }

                case INS_str:
                {
                    if (isGeneralRegisterOrZR(reg1))
                    {
                        assert(isValidGeneralDatasize(size));
                        scale = (size == EA_8BYTE) ? 3 : 2;
                    }
                    else
                    {
                        assert(isVectorRegister(reg1));
                        assert(isValidVectorLSDatasize(size));
                        scale = (int)NaturalScale_helper(size);
                        isVectorStore = true;
                    }
                    isStr = true;
                    break;
                }

                case INS_sve_str:
                {
                    assert(isVectorRegister(reg1) || isPredicateRegister(reg1));
                    isSimple = false;
                    size = EA_SCALABLE;
                    attr = size;
                    fmt = isPredicateRegister(reg1) ? IF_SVE_JG_2A : IF_SVE_JH_2A;

                    useRegForImm = true;
                    var rsvdReg = codeGen.rsGetRsvdReg();
                    codeGen.instGen_Set_Reg_To_Base_Plus_Imm(EA_PTRSIZE, rsvdReg, reg2, imm);
                    reg2 = rsvdReg;
                    imm = 0;
                    break;
                }

                default:
                {
                    NYI("emitIns_S_R");
                    return;
                }
            }
        }

        if (isVectorStore || !isSimple)
        {
            assert(scale <= 4);
        }
        else
        {
            assert(scale <= 3);
        }

        if (isSimple)
        {
            nint mask = (1 << scale) - 1;

            if (imm == 0)
            {
                fmt = IF_LS_2A;
            }
            else if ((imm < 0) || ((imm & mask) != 0))
            {
                if (isValidSimm(imm, 9))
                {
                    fmt = IF_LS_2C;
                }
                else
                {
                    useRegForImm = true;
                }
            }
            else if (imm > 0)
            {
                if (((imm & mask) == 0) && ((imm >> scale) < 0x1000))
                {
                    imm >>= scale;
                    fmt = IF_LS_2B;
                }
                else
                {
                    useRegForImm = true;
                }
            }

            if (useRegForImm)
            {
                // The native idReg3 field overlaps the local address; this format implies the reserved register.
                var rsvdReg = codeGen.rsGetRsvdReg();
                codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, rsvdReg, imm);
                fmt = IF_LS_3A;
            }
        }

        assert(fmt != IF_NONE);

        if (isStr && _compiler.opts.OptimizationEnabled &&
            OptimizeLdrStr(ins, attr, reg1, reg2, imm, size, fmt, true, varx, offs
#if DEBUG
                , useRegForImm
#endif
                ))
        {
            return;
        }

        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(encodingSPtoZR(reg2));
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
        id.idSetIsLclVar();

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_S_S_R_R(instruction ins, emitAttr attr1, emitAttr attr2,
        regNumber reg1, regNumber reg2, int varx, int offs)
    {
        assert(_compiler is not null);
        assert((ins == INS_stp) || (ins == INS_stnp));
        assert(EA_8BYTE == EA_SIZE(attr1));
        assert(EA_8BYTE == EA_SIZE(attr2));
        assert(isGeneralRegisterOrZR(reg1));
        assert(isGeneralRegisterOrZR(reg2));
        assert(offs >= 0);

        var fmt = IF_LS_3B;
        const int scale = 3;
        var frameBase = _compiler.lvaFrameAddress(varx, out var FPbased);
        var disp = unchecked(frameBase + offs);
        var reg3 = FPbased ? REG_FPBASE : REG_SPBASE;

        var useRegForAdr = true;
        nint imm = disp;
        nint mask = (1 << scale) - 1;
        if (imm == 0)
        {
            useRegForAdr = false;
        }
        else
        {
            if ((imm & mask) == 0)
            {
                var immShift = imm >> scale;
                if ((immShift >= -64) && (immShift <= 63))
                {
                    fmt = IF_LS_3C;
                    useRegForAdr = false;
                    imm = immShift;
                }
            }
        }

        if (useRegForAdr)
        {
            var rsvd = codeGen.rsGetRsvdReg();
            emitIns_R_R_Imm(INS_add, EA_PTRSIZE, rsvd, reg3, imm);
            reg3 = rsvd;
            imm = 0;
        }

#pragma warning disable CA1508 // Preserve the native format assertion after exhaustive selection.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508

        var id = emitNewInstrCns(attr1, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);

        if (EA_IS_GCREF(attr2))
        {
            id.idGCrefReg2(GCT_GCREF);
        }
        else if (EA_IS_BYREF(attr2))
        {
            id.idGCrefReg2(GCT_BYREF);
        }
        else
        {
            id.idGCrefReg2(GCT_NONE);
        }

        reg3 = encodingSPtoZR(reg3);

        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
        id.idSetIsLclVar();

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
