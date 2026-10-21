// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.insCFlags;

namespace RyuJitSharp;

public partial class Emitter
{
    private static emitAttr GetNarrowedDstAttr(instruction ins, emitAttr srcAttr)
    {
        return ins switch {
            INS_cvtpd2dq or INS_cvtpd2ps or INS_cvttpd2dq or INS_vcvtdq2ph or INS_vcvtneps2bf16 or
                INS_vcvtpd2udq or INS_vcvtps2phx or INS_vcvtqq2ps or INS_vcvttpd2dqs or INS_vcvttpd2udq or
                INS_vcvttpd2udqs or INS_vcvtudq2ph or INS_vcvtuqq2ps =>
                (emitAttr)Math.Max(16, EA_SIZE_IN_BYTES(srcAttr) / 2),
            INS_vcvtpd2ph or INS_vcvtqq2ph or INS_vcvtuqq2ph =>
                (emitAttr)Math.Max(16, EA_SIZE_IN_BYTES(srcAttr) / 4),
            _ => srcAttr,
        };
    }

    public unsafe void emitDispInsXarch(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InstructionDisplayMissingCompiler);
            return;
        }
        var ins = id.idIns();
        var debug = id.idDebugOnlyInfo();

        if (Configuration.DEBUG && compiler.verbose)
        {
            var info = debug;
            if (info is null)
            {
                FatalJitException.ThrowFatalJitException("Instruction display requires descriptor debug information.");
                return;
            }
            jitprintf($"IN{info.idNum:x4}: ");
        }
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
            emitDispInsHexXarch(id, unchecked(code + writeableOffset), size);
        }

        if (Configuration.TARGET_AMD64 && IsApxNfEncodableInstruction(ins) && id.idIsEvexNfContextSet())
        {
            jitprintf("{nf}    ");
        }
        var mnemonic = emitDisplayName(id);
        jitprintf(" ");
        jitprintf(mnemonic);
        if (mnemonic.Length < 9)
        {
            jitprintf("         ".AsSpan(0, 9 - mnemonic.Length));
        }
        if (Configuration.TARGET_AMD64 && (IsCCMP(ins) || IsCTEST(ins)))
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
        var attr = id.idGCref() switch {
            GCT_GCREF => EA_GCREF,
            GCT_BYREF => EA_BYREF,
            _ => id.idOpSize(),
        };
        if (EA_SIZE(attr) != id.idOpSize())
        {
            attr = id.idOpSize();
        }
        var sizeName = id.idGCref() switch {
            GCT_GCREF => "gword ptr ",
            GCT_BYREF => "bword ptr ",
            _ => emitSizeString(emitGetMemOpSize(id, !id.idHasMem())),
        };
        if ((ins == INS_lea) && (id.idGCref() is not (GCT_GCREF or GCT_BYREF)))
        {
            assert(Configuration.TARGET_AMD64
                ? attr is EA_4BYTE or EA_8BYTE
                : attr == EA_4BYTE);
            sizeName = "";
        }

        if (Configuration.TARGET_XARCH && instrHasImplicitRegPairDest(ins))
        {
            var highReg = Configuration.TARGET_X86 ? REG_X86_EDX : REG_X64_RDX;
            var lowReg = Configuration.TARGET_X86 ? REG_X86_EAX : REG_X64_RAX;
            jitprintf($"{emitRegName(highReg, id.idOpSize())}:{emitRegName(lowReg, id.idOpSize())}, ");
        }
        else if (IsThreeOperandImulInstruction(ins))
        {
            jitprintf($"{emitRegName(inst3opImulReg(ins), id.idOpSize())}, ");
        }

        var format = id.idInsFmt();
        void Reg(int number, emitAttr? width = null)
        {
            regNumber reg;
            switch (number)
            {
                case 1:
                {
                    reg = id.idReg1();
                    break;
                }

                case 2:
                {
                    reg = id.idReg2();
                    break;
                }

                case 3:
                {
                    reg = id.idReg3();
                    break;
                }

                case 4:
                {
                    reg = id.idReg4();
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.InvalidDisplayOperandRegister
                    );
                    return;
                }
            }

            jitprintf(emitRegName(reg, width ?? attr));
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
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.UnexpectedMemoryOperandFormat);
                return;
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
            if (!constant && ins is (INS_bextr or INS_bzhi or INS_sarx or INS_shlx or INS_shrx))
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
            case IF_XARCH_CNS:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));
                emitDispConstant(id, skipComma: true);
                break;
            }

            case IF_XARCH_ARD:
            case IF_XARCH_AWR:
            case IF_XARCH_ARW:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));

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
                if (ins is INS_call or INS_tail_i_jmp)
                {
                    assert(format == IF_XARCH_ARD);

                    if (debug is not null && debug.idMemCookie != 0)
                    {
                        assert(debug.idMemCookie != 0);

                        if (id.idIsCallRegPtr())
                        {
                            jitprintf(" ; ");
                        }
                        jitprintf(compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debug.idMemCookie));
                    }
                }
                break;
            }

            case IF_XARCH_RRD_ARD:
            case IF_XARCH_RWR_ARD:
            case IF_XARCH_RRW_ARD:
            case IF_XARCH_RRD_SRD:
            case IF_XARCH_RWR_SRD:
            case IF_XARCH_RRW_SRD:
            case IF_XARCH_RRD_MRD:
            case IF_XARCH_RWR_MRD:
            case IF_XARCH_RRW_MRD:
            {
                if (Configuration.TARGET_AMD64 && (ins == INS_movsxd))
                {
                    attr = EA_8BYTE;
                }
                else if (ins == INS_movsx)
                {
                    attr = EA_PTRSIZE;
                }
                else if (ins == INS_movzx)
                {
                    attr = EA_4BYTE;
                }
                else if (ins == INS_crc32 && attr != EA_8BYTE)
                {
                    attr = EA_4BYTE;
                }
                else
                {
                    attr = GetNarrowedDstAttr(ins, attr);
                }
                RegMem(1);
                if (format is IF_XARCH_RRD_ARD or IF_XARCH_RWR_ARD or IF_XARCH_RRW_ARD && debug is not null)
                {
                    emitDispCommentForHandle(debug.idMemCookie, 0, debug.idFlags);
                }
                break;
            }

            case IF_XARCH_RRD_ARD_CNS:
            case IF_XARCH_RRW_ARD_CNS:
            case IF_XARCH_RWR_ARD_CNS:
            case IF_XARCH_RRD_SRD_CNS:
            case IF_XARCH_RWR_SRD_CNS:
            case IF_XARCH_RRW_SRD_CNS:
            case IF_XARCH_RRD_MRD_CNS:
            case IF_XARCH_RWR_MRD_CNS:
            case IF_XARCH_RRW_MRD_CNS:
            {
                RegMem(1, constant: true);
                break;
            }

            case IF_XARCH_ARD_RRD_CNS:
            case IF_XARCH_AWR_RRD_CNS:
            case IF_XARCH_ARW_RRD_CNS:
            case IF_XARCH_MRD_RRD_CNS:
            case IF_XARCH_MWR_RRD_CNS:
            case IF_XARCH_MRW_RRD_CNS:
            {
                if (ins is INS_vextractf32x4 or INS_vextractf64x2 or INS_vextracti32x4 or INS_vextracti64x2)
                {
                    sizeName = emitSizeString(EA_16BYTE);
                }
                else if (ins is INS_vextractf32x8 or INS_vextractf64x4 or INS_vextracti32x8 or INS_vextracti64x4)
                {
                    sizeName = emitSizeString(EA_32BYTE);
                }
                MemReg(1, constant: true);
                break;
            }

            case IF_XARCH_RRD_RRD_ARD:
            case IF_XARCH_RWR_RRD_ARD:
            case IF_XARCH_RRW_RRD_ARD:
            case IF_XARCH_RWR_RWR_ARD:
            case IF_XARCH_RRD_RRD_SRD:
            case IF_XARCH_RWR_RRD_SRD:
            case IF_XARCH_RRW_RRD_SRD:
            case IF_XARCH_RWR_RWR_SRD:
            case IF_XARCH_RRD_RRD_MRD:
            case IF_XARCH_RWR_RRD_MRD:
            case IF_XARCH_RRW_RRD_MRD:
            case IF_XARCH_RWR_RWR_MRD:
            {
                RegRegMem();
                break;
            }

            case IF_XARCH_RRD_ARD_RRD:
            case IF_XARCH_RWR_ARD_RRD:
            case IF_XARCH_RRW_ARD_RRD:
            case IF_XARCH_RRD_SRD_RRD:
            case IF_XARCH_RWR_SRD_RRD:
            case IF_XARCH_RRW_SRD_RRD:
            case IF_XARCH_RRD_MRD_RRD:
            case IF_XARCH_RWR_MRD_RRD:
            case IF_XARCH_RRW_MRD_RRD:
            {
                if (format is IF_XARCH_RRD_ARD_RRD or IF_XARCH_RWR_ARD_RRD or IF_XARCH_RRW_ARD_RRD)
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
                if (format is IF_XARCH_RRD_ARD_RRD or IF_XARCH_RWR_ARD_RRD or IF_XARCH_RRW_ARD_RRD
                    or IF_XARCH_RRD_SRD_RRD or IF_XARCH_RWR_SRD_RRD or IF_XARCH_RRW_SRD_RRD)
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

            case IF_XARCH_RWR_RRD_ARD_CNS:
            case IF_XARCH_RWR_RRD_SRD_CNS:
            case IF_XARCH_RWR_RRD_MRD_CNS:
            {
                RegRegMem(constant: true);
                break;
            }

            case IF_XARCH_RWR_RRD_ARD_RRD:
            case IF_XARCH_RWR_RRD_SRD_RRD:
            case IF_XARCH_RWR_RRD_MRD_RRD:
            {
                Reg(1);

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

            case IF_XARCH_ARD_RRD:
            case IF_XARCH_AWR_RRD:
            case IF_XARCH_ARW_RRD:
            case IF_XARCH_ARW_RRW:
            case IF_XARCH_SRD_RRD:
            case IF_XARCH_SWR_RRD:
            case IF_XARCH_SRW_RRD:
            case IF_XARCH_SRW_RRW:
            case IF_XARCH_MRD_RRD:
            case IF_XARCH_MWR_RRD:
            case IF_XARCH_MRW_RRD:
            case IF_XARCH_MRW_RRW:
            {
                MemReg(1);
                break;
            }

            case IF_XARCH_AWR_RRD_RRD:
            case IF_XARCH_SWR_RRD_RRD:
            {
                MemReg(1);
                jitprintf(", ");
                Reg(2);
                break;
            }

            case IF_XARCH_MWR_RRD_RRD:
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

            case IF_XARCH_ARD_CNS:
            case IF_XARCH_AWR_CNS:
            case IF_XARCH_ARW_CNS:
            case IF_XARCH_ARW_SHF:
            case IF_XARCH_SRD_CNS:
            case IF_XARCH_SWR_CNS:
            case IF_XARCH_SRW_CNS:
            case IF_XARCH_SRW_SHF:
            case IF_XARCH_MRD_CNS:
            case IF_XARCH_MWR_CNS:
            case IF_XARCH_MRW_CNS:
            case IF_XARCH_MRW_SHF:
            {
                Mem();
                Mask();
                Constant();
                break;
            }

            case IF_XARCH_SRD:
            case IF_XARCH_SWR:
            case IF_XARCH_SRW:
            case IF_XARCH_MRD:
            case IF_XARCH_MWR:
            case IF_XARCH_MRW:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));
                if (!Configuration.FEATURE_FIXED_OUT_ARGS && (ins == INS_pop))
                {
                    emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) - sizeof(int)));
                }
                Mem();
                if (!Configuration.FEATURE_FIXED_OUT_ARGS && (ins == INS_pop))
                {
                    emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) + sizeof(int)));
                }
                emitDispShift(ins);
                break;
            }

            case IF_XARCH_SRD_RRD_CNS:
            case IF_XARCH_SWR_RRD_CNS:
            case IF_XARCH_SRW_RRD_CNS:
            {
                assert(IsSSEOrAVXInstruction(ins));
                MemReg(1, constant: true);
                break;
            }

            case IF_XARCH_RRD_RRD:
            case IF_XARCH_RWR_RRD:
            case IF_XARCH_RRW_RRD:
            case IF_XARCH_RRW_RRW:
            {
                emitDispRegisterPair(id, ref attr);
                break;
            }

            case IF_XARCH_RRD_RRD_RRD:
            case IF_XARCH_RWR_RRD_RRD:
            case IF_XARCH_RRW_RRD_RRD:
            case IF_XARCH_RWR_RWR_RRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins) || IsApxExtendedEvexInstruction(ins));
                assert(IsThreeOperandAVXInstruction(ins) || IsKInstruction(ins) || IsApxExtendedEvexInstruction(ins));
                var attr12 = attr;
                var attr3 = attr;
                if ((insTupleTypeInfo(ins) & INS_TT_MEM128) != 0)
                {
                    attr3 = EA_16BYTE;
                }
                else if (ins is INS_cvtsi2sd32 or INS_cvtsi2sd64 or INS_cvtsi2ss32 or INS_cvtsi2ss64 or
                    INS_vcvtsi2sh32 or INS_vcvtsi2sh64 or INS_vcvtusi2sd32 or INS_vcvtusi2sd64 or
                    INS_vcvtusi2sh32 or INS_vcvtusi2sh64 or INS_vcvtusi2ss32 or INS_vcvtusi2ss64)
                {
                    attr12 = EA_16BYTE;
                    attr3 = (emitAttr)GetInputSizeInBytes(id);
                }
                Reg(1, attr12);
                Mask();
                var second = id.idReg2();
                var third = id.idReg3();
                if (ins is INS_bextr or INS_bzhi or INS_sarx or INS_shlx or INS_shrx)
                {
                    (second, third) = (third, second);
                }
                jitprintf($", {emitRegName(second, attr12)}, ");
                jitprintf(emitRegName(third, attr3));
                emitDispEmbRounding(id);
                break;
            }

            case IF_XARCH_RWR_RRD_RRD_CNS:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins));
                assert(IsThreeOperandAVXInstruction(ins));
                emitDispRegisterConstant(id, format, ref attr);
                break;
            }

            case IF_XARCH_RRD_RRD_CNS:
            case IF_XARCH_RWR_RRD_CNS:
            case IF_XARCH_RRW_RRD_CNS:
            {
                emitDispRegisterConstant(id, format, ref attr);
                break;
            }

            case IF_XARCH_RWR_RRD_RRD_RRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins));
                assert(UseVexEncodings);
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

            case IF_XARCH_RRD:
            case IF_XARCH_RWR:
            case IF_XARCH_RRW:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));
                Reg(1);
                emitDispShift(ins);
                break;
            }

            case IF_XARCH_RRD_CNS:
            case IF_XARCH_RWR_CNS:
            case IF_XARCH_RRW_CNS:
            case IF_XARCH_RRW_SHF:
            {
                Reg(1);
                Mask();
                Constant();
                break;
            }

            case IF_XARCH_RWR_RRD_SHF:
            {
                assert(IsApxExtendedEvexInstruction(ins));
                Reg(1);
                jitprintf(", ");
                Reg(2);
                Constant();
                break;
            }

            case IF_XARCH_RWR_MRD_OFF:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));
                Reg(1);
                jitprintf(", offset");
                emitDispClsVar(id.idAddr().iiaFieldHnd, emitGetInsDsp(id), id.idIsDspReloc());
                break;
            }

            case IF_XARCH_MRD_OFF:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));
                jitprintf("offset ");
                emitDispClsVar(id.idAddr().iiaFieldHnd, emitGetInsDsp(id), id.idIsDspReloc());
                break;
            }

            case IF_XARCH_LABEL:
            case IF_XARCH_RWR_LABEL:
            case IF_XARCH_SWR_LABEL:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));

                if (ins == INS_lea)
                {
                    Reg(1);
                    jitprintf(", ");
                }
                else if (ins == INS_mov)
                {
                    assert(format == IF_XARCH_SWR_LABEL);
                    if (id is not instrDescLbl label)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.MoveLabelMissingDescriptor);
                        return;
                    }
                    emitDispFrameRef(label.idAddr().iiaLclVar.lvaVarNum(),
                        unchecked((int)label.idAddr().iiaLclVar.lvaOffset()), 0, asmfm);
                    jitprintf(", ");
                }
                var jump = (instrDescJmp)id;
                if (jump.idjShort)
                {
                    jitprintf("SHORT ");
                }
                if (id.idIsBound())
                {
                    var targetIG = jump.idjTargetIG;
                    if (targetIG is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.BoundLabelDescriptorMissingTargetGroup
                        );
                        return;
                    }

                    emitPrintLabel(targetIG);
                }
                else
                {
                    if (jump.idjTarget is not BasicBlock target)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.UnboundLabelDescriptorMissingTargetBlock);
                        return;
                    }
                    jitprintf($"L_M{unchecked((uint)compiler.compMethodID):D3}_BB{target.bbNum:D2}");
                }
                break;
            }

            case IF_XARCH_METHOD:
            case IF_XARCH_METHPTR:
            {
                assert(!IsSimdEvexEncodableInstruction(ins));
                var methodName = compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)(debug?.idMemCookie ?? 0));

                if (format == IF_XARCH_METHPTR)
                {
                    jitprintf("[");
                }
                jitprintf(methodName);
                if (format == IF_XARCH_METHPTR)
                {
                    jitprintf("]");
                }
                break;
            }

            case IF_NONE:
            {
                if (Configuration.FEATURE_LOOP_ALIGN && (ins == INS_align))
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
                break;
            }

            default:
            {
                jitprintf($"unexpected format {emitIfName(format)}");
                assert(false, "unexpectedFormat");
                break;
            }
        }

        if (Configuration.DEBUG && size != 0 && size != id.idCodeSize() && (!asmfm || compiler.verbose))
        {
            jitprintf($" (ECS:{id.idCodeSize()}, ACS:{size})");
        }
        jitprintf("\n");
    }
}
