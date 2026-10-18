// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    internal void getMemoryOperation(instrDesc id, out PerfScoreMemoryAccessKind memAccessKind, out bool isLocalAccess)
    {
        RequireArm64Target();
        var memoryAccessKind = PerfScoreMemoryAccessKind.None;
        var localAccess = false;
        var ins = id.idIns();

        if (emitInsIsArmLoadOrStore(ins))
        {
            if (emitInsIsLoadArm64(ins))
            {
                if (emitInsIsStore(ins))
                {
                    memoryAccessKind = PerfScoreMemoryAccessKind.ReadWrite;
                }
                else
                {
                    memoryAccessKind = PerfScoreMemoryAccessKind.Read;
                }
            }
            else
            {
                assert(emitInsIsStore(ins));
                memoryAccessKind = PerfScoreMemoryAccessKind.Write;
            }

            switch (id.idInsFmt())
            {
                case IF_ARM64_LS_1A:
                {
                    localAccess = true;
                    break;
                }

                case IF_ARM64_LS_2A:
                case IF_ARM64_LS_2B:
                case IF_ARM64_LS_2C:
                case IF_ARM64_LS_2D:
                case IF_ARM64_LS_2E:
                case IF_ARM64_LS_2F:
                case IF_ARM64_LS_2G:
                case IF_ARM64_LS_3A:
                case IF_ARM64_LS_3F:
                case IF_ARM64_LS_3G:
                case IF_ARM64_SVE_ID_2A:
                case IF_ARM64_SVE_IE_2A:
                case IF_ARM64_SVE_JG_2A:
                case IF_ARM64_SVE_JH_2A:
                {
                    if (isStackRegister(id.idReg2()))
                    {
                        localAccess = true;
                    }

                    break;
                }

                case IF_ARM64_LS_3B:
                case IF_ARM64_LS_3C:
                case IF_ARM64_LS_3D:
                case IF_ARM64_LS_3E:
                case IF_ARM64_SVE_HW_4A:
                case IF_ARM64_SVE_HW_4A_A:
                case IF_ARM64_SVE_HW_4A_B:
                case IF_ARM64_SVE_HW_4A_C:
                case IF_ARM64_SVE_HW_4B:
                case IF_ARM64_SVE_HW_4B_D:
                case IF_ARM64_SVE_IC_3A:
                case IF_ARM64_SVE_IC_3A_A:
                case IF_ARM64_SVE_IC_3A_B:
                case IF_ARM64_SVE_IC_3A_C:
                case IF_ARM64_SVE_IG_4A:
                case IF_ARM64_SVE_IG_4A_D:
                case IF_ARM64_SVE_IG_4A_E:
                case IF_ARM64_SVE_IG_4A_F:
                case IF_ARM64_SVE_IG_4A_G:
                case IF_ARM64_SVE_IH_3A:
                case IF_ARM64_SVE_IH_3A_A:
                case IF_ARM64_SVE_IH_3A_F:
                case IF_ARM64_SVE_II_4A:
                case IF_ARM64_SVE_II_4A_B:
                case IF_ARM64_SVE_II_4A_H:
                case IF_ARM64_SVE_IJ_3A:
                case IF_ARM64_SVE_IJ_3A_D:
                case IF_ARM64_SVE_IJ_3A_E:
                case IF_ARM64_SVE_IJ_3A_F:
                case IF_ARM64_SVE_IJ_3A_G:
                case IF_ARM64_SVE_IK_4A:
                case IF_ARM64_SVE_IK_4A_F:
                case IF_ARM64_SVE_IK_4A_G:
                case IF_ARM64_SVE_IK_4A_H:
                case IF_ARM64_SVE_IK_4A_I:
                case IF_ARM64_SVE_IL_3A:
                case IF_ARM64_SVE_IL_3A_A:
                case IF_ARM64_SVE_IL_3A_B:
                case IF_ARM64_SVE_IL_3A_C:
                case IF_ARM64_SVE_IM_3A:
                case IF_ARM64_SVE_IN_4A:
                case IF_ARM64_SVE_IO_3A:
                case IF_ARM64_SVE_IP_4A:
                case IF_ARM64_SVE_IQ_3A:
                case IF_ARM64_SVE_IR_4A:
                case IF_ARM64_SVE_IS_3A:
                case IF_ARM64_SVE_IT_4A:
                case IF_ARM64_SVE_IU_4A:
                case IF_ARM64_SVE_IU_4A_A:
                case IF_ARM64_SVE_IU_4A_C:
                case IF_ARM64_SVE_IU_4B:
                case IF_ARM64_SVE_IU_4B_B:
                case IF_ARM64_SVE_IU_4B_D:
                case IF_ARM64_SVE_JB_4A:
                case IF_ARM64_SVE_JC_4A:
                case IF_ARM64_SVE_JD_4A:
                case IF_ARM64_SVE_JD_4B:
                case IF_ARM64_SVE_JD_4C:
                case IF_ARM64_SVE_JD_4C_A:
                case IF_ARM64_SVE_JE_3A:
                case IF_ARM64_SVE_JF_4A:
                case IF_ARM64_SVE_JJ_4A:
                case IF_ARM64_SVE_JJ_4A_B:
                case IF_ARM64_SVE_JJ_4A_C:
                case IF_ARM64_SVE_JJ_4A_D:
                case IF_ARM64_SVE_JJ_4B:
                case IF_ARM64_SVE_JJ_4B_C:
                case IF_ARM64_SVE_JJ_4B_E:
                case IF_ARM64_SVE_JK_4A:
                case IF_ARM64_SVE_JK_4A_B:
                case IF_ARM64_SVE_JK_4B:
                case IF_ARM64_SVE_JM_3A:
                case IF_ARM64_SVE_JN_3A:
                case IF_ARM64_SVE_JN_3B:
                case IF_ARM64_SVE_JN_3C:
                case IF_ARM64_SVE_JN_3C_D:
                case IF_ARM64_SVE_JO_3A:
                {
                    if (isStackRegister(id.idReg3()))
                    {
                        localAccess = true;
                    }

                    break;
                }

                case IF_ARM64_SVE_HX_3A_B:
                case IF_ARM64_SVE_HX_3A_E:
                case IF_ARM64_SVE_IF_4A:
                case IF_ARM64_SVE_IF_4A_A:
                case IF_ARM64_SVE_IV_3A:
                case IF_ARM64_SVE_IW_4A:
                case IF_ARM64_SVE_IX_4A:
                case IF_ARM64_SVE_IY_4A:
                case IF_ARM64_SVE_IZ_4A:
                case IF_ARM64_SVE_IZ_4A_A:
                case IF_ARM64_SVE_JA_4A:
                case IF_ARM64_SVE_JI_3A_A:
                case IF_ARM64_SVE_JL_3A:
                case IF_ARM64_LARGELDC:
                {
                    localAccess = false;
                    break;
                }

                default:
                {
                    assert(false, "Logic Error");
                    memoryAccessKind = PerfScoreMemoryAccessKind.None;
                    break;
                }
            }
        }

        memAccessKind = memoryAccessKind;
        isLocalAccess = localAccess;
    }

    private static bool isStackRegister(regNumber reg)
    {
        return reg is REG_ARM64_ZR or REG_ARM64_FP;
    }
}
