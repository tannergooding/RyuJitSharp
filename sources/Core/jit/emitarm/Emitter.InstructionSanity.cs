// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM && DEBUG
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitInsSanityCheck(instrDesc id)
    {
        switch (id.idInsFmt())
        {
            case IF_T1_A:
            case IF_T2_A:
            {
                break;
            }

            case IF_T1_B:
            case IF_T2_B:
            {
                assert(emitGetInsSC(id) < 0x10);
                break;
            }

            case IF_T1_C:
            {
                assert(isLowRegister(id.idReg1()));
                assert(isLowRegister(id.idReg2()));
                assert(insUnscaleImm(id.idIns(), unchecked((int)emitGetInsSC(id))) < 0x20);
                break;
            }

            case IF_T1_D0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                break;
            }

            case IF_T1_D1:
            {
                assert(isGeneralRegister(id.idReg1()));
                break;
            }

            case IF_T1_D2:
            {
                assert(isGeneralRegister(id.idReg3()));
                break;
            }

            case IF_T1_E:
            {
                assert(isLowRegister(id.idReg1()));
                assert(isLowRegister(id.idReg2()));
                assert(id.idSmallCns() < 0x20);
                break;
            }

            case IF_T1_F:
            {
                assert(id.idReg1() == REG_SP);
                assert(id.idOpSize() == EA_4BYTE);
                assert((emitGetInsSC(id) & ~0x1FC) == 0);
                break;
            }

            case IF_T1_G:
            {
                assert(isLowRegister(id.idReg1()));
                assert(isLowRegister(id.idReg2()));
                assert(id.idSmallCns() < 0x8);
                break;
            }

            case IF_T1_H:
            {
                assert(isLowRegister(id.idReg1()));
                assert(isLowRegister(id.idReg2()));
                assert(isLowRegister(id.idReg3()));
                break;
            }

            case IF_T1_I:
            {
                assert(isLowRegister(id.idReg1()));
                break;
            }

            case IF_T1_J0:
            case IF_T1_J1:
            {
                assert(isLowRegister(id.idReg1()));
                assert(emitGetInsSC(id) < 0x100);
                break;
            }

            case IF_T1_J2:
            {
                assert(isLowRegister(id.idReg1()));
                assert(id.idReg2() == REG_SP);
                assert(id.idOpSize() == EA_4BYTE);
                assert((emitGetInsSC(id) & ~0x3FC) == 0);
                break;
            }

            case IF_T1_L0:
            {
                assert(emitGetInsSC(id) < 0x100);
                break;
            }

            case IF_T1_L1:
            {
                assert(emitGetInsSC(id) < 0x400);
                break;
            }

            case IF_T2_C0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(emitGetInsSC(id) < 0x20);
                break;
            }

            case IF_T2_C4:
            case IF_T2_C5:
            case IF_T2_G1:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                break;
            }

            case IF_T2_C1:
            case IF_T2_C2:
            case IF_T2_C8:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(emitGetInsSC(id) < 0x20);
                break;
            }

            case IF_T2_C6:
            case IF_T2_C7:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(emitGetInsSC(id) < 0x4);
                break;
            }

            case IF_T2_C3:
            case IF_T2_C9:
            case IF_T2_C10:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                break;
            }

            case IF_T2_D0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(emitGetInsSC(id) < 0x400);
                break;
            }

            case IF_T2_D1:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(emitGetInsSC(id) < 0x400);
                break;
            }

            case IF_T2_E0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                if (id.idIsLclVar())
                {
                    assert(isGeneralRegister(codeGen.rsGetRsvdReg()));
                }
                else
                {
                    assert(isGeneralRegister(id.idReg3()));
                    assert(emitGetInsSC(id) < 0x4);
                }
                break;
            }

            case IF_T2_E1:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                break;
            }

            case IF_T2_E2:
            {
                assert(isGeneralRegister(id.idReg1()));
                break;
            }

            case IF_T2_F1:
            case IF_T2_F2:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                break;
            }

            case IF_T2_G0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(unsigned_abs(unchecked((int)emitGetInsSC(id))) < 0x100);
                break;
            }

            case IF_T2_H0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(unsigned_abs(unchecked((int)emitGetInsSC(id))) < 0x100);
                break;
            }

            case IF_T2_H1:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(emitGetInsSC(id) < 0x100);
                break;
            }

            case IF_T2_H2:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(emitGetInsSC(id) < 0x100);
                break;
            }

            case IF_T2_I0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(emitGetInsSC(id) < 0x10000);
                break;
            }

            case IF_T2_N:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(!id.idIsReloc());
                break;
            }

            case IF_T2_N2:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(unchecked((uint)emitGetInsSC(id)) < emitDataSize());
                break;
            }

            case IF_T2_N3:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(id.idIsReloc());
                break;
            }

            case IF_T2_I1:
            {
                assert(emitGetInsSC(id) < 0x10000);
                break;
            }

            case IF_T2_K1:
            case IF_T2_M0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(emitGetInsSC(id) < 0x1000);
                break;
            }

            case IF_T2_L0:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isModImmConst(unchecked((int)emitGetInsSC(id))));
                break;
            }

            case IF_T2_K4:
            case IF_T2_M1:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(id.idReg2() == REG_PC);
                assert(emitGetInsSC(id) < 0x1000);
                break;
            }

            case IF_T2_K3:
            {
                assert(id.idReg1() == REG_PC);
                assert(emitGetInsSC(id) < 0x1000);
                break;
            }

            case IF_T2_K2:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(emitGetInsSC(id) < 0x1000);
                break;
            }

            case IF_T2_L1:
            case IF_T2_L2:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(isModImmConst(unchecked((int)emitGetInsSC(id))));
                break;
            }

            case IF_T1_J3:
            {
                assert(isGeneralRegister(id.idReg1()));
                assert(id.idReg2() == REG_PC);
                assert(emitGetInsSC(id) < 0x100);
                break;
            }

            case IF_T1_K:
            case IF_T1_M:
            case IF_T2_J1:
            case IF_T2_J2:
            case IF_T2_N1:
            case IF_T2_J3:
            case IF_LARGEJMP:
            {
                break;
            }

            case IF_T2_VFP3:
            {
                if (id.idOpSize() == EA_8BYTE)
                {
                    assert(isDoubleReg(id.idReg1()));
                    assert(isDoubleReg(id.idReg2()));
                    assert(isDoubleReg(id.idReg3()));
                }
                else
                {
                    assert(id.idOpSize() == EA_4BYTE);
                    assert(isFloatReg(id.idReg1()));
                    assert(isFloatReg(id.idReg2()));
                    assert(isFloatReg(id.idReg3()));
                }
                break;
            }

            case IF_T2_VFP2:
            {
                assert(isFloatReg(id.idReg1()));
                assert(isFloatReg(id.idReg2()));
                break;
            }

            case IF_T2_VLDST:
            {
                if (id.idOpSize() == EA_8BYTE)
                {
                    assert(isDoubleReg(id.idReg1()));
                }
                else
                {
                    assert(isFloatReg(id.idReg1()));
                }
                assert(isGeneralRegister(id.idReg2()));
                assert(offsetFitsInVectorMem(unchecked((int)emitGetInsSC(id))));
                break;
            }

            case IF_T2_VMOVD:
            {
                assert(id.idOpSize() == EA_8BYTE);
                if (id.idIns() == INS_vmov_d2i)
                {
                    assert(isGeneralRegister(id.idReg1()));
                    assert(isGeneralRegister(id.idReg2()));
                    assert(isDoubleReg(id.idReg3()));
                }
                else
                {
                    assert(id.idIns() == INS_vmov_i2d);
                    assert(isDoubleReg(id.idReg1()));
                    assert(isGeneralRegister(id.idReg2()));
                    assert(isGeneralRegister(id.idReg3()));
                }
                break;
            }

            case IF_T2_VMOVS:
            {
                assert(id.idOpSize() == EA_4BYTE);
                if (id.idIns() == INS_vmov_i2f)
                {
                    assert(isFloatReg(id.idReg1()));
                    assert(isGeneralRegister(id.idReg2()));
                }
                else
                {
                    assert(id.idIns() == INS_vmov_f2i);
                    assert(isGeneralRegister(id.idReg1()));
                    assert(isFloatReg(id.idReg2()));
                }
                break;
            }

            default:
            {
                jitprintf($"unexpected format {emitIfName(id.idInsFmt())}\n");
                assert(false, "Unexpected format");
                break;
            }
        }
    }
}
#endif
