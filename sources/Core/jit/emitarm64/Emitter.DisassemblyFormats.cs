// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Diagnostics;
using System.Globalization;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitDispInsHelp(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset, byte* pCode, nuint sz, insGroup? ig)
    {
        assert(_compiler is not null);
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);

#if DEBUG
        if (_compiler.verbose)
        {
            var idNum = debugInfo.idNum; // Do not remove this!  It is needed for VisualStudio conditional breakpoints

            jitprintf($"IN{idNum:x4}: ");
        }
#endif

        if (pCode == null)
        {
            sz = 0;
        }

        if (!isNew && !asmfm && (sz != 0))
        {
            doffs = true;
        }

        /* Display the instruction address */

        emitDispInsAddr(pCode);

        /* Display the instruction offset */

        emitDispInsOffs(offset, doffs);

        byte* pCodeRW = null;
        if (pCode != null)
        {
            /* Display the instruction hex code */
            assert(((pCode >= emitCodeBlock) && (pCode < emitCodeBlock + emitTotalHotCodeSize)) ||
                   ((pCode >= emitColdCodeBlock) && (pCode < emitColdCodeBlock + emitTotalColdCodeSize)));

            pCodeRW = unchecked(pCode + writeableOffset);
        }

        emitDispInsHex(id, pCodeRW, sz);

        jitprintf("      ");

        /* Get the instruction and format */

        var ins = id.idIns();
        var fmt = id.idInsFmt();

        emitDispInst(ins);

        /* If this instruction has just been added, check its size */

        assert(!isNew || ((nuint)emitSizeOfInsDsc(id) == unchecked(emitCurIGfreeNext - id.StorageOffset)));

        /* Figure out the operand size */
        var size = id.idOpSize();
        var attr = size;
        if (id.idGCref() == GCT_GCREF)
        {
            attr = EA_GCREF;
        }
        else if (id.idGCref() == GCT_BYREF)
        {
            attr = EA_BYREF;
        }

        nint imm;
        int dataOffset;
        bitMaskImm bmi = default;
        halfwordImm hwi = default;
        condFlagsImm cfi = default;
        uint scale;
        uint immShift;
        bool hasShift;
        string? methodName;
        emitAttr elemsize;
        emitAttr datasize;
        emitAttr srcsize;
        emitAttr dstsize;
        nint index;
        nint index2;
        uint registerListSize;
        string? targetName;

        switch (fmt)
        {
            case IF_BI_0A: // BI_0A   ......iiiiiiiiii iiiiiiiiiiiiiiii               simm26:00
            case IF_BI_0B: // BI_0B   ......iiiiiiiiii iiiiiiiiiii.....               simm19:00
            case IF_LARGEJMP:
            {
                if (fmt == IF_LARGEJMP)
                {
                    jitprintf("(LARGEJMP)");
                }
                if (id.idAddr().iiaHasInstrCount())
                {
                    var instrCount = id.idAddr().iiaGetInstrCount();

                    if (ig is null)
                    {
                        jitprintf($"pc{(instrCount >= 0 ? "+" : "")}" +
                            $"{instrCount.ToString(CultureInfo.InvariantCulture)} instructions");
                    }
                    else
                    {
                        var insNum = emitFindInsNum(ig, id);
                        var srcOffs = unchecked(ig.igOffs + emitFindOffset(ig, insNum + 1));
                        var dstOffs = unchecked(ig.igOffs + emitFindOffset(ig, unchecked(insNum + 1 + (uint)instrCount)));
                        var relOffs = (nint)(emitOffsetToPtr(dstOffs) - emitOffsetToPtr(srcOffs));
                        jitprintf($"pc{(relOffs >= 0 ? "+" : "")}" +
                            $"{unchecked((int)relOffs).ToString(CultureInfo.InvariantCulture)} " +
                            $"({instrCount.ToString(CultureInfo.InvariantCulture)} instructions)");
                    }
                }
                else if (id.idIsBound())
                {
                    var target = ((instrDescJmp)id).idjTargetIG;
                    assert(target is not null);
                    emitPrintLabel(target);
                }
                else
                {
                    var target = ((instrDescJmp)id).idjTarget;
                    assert(target is not null);
                    jitprintf($"L_M{_compiler.compMethodID:D3}_{FMT_BB(target.bbNum)}");
                }
                break;
            }

            case IF_BI_0C: // BI_0C   ......iiiiiiiiii iiiiiiiiiiiiiiii               simm26:00
            {
                methodName = _compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debugInfo.idMemCookie);
                jitprintf($"{methodName}");
                break;
            }

            case IF_BI_1A: // BI_1A   ......iiiiiiiiii iiiiiiiiiiittttt      Rt       simm19:00
            case IF_BI_1B: // BI_1B   B.......bbbbbiii iiiiiiiiiiittttt      Rt imm6, simm14:00
            {
                assert(insOptsNone(id.idInsOpt()));
                emitDispReg(id.idReg1(), size, true);

                if (fmt == IF_BI_1B)
                {
                    emitDispImm(emitGetInsSC(id), true);
                }

                if (id.idAddr().iiaHasInstrCount())
                {
                    var instrCount = id.idAddr().iiaGetInstrCount();

                    if (ig is null)
                    {
                        jitprintf($"pc{(instrCount >= 0 ? "+" : "")}" +
                            $"{instrCount.ToString(CultureInfo.InvariantCulture)} instructions");
                    }
                    else
                    {
                        var insNum = emitFindInsNum(ig, id);
                        var srcOffs = unchecked(ig.igOffs + emitFindOffset(ig, insNum + 1));
                        var dstOffs = unchecked(ig.igOffs + emitFindOffset(ig, unchecked(insNum + 1 + (uint)instrCount)));
                        var relOffs = (nint)(emitOffsetToPtr(dstOffs) - emitOffsetToPtr(srcOffs));
                        jitprintf($"pc{(relOffs >= 0 ? "+" : "")}" +
                            $"{unchecked((int)relOffs).ToString(CultureInfo.InvariantCulture)} " +
                            $"({instrCount.ToString(CultureInfo.InvariantCulture)} instructions)");
                    }
                }
                else if (id.idIsBound())
                {
                    var target = ((instrDescJmp)id).idjTargetIG;
                    assert(target is not null);
                    emitPrintLabel(target);
                }
                else
                {
                    var target = ((instrDescJmp)id).idjTarget;
                    assert(target is not null);
                    jitprintf($"L_M{_compiler.compMethodID:D3}_{FMT_BB(target.bbNum)}");
                }
                break;
            }

            case IF_BR_0A: // BR_0A   ................ ................
            {
                assert(insOptsNone(id.idInsOpt()));
                break;
            }

            case IF_BR_1A: // BR_1A   ................ ......nnnnn.....         Rn
            {
                assert(insOptsNone(id.idInsOpt()));
                emitDispReg(id.idReg1(), size, false);
                break;
            }

            case IF_BR_1B: // BR_1B   ................ ......nnnnn.....         Rn
                // The size of a branch target is always EA_PTRSIZE
            {
                assert(insOptsNone(id.idInsOpt()));

                if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && id.idIsTlsGD())
                {
                    emitDispReg(REG_R2, EA_PTRSIZE, false);
                }
                else
                {
                    emitDispReg(id.idReg3(), EA_PTRSIZE, false);
                }

                emitDispCommentForHandle(0, debugInfo.idMemCookie, GTF_ICON_FTN_ADDR);
                break;
            }

            case IF_LS_1A: // LS_1A   XX...V..iiiiiiii iiiiiiiiiiittttt      Rt    PC imm(1MB)
            case IF_DI_1E: // DI_1E   .ii.....iiiiiiii iiiiiiiiiiiddddd      Rd       simm21
            case IF_LARGELDC:
            case IF_LARGEADR:
            {
                assert(insOptsNone(id.idInsOpt()));
                emitDispReg(id.idReg1(), size, true);
                imm = emitGetInsSC(id);
                targetName = null;

                /* Is this actually a reference to a data section? */
                if (fmt == IF_LARGEADR)
                {
                    jitprintf("(LARGEADR)");
                }
                else if (fmt == IF_LARGELDC)
                {
                    jitprintf("(LARGELDC)");
                }

                jitprintf("[");
                if (id.idAddr().iiaIsJitDataOffset())
                {
                    dataOffset = Compiler.eeGetJitDataOffs(id.idAddr().iiaFieldHnd);
                    /* Display a data section reference */

                    if ((dataOffset & 1) != 0)
                    {
                        jitprintf($"@CNS{dataOffset - 1:D2}");
                    }
                    else
                    {
                        jitprintf($"@RWD{dataOffset:D2}");
                    }

                    if (imm != 0)
                    {
                        jitprintf(imm.ToString("+0;-0;+0", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    assert(imm == 0);
                    if (id.idIsReloc())
                    {
                        jitprintf("HIGH RELOC ");
                        emitDispImm((nint)id.idAddr().iiaAddr, false);
                        var targetHandle = debugInfo.idMemCookie;

#if DEBUG
                        if (targetHandle == (nint)TargetHandleType.THT_InitializeArrayIntrinsics)
                        {
                            targetName = "InitializeArrayIntrinsics";
                        }
                        else if (targetHandle == (nint)TargetHandleType.THT_GSCookieCheck)
                        {
                            targetName = "GlobalSecurityCookieCheck";
                        }
                        else if (targetHandle == (nint)TargetHandleType.THT_SetGSCookie)
                        {
                            targetName = "SetGlobalSecurityCookie";
                        }
#endif
                    }
                    else if (id.idIsBound())
                    {
                        var target = ((instrDescJmp)id).idjTargetIG;
                    assert(target is not null);
                    emitPrintLabel(target);
                    }
                    else
                    {
                        var target = ((instrDescJmp)id).idjTarget;
                    assert(target is not null);
                    jitprintf($"L_M{_compiler.compMethodID:D3}_{FMT_BB(target.bbNum)}");
                    }
                }
                jitprintf("]");
                if (targetName is not null)
                {
                    jitprintf($"      // [{targetName}]");
                }
                else
                {
                    emitDispCommentForHandle(debugInfo.idMemCookie, 0, debugInfo.idFlags);
                }
                break;
            }

            case IF_LS_2A: // LS_2A   .X.......X...... ......nnnnnttttt      Rt Rn
            {
                assert(insOptsNone(id.idInsOpt()));
                assert((emitGetInsSC(id) == 0) || id.idIsTlsGD() || id.idIsReloc());
                emitDispReg(id.idReg1(), emitInsTargetRegSize(id), true);
                if (id.idIsReloc() && !id.idIsTlsGD())
                {
                    // "ldr Rt,[Rn,#:lo12:sym]" with the page offset folded in from a preceding adrp/add pair.
                    jitprintf("[");
                    emitDispReg(id.idReg2(), EA_PTRSIZE, true);
                    jitprintf("[LOW RELOC ");
                    emitDispImm((nint)emitGetInsSC(id), false);
                    jitprintf("]]");
                }
                else
                {
                    emitDispAddrRI(id.idReg2(), id.idInsOpt(), 0);
                }
                break;
            }

            case IF_LS_2B: // LS_2B   .X.......Xiiiiii iiiiiinnnnnttttt      Rt Rn    imm(0-4095)
            {
                assert(insOptsNone(id.idInsOpt()));
                imm = emitGetInsSC(id);
                scale = NaturalScale_helper(emitInsLoadStoreSize(id));
                imm <<= (int)scale; // The immediate is scaled by the size of the ld/st
                emitDispReg(id.idReg1(), emitInsTargetRegSize(id), true);
                emitDispAddrRI(id.idReg2(), id.idInsOpt(), imm);
                break;
            }

            case IF_LS_2C: // LS_2C   .X.......X.iiiii iiiiPPnnnnnttttt      Rt Rn    imm(-256..+255) no/pre/post inc
            {
                assert(insOptsNone(id.idInsOpt()) || insOptsIndexed(id.idInsOpt()));
                imm = emitGetInsSC(id);
                emitDispReg(id.idReg1(), emitInsTargetRegSize(id), true);
                emitDispAddrRI(id.idReg2(), id.idInsOpt(), imm);
                break;
            }

            case IF_LS_2D: // LS_2D   .Q.............. ....ssnnnnnttttt      Vt Rn
            case IF_LS_2E: // LS_2E   .Q.............. ....ssnnnnnttttt      Vt Rn
            {
                registerListSize = insGetRegisterListSize(id.idIns());
                emitDispVectorRegList(id.idReg1(), registerListSize, id.idInsOpt(), true);

                if (fmt == IF_LS_2D)
                {
                    // Load/Store multiple structures       base register
                    // Load single structure and replicate  base register
                    emitDispAddrRI(id.idReg2(), INS_OPTS_NONE, 0);
                }
                else
                {
                    // Load/Store multiple structures       post-indexed by an immediate
                    // Load single structure and replicate  post-indexed by an immediate
                    emitDispAddrRI(id.idReg2(), INS_OPTS_POST_INDEX, id.idSmallCns());
                }
                break;
            }

            case IF_LS_2F: // LS_2F   .Q.............. xx.Sssnnnnnttttt      Vt[] Rn
            case IF_LS_2G: // LS_2G   .Q.............. xx.Sssnnnnnttttt      Vt[] Rn
            {
                registerListSize = insGetRegisterListSize(id.idIns());
                elemsize = id.idOpSize();
                emitDispVectorElemList(id.idReg1(), registerListSize, elemsize, unchecked((uint)id.idSmallCns()), true);

                if (fmt == IF_LS_2F)
                {
                    // Load/Store single structure  base register
                    emitDispAddrRI(id.idReg2(), INS_OPTS_NONE, 0);
                }
                else
                {
                    // Load/Store single structure  post-indexed by an immediate
                    emitDispAddrRI(id.idReg2(), INS_OPTS_POST_INDEX, unchecked((nint)(registerListSize * (uint)elemsize)));
                }
                break;
            }

            case IF_LS_3A: // LS_3A   .X.......X.mmmmm oooS..nnnnnttttt      Rt Rn Rm ext(Rm) LSL {}
            {
                assert(insOptsLSExtend(id.idInsOpt()));
                emitDispReg(id.idReg1(), emitInsTargetRegSize(id), true);
                if (id.idIsLclVar())
                {
                    emitDispAddrRRExt(id.idReg2(), codeGen.rsGetRsvdReg(), id.idInsOpt(), false, size);
                }
                else
                {
                    emitDispAddrRRExt(id.idReg2(), id.idReg3(), id.idInsOpt(), id.idReg3Scaled(), size);
                }
                break;
            }

            case IF_LS_3B: // LS_3B   X............... .aaaaannnnnddddd      Rt Ra Rn
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(emitGetInsSC(id) == 0);
                emitDispReg(id.idReg1(), emitInsTargetRegSize(id), true);
                emitDispReg(id.idReg2(), emitInsTargetRegSize(id), true);
                emitDispAddrRI(id.idReg3(), id.idInsOpt(), 0);
                break;
            }

            case IF_LS_3C: // LS_3C   X.........iiiiii iaaaaannnnnddddd      Rt Ra Rn imm(im7,sh)
            {
                assert(insOptsNone(id.idInsOpt()) || insOptsIndexed(id.idInsOpt()));
                imm = emitGetInsSC(id);
                scale = NaturalScale_helper(emitInsLoadStoreSize(id));
                imm <<= (int)scale;
                emitDispReg(id.idReg1(), emitInsTargetRegSize(id), true);
                emitDispReg(id.idReg2(), emitInsTargetRegSize(id), true);
                emitDispAddrRI(id.idReg3(), id.idInsOpt(), imm);
                break;
            }

            case IF_LS_3D: // LS_3D   .X.......X.mmmmm ......nnnnnttttt      Wm Rt Rn
            {
                assert(insOptsNone(id.idInsOpt()));
                emitDispReg(id.idReg1(), EA_4BYTE, true);
                emitDispReg(id.idReg2(), emitInsTargetRegSize(id), true);
                emitDispAddrRI(id.idReg3(), id.idInsOpt(), 0);
                break;
            }

            case IF_LS_3E: // LS_3E   .X.........mmmmm ......nnnnnttttt      Rm Rt Rn ARMv8.1 LSE Atomics
            {
                assert(insOptsNone(id.idInsOpt()));
                assert((EA_SIZE(size) == EA_4BYTE) || (EA_SIZE(size) == EA_8BYTE));
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispAddrRI(id.idReg3(), id.idInsOpt(), 0);
                break;
            }

            case IF_LS_3F: // LS_3F   .Q.........mmmmm ....ssnnnnnttttt      Vt Rn Rm
            case IF_LS_3G: // LS_3G   .Q.........mmmmm ...Sssnnnnnttttt      Vt[] Rn Rm
            {
                registerListSize = insGetRegisterListSize(id.idIns());

                if (fmt == IF_LS_3F)
                {
                    // Load/Store multiple structures       post-indexed by a register
                    // Load single structure and replicate  post-indexed by a register
                    emitDispVectorRegList(id.idReg1(), registerListSize, id.idInsOpt(), true);
                }
                else
                {
                    // Load/Store single structure          post-indexed by a register
                    elemsize = id.idOpSize();
                    emitDispVectorElemList(id.idReg1(), registerListSize, elemsize, unchecked((uint)id.idSmallCns()), true);
                }

                jitprintf("[");
                emitDispReg(encodingZRtoSP(id.idReg2()), EA_8BYTE, false);
                jitprintf("], ");
                emitDispReg(id.idReg3(), EA_8BYTE, false);
                break;
            }

            case IF_DI_1A: // DI_1A   X.......shiiiiii iiiiiinnnnn.....      Rn       imm(i12,sh)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispImmOptsLSL(emitGetInsSC(id), insOptsLSL12(id.idInsOpt()), 12);
                emitDispCommentForHandle(0, debugInfo.idMemCookie, debugInfo.idFlags);
                break;
            }

            case IF_DI_1B: // DI_1B   X........hwiiiii iiiiiiiiiiiddddd      Rd       imm(i16,hw)
            {
                emitDispReg(id.idReg1(), size, true);
                hwi.immHWVal = unchecked((uint)emitGetInsSC(id));
                if (ins == INS_mov)
                {
                    emitDispImm(emitDecodeHalfwordImm(hwi, size), false);
                }
                else // movz, movn, movk
                {
                    emitDispImm(hwi.immVal, false);
                    if (hwi.immHW != 0)
                    {
                        emitDispShiftOpts(INS_OPTS_LSL);
                        emitDispImm(hwi.immHW * 16, false);
                    }
                }
                emitDispCommentForHandle(0, debugInfo.idMemCookie, debugInfo.idFlags);
                break;
            }

            case IF_DI_1C: // DI_1C   X........Nrrrrrr ssssssnnnnn.....         Rn    imm(N,r,s)
            {
                emitDispReg(id.idReg1(), size, true);
                bmi.immNRS = unchecked((uint)emitGetInsSC(id));
                emitDispImm(emitDecodeBitMaskImm(bmi, size), false);
                emitDispCommentForHandle(0, debugInfo.idMemCookie, debugInfo.idFlags);
                break;
            }

            case IF_DI_1D: // DI_1D   X........Nrrrrrr ssssss.....ddddd      Rd       imm(N,r,s)
            {
                emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                bmi.immNRS = unchecked((uint)emitGetInsSC(id));
                emitDispImm(emitDecodeBitMaskImm(bmi, size), false);
                emitDispCommentForHandle(0, debugInfo.idMemCookie, debugInfo.idFlags);
                break;
            }

            case IF_DI_2A: // DI_2A   X.......shiiiiii iiiiiinnnnnddddd      Rd Rn    imm(i12,sh)
            {
                if ((ins == INS_add) || (ins == INS_sub))
                {
                    emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                    emitDispReg(encodingZRtoSP(id.idReg2()), size, true);
                }
                else
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                }
                if (id.idIsReloc())
                {
                    assert(ins == INS_add);

                    if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && TargetOS.IsWindows && id.idIsTlsGD())
                    {
                        jitprintf("[HIGH RELOC ");
                    }
                    else
                    {
                        jitprintf("[LOW RELOC ");
                    }

                    emitDispImm((nint)id.idAddr().iiaAddr, false);
                    jitprintf("]");
                }
                else
                {
                    if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && TargetOS.IsWindows && id.idIsTlsGD())
                    {
                        assert(ins == INS_add);
                        jitprintf("[LOW RELOC ");
                        emitDispImm((nint)id.idAddr().iiaAddr, false);
                        jitprintf("]");
                    }
                    else
                    {
                        emitDispImmOptsLSL(emitGetInsSC(id), insOptsLSL12(id.idInsOpt()), 12);
                    }
                }
                break;
            }

            case IF_DI_2B: // DI_2B   X........X.nnnnn ssssssnnnnnddddd      Rd Rn    imm(0-63)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispImm(emitGetInsSC(id), false);
                break;
            }

            case IF_DI_2C: // DI_2C   X........Nrrrrrr ssssssnnnnnddddd      Rd Rn    imm(N,r,s)
            {
                if (ins == INS_ands)
                {
                    emitDispReg(id.idReg1(), size, true);
                }
                else
                {
                    emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                }
                emitDispReg(id.idReg2(), size, true);
                bmi.immNRS = unchecked((uint)emitGetInsSC(id));
                emitDispImm(emitDecodeBitMaskImm(bmi, size), false);
                break;
            }

            case IF_DI_2D: // DI_2D   X........Nrrrrrr ssssssnnnnnddddd      Rd Rn    imr, ims   (N,r,s)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);

                imm = emitGetInsSC(id);
                bmi.immNRS = unchecked((uint)imm);

                switch (ins)
                {
                    case INS_bfm:
                    case INS_sbfm:
                    case INS_ubfm:
                    {
                        emitDispImm(bmi.immR, true);
                        emitDispImm(bmi.immS, false);
                        break;
                    }

                    case INS_bfi:
                    case INS_sbfiz:
                    case INS_ubfiz:
                    {
                        emitDispImm(getBitWidth(size) - bmi.immR, true);
                        emitDispImm(bmi.immS + 1, false);
                        break;
                    }

                    case INS_bfxil:
                    case INS_sbfx:
                    case INS_ubfx:
                    {
                        emitDispImm(bmi.immR, true);
                        emitDispImm(unchecked(bmi.immS - bmi.immR + 1), false);
                        break;
                    }

                    case INS_asr:
                    case INS_lsr:
                    case INS_lsl:
                    {
                        emitDispImm(imm, false);
                        break;
                    }

                    default:
                    {
                        assert(false, "Unexpected instruction in IF_DI_2D");
                        break;
                    }
                }

                break;
            }

            case IF_DI_1F: // DI_1F   X..........iiiii cccc..nnnnn.nzcv      Rn imm5  nzcv cond
            {
                emitDispReg(id.idReg1(), size, true);
                cfi.immCFVal = unchecked((uint)emitGetInsSC(id));
                emitDispImm(cfi.imm5, true);
                emitDispFlags(cfi.flags);
                emitDispComma();
                emitDispCond(cfi.cond);
                break;
            }

            case IF_DR_1D: // DR_1D   X............... cccc.......mmmmm      Rd       cond
            {
                emitDispReg(id.idReg1(), size, true);
                cfi.immCFVal = unchecked((uint)emitGetInsSC(id));
                emitDispCond(cfi.cond);
                break;
            }

            case IF_DR_2A: // DR_2A   X..........mmmmm ......nnnnn.....         Rn Rm
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, false);
                break;
            }

            case IF_DR_2B: // DR_2B   X.......sh.mmmmm ssssssnnnnn.....         Rn Rm {LSL,LSR,ASR,ROR} imm(0-63)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispShiftedReg(id.idReg2(), id.idInsOpt(), emitGetInsSC(id), size);
                break;
            }

            case IF_DR_2C: // DR_2C   X..........mmmmm ooosssnnnnn.....         Rn Rm ext(Rm) LSL imm(0-4)
            {
                emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                imm = emitGetInsSC(id);
                emitDispExtendReg(id.idReg2(), id.idInsOpt(), imm);
                break;
            }

            case IF_DR_2D: // DR_2D   X..........nnnnn cccc..nnnnnddddd      Rd Rn    cond
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                cfi.immCFVal = unchecked((uint)emitGetInsSC(id));
                emitDispCond(cfi.cond);
                break;
            }

            case IF_DR_2E: // DR_2E   X..........mmmmm ...........ddddd      Rd    Rm
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, false);
                break;
            }

            case IF_DV_2U: // DV_2U   ................ ......nnnnnddddd      Vd    Vn
            {
                if (ins == INS_sha1h)
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, false);
                }
                else
                {
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                break;
            }

            case IF_DV_2V: // DV_2V   ................ ......nnnnnddddd      Vd Vn      (vector)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                break;
            }

            case IF_DR_2F: // DR_2F   X.......sh.mmmmm ssssss.....ddddd      Rd    Rm {LSL,LSR,ASR} imm(0-63)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispShiftedReg(id.idReg2(), id.idInsOpt(), emitGetInsSC(id), size);
                break;
            }

            case IF_DR_2G: // DR_2G   X............... ......nnnnnddddd      Rd Rn
            {
                emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                emitDispReg(encodingZRtoSP(id.idReg2()), size, false);
                break;
            }

            case IF_DR_2H: // DR_2H   X........X...... ......nnnnnddddd      Rd Rn
            {
                if ((ins == INS_uxtb) || (ins == INS_uxth))
                {
                    // There is no 64-bit variant of uxtb and uxth
                    // However, we allow idOpSize() to have EA_8BYTE value for these instruction
                    emitDispReg(id.idReg1(), EA_4BYTE, true);
                    emitDispReg(id.idReg2(), EA_4BYTE, false);
                }
                else
                {
                    emitDispReg(id.idReg1(), size, true);
                    // sxtb, sxth and sxtb always operate on 32-bit source register
                    emitDispReg(id.idReg2(), EA_4BYTE, false);
                }
                break;
            }

            case IF_DR_2I: // DR_2I   X..........mmmmm cccc..nnnnn.nzcv      Rn Rm    nzcv cond
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                cfi.immCFVal = unchecked((uint)emitGetInsSC(id));
                emitDispFlags(cfi.flags);
                emitDispComma();
                emitDispCond(cfi.cond);
                break;
            }

            case IF_DR_3A: // DR_3A   X..........mmmmm ......nnnnnmmmmm      Rd Rn Rm
            {
                var reg3 = id.idIsLclVar() ? codeGen.rsGetRsvdReg() : id.idReg3();

                if ((ins == INS_add) || (ins == INS_sub))
                {
                    emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                    emitDispReg(encodingZRtoSP(id.idReg2()), size, true);
                    emitDispReg(reg3, size, false);
                }
                else if ((ins == INS_smulh) || (ins == INS_umulh))
                {
                    size = EA_8BYTE;
                    // smulh Xd, Xn, Xm
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                    emitDispReg(reg3, size, false);
                }
                else if ((ins == INS_smull) || (ins == INS_umull) || (ins == INS_smnegl) || (ins == INS_umnegl))
                {
                    // smull Xd, Wn, Wm
                    emitDispReg(id.idReg1(), EA_8BYTE, true);
                    emitDispReg(id.idReg2(), EA_4BYTE, true);
                    emitDispReg(reg3, EA_4BYTE, false);
                }
                else
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                    emitDispReg(reg3, size, false);
                }
                break;
            }

            case IF_DR_3B: // DR_3B   X.......sh.mmmmm ssssssnnnnnddddd      Rd Rn Rm {LSL,LSR,ASR} imm(0-63)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispShiftedReg(id.idReg3(), id.idInsOpt(), emitGetInsSC(id), size);
                break;
            }

            case IF_DR_3C: // DR_3C   X..........mmmmm ooosssnnnnnddddd      Rd Rn Rm ext(Rm) LSL imm(0-4)
            {
                emitDispReg(encodingZRtoSP(id.idReg1()), size, true);
                emitDispReg(encodingZRtoSP(id.idReg2()), size, true);
                imm = emitGetInsSC(id);
                emitDispExtendReg(id.idReg3(), id.idInsOpt(), imm);
                break;
            }

            case IF_DR_3D: // DR_3D   X..........mmmmm cccc..nnnnnmmmmm      Rd Rn Rm cond
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispReg(id.idReg3(), size, true);
                cfi.immCFVal = unchecked((uint)emitGetInsSC(id));
                emitDispCond(cfi.cond);
                break;
            }

            case IF_DR_3E: // DR_3E   X........X.mmmmm ssssssnnnnnddddd      Rd Rn Rm imm(0-63)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispReg(id.idReg3(), size, true);
                emitDispImm(emitGetInsSC(id), false);
                break;
            }

            case IF_DR_4A: // DR_4A   X..........mmmmm .aaaaannnnnmmmmm      Rd Rn Rm Ra
            {
                if ((ins == INS_smaddl) || (ins == INS_smsubl) || (ins == INS_umaddl) || (ins == INS_umsubl))
                {
                    // smaddl Xd, Wn, Wm, Xa
                    emitDispReg(id.idReg1(), EA_8BYTE, true);
                    emitDispReg(id.idReg2(), EA_4BYTE, true);
                    emitDispReg(id.idReg3(), EA_4BYTE, true);
                    emitDispReg(id.idReg4(), EA_8BYTE, false);
                }
                else
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                    emitDispReg(id.idReg3(), size, true);
                    emitDispReg(id.idReg4(), size, false);
                }
                break;
            }

            case IF_DV_1A: // DV_1A   .........X.iiiii iii........ddddd      Vd imm8 (fmov - immediate scalar)
            {
                elemsize = id.idOpSize();
                emitDispReg(id.idReg1(), elemsize, true);
                emitDispFloatImm(emitGetInsSC(id));
                break;
            }

            case IF_DV_1B: // DV_1B   .QX..........iii cmod..iiiiiddddd      Vd imm8 (immediate vector)
            {
                imm = emitGetInsSC(id) & 0x0ff;
                immShift = unchecked((uint)((emitGetInsSC(id) & 0x700) >> 8));
                hasShift = (immShift != 0);
                elemsize = optGetElemsize(id.idInsOpt());
                if (id.idInsOpt() == INS_OPTS_1D)
                {
                    assert(elemsize == size);
                    emitDispReg(id.idReg1(), size, true);
                }
                else
                {
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                }
                if (ins == INS_fmov)
                {
                    emitDispFloatImm(imm);
                    assert(hasShift == false);
                }
                else
                {
                    if (elemsize == EA_8BYTE)
                    {
                        assert(ins == INS_movi);
                        nint imm64 = 0;
                        nint mask8 = 0xFF;
                        for (uint b = 0; b < 8; b++)
                        {
                            if ((imm & ((nint)1 << (int)b)) != 0)
                            {
                                imm64 |= (mask8 << (int)(b * 8));
                            }
                        }
                        emitDispImm(imm64, hasShift, true);
                    }
                    else
                    {
                        emitDispImm(imm, hasShift, true);
                    }
                    if (hasShift)
                    {
                        var opt = ((immShift & 0x4) != 0) ? INS_OPTS_MSL : INS_OPTS_LSL;
                        var shift = (immShift & 0x3) * 8;
                        emitDispShiftOpts(opt);
                        emitDispImm(shift, false);
                    }
                }
                break;
            }

            case IF_DV_1C: // DV_1C   .........X...... ......nnnnn.....      Vn #0.0 (fcmp - with zero)
            {
                elemsize = id.idOpSize();
                emitDispReg(id.idReg1(), elemsize, true);
                emitDispFloatZero();
                break;
            }

            case IF_DV_2A: // DV_2A   .Q.......X...... ......nnnnnddddd      Vd Vn   (fabs, fcvt - vector)
            {
                if (emitInsIsVectorLongArm64(ins))
                {
                    emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                else if (emitInsIsVectorNarrowArm64(ins))
                {
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), optWidenElemsizeArrangement(id.idInsOpt()), false);
                }
                else
                {
                    assert(!emitInsIsVectorWideArm64(ins));
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                if (ins == INS_fcmeq || ins == INS_fcmge || ins == INS_fcmgt || ins == INS_fcmle || ins == INS_fcmlt)
                {
                    emitDispComma();
                    emitDispFloatZero();
                }
                break;
            }

            case IF_DV_2P: // DV_2P   ................ ......nnnnnddddd      Vd Vn   (aes*)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                break;
            }

            case IF_DV_2M: // DV_2M   .Q......XX...... ......nnnnnddddd      Vd Vn   (abs, neg - vector)
            {
                if (emitInsIsVectorNarrowArm64(ins))
                {
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), optWidenElemsizeArrangement(id.idInsOpt()), false);
                }
                else
                {
                    assert(!emitInsIsVectorLongArm64(ins) && !emitInsIsVectorWideArm64(ins));
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                if (ins == INS_cmeq || ins == INS_cmge || ins == INS_cmgt || ins == INS_cmle || ins == INS_cmlt)
                {
                    emitDispComma();
                    emitDispImm(0, false);
                }
                break;
            }

            case IF_DV_2N: // DV_2N   .........iiiiiii ......nnnnnddddd      Vd Vn imm   (shift - scalar)
            {
                elemsize = id.idOpSize();
                if (emitInsIsVectorLongArm64(ins))
                {
                    emitDispReg(id.idReg1(), widenDatasize(elemsize), true);
                    emitDispReg(id.idReg2(), elemsize, true);
                }
                else if (emitInsIsVectorNarrowArm64(ins))
                {
                    emitDispReg(id.idReg1(), elemsize, true);
                    emitDispReg(id.idReg2(), widenDatasize(elemsize), true);
                }
                else
                {
                    assert(!emitInsIsVectorWideArm64(ins));
                    emitDispReg(id.idReg1(), elemsize, true);
                    emitDispReg(id.idReg2(), elemsize, true);
                }
                imm = emitGetInsSC(id);
                emitDispImm(imm, false);
                break;
            }

            case IF_DV_2O: // DV_2O   .Q.......iiiiiii ......nnnnnddddd      Vd Vn imm   (shift - vector)
            {
                if ((ins == INS_sxtl) || (ins == INS_sxtl2) || (ins == INS_uxtl) || (ins == INS_uxtl2))
                {
                    assert((emitInsIsVectorLongArm64(ins)));
                    emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                else
                {
                    if (emitInsIsVectorLongArm64(ins))
                    {
                        emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    }
                    else if (emitInsIsVectorNarrowArm64(ins))
                    {
                        emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                        emitDispVectorReg(id.idReg2(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                    }
                    else
                    {
                        assert(!emitInsIsVectorWideArm64(ins));
                        emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    }

                    imm = emitGetInsSC(id);
                    emitDispImm(imm, false);
                }
                break;
            }

            case IF_DV_2B: // DV_2B   .Q.........iiiii ......nnnnnddddd      Rd Vn[] (umov/smov    - to general)
            {
                srcsize = id.idOpSize();
                index = emitGetInsSC(id);
                if (ins == INS_smov)
                {
                    dstsize = EA_8BYTE;
                }
                else // INS_umov or INS_mov
                {
                    dstsize = (srcsize == EA_8BYTE) ? EA_8BYTE : EA_4BYTE;
                }
                emitDispReg(id.idReg1(), dstsize, true);
                emitDispVectorRegIndex(id.idReg2(), srcsize, index, false);
                break;
            }

            case IF_DV_2C: // DV_2C   .Q.........iiiii ......nnnnnddddd      Vd Rn   (dup/ins - vector from general)
            {
                if (ins == INS_dup)
                {
                    datasize = id.idOpSize();
                    assert(isValidVectorDatasize(datasize));
                    assert(isValidArrangement(datasize, id.idInsOpt()));
                    elemsize = optGetElemsize(id.idInsOpt());
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                }
                else // INS_ins
                {
                    elemsize = id.idOpSize();
                    index = emitGetInsSC(id);
                    assert(isValidVectorElemsize(elemsize));
                    emitDispVectorRegIndex(id.idReg1(), elemsize, index, true);
                }
                emitDispReg(id.idReg2(), (elemsize == EA_8BYTE) ? EA_8BYTE : EA_4BYTE, false);
                break;
            }

            case IF_DV_2D: // DV_2D   .Q.........iiiii ......nnnnnddddd      Vd Vn[]   (dup - vector)
            {
                datasize = id.idOpSize();
                assert(isValidVectorDatasize(datasize));
                assert(isValidArrangement(datasize, id.idInsOpt()));
                elemsize = optGetElemsize(id.idInsOpt());
                index = emitGetInsSC(id);
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorRegIndex(id.idReg2(), elemsize, index, false);
                break;
            }

            case IF_DV_2E: // DV_2E   ...........iiiii ......nnnnnddddd      Vd Vn[]   (dup - scalar)
            {
                elemsize = id.idOpSize();
                index = emitGetInsSC(id);
                emitDispReg(id.idReg1(), elemsize, true);
                emitDispVectorRegIndex(id.idReg2(), elemsize, index, false);
                break;
            }

            case IF_DV_2F: // DV_2F   ...........iiiii .jjjj.nnnnnddddd      Vd[] Vn[] (ins - element)
            {
                imm = emitGetInsSC(id);
                index = (imm >> 4) & 0xf;
                index2 = imm & 0xf;
                elemsize = id.idOpSize();
                emitDispVectorRegIndex(id.idReg1(), elemsize, index, true);
                emitDispVectorRegIndex(id.idReg2(), elemsize, index2, false);
                break;
            }

            case IF_DV_2G: // DV_2G   .........X...... ......nnnnnddddd      Vd Vn      (fmov, fcvtXX - register)
            case IF_DV_2K: // DV_2K   .........X.mmmmm ......nnnnn.....      Vn Vm      (fcmp)
            case IF_DV_2L: // DV_2L   ........XX...... ......nnnnnddddd      Vd Vn      (abs, neg - scalar)
            {
                size = id.idOpSize();
                if ((ins == INS_fcmeq) || (ins == INS_fcmge) || (ins == INS_fcmgt) || (ins == INS_fcmle) ||
                    (ins == INS_fcmlt))
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                    emitDispFloatZero();
                }
                else if (emitInsIsVectorNarrowArm64(ins))
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), widenDatasize(size), false);
                }
                else
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, false);
                }
                if (fmt == IF_DV_2L &&
                    (ins == INS_cmeq || ins == INS_cmge || ins == INS_cmgt || ins == INS_cmle || ins == INS_cmlt))
                {
                    emitDispComma();
                    emitDispImm(0, false);
                }
                break;
            }

            case IF_DV_2H: // DV_2H   X........X...... ......nnnnnddddd      Rd Vn      (fmov, fcvtXX - to general)
            case IF_DV_2I: // DV_2I   X........X...... ......nnnnnddddd      Vd Rn      (fmov, Xcvtf - from general)
            case IF_DV_2J: // DV_2J   ........SS.....D D.....nnnnnddddd      Vd Vn      (fcvt)
            {
                dstsize = optGetDstsize(id.idInsOpt());
                srcsize = optGetSrcsize(id.idInsOpt());

                emitDispReg(id.idReg1(), dstsize, true);
                emitDispReg(id.idReg2(), srcsize, false);
                break;
            }

            case IF_DV_2Q: // DV_2Q   .........X...... ......nnnnnddddd      Sd Vn      (faddp, fmaxnmp, fmaxp, fminnmp,
                           // fminp - scalar)
            case IF_DV_2R: // DV_2R   .Q.......X...... ......nnnnnddddd      Sd Vn      (fmaxnmv, fmaxv, fminnmv, fminv)
            case IF_DV_2S: // DV_2S   ........XX...... ......nnnnnddddd      Sd Vn      (addp - scalar)
            case IF_DV_2T: // DV_2T   .Q......XX...... ......nnnnnddddd      Sd Vn      (addv, saddlv, smaxv, sminv, uaddlv,
                           // umaxv, uminv)
            {
                if ((ins == INS_sadalp) || (ins == INS_saddlp) || (ins == INS_uadalp) || (ins == INS_uaddlp))
                {
                    emitDispVectorReg(id.idReg1(), optWidenDstArrangement(id.idInsOpt()), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                else
                {
                    if ((ins == INS_saddlv) || (ins == INS_uaddlv))
                    {
                        elemsize = optGetElemsize(optWidenDstArrangement(id.idInsOpt()));
                    }
                    else
                    {
                        elemsize = optGetElemsize(id.idInsOpt());
                    }
                    emitDispReg(id.idReg1(), elemsize, true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), false);
                }
                break;
            }

            case IF_DV_3A: // DV_3A   .Q......XX.mmmmm ......nnnnnddddd      Vd Vn Vm   (vector)
            {
                if ((ins == INS_sdot) || (ins == INS_udot))
                {
                    // sdot/udot Vd.2s, Vn.8b, Vm.8b
                    // sdot/udot Vd.4s, Vn.16b, Vm.16b
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    size = id.idOpSize();
                    emitDispVectorReg(id.idReg2(), (size == EA_8BYTE) ? INS_OPTS_8B : INS_OPTS_16B, true);
                    emitDispVectorReg(id.idReg3(), (size == EA_8BYTE) ? INS_OPTS_8B : INS_OPTS_16B, false);
                }
                else if (((ins == INS_pmull) && (id.idInsOpt() == INS_OPTS_1D)) ||
                         ((ins == INS_pmull2) && (id.idInsOpt() == INS_OPTS_2D)))
                {
                    // pmull Vd.1q, Vn.1d, Vm.1d
                    // pmull2 Vd.1q, Vn.2d, Vm.2d
                    jitprintf($"{emitVectorRegName(id.idReg1())}.1q, ");
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                }
                else if (emitInsIsVectorNarrowArm64(ins))
                {
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                    emitDispVectorReg(id.idReg3(), optWidenElemsizeArrangement(id.idInsOpt()), false);
                }
                else
                {
                    if (emitInsIsVectorLongArm64(ins))
                    {
                        emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    }
                    else if (emitInsIsVectorWideArm64(ins))
                    {
                        emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                        emitDispVectorReg(id.idReg2(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                    }
                    else
                    {
                        emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    }

                    emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                }
                break;
            }

            case IF_DV_3AI: // DV_3AI  .Q......XXLMmmmm ....H.nnnnnddddd      Vd Vn Vm[] (vector by element)
            {
                if ((ins == INS_sdot) || (ins == INS_udot))
                {
                    // sdot/udot Vd.2s, Vn.8b, Vm.4b[index]
                    // sdot/udot Vd.4s, Vn.16b, Vm.4b[index]
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    size = id.idOpSize();
                    emitDispVectorReg(id.idReg2(), (size == EA_8BYTE) ? INS_OPTS_8B : INS_OPTS_16B, true);
                    index = emitGetInsSC(id);
                    jitprintf($"{emitVectorRegName(id.idReg3())}.4b" +
                        $"[{unchecked((int)index).ToString(CultureInfo.InvariantCulture)}]");
                }
                else
                {
                    if (emitInsIsVectorLongArm64(ins))
                    {
                        emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    }
                    else if (emitInsIsVectorWideArm64(ins))
                    {
                        emitDispVectorReg(id.idReg1(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                        emitDispVectorReg(id.idReg2(), optWidenElemsizeArrangement(id.idInsOpt()), true);
                    }
                    else
                    {
                        assert(!emitInsIsVectorNarrowArm64(ins));
                        emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    }

                    elemsize = optGetElemsize(id.idInsOpt());
                    index = emitGetInsSC(id);
                    emitDispVectorRegIndex(id.idReg3(), elemsize, index, false);
                }
                break;
            }

            case IF_DV_3B: // DV_3B   .Q.........mmmmm ......nnnnnddddd      Vd Vn Vm  (vector)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                break;
            }

            case IF_DV_3C: // DV_3C   .Q.........mmmmm ......nnnnnddddd      Vd Vn Vm  (vector)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                switch (ins)
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
                        registerListSize = insGetRegisterListSize(ins);
                        emitDispVectorRegList(id.idReg2(), registerListSize, INS_OPTS_16B, true);
                        break;
                    }
                    case INS_mov:
                    {
                        break;
                    }
                    default:
                    {
                        emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                        break;
                    }
                }
                emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                break;
            }

            case IF_DV_3BI: // DV_3BI  .Q........Lmmmmm ....H.nnnnnddddd      Vd Vn Vm[] (vector by element)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                elemsize = optGetElemsize(id.idInsOpt());
                emitDispVectorRegIndex(id.idReg3(), elemsize, emitGetInsSC(id), false);
                break;
            }

            case IF_DV_3D: // DV_3D   .........X.mmmmm ......nnnnnddddd      Vd Vn Vm  (scalar)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispReg(id.idReg3(), size, false);
                break;
            }

            case IF_DV_3E: // DV_3E   ........XX.mmmmm ......nnnnnddddd      Vd Vn Vm  (scalar)
            {
                if (emitInsIsVectorLongArm64(ins))
                {
                    emitDispReg(id.idReg1(), widenDatasize(size), true);
                }
                else
                {
                    assert(!emitInsIsVectorNarrowArm64(ins) && !emitInsIsVectorWideArm64(ins));
                    emitDispReg(id.idReg1(), size, true);
                }

                emitDispReg(id.idReg2(), size, true);
                emitDispReg(id.idReg3(), size, false);
                break;
            }

            case IF_DV_3EI: // DV_3EI ........XXLMmmmm ....H.nnnnnddddd      Vd Vn Vm[] (scalar by element)
            {
                if (emitInsIsVectorLongArm64(ins))
                {
                    emitDispReg(id.idReg1(), widenDatasize(size), true);
                }
                else
                {
                    assert(!emitInsIsVectorNarrowArm64(ins) && !emitInsIsVectorWideArm64(ins));
                    emitDispReg(id.idReg1(), size, true);
                }
                emitDispReg(id.idReg2(), size, true);
                elemsize = id.idOpSize();
                index = emitGetInsSC(id);
                emitDispVectorRegIndex(id.idReg3(), elemsize, index, false);
                break;
            }

            case IF_DV_3F: // DV_3F   ..........mmmmm ......nnnnnddddd       Vd Vn Vm (vector)
            {
                if ((ins == INS_sha1c) || (ins == INS_sha1m) || (ins == INS_sha1p))
                {
                    // Qd, Sn, Vm (vector)
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), EA_4BYTE, true);
                    emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                }
                else if ((ins == INS_sha256h) || (ins == INS_sha256h2))
                {
                    // Qd Qn Vm (vector)
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                    emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                }
                else // INS_sha1su0, INS_sha256su1
                {
                    // Vd, Vn, Vm   (vector)
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                }
                break;
            }

            case IF_DV_3DI: // DV_3DI  .........XLmmmmm ....H.nnnnnddddd      Vd Vn Vm[] (scalar by element)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                elemsize = size;
                emitDispVectorRegIndex(id.idReg3(), elemsize, emitGetInsSC(id), false);
                break;
            }

            case IF_DV_3G: // DV_3G   .Q.........mmmmm .iiii.nnnnnddddd      Vd Vn Vm imm (vector)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg3(), id.idInsOpt(), true);
                emitDispImm(emitGetInsSC(id), false);
                break;
            }

            case IF_DV_3H: // DV_3H   ...........mmmmm .O....nnnnnddddd      Vd Vn Vm     (vector)
            {
                if ((ins == INS_sha512h) || (ins == INS_sha512h2))
                {
                    emitDispReg(id.idReg1(), size, true);
                    emitDispReg(id.idReg2(), size, true);
                }
                else
                {
                    emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                    emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                }
                emitDispVectorReg(id.idReg3(), id.idInsOpt(), false);
                break;
            }

            case IF_DV_3I: // DV_3I  ...........mmmmm iiiiiinnnnnddddd      Vd Vn Vm imm6 (vector)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg3(), id.idInsOpt(), true);
                emitDispImm(emitGetInsSC(id), false);
                break;
            }

            case IF_DV_4A: // DV_4A   .........X.mmmmm .aaaaannnnnddddd      Vd Va Vn Vm (scalar)
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(id.idReg2(), size, true);
                emitDispReg(id.idReg3(), size, true);
                emitDispReg(id.idReg4(), size, false);
                break;
            }

            case IF_DV_4B: // DV_4B   ...........mmmmm .aaaaannnnnddddd      Vd Vn Vm Va (vector)
            {
                emitDispVectorReg(id.idReg1(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg2(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg3(), id.idInsOpt(), true);
                emitDispVectorReg(id.idReg4(), id.idInsOpt(), false);
                break;
            }

            case IF_PC_0A: // PC_0A   ................ ................
            case IF_SN_0A: // SN_0A   ................ ................
            {
#if FEATURE_LOOP_ALIGN
                if (ins == INS_align)
                {
                    instrDescAlign alignInstrId = (instrDescAlign)id;
                    jitprintf($"[{(id.idIsEmptyAlign() ? 0 : INSTR_ENCODED_SIZE)} bytes");

                    // targetIG is only set for 1st of the series of align instruction
                    var loopHead = alignInstrId.idaLoopHeadPredIG is not null ? alignInstrId.loopHeadIG() : null;
                    if (loopHead is not null)
                    {
                        jitprintf($" for IG{loopHead.GetDisplayId():D2}");
                    }
                    jitprintf("]");
                }
#endif
                break;
            }

            case IF_PC_1A: // PC_1A   ................ ...........ddddd      Rd
            {
                emitDispReg(id.idReg1(), size, false);
                break;
            }

            case IF_PC_2A: // PC_2A   X........X...... ......nnnnnddddd      Rd Rn
            {
                emitDispReg(id.idReg1(), size, true);
                emitDispReg(encodingZRtoSP(id.idReg2()), size, false);
                break;
            }

            case IF_SI_0A: // SI_0A   ...........iiiii iiiiiiiiiii.....               imm16
            {
                emitDispImm(emitGetInsSC(id), false);
                break;
            }

            case IF_SI_0B: // SI_0B   ................ ....bbbb........               imm4 - barrier
            {
                emitDispBarrier((insBarrier)emitGetInsSC(id));
                break;
            }

            case IF_SR_1A: // SR_1A   ................ ...........ttttt      Rt       (dc zva, mrs)
            {
                if (ins == INS_mrs_tpid0)
                {
                    emitDispReg(id.idReg1(), size, true);
                    jitprintf("tpidr_el0");
                }
                else
                {
                    emitDispReg(id.idReg1(), size, false);
                }
                break;
            }

            default:
                // fallback to display SVE instructions.
            {
                emitDispInsSveHelp(id);
                break;
            }
        }

#if DEBUG
        if (id.idIsLclVar())
        {
            jitprintf("\t// ");
            emitDispFrameRef(id.idAddr().iiaLclVar.lvaVarNum(), unchecked((int)id.idAddr().iiaLclVar.lvaOffset()),
                             debugInfo.idVarRefOffs, asmfm);
            if (id.idIsLclVarPair())
            {
                jitprintf(", ");
                ref var iiaLclVar2 = ref emitGetLclVarPairLclVar2(id);
                emitDispFrameRef(iiaLclVar2.lvaVarNum(), unchecked((int)iiaLclVar2.lvaOffset()), debugInfo.idVarRefOffs2,
                                 asmfm);
            }
        }
#endif

        jitprintf("\n");
    }
}
#endif
