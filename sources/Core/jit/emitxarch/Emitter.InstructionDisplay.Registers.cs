// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private static regNumber decodeRegFromIval(nint value)
    {
        assert((value >= -128) && (value <= 127));
        var opReg = unchecked((regNumber)value);
        assert(isLowSimdReg(opReg) || isMaskReg(opReg));

        return opReg;
    }

    private void emitDispRegisterPair(instrDesc id, ref emitAttr attr)
    {
        var ins = id.idIns();
        if ((id.idInsFmt() is IF_XARCH_RRD_RRD or IF_XARCH_RWR_RRD) && ins is
            (INS_rol or INS_ror or INS_rcl or INS_rcr or INS_shl or INS_shr or INS_sar))
        {
            jitprintf($"{emitRegName(id.idReg1(), attr)}, {emitRegName(id.idReg2(), attr)}");
            emitDispShift(ins);
            return;
        }

        var targetAttr = attr;
        var sourceAttr = attr;
        var tupleType = insTupleTypeInfo(ins);
        if (tupleType == INS_TT_NONE)
        {
            switch (ins)
            {
                case INS_movmskpd:
                case INS_movmskps:
                case INS_pmovmskb:
                {
                    assert(!id.idIsEvexAaaContextSet());
                    targetAttr = EA_4BYTE;
                    break;
                }

                case INS_movsxd when Configuration.TARGET_AMD64:
                {
                    targetAttr = EA_8BYTE;
                    sourceAttr = EA_4BYTE;
                    break;
                }

                case INS_movsx:
                case INS_movzx:
                {
                    targetAttr = ins == INS_movsx ? EA_PTRSIZE : EA_4BYTE;
                    break;
                }

                case INS_crc32:
                {
                    targetAttr = (emitAttr)Math.Max((int)EA_4BYTE, (int)Globals.EA_SIZE(attr));
                    break;
                }
            }
            jitprintf($"{emitRegName(id.idReg1(), targetAttr)}, {emitRegName(id.idReg2(), sourceAttr)}");
            return;
        }

        var inputSize = GetInputSizeInBytes(id);
        switch (tupleType)
        {
            case INS_TT_HALF:
            case INS_TT_HALF_MEM:
            case INS_TT_QUARTER:
            case INS_TT_QUARTER_MEM:
            case INS_TT_EIGHTH_MEM:
            {
                var divisor = tupleType is INS_TT_HALF or INS_TT_HALF_MEM ? 2 :
                    tupleType is INS_TT_QUARTER or INS_TT_QUARTER_MEM ? 4 : 8;
                var maskElemSize = Configuration.XMM_REGSIZE_BYTES / CodeGen.instKMaskBaseSize(ins);
                var width = (emitAttr)Math.Max(16, EA_SIZE_IN_BYTES(attr) / divisor);
                if (inputSize < maskElemSize)
                {
                    assert(inputSize == maskElemSize / divisor);
                    sourceAttr = width;
                }
                else
                {
                    targetAttr = width;
                }
                break;
            }

            default:
            {
                if (ins is INS_cvtsi2ss32 or INS_cvtsi2sd32 or INS_cvtsi2ss64 or INS_cvtsi2sd64)
                {
                    assert(!id.idIsEvexAaaContextSet());
                    targetAttr = EA_16BYTE;
                    sourceAttr = (emitAttr)GetInputSizeInBytes(id);
                }
                else if (ins is INS_cvttsd2si32 or INS_cvttsd2si64 or INS_cvtsd2si32 or INS_cvtsd2si64
                    or INS_cvtss2si32 or INS_cvtss2si64 or INS_cvttss2si32 or INS_cvttss2si64
                    or INS_vcvtsd2usi32 or INS_vcvtsd2usi64 or INS_vcvtss2usi32 or INS_vcvtss2usi64
                    or INS_vcvttsd2usi32 or INS_vcvttsd2usi64 or INS_vcvttss2usi32 or INS_vcvttss2usi64
                    or INS_vcvttsd2sis32 or INS_vcvttsd2sis64 or INS_vcvttss2sis32 or INS_vcvttss2sis64
                    or INS_vcvttsd2usis32 or INS_vcvttsd2usis64 or INS_vcvttss2usis32 or INS_vcvttss2usis64)
                {
                    assert(!id.idIsEvexAaaContextSet());
                    sourceAttr = EA_16BYTE;
                }
                else if (ins is INS_vpbroadcastb_gpr or INS_vpbroadcastd_gpr or INS_vpbroadcastw_gpr)
                {
                    sourceAttr = EA_4BYTE;
                }
                else if (ins == INS_vpbroadcastq_gpr)
                {
                    sourceAttr = EA_8BYTE;
                }
                else if (ins is INS_movd32 or INS_movd64)
                {
                    if (isGeneralRegister(id.idReg1()))
                    {
                        targetAttr = (emitAttr)GetInputSizeInBytes(id);
                        sourceAttr = EA_16BYTE;
                    }
                    else
                    {
                        targetAttr = EA_16BYTE;
                        sourceAttr = (emitAttr)GetInputSizeInBytes(id);
                    }
                }
                else if (ins is INS_vbroadcastf32x2 or INS_vbroadcasti32x2 or INS_vbroadcastsd or
                    INS_vbroadcastss or INS_vpbroadcastb or INS_vpbroadcastd or INS_vpbroadcastq or INS_vpbroadcastw)
                {
                    sourceAttr = EA_16BYTE;
                }
                else
                {
                    targetAttr = GetNarrowedDstAttr(ins, attr);
                }
                break;
            }
        }

        jitprintf(emitRegName(id.idReg1(), targetAttr));
        emitDispEmbMasking(id);
        jitprintf($", {emitRegName(id.idReg2(), sourceAttr)}");
        emitDispEmbRounding(id);
    }

    private void emitDispRegisterConstant(instrDesc id, insFormat format, ref emitAttr attr)
    {
        var ins = id.idIns();
        var targetAttr = attr;
        if (format == IF_XARCH_RWR_RRD_RRD_CNS)
        {
            jitprintf(emitRegName(id.idReg1(), attr));
            emitDispEmbMasking(id);
            jitprintf($", {emitRegName(id.idReg2(), attr)}");
            attr = ins switch
            {
                INS_vinsertf32x8 or INS_vinsertf64x4 or INS_vinserti32x8 or INS_vinserti64x4 => EA_32BYTE,
                INS_vinsertf32x4 or INS_vinsertf64x2 or INS_vinserti32x4 or INS_vinserti64x2 => EA_16BYTE,
                INS_pinsrb or INS_pinsrw or INS_pinsrd => EA_4BYTE,
                INS_pinsrq => EA_8BYTE,
                _ => attr,
            };
            jitprintf($", {emitRegName(id.idReg3(), attr)}");
            emitDispConstant(id);
            return;
        }

        switch (ins)
        {
            case INS_vextractf32x4:
            case INS_vextractf64x2:
            case INS_vextracti32x4:
            case INS_vextracti64x2:
            {
                targetAttr = EA_16BYTE;
                break;
            }

            case INS_vextractf32x8:
            case INS_vextractf64x4:
            case INS_vextracti32x8:
            case INS_vextracti64x4:
            {
                targetAttr = EA_32BYTE;
                break;
            }

            case INS_extractps:
            case INS_pextrb:
            case INS_pextrw:
            case INS_pextrd:
            {
                targetAttr = EA_4BYTE;
                break;
            }

            case INS_pextrq:
            {
                targetAttr = EA_8BYTE;
                break;
            }

            case INS_pinsrb:
            case INS_pinsrw:
            case INS_pinsrd:
            {
                attr = EA_4BYTE;
                break;
            }

            case INS_pinsrq:
            {
                attr = EA_8BYTE;
                break;
            }

            case INS_vcvtps2ph:
            {
                targetAttr = (emitAttr)Math.Max(16, EA_SIZE_IN_BYTES(attr) / 2);
                break;
            }
        }
        jitprintf(emitRegName(id.idReg1(), targetAttr));
        emitDispEmbMasking(id);
        jitprintf($", {emitRegName(id.idReg2(), attr)}");
        emitDispConstant(id);
    }
}
