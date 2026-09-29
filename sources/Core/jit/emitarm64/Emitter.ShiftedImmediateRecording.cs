// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_I_I(instruction ins, emitAttr attr, regNumber reg, nint imm1, nint imm2,
        insOpts opt = INS_OPTS_NONE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        nuint immOut = 0;
        switch (ins)
        {
            case INS_mov:
            {
                ins = INS_movz;
                goto case INS_movz;
            }

            case INS_movk:
            case INS_movn:
            case INS_movz:
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg));
                assert(isValidUimm(imm1, 16));
                assert(insOptsLSL(opt));
                if (size == EA_8BYTE)
                {
                    assert((imm2 == 0) || (imm2 == 16) || (imm2 == 32) || (imm2 == 48));
                }
                else
                {
                    assert((imm2 == 0) || (imm2 == 16));
                }

                halfwordImm hwi;
                hwi.immHWVal = 0;
                bool canEncode;
                switch (imm2)
                {
                    case 0:
                    {
                        hwi.immHW = 0;
                        canEncode = true;
                        break;
                    }

                    case 16:
                    {
                        hwi.immHW = 1;
                        canEncode = true;
                        break;
                    }

                    case 32:
                    {
                        hwi.immHW = 2;
                        canEncode = true;
                        break;
                    }

                    case 48:
                    {
                        hwi.immHW = 3;
                        canEncode = true;
                        break;
                    }

                    default:
                    {
                        canEncode = false;
                        break;
                    }
                }

                if (canEncode)
                {
                    hwi.immVal = unchecked((uint)imm1);
                    immOut = hwi.immHWVal;
                    assert(isValidImmHWVal(immOut, size));
                    fmt = IF_DI_1B;
                }
                break;
            }

            default:
            {
                emitInsSve_R_I_I(ins, attr, reg, imm1, imm2, opt);
                return;
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrSC(attr, unchecked((nint)immOut));
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg);

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idFlags = gtFlags;
        debugInfo.idMemCookie = unchecked((nint)targetHandle);
#endif
        dispIns(id);
        appendToCurIG(id);
    }

    private static void emitInsSve_R_I_I(instruction ins, emitAttr attr, regNumber reg,
        nint imm1, nint imm2, insOpts opt)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE register/two-immediate instruction recording is not ported.");
}
#endif
