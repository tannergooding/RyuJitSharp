// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.insCFlags;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "x86 instruction display is not implemented.");
#else
        var compiler = _compiler ?? throw new FatalJitException("Instruction display requires an active compiler.");
        var ins = id.idIns();
        var debug = id.idDebugOnlyInfo();

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"IN{debug?.idNum ?? 0:x4}: ");
        }
#endif
        if (!isNew && !asmfm)
        {
            doffs = true;
        }
        emitDispInsAddr(code);
        emitDispInsOffs(offset, doffs);
        if (code is not null)
        {
            assert((code >= emitCodeBlock && code < emitCodeBlock + emitTotalHotCodeSize)
                || (code >= emitColdCodeBlock && code < emitColdCodeBlock + emitTotalColdCodeSize));
            emitDispInsHex(id, unchecked(code + writeableOffset), size);
        }

        if (IsApxNfEncodableInstruction(ins) && id.idIsEvexNfContextSet())
        {
            jitprintf("{nf}    ");
        }
        var mnemonic = emitDisplayName(id);
        jitprintf($" {mnemonic,-9}");
        if (IsCCMP(ins) || IsCTEST(ins))
        {
            var dfv = id.idGetEvexDFV();
            var flags = "";
            if ((dfv & (uint)INS_FLAGS_OF) != 0)
            {
                flags += "of,";
            }
            if ((dfv & (uint)INS_FLAGS_SF) != 0)
            {
                flags += "sf,";
            }
            if ((dfv & (uint)INS_FLAGS_ZF) != 0)
            {
                flags += "zf,";
            }
            if ((dfv & (uint)INS_FLAGS_CF) != 0)
            {
                flags += "cf,";
            }
            jitprintf($"{{dfv={flags.TrimEnd(',')}}}    ");
        }
        if (mnemonic.Length >= 9)
        {
            jitprintf(" ");
        }

        assert(id.idCodeSize() != 0 || emitInstHasNoCode(id));
        var attr = id.idGCref() switch
        {
            GCT_GCREF => EA_GCREF,
            GCT_BYREF => EA_BYREF,
            _ => id.idOpSize(),
        };
        var sizeName = id.idGCref() switch
        {
            GCT_GCREF => "gword ptr ",
            GCT_BYREF => "bword ptr ",
            _ => emitSizeString(emitGetMemOpSize(id, !id.idHasMem())),
        };
        if ((ins == INS_lea) && (id.idGCref() is not (GCT_GCREF or GCT_BYREF)))
        {
            assert(attr is EA_4BYTE or EA_8BYTE);
            sizeName = "";
        }

        if (instrHasImplicitRegPairDest(ins))
        {
            jitprintf($"{emitRegName(REG_EDX, attr)}:{emitRegName(REG_EAX, attr)}, ");
        }
        else if (instrIs3opImul(ins))
        {
            jitprintf($"{emitRegName(inst3opImulReg(ins), attr)}, ");
        }

        var format = id.idInsFmt();
        void Reg(int number, emitAttr? width = null)
        {
            jitprintf(emitRegName(number switch
            {
                1 => id.idReg1(),
                2 => id.idReg2(),
                3 => id.idReg3(),
                4 => id.idReg4(),
                _ => throw new FatalJitException("Invalid operand register."),
            }, width ?? attr));
        }

        void Mem()
        {
            jitprintf(sizeName);
            if (id.idHasMemAdr())
            {
                emitDispAddrMode(id);
            }
            else if (id.idHasMemStk())
            {
                emitDispFrameRef(id.idAddr().iiaLclVar.lvaVarNum(),
                    unchecked((int)id.idAddr().iiaLclVar.lvaOffset()),
                    debug?.idVarRefOffs ?? unchecked((uint)BAD_IL_OFFSET), asmfm);
            }
            else if (id.idHasMemGen())
            {
                emitDispClsVar(id.idAddr().iiaFieldHnd, emitGetInsDsp(id), id.idIsDspReloc());
            }
            else
            {
                throw new FatalJitException("Unexpected memory operand format.");
            }
        }
        void Mask()
        {
            emitDispEmbMasking(id);
        }

        void Broadcast()
        {
            emitDispEmbBroadcastCount(id);
        }

        void Constant()
        {
            emitDispConstant(id);
        }
        void RegMem(int reg, bool constant = false)
        {
            Reg(reg);
            Mask();
            jitprintf(", ");
            Mem();
            Broadcast();
            if (constant)
            {
                Constant();
            }
        }
        void MemReg(int reg, bool constant = false)
        {
            Mem();
            Mask();
            jitprintf(", ");
            Reg(reg);
            if (constant)
            {
                Constant();
            }
        }
        void RegRegMem(bool constant = false)
        {
            Reg(1);
            Mask();
            if (ins is INS_bextr or INS_bzhi or INS_sarx or INS_shlx or INS_shrx)
            {
                jitprintf(", ");
                Mem();
                jitprintf(", ");
                Reg(2);
            }
            else
            {
                jitprintf(", ");
                Reg(2);
                jitprintf(", ");
                Mem();
                Broadcast();
                if (constant)
                {
                    Constant();
                }
            }
        }

        switch (format)
        {
            case IF_CNS:
            {
                assert(!(IsEvexEncodableInstruction(ins) && IsSimdInstruction(ins)));
                emitDispConstant(id, skipComma: true);
                break;
            }

            case IF_ARD:
            case IF_AWR:
            case IF_ARW:
            {
                if (ins is INS_call or INS_tail_i_jmp && id.idIsCallRegPtr())
                {
                    jitprintf(emitRegName(id.idAddr().iiaAddrMode.amBaseReg));
                }
                else
                {
                    if (ins is not INS_call and not INS_tail_i_jmp)
                    {
                        jitprintf(sizeName);
                    }
                    emitDispAddrMode(id);
                    emitDispShift(ins);
                }
                if (ins is INS_call or INS_tail_i_jmp && debug is not null && debug.idMemCookie != 0)
                {
                    if (id.idIsCallRegPtr())
                    {
                        jitprintf(" ; ");
                    }
                    jitprintf(compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debug.idMemCookie));
                }
                break;
            }

            case IF_RRD_ARD:
            case IF_RWR_ARD:
            case IF_RRW_ARD:
            case IF_RRD_SRD:
            case IF_RWR_SRD:
            case IF_RRW_SRD:
            case IF_RRD_MRD:
            case IF_RWR_MRD:
            case IF_RRW_MRD:
            {
                if (ins == INS_movsxd || ins is INS_movsx or INS_movzx)
                {
                    attr = EA_PTRSIZE;
                }
                else if (ins == INS_crc32 && attr != EA_8BYTE)
                {
                    attr = EA_4BYTE;
                }
                RegMem(1);
                if (format is IF_RRD_ARD or IF_RWR_ARD or IF_RRW_ARD && debug is not null)
                {
                    emitDispCommentForHandle(debug.idMemCookie, 0, debug.idFlags);
                }
                break;
            }

            case IF_RRD_ARD_CNS:
            case IF_RRW_ARD_CNS:
            case IF_RWR_ARD_CNS:
            case IF_RRD_SRD_CNS:
            case IF_RWR_SRD_CNS:
            case IF_RRW_SRD_CNS:
            case IF_RRD_MRD_CNS:
            case IF_RWR_MRD_CNS:
            case IF_RRW_MRD_CNS:
            {
                RegMem(1, constant: true);
                break;
            }

            case IF_ARD_RRD_CNS:
            case IF_AWR_RRD_CNS:
            case IF_ARW_RRD_CNS:
            case IF_MRD_RRD_CNS:
            case IF_MWR_RRD_CNS:
            case IF_MRW_RRD_CNS:
            {
                var memoryName = sizeName;
                if (ins is INS_vextractf32x4 or INS_vextractf64x2 or INS_vextracti32x4 or INS_vextracti64x2)
                {
                    sizeName = emitSizeString(EA_16BYTE);
                }
                else if (ins is INS_vextractf32x8 or INS_vextractf64x4 or INS_vextracti32x8 or INS_vextracti64x4)
                {
                    sizeName = emitSizeString(EA_32BYTE);
                }
                MemReg(1, constant: true);
                sizeName = memoryName;
                break;
            }

            case IF_RRD_RRD_ARD:
            case IF_RWR_RRD_ARD:
            case IF_RRW_RRD_ARD:
            case IF_RWR_RWR_ARD:
            case IF_RRD_RRD_SRD:
            case IF_RWR_RRD_SRD:
            case IF_RRW_RRD_SRD:
            case IF_RWR_RWR_SRD:
            case IF_RRD_RRD_MRD:
            case IF_RWR_RRD_MRD:
            case IF_RRW_RRD_MRD:
            case IF_RWR_RWR_MRD:
            {
                RegRegMem();
                break;
            }

            case IF_RRD_ARD_RRD:
            case IF_RWR_ARD_RRD:
            case IF_RRW_ARD_RRD:
            case IF_RRD_SRD_RRD:
            case IF_RWR_SRD_RRD:
            case IF_RRW_SRD_RRD:
            case IF_RRD_MRD_RRD:
            case IF_RWR_MRD_RRD:
            case IF_RRW_MRD_RRD:
            {
                if (format is IF_RRD_ARD_RRD or IF_RWR_ARD_RRD or IF_RRW_ARD_RRD)
                {
                    sizeName = emitSizeString(EA_4BYTE);
                    if (ins is INS_vpgatherqd or INS_vgatherqps)
                    {
                        attr = EA_16BYTE;
                    }
                }
                Reg(1);
                Mask();
                jitprintf(", ");
                if (format is IF_RRD_ARD_RRD or IF_RWR_ARD_RRD or IF_RRW_ARD_RRD
                    or IF_RRD_SRD_RRD or IF_RWR_SRD_RRD or IF_RRW_SRD_RRD)
                {
                    Mem();
                    Broadcast();
                    jitprintf(", ");
                    Reg(2);
                }
                else
                {
                    Reg(2);
                    jitprintf(", ");
                    Mem();
                    Broadcast();
                    jitprintf(", ");
                    Reg(2);
                }
                break;
            }

            case IF_RWR_RRD_ARD_CNS:
            case IF_RWR_RRD_SRD_CNS:
            case IF_RWR_RRD_MRD_CNS:
            {
                RegRegMem(constant: true);
                break;
            }

            case IF_RWR_RRD_ARD_RRD:
            case IF_RWR_RRD_SRD_RRD:
            case IF_RWR_RRD_MRD_RRD:
            {
                CnsVal regVal = default;
                if (id.idHasMemGen())
                {
                    emitGetInsDcmCns(id, ref regVal);
                }
                else if (id.idHasMemAdr())
                {
                    _ = emitGetInsAmdCns(id, ref regVal);
                }
                else
                {
                    emitGetInsCns(id, ref regVal);
                }
                var third = decodeRegFromIval(regVal.cnsVal);
                Reg(1);
                if (third.IsMskReg)
                {
                    emitDispMask(id, third);
                }
                jitprintf(", ");
                Reg(2);
                jitprintf(", ");
                Mem();
                Broadcast();
                if (!third.IsMskReg)
                {
                    jitprintf($", {emitRegName(third, attr)}");
                }
                break;
            }

            case IF_ARD_RRD:
            case IF_AWR_RRD:
            case IF_ARW_RRD:
            case IF_ARW_RRW:
            case IF_SRD_RRD:
            case IF_SWR_RRD:
            case IF_SRW_RRD:
            case IF_SRW_RRW:
            case IF_MRD_RRD:
            case IF_MWR_RRD:
            case IF_MRW_RRD:
            case IF_MRW_RRW:
            {
                MemReg(1);
                break;
            }

            case IF_AWR_RRD_RRD:
            case IF_SWR_RRD_RRD:
            {
                MemReg(1);
                jitprintf(", ");
                Reg(2);
                break;
            }

            case IF_MWR_RRD_RRD:
            {
                jitprintf(sizeName);
                Mask();
                emitDispClsVar(id.idAddr().iiaFieldHnd, emitGetInsDsp(id), id.idIsDspReloc());
                jitprintf(", ");
                Reg(1);
                jitprintf(", ");
                Reg(2);
                break;
            }

            case IF_ARD_CNS:
            case IF_AWR_CNS:
            case IF_ARW_CNS:
            case IF_ARW_SHF:
            case IF_SRD_CNS:
            case IF_SWR_CNS:
            case IF_SRW_CNS:
            case IF_SRW_SHF:
            case IF_MRD_CNS:
            case IF_MWR_CNS:
            case IF_MRW_CNS:
            case IF_MRW_SHF:
            {
                Mem();
                Mask();
                Constant();
                break;
            }

            case IF_SRD:
            case IF_SWR:
            case IF_SRW:
            case IF_MRD:
            case IF_MWR:
            case IF_MRW:
            {
                Mem();
                emitDispShift(ins);
                break;
            }

            case IF_SRD_RRD_CNS:
            case IF_SWR_RRD_CNS:
            case IF_SRW_RRD_CNS:
            {
                MemReg(1, constant: true);
                break;
            }

            case IF_RRD_RRD:
            case IF_RWR_RRD:
            case IF_RRW_RRD:
            case IF_RRW_RRW:
            {
                emitDispRegisterPair(id, ref attr);
                break;
            }

            case IF_RRD_RRD_RRD:
            case IF_RWR_RRD_RRD:
            case IF_RRW_RRD_RRD:
            case IF_RWR_RWR_RRD:
            {
                Reg(1);
                Mask();
                var second = id.idReg2();
                var third = id.idReg3();
                if (ins is INS_bextr or INS_bzhi or INS_sarx or INS_shlx or INS_shrx)
                {
                    (second, third) = (third, second);
                }
                jitprintf($", {emitRegName(second, attr)}, ");
                var thirdAttr = (insTupleTypeInfo(ins) & INS_TT_MEM128) != 0 ? EA_16BYTE : attr;
                jitprintf(emitRegName(third, thirdAttr));
                emitDispEmbRounding(id);
                break;
            }

            case IF_RWR_RRD_RRD_CNS:
            case IF_RRD_RRD_CNS:
            case IF_RWR_RRD_CNS:
            case IF_RRW_RRD_CNS:
            {
                emitDispRegisterConstant(id, format, ref attr);
                break;
            }

            case IF_RWR_RRD_RRD_RRD:
            {
                Reg(1);
                var fourth = id.idReg4();
                if (fourth.IsMskReg)
                {
                    emitDispMask(id, fourth);
                }
                jitprintf(", ");
                Reg(2);
                jitprintf(", ");
                Reg(3);
                if (!fourth.IsMskReg)
                {
                    jitprintf($", {emitRegName(fourth, attr)}");
                }
                break;
            }

            case IF_RRD:
            case IF_RWR:
            case IF_RRW:
            {
                Reg(1);
                emitDispShift(ins);
                break;
            }

            case IF_RRD_CNS:
            case IF_RWR_CNS:
            case IF_RRW_CNS:
            case IF_RRW_SHF:
            {
                Reg(1);
                Mask();
                Constant();
                break;
            }

            case IF_RWR_RRD_SHF:
            {
                Reg(1);
                jitprintf(", ");
                Reg(2);
                Constant();
                break;
            }

            case IF_RWR_MRD_OFF:
            {
                Reg(1);
                jitprintf(", offset");
                emitDispClsVar(id.idAddr().iiaFieldHnd, emitGetInsDsp(id), id.idIsDspReloc());
                break;
            }

            case IF_MRD_OFF:
            {
                jitprintf("offset ");
                emitDispClsVar(id.idAddr().iiaFieldHnd, emitGetInsDsp(id), id.idIsDspReloc());
                break;
            }

            case IF_LABEL:
            case IF_RWR_LABEL:
            case IF_SWR_LABEL:
            {
                if (ins == INS_lea)
                {
                    Reg(1);
                    jitprintf(", ");
                }
                else if (ins == INS_mov)
                {
                    assert(id is instrDescLbl);
                    emitDispFrameRef(id.idAddr().iiaLclVar.lvaVarNum(),
                        unchecked((int)id.idAddr().iiaLclVar.lvaOffset()), 0, asmfm);
                    jitprintf(", ");
                }
                var jump = (instrDescJmp)id;
                if (jump.idjShort)
                {
                    jitprintf("SHORT ");
                }
                if (id.idIsBound())
                {
                    emitPrintLabel(jump.idjTargetIG
                        ?? throw new FatalJitException("Bound label descriptor has no target group."));
                }
                else
                {
                    var target = jump.idjTarget
                        ?? throw new FatalJitException("Unbound label descriptor has no target block.");
                    jitprintf($"L_M{unchecked((uint)compiler.compMethodID):D3}_BB{target.bbNum:D2}");
                }
                break;
            }

            case IF_METHOD:
            case IF_METHPTR:
            {
                if (format == IF_METHPTR)
                {
                    jitprintf("[");
                }
                jitprintf(compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)(debug?.idMemCookie ?? 0)));
                if (format == IF_METHPTR)
                {
                    jitprintf("]");
                }
                break;
            }

            case IF_NONE:
            {
#if FEATURE_LOOP_ALIGN
                if (ins == INS_align)
                {
                    var align = (instrDescAlign)id;
                    jitprintf($"[{align.idCodeSize()} bytes");
                    var loopHead = align.loopHeadIG();
                    if (align.idaLoopHeadPredIG is not null && loopHead is not null)
                    {
                        jitprintf($" for IG{loopHead.GetDisplayId():D2}");
                    }
                    jitprintf("]");
                }
#endif
                break;
            }

            default:
            {
                throw new FatalJitException($"Unexpected instruction format: {format}.");
            }
        }

#if DEBUG
        if (size != 0 && size != id.idCodeSize() && (!asmfm || compiler.verbose))
        {
            jitprintf($" (ECS:{id.idCodeSize()}, ACS:{size})");
        }
#endif
        jitprintf("\n");
#endif
    }
}
#endif
