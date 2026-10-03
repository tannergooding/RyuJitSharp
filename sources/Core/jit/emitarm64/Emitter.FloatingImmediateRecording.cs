// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public unsafe partial class Emitter
{
    public void emitIns_R_F(instruction ins, emitAttr attr, regNumber reg, double immDbl, insOpts opt = INS_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        nint imm = 0;
        var canEncode = false;
        switch (ins)
        {
            case INS_fcmp:
            case INS_fcmpe:
            {
                assert(insOptsNone(opt));
                assert(isValidVectorElemsizeFloat(size));
                assert(isVectorRegister(reg));
                if (immDbl == 0.0)
                {
                    canEncode = true;
                    fmt = IF_DV_1C;
                }
                break;
            }

            case INS_fmov:
            {
                assert(isVectorRegister(reg));
                floatImm8 fpi;
                fpi.immFPIVal = 0;
                canEncode = canEncodeFloatImm8(immDbl, &fpi);

                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    var elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(opt != INS_OPTS_1D);
                    if (canEncode)
                    {
                        imm = (nint)fpi.immFPIVal;
                        assert((imm >= 0) && (imm <= 0xff));
                        fmt = IF_DV_1B;
                    }
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidVectorElemsizeFloat(size));
                    if (canEncode)
                    {
                        imm = (nint)fpi.immFPIVal;
                        assert((imm >= 0) && (imm <= 0xff));
                        fmt = IF_DV_1A;
                    }
                }
                break;
            }

            default:
            {
                emitInsSve_R_F(ins, attr, reg, immDbl, opt);
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

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_F(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        double immDbl, insOpts opt = INS_OPTS_NONE)
    {
        emitInsSve_R_R_F(ins, attr, reg1, reg2, immDbl, opt);
    }

    private struct floatImm8
    {
        public uint immFPIVal;

        public uint immMant
        {
            readonly get
            {
                return immFPIVal & 0xF;
            }
            set
            {
                immFPIVal = (immFPIVal & ~0xFu) | (value & 0xF);
            }
        }

        public uint immExp
        {
            readonly get
            {
                return (immFPIVal >> 4) & 7;
            }
            set
            {
                immFPIVal = (immFPIVal & ~(7u << 4)) | ((value & 7) << 4);
            }
        }

        public uint immSign
        {
            readonly get
            {
                return (immFPIVal >> 7) & 1;
            }
            set
            {
                immFPIVal = (immFPIVal & ~(1u << 7)) | ((value & 1) << 7);
            }
        }
    }

    private static double emitDecodeFloatImm8(floatImm8 fpImm)
    {
        var sign = fpImm.immSign;
        var exp = fpImm.immExp ^ 4;
        var mant = fpImm.immMant + 16;
        uint scale = 16 * 8;
        while (exp > 0)
        {
            scale /= 2;
            exp--;
        }

        var result = (double)mant / scale;
        if (sign == 1)
        {
            result = -result;
        }

        return result;
    }
}
#endif
