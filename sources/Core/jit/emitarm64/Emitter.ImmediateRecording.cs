// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public unsafe partial class Emitter
{
    public void emitIns_I(instruction ins, emitAttr attr, nint imm)
    {
        var fmt = IF_NONE;
        if (ins == INS_BREAKPOINT)
        {
            if ((imm & 0x0000ffff) == imm)
            {
                fmt = IF_SI_0A;
            }
            else
            {
                assert(false, "Instruction cannot be encoded: IF_SI_0A");
            }
        }
        else
        {
            emitInsSve_I(ins, attr, imm);
            return;
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R(instruction ins, emitAttr attr, regNumber reg, insOpts opt = INS_OPTS_NONE)
    {
        insFormat fmt;
        switch (ins)
        {
            case INS_br:
            case INS_ret:
            {
                assert(isGeneralRegister(reg));
                fmt = IF_BR_1A;
                break;
            }

            case INS_dczva:
            case INS_autiza:
            case INS_autizb:
            case INS_paciza:
            case INS_pacizb:
            case INS_xpacd:
            case INS_xpaci:
            {
                assert(isGeneralRegister(reg));
                assert(attr == EA_8BYTE);
                fmt = (ins == INS_dczva) ? IF_SR_1A : IF_PC_1A;
                break;
            }

            case INS_mrs_tpid0:
            {
                fmt = IF_SR_1A;
                break;
            }

            default:
            {
                emitInsSve_R(ins, attr, reg, opt);
                return;
            }
        }
        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, nint imm,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        var size = EA_SIZE(attr);
        var elemsize = EA_UNKNOWN;
        var fmt = IF_NONE;
        var canEncode = false;
        bitMaskImm bmi;
        halfwordImm hwi;
        byteShiftedImm bsi;
        nint notOfImm;

        switch (ins)
        {
            case INS_tst:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg));
                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, size, &bmi);
                if (canEncode)
                {
                    imm = (nint)bmi.immNRS;
                    assert(isValidImmNRS(unchecked((nuint)imm), size));
                    fmt = IF_DI_1C;
                }
                break;
            }

            case INS_movk:
            case INS_movn:
            case INS_movz:
            {
                assert(isValidGeneralDatasize(size));
                assert(insOptsNone(opt)); // Explicit shifts use emitIns_R_I_I.
                assert(isGeneralRegister(reg));
                assert(isValidUimm(imm, 16));

                hwi = default;
                hwi.immHW = 0;
                hwi.immVal = unchecked((uint)imm);
                assert(imm == emitDecodeHalfwordImm(hwi, size));
                imm = (nint)hwi.immHWVal;
                canEncode = true;
                fmt = IF_DI_1B;
                break;
            }

            case INS_mov:
            {
                assert(isValidGeneralDatasize(size));
                assert(insOptsNone(opt));

                // Prefer a halfword, then its complement, then a replicated bitmask.
                hwi.immHWVal = 0;
                canEncode = canEncodeHalfwordImm(imm, size, &hwi);
                if (canEncode)
                {
                    assert(isGeneralRegister(reg));
                    imm = (nint)hwi.immHWVal;
                    assert(isValidImmHWVal(unchecked((nuint)imm), size));
                    fmt = IF_DI_1B;
                    break;
                }

                notOfImm = unchecked((nint)NOT_helper(imm, getBitWidth(size)));
                canEncode = canEncodeHalfwordImm(notOfImm, size, &hwi);
                if (canEncode)
                {
                    assert(isGeneralRegister(reg));
                    imm = (nint)hwi.immHWVal;
                    ins = INS_movn;
                    assert(isValidImmHWVal(unchecked((nuint)imm), size));
                    fmt = IF_DI_1B;
                    break;
                }

                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, size, &bmi);
                if (canEncode)
                {
                    assert(isGeneralRegisterOrSP(reg));
                    reg = encodingSPtoZR(reg);
                    imm = (nint)bmi.immNRS;
                    assert(isValidImmNRS(unchecked((nuint)imm), size));
                    fmt = IF_DI_1D;
                    break;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded: mov imm");
                }
                break;
            }

            case INS_movi:
            {
                assert(isValidVectorDatasize(size));
                assert(isVectorRegister(reg));
                if (insOptsNone(opt) && (size == EA_8BYTE))
                {
                    opt = INS_OPTS_1D;
                }
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);

                if (elemsize == EA_8BYTE)
                {
                    var uimm = unchecked((nuint)imm);
                    nint imm8 = 0;
                    var pos = 0;
                    canEncode = true;
                    while (uimm != 0)
                    {
                        var loByte = uimm & 0xFF;
                        if (((loByte == 0) || (loByte == 0xFF)) && (pos < 8))
                        {
                            if (loByte == 0xFF)
                            {
                                imm8 |= (nint)1 << pos;
                            }
                            uimm >>= 8;
                            pos++;
                        }
                        else
                        {
                            canEncode = false;
                            break;
                        }
                    }
                    imm = imm8;
                    assert(isValidUimm(imm, 8));
                    fmt = IF_DV_1B;
                    break;
                }
                else
                {
                    // Select LSL/MSL from the value, trying the complement only
                    // for the halfword and word arrangements that support MVNI.
                    bsi.immBSVal = 0;
                    canEncode = canEncodeByteShiftedImm(imm, elemsize, true, &bsi);
                    if (canEncode)
                    {
                        imm = (nint)bsi.immBSVal;
                        assert(isValidImmBSVal(unchecked((nuint)imm), size));
                        fmt = IF_DV_1B;
                        break;
                    }

                    if ((elemsize == EA_2BYTE) || (elemsize == EA_4BYTE))
                    {
                        notOfImm = unchecked((nint)NOT_helper(imm, getBitWidth(elemsize)));
                        canEncode = canEncodeByteShiftedImm(notOfImm, elemsize, true, &bsi);
                        if (canEncode)
                        {
                            imm = (nint)bsi.immBSVal;
                            ins = INS_mvni;
                            assert(isValidImmBSVal(unchecked((nuint)imm), size));
                            fmt = IF_DV_1B;
                            break;
                        }
                    }
                }
                break;
            }

            case INS_orr:
            case INS_bic:
            case INS_mvni:
            {
                assert(isValidVectorDatasize(size));
                assert(isVectorRegister(reg));
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert((elemsize == EA_2BYTE) || (elemsize == EA_4BYTE));

                bsi.immBSVal = 0;
                canEncode = canEncodeByteShiftedImm(imm, elemsize, ins == INS_mvni, &bsi);
                if (canEncode)
                {
                    imm = (nint)bsi.immBSVal;
                    assert(isValidImmBSVal(unchecked((nuint)imm), size));
                    fmt = IF_DV_1B;
                    break;
                }
                break;
            }

            case INS_cmp:
            case INS_cmn:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg));
                if (unsigned_abs(imm) <= 0x0fff)
                {
                    if (imm < 0)
                    {
                        ins = insReverse(ins);
                        imm = unchecked(-imm);
                    }
                    assert(isValidUimm(imm, 12));
                    canEncode = true;
                    fmt = IF_DI_1A;
                }
                else if (canEncodeWithShiftImmBy12(imm))
                {
                    opt = INS_OPTS_LSL12;
                    if (imm < 0)
                    {
                        ins = insReverse(ins);
                        imm = unchecked(-imm);
                    }
                    assert((imm & 0xfff) == 0);
                    imm >>= 12;
                    assert(isValidUimm(imm, 12));
                    canEncode = true;
                    fmt = IF_DI_1A;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded: IF_DI_1A");
                }
                break;
            }

            default:
            {
                emitInsSve_R_I(ins, attr, reg, imm, opt, sopt);
                return;
            }
        }
        assert(canEncode);
        assert(fmt != IF_NONE);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg);

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idMemCookie = unchecked((nint)targetHandle);
        debugInfo.idFlags = gtFlags;
#endif
        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
