// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && DEBUG
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitInsSanityCheck(instrDesc id)
    {
        assert(_compiler is not null);
        /* What instruction format have we got? */

        instruction ins;
        emitAttr elemsize;
        emitAttr datasize;
        emitAttr dstsize;
        emitAttr srcsize;
        nint imm;
        uint immShift;
        nint index;
        nint index2;

        switch (id.idInsFmt())
        {
            case IF_BI_0A: // BI_0A   ......iiiiiiiiii iiiiiiiiiiiiiiii               simm26:00
            {
                break;
            }

            case IF_BI_0B: // BI_0B   ......iiiiiiiiii iiiiiiiiiiii....               simm19:00
            {
                break;
            }

            case IF_LARGEJMP:
            case IF_LARGEADR:
            case IF_LARGELDC:
            {
                break;
            }

            case IF_BI_0C: // BI_0C   ......iiiiiiiiii iiiiiiiiiiiiiiii               simm26:00
            {
                break;
            }

            case IF_BI_1A: // BI_1A   ......iiiiiiiiii iiiiiiiiiiittttt      Rt       simm19:00
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                break;
            }

            case IF_BI_1B: // BI_1B   B.......bbbbbiii iiiiiiiiiiittttt      Rt imm6, simm14:00
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidImmShift(emitGetInsSC(id), id.idOpSize()));
                break;
            }

            case IF_BR_0A: // BR_0A   ................ ................
            {
                break;
            }

            case IF_BR_1A: // BR_1A   ................ ......nnnnn.....         Rn
            {
                assert(isGeneralRegister(id.idReg1()));
                break;
            }

            case IF_BR_1B: // BR_1B   ................ ......nnnnn.....         Rn
            {
                if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && id.idIsTlsGD())
                {
                    assert(id.idAddr().iiaAddr != null);
                }
                else
                {
                    assert(isGeneralRegister(id.idReg3()));
                }
                break;
            }

            case IF_LS_1A: // LS_1A   .X......iiiiiiii iiiiiiiiiiittttt      Rt    PC imm(1MB)
            {
                assert(isGeneralRegister(id.idReg1()) || isVectorRegister(id.idReg1()));
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_LS_2A:                                // LS_2A   .X.......X...... ......nnnnnttttt      Rt Rn
            {
                assert(isIntegerRegister(id.idReg1()) || // ZR
                       isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                assert((emitGetInsSC(id) == 0) || id.idIsTlsGD() || id.idIsReloc());
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_LS_2B: // LS_2B   .X.......Xiiiiii iiiiiinnnnnttttt      Rt Rn    imm(0-4095)
            {
                assert(isIntegerRegister(id.idReg1()) || // ZR
                       isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                assert(isValidUimm(emitGetInsSC(id), 12));
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_LS_2C: // LS_2C   .X.......X.iiiii iiiiPPnnnnnttttt      Rt Rn    imm(-256..+255) no/pre/post inc
            {
                assert(isIntegerRegister(id.idReg1()) || // ZR
                       isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                assert(emitGetInsSC(id) >= -0x100);
                assert(emitGetInsSC(id) < 0x100);
                assert(insOptsNone(id.idInsOpt()) || insOptsIndexed(id.idInsOpt()));
                break;
            }

            case IF_LS_2D: // LS_2D   .Q.............. ....ssnnnnnttttt      Vt Rn
            case IF_LS_2E: // LS_2E   .Q.............. ....ssnnnnnttttt      Vt Rn
            case IF_LS_2F: // LS_2F   .Q.............. xx.Sssnnnnnttttt      Vt[] Rn
            case IF_LS_2G: // LS_2G   .Q.............. xx.Sssnnnnnttttt      Vt[] Rn
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                if (insOptsAnyArrangement(id.idInsOpt()))
                {
                    datasize = id.idOpSize();
                    assert(isValidVectorDatasize(datasize));
                    assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                }
                else
                {
                    elemsize = id.idOpSize();
                    assert(isValidVectorElemsize(elemsize));
                    assert(insOptsNone(id.idInsOpt()) || insOptsPostIndex(id.idInsOpt()));
                }
                assert(!id.idIsLclVar());
                break;
            }

            case IF_LS_3A: // LS_3A   .X.......X.mmmmm oooS..nnnnnttttt      Rt Rn Rm ext(Rm) LSL {}
            {
                assert(isIntegerRegister(id.idReg1()) || // ZR
                       isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                if (id.idIsLclVar())
                {
                    assert(isGeneralRegister(codeGen.rsGetRsvdReg()));
                }
                else
                {
                    assert(isGeneralRegister(id.idReg3()));
                }
                assert(insOptsLSExtend(id.idInsOpt()));
                break;
            }

            case IF_LS_3B: // LS_3B   X............... .aaaaannnnnttttt      Rt Ra Rn
            {
                assert((isValidGeneralDatasize(id.idOpSize()) && isIntegerRegister(id.idReg1())) ||
                       (isValidVectorLSPDatasize(id.idOpSize()) && isVectorRegister(id.idReg1())));
                assert(isIntegerRegister(id.idReg1()) || // ZR
                       isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2()) || // ZR
                       isVectorRegister(id.idReg2()));
                assert(isIntegerRegister(id.idReg3())); // SP
                assert(emitGetInsSC(id) == 0);
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_LS_3C: // LS_3C   X.........iiiiii iaaaaannnnnttttt      Rt Ra Rn imm(im7,sh)
            {
                assert((isValidGeneralDatasize(id.idOpSize()) && isIntegerRegister(id.idReg1())) ||
                       (isValidVectorLSPDatasize(id.idOpSize()) && isVectorRegister(id.idReg1())));
                assert(isIntegerRegister(id.idReg1()) || // ZR
                       isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2()) || // ZR
                       isVectorRegister(id.idReg2()));
                assert(isIntegerRegister(id.idReg3())); // SP
                assert(emitGetInsSC(id) >= -0x40);
                assert(emitGetInsSC(id) < 0x40);
                assert(insOptsNone(id.idInsOpt()) || insOptsIndexed(id.idInsOpt()));
                break;
            }

            case IF_LS_3D: // LS_3D   .X.......X.mmmmm ......nnnnnttttt      Wm Rt Rn
            {
                assert(isIntegerRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2()));
                assert(isIntegerRegister(id.idReg3()));
                assert(emitGetInsSC(id) == 0);
                assert(!id.idIsLclVar());
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_LS_3E: // LS_3E   .X.........mmmmm ......nnnnnttttt      Rm Rt Rn ARMv8.1 LSE Atomics
            {
                assert(isIntegerRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2()));
                assert(isIntegerRegister(id.idReg3()));
                assert(emitGetInsSC(id) == 0);
                assert(!id.idIsLclVar());
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_LS_3F: // LS_3F   .Q.........mmmmm ....ssnnnnnttttt      Vt Rn Rm
            case IF_LS_3G: // LS_3G   .Q.........mmmmm ...Sssnnnnnttttt      Vt[] Rn Rm
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                assert(isGeneralRegister(id.idReg3()));
                if (insOptsAnyArrangement(id.idInsOpt()))
                {
                    datasize = id.idOpSize();
                    assert(isValidVectorDatasize(datasize));
                    assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                }
                else
                {
                    elemsize = id.idOpSize();
                    assert(isValidVectorElemsize(elemsize));
                    assert(insOptsNone(id.idInsOpt()) || insOptsPostIndex(id.idInsOpt()));
                }
                assert(!id.idIsLclVar());
                break;
            }

            case IF_DI_1A: // DI_1A   X.......shiiiiii iiiiiinnnnn.....         Rn    imm(i12,sh)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidUimm(emitGetInsSC(id), 12));
                assert(insOptsNone(id.idInsOpt()) || insOptsLSL12(id.idInsOpt()));
                break;
            }

            case IF_DI_1B: // DI_1B   X........hwiiiii iiiiiiiiiiiddddd      Rd       imm(i16,hw)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidImmHWVal(unchecked((nuint)emitGetInsSC(id)), id.idOpSize()));
                break;
            }

            case IF_DI_1C: // DI_1C   X........Nrrrrrr ssssssnnnnn.....         Rn    imm(N,r,s)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidImmNRS(unchecked((nuint)emitGetInsSC(id)), id.idOpSize()));
                break;
            }

            case IF_DI_1D: // DI_1D   X........Nrrrrrr ssssss.....ddddd      Rd       imm(N,r,s)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isValidImmNRS(unchecked((nuint)emitGetInsSC(id)), id.idOpSize()));
                break;
            }

            case IF_DI_1E: // DI_1E   .ii.....iiiiiiii iiiiiiiiiiiddddd      Rd       simm21
            {
                assert(isGeneralRegister(id.idReg1()));
                break;
            }

            case IF_DI_1F: // DI_1F   X..........iiiii cccc..nnnnn.nzcv      Rn imm5  nzcv cond
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidImmCondFlagsImm5(emitGetInsSC(id)));
                break;
            }

            case IF_DI_2A: // DI_2A   X.......shiiiiii iiiiiinnnnnddddd      Rd Rn    imm(i12,sh)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isIntegerRegister(id.idReg2())); // SP
                assert(isValidUimm(emitGetInsSC(id), 12));
                assert(insOptsNone(id.idInsOpt()) || insOptsLSL12(id.idInsOpt()));
                break;
            }

            case IF_DI_2B: // DI_2B   X.........Xnnnnn ssssssnnnnnddddd      Rd Rn    imm(0-63)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidImmShift(emitGetInsSC(id), id.idOpSize()));
                break;
            }

            case IF_DI_2C: // DI_2C   X........Nrrrrrr ssssssnnnnnddddd      Rd Rn    imm(N,r,s)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidImmNRS(unchecked((nuint)emitGetInsSC(id)), id.idOpSize()));
                break;
            }

            case IF_DI_2D: // DI_2D   X........Nrrrrrr ssssssnnnnnddddd      Rd Rn    imr, imms   (N,r,s)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isValidImmNRS(unchecked((nuint)emitGetInsSC(id)), id.idOpSize()));
                break;
            }

            case IF_DR_1D: // DR_1D   X............... cccc.......ddddd      Rd       cond
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidImmCond(emitGetInsSC(id)));
                break;
            }

            case IF_DR_2A: // DR_2A   X..........mmmmm ......nnnnn.....         Rn Rm
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegisterOrZR(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                break;
            }

            case IF_DR_2B: // DR_2B   X.......sh.mmmmm ssssssnnnnn.....         Rn Rm {LSL,LSR,ASR,ROR} imm(0-63)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // ZR
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidImmShift(emitGetInsSC(id), id.idOpSize()));
                if (!insOptsNone(id.idInsOpt()))
                {
                    if (id.idIns() == INS_tst) // tst allows ROR, cmp/cmn don't
                    {
                        assert(insOptsAnyShift(id.idInsOpt()));
                    }
                    else
                    {
                        assert(insOptsAluShift(id.idInsOpt()));
                    }
                }
                assert(insOptsNone(id.idInsOpt()) || (emitGetInsSC(id) > 0));
                break;
            }

            case IF_DR_2C: // DR_2C   X..........mmmmm ooosssnnnnn.....         Rn Rm ext(Rm) LSL imm(0-4)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isGeneralRegister(id.idReg2()));
                assert(insOptsNone(id.idInsOpt()) || insOptsLSL(id.idInsOpt()) || insOptsAnyExtend(id.idInsOpt()));
                assert(emitGetInsSC(id) >= 0);
                assert(emitGetInsSC(id) <= 4);
                if (insOptsLSL(id.idInsOpt()))
                {
                    assert(emitGetInsSC(id) > 0);
                }
                break;
            }

            case IF_DR_2D: // DR_2D   X..........nnnnn cccc..nnnnnmmmmm      Rd Rn    cond
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isValidImmCond(emitGetInsSC(id)));
                break;
            }

            case IF_DR_2E: // DR_2E   X..........mmmmm ...........ddddd      Rd    Rm
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // ZR
                break;
            }

            case IF_DR_2F: // DR_2F   X.......sh.mmmmm ssssss.....ddddd      Rd    Rm {LSL,LSR,ASR} imm(0-63)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidImmShift(emitGetInsSC(id), id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()) || insOptsAluShift(id.idInsOpt()));
                assert(insOptsNone(id.idInsOpt()) || (emitGetInsSC(id) > 0));
                break;
            }

            case IF_DR_2G: // DR_2G   X............... ......nnnnnddddd      Rd    Rm
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isIntegerRegister(id.idReg2())); // SP
                break;
            }

            case IF_DR_2H: // DR_2H   X........X...... ......nnnnnddddd      Rd Rn
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                break;
            }

            case IF_DR_2I: // DR_2I   X..........mmmmm cccc..nnnnn.nzcv      Rn Rm    nzcv cond
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidImmCondFlags(emitGetInsSC(id)));
                break;
            }

            case IF_DR_3A: // DR_3A   X..........mmmmm ......nnnnnmmmmm      Rd Rn Rm
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isIntegerRegister(id.idReg2())); // SP
                if (id.idIsLclVar())
                {
                    assert(isGeneralRegister(codeGen.rsGetRsvdReg()));
                }
                else
                {
                    assert(isGeneralRegister(id.idReg3()));
                }
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_DR_3B: // DR_3B   X.......sh.mmmmm ssssssnnnnnddddd      Rd Rn Rm {LSL,LSR,ASR,ROR} imm(0-63)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isValidImmShift(emitGetInsSC(id), id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()) || insOptsAnyShift(id.idInsOpt()));
                assert(insOptsNone(id.idInsOpt()) || (emitGetInsSC(id) > 0));
                break;
            }

            case IF_DR_3C: // DR_3C   X..........mmmmm ooosssnnnnnddddd      Rd Rn Rm ext(Rm) LSL imm(0-4)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isIntegerRegister(id.idReg1())); // SP
                assert(isIntegerRegister(id.idReg2())); // SP
                assert(isGeneralRegister(id.idReg3()));
                assert(insOptsNone(id.idInsOpt()) || insOptsLSL(id.idInsOpt()) || insOptsAnyExtend(id.idInsOpt()));
                assert(emitGetInsSC(id) >= 0);
                assert(emitGetInsSC(id) <= 4);
                if (insOptsLSL(id.idInsOpt()))
                {
                    // REG_ZR encodes SP and permits a shift of zero.
                    assert((emitGetInsSC(id) > 0) || (id.idReg2() == REG_ZR));
                }
                break;
            }

            case IF_DR_3D: // DR_3D   X..........mmmmm cccc..nnnnnmmmmm      Rd Rn Rm cond
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isGeneralRegisterOrZR(id.idReg3()));
                assert(isValidImmCond(emitGetInsSC(id)));
                break;
            }

            case IF_DR_3E: // DR_3E   X........X.mmmmm ssssssnnnnnddddd      Rd Rn Rm imm(0-63)
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isValidImmShift(emitGetInsSC(id), id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_DR_4A: // DR_4A   X..........mmmmm .aaaaannnnnddddd      Rd Rn Rm Ra
            {
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                break;
            }

            case IF_DV_1A: // DV_1A   .........X.iiiii iii........ddddd      Vd imm8    (fmov - immediate scalar)
            {
                assert(insOptsNone(id.idInsOpt()));
                elemsize = id.idOpSize();
                assert(isValidVectorElemsizeFloat(elemsize));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidUimm(emitGetInsSC(id), 8));
                break;
            }

            case IF_DV_1B: // DV_1B   .QX..........iii cmod..iiiiiddddd      Vd imm8    (immediate vector)
            {
                ins = id.idIns();
                imm = emitGetInsSC(id) & 0x0ff;
                immShift = unchecked((uint)((emitGetInsSC(id) & 0x700) >> 8));
                assert(immShift >= 0);
                datasize = id.idOpSize();
                assert(isValidVectorDatasize(datasize));
                assert(isValidArrangement(datasize, id.idInsOpt()));
                elemsize = optGetElemsize(id.idInsOpt());
                if (ins == INS_fmov)
                {
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(id.idInsOpt() != INS_OPTS_1D); // Reserved encoding
                    assert(immShift == 0);
                }
                else
                {
                    assert(isValidVectorElemsize(elemsize));
                    assert((immShift != 4) && (immShift != 7)); // always invalid values
                    if (ins != INS_movi) // INS_mvni, INS_orr, INS_bic
                    {
                        assert((elemsize != EA_1BYTE) && (elemsize != EA_8BYTE)); // only H or S
                        if (elemsize == EA_2BYTE)
                        {
                            assert(immShift < 2);
                        }
                        else // (elemsize == EA_4BYTE)
                        {
                            if (ins != INS_mvni)
                            {
                                assert(immShift < 4);
                            }
                        }
                    }
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isValidUimm(imm, 8));
                break;
            }

            case IF_DV_1C: // DV_1C   .........X...... ......nnnnn.....      Vn #0.0    (fcmp - with zero)
            {
                assert(insOptsNone(id.idInsOpt()));
                elemsize = id.idOpSize();
                assert(isValidVectorElemsizeFloat(elemsize));
                assert(isVectorRegister(id.idReg1()));
                break;
            }

            case IF_DV_2A: // DV_2A   .Q.......X...... ......nnnnnddddd      Vd Vn      (fabs, fcvt - vector)
            case IF_DV_2M: // DV_2M   .Q......XX...... ......nnnnnddddd      Vd Vn      (abs, neg   - vector)
            case IF_DV_2P: // DV_2P   ................ ......nnnnnddddd      Vd Vn      (aes*)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2N: // DV_2N   .........iiiiiii ......nnnnnddddd      Vd Vn imm   (shift - scalar)
            {
                ins = id.idIns();
                datasize = id.idOpSize();
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidVectorShiftAmount(emitGetInsSC(id), datasize, emitInsIsVectorRightShift(ins)));
                break;
            }

            case IF_DV_2O: // DV_2O   .Q.......iiiiiii ......nnnnnddddd      Vd Vn imm   (shift - vector)
            {
                ins = id.idIns();
                datasize = id.idOpSize();
                elemsize = optGetElemsize(id.idInsOpt());
                assert(isValidVectorDatasize(datasize));
                assert(isValidArrangement(datasize, id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidVectorShiftAmount(emitGetInsSC(id), elemsize, emitInsIsVectorRightShift(ins)));
                break;
            }

            case IF_DV_2B: // DV_2B   .Q.........iiiii ......nnnnnddddd      Rd Vn[]  (umov/smov    - to general)
            {
                elemsize = id.idOpSize();
                index = emitGetInsSC(id);
                assert(insOptsNone(id.idInsOpt()));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, index));
                assert(isValidVectorElemsize(elemsize));
                assert(isGeneralRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2C: // DV_2C   .Q.........iiiii ......nnnnnddddd      Vd Rn    (dup/ins - vector from general)
            {
                if (id.idIns() == INS_dup)
                {
                    datasize = id.idOpSize();
                    assert(isValidVectorDatasize(datasize));
                    assert(isValidArrangement(datasize, id.idInsOpt()));
                    _ = optGetElemsize(id.idInsOpt());
                }
                else // INS_ins
                {
                    elemsize = id.idOpSize();
                    assert(isValidVectorElemsize(elemsize));
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                break;
            }

            case IF_DV_2D: // DV_2D   .Q.........iiiii ......nnnnnddddd      Vd Vn[]  (dup - vector)
            {
                ins = id.idIns();
                datasize = id.idOpSize();
                assert(isValidVectorDatasize(datasize));
                assert(isValidArrangement(datasize, id.idInsOpt()));
                elemsize = optGetElemsize(id.idInsOpt());
                index = emitGetInsSC(id);
                assert((ins == INS_dup) || isValidVectorIndex(datasize, elemsize, index));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2E: // DV_2E   ...........iiiii ......nnnnnddddd      Vd Vn[]  (dup - scalar)
            {
                elemsize = id.idOpSize();
                index = emitGetInsSC(id);
                assert(isValidVectorIndex(EA_16BYTE, elemsize, index));
                assert(isValidVectorElemsize(elemsize));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2F: // DV_2F   ...........iiiii .jjjj.nnnnnddddd      Vd[] Vn[] (ins - element)
            {
                imm = emitGetInsSC(id);
                index = (imm >> 4) & 0xf;
                index2 = imm & 0xf;
                elemsize = id.idOpSize();
                assert(isValidVectorElemsize(elemsize));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, index));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, index2));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2L: // DV_2L   ........XX...... ......nnnnnddddd      Vd Vn      (abs, neg - scalar)
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isValidVectorElemsize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2G: // DV_2G   .........X...... ......nnnnnddddd      Vd Vn      (fmov, fcvtXX - register)
            case IF_DV_2K: // DV_2K   .........X.mmmmm ......nnnnn.....      Vn Vm      (fcmp)
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isValidVectorElemsizeFloat(id.idOpSize()) || (id.idOpSize() == EA_2BYTE));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2H: // DV_2H   X........X...... ......nnnnnddddd      Rd Vn      (fmov/fcvtXX - to general)
            {
                assert(insOptsConvertFloatToInt(id.idInsOpt()));
                dstsize = optGetDstsize(id.idInsOpt());
                srcsize = optGetSrcsize(id.idInsOpt());
                assert(isValidGeneralDatasize(dstsize));
                assert(isValidVectorElemsizeFloat(srcsize) || (srcsize == EA_2BYTE));
                assert(dstsize == id.idOpSize());
                assert(isGeneralRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2I: // DV_2I   X........X...... ......nnnnnddddd      Vd Rn      (fmov/Xcvtf - from general)
            {
                assert(insOptsConvertIntToFloat(id.idInsOpt()));
                dstsize = optGetDstsize(id.idInsOpt());
                srcsize = optGetSrcsize(id.idInsOpt());
                assert(isValidGeneralDatasize(srcsize));
                assert(isValidVectorElemsizeFloat(dstsize) || (dstsize == EA_2BYTE));
                assert(dstsize == id.idOpSize());
                assert(isVectorRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                break;
            }

            case IF_DV_2J: // DV_2J   ........SS.....D D.....nnnnnddddd      Vd Vn      (fcvt)
            {
                assert(insOptsConvertFloatToFloat(id.idInsOpt()));
                dstsize = optGetDstsize(id.idInsOpt());
                srcsize = optGetSrcsize(id.idInsOpt());
                assert(isValidVectorFcvtsize(srcsize));
                assert(isValidVectorFcvtsize(dstsize));
                assert(dstsize == id.idOpSize());
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2Q: // DV_2Q   .........X...... ......nnnnnddddd      Sd Vn      (faddp, fmaxnmp, fmaxp, fminnmp,
                           // fminp - scalar)
            {
                if (id.idOpSize() == EA_16BYTE)
                {
                    assert(id.idInsOpt() == INS_OPTS_2D);
                }
                else
                {
                    assert(id.idOpSize() == EA_8BYTE);
                    assert(id.idInsOpt() == INS_OPTS_2S);
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2R: // DV_2R   .Q.......X...... ......nnnnnddddd      Sd Vn      (fmaxnmv, fmaxv, fminnmv, fminv)
            {
                assert(id.idOpSize() == EA_16BYTE);
                assert(id.idInsOpt() == INS_OPTS_4S);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2S: // DV_2S   ........XX...... ......nnnnnddddd      Sd Vn      (addp - scalar)
            {
                assert(id.idOpSize() == EA_16BYTE);
                assert(id.idInsOpt() == INS_OPTS_2D);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2T: // DV_2T   .Q......XX...... ......nnnnnddddd      Sd Vn      (addv, saddlv, smaxv, sminv, uaddlv,
                           // umaxv, uminv)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_2U: // DV_2U   ................ ......nnnnnddddd      Vd Vn    (sha)
            case IF_DV_2V: // DV_2V   ................ ......nnnnnddddd      Vd Vn      (vector)
            {
                if (id.idIns() == INS_sha1h)
                {
                    assert(id.idOpSize() == EA_4BYTE);
                }
                else
                {
                    assert(isValidVectorDatasize(id.idOpSize()));
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_DV_3A: // DV_3A   .Q......XX.mmmmm ......nnnnnddddd      Vd Vn Vm   (vector)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                elemsize = optGetElemsize(id.idInsOpt());
                ins = id.idIns();
                if (ins == INS_mul)
                {
                    assert(elemsize != EA_8BYTE); // can't use 2D or 1D
                }
                else if (ins == INS_pmul)
                {
                    assert(elemsize == EA_1BYTE); // only supports 8B or 16B
                }
                break;
            }

            case IF_DV_3AI: // DV_3AI  .Q......XXLMmmmm ....H.nnnnnddddd      Vd Vn Vm[] (vector by element)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                elemsize = optGetElemsize(id.idInsOpt());
                assert(isValidVectorIndex(EA_16BYTE, elemsize, emitGetInsSC(id)));
                // Only has encodings for H or S elemsize
                assert((elemsize == EA_2BYTE) || (elemsize == EA_4BYTE));
                break;
            }

            case IF_DV_3B: // DV_3B   .Q.......X.mmmmm ......nnnnnddddd      Vd Vn Vm   (vector)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_DV_3BI: // DV_3BI  .Q.......XLmmmmm ....H.nnnnnddddd      Vd Vn Vm[] (vector by element)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                elemsize = optGetElemsize(id.idInsOpt());
                assert(isValidVectorIndex(EA_16BYTE, elemsize, emitGetInsSC(id)));
                break;
            }

            case IF_DV_3C: // DV_3C   .Q.........mmmmm ......nnnnnddddd      Vd Vn Vm   (vector)
            {
                switch (id.idIns())
                {
                    case INS_tbl:
                    case INS_tbl_2regs:
                    case INS_tbl_3regs:
                    case INS_tbl_4regs:
                    case INS_tbx:
                    case INS_tbx_2regs:
                    case INS_tbx_3regs:
                    case INS_tbx_4regs:
                    {
                        elemsize = optGetElemsize(id.idInsOpt());
                        assert(elemsize == EA_1BYTE);
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_DV_3D: // DV_3D   .........X.mmmmm ......nnnnnddddd      Vd Vn Vm   (scalar)
            {
                assert(isValidScalarDatasize(id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_DV_3DI: // DV_3DI .........XLmmmmm ....H.nnnnnddddd      Vd Vn Vm[] (scalar by element)
            {
                assert(isValidScalarDatasize(id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                elemsize = id.idOpSize();
                assert(isValidVectorIndex(EA_16BYTE, elemsize, emitGetInsSC(id)));
                break;
            }

            case IF_DV_3E: // DV_3E   ........XX.mmmmm ......nnnnnddddd      Vd Vn Vm  (scalar)
            {
                assert(isValidVectorElemsize(id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                elemsize = id.idOpSize();
                index = emitGetInsSC(id);
                assert(isValidVectorIndex(EA_16BYTE, elemsize, index));
                break;
            }

            case IF_DV_3EI: // DV_3EI ........XXLMmmmm ....H.nnnnnddddd      Vd Vn Vm[] (scalar by element)
            {
                assert(isValidVectorElemsize(id.idOpSize()));
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                elemsize = id.idOpSize();
                index = emitGetInsSC(id);
                assert(isValidVectorIndex(EA_16BYTE, elemsize, index));
                break;
            }

            case IF_DV_3F: // DV_3F   ...........mmmmm ......nnnnnddddd      Vd Vn Vm
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_DV_3G: // DV_3G   .Q.........mmmmm .iiii.nnnnnddddd      Vd Vn Vm imm (vector)
            {
                assert(isValidVectorDatasize(id.idOpSize()));
                assert(isValidArrangement(id.idOpSize(), id.idInsOpt()));
                assert(isValidVectorIndex(id.idOpSize(), EA_1BYTE, emitGetInsSC(id)));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_DV_3H: // DV_3H  ...........mmmmm .O....nnnnnddddd      Vd Vn Vm     (vector)
            {
                assert(id.idOpSize() == EA_16BYTE);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_DV_3I: // DV_3I  ...........mmmmm iiiiiinnnnnddddd      Vd Vn Vm imm6 (vector)
            {
                assert(id.idOpSize() == EA_16BYTE);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 6));
                break;
            }

            case IF_DV_4A: // DV_4A   .........X.mmmmm .aaaaannnnnddddd      Rd Rn Rm Ra (scalar)
            {
                assert(isValidScalarDatasize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                break;
            }

            case IF_DV_4B: // DV_4B   ...........mmmmm .aaaaannnnnddddd      Vd Vn Vm Va (vector)
            {
                assert(id.idOpSize() == EA_16BYTE);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                break;
            }

            case IF_PC_1A: // PC_1A   ................ ...........ddddd      Rd
            {
                assert(id.idOpSize() == EA_8BYTE);
                assert(isGeneralRegister(id.idReg1()));
                break;
            }

            case IF_PC_2A: // PC_2A   X........X...... ......nnnnnddddd      Rd Rn
            {
                assert(id.idOpSize() == EA_8BYTE);
                assert(isGeneralRegister(id.idReg1()));
                assert(isIntegerRegister(id.idReg2())); // SP
                break;
            }

            case IF_PC_0A: // PC_0A   ................ ................
            case IF_SN_0A: // SN_0A   ................ ................
            case IF_SI_0A: // SI_0A   ...........iiiii iiiiiiiiiii.....               imm16
            case IF_SI_0B: // SI_0B   ................ ....bbbb........               imm4 - barrier
            {
                break;
            }

            case IF_SR_1A: // SR_1A   ................ ...........ttttt      Rt       (dc zva, mrs)
            {
                datasize = id.idOpSize();
                assert(isGeneralRegister(id.idReg1()));
                assert(datasize == EA_8BYTE);
                break;
            }

            default:
            {
                // fallback to check SVE instructions.
                emitInsSveSanityCheck(id);
                break;
            }
        }
    }
}
#endif
