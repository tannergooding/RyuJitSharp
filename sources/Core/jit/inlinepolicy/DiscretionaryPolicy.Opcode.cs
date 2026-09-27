// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class DiscretionaryPolicy
{
    protected void ComputeOpcodeBin(OPCODE opcode)
    {
        unchecked
        {
            switch (opcode)
            {
                case CEE_LDARG_0:
                case CEE_LDARG_1:
                case CEE_LDARG_2:
                case CEE_LDARG_3:
                case CEE_LDARG_S:
                case CEE_LDARG:
                case CEE_STARG_S:
                case CEE_STARG:
                {
                    _argAccessCount++;
                    break;
                }

                case CEE_LDLOC_0:
                case CEE_LDLOC_1:
                case CEE_LDLOC_2:
                case CEE_LDLOC_3:
                case CEE_LDLOC_S:
                case CEE_STLOC_0:
                case CEE_STLOC_1:
                case CEE_STLOC_2:
                case CEE_STLOC_3:
                case CEE_STLOC_S:
                case CEE_LDLOC:
                case CEE_STLOC:
                {
                    _localAccessCount++;
                    break;
                }

                case CEE_LDNULL:
                case CEE_LDC_I4_M1:
                case CEE_LDC_I4_0:
                case CEE_LDC_I4_1:
                case CEE_LDC_I4_2:
                case CEE_LDC_I4_3:
                case CEE_LDC_I4_4:
                case CEE_LDC_I4_5:
                case CEE_LDC_I4_6:
                case CEE_LDC_I4_7:
                case CEE_LDC_I4_8:
                case CEE_LDC_I4_S:
                {
                    _intConstantCount++;
                    break;
                }

                case CEE_LDC_R4:
                case CEE_LDC_R8:
                {
                    _floatConstantCount++;
                    break;
                }

                case CEE_LDIND_I1:
                case CEE_LDIND_U1:
                case CEE_LDIND_I2:
                case CEE_LDIND_U2:
                case CEE_LDIND_I4:
                case CEE_LDIND_U4:
                case CEE_LDIND_I8:
                case CEE_LDIND_I:
                {
                    _intLoadCount++;
                    break;
                }

                case CEE_LDIND_R4:
                case CEE_LDIND_R8:
                {
                    _floatLoadCount++;
                    break;
                }

                case CEE_STIND_I1:
                case CEE_STIND_I2:
                case CEE_STIND_I4:
                case CEE_STIND_I8:
                case CEE_STIND_I:
                {
                    _intStoreCount++;
                    break;
                }

                case CEE_STIND_R4:
                case CEE_STIND_R8:
                {
                    _floatStoreCount++;
                    break;
                }

                case CEE_SUB:
                case CEE_AND:
                case CEE_OR:
                case CEE_XOR:
                case CEE_SHL:
                case CEE_SHR:
                case CEE_SHR_UN:
                case CEE_NEG:
                case CEE_NOT:
                case CEE_CONV_I1:
                case CEE_CONV_I2:
                case CEE_CONV_I4:
                case CEE_CONV_I8:
                case CEE_CONV_U4:
                case CEE_CONV_U8:
                case CEE_CONV_U2:
                case CEE_CONV_U1:
                case CEE_CONV_I:
                case CEE_CONV_U:
                {
                    _simpleMathCount++;
                    break;
                }

                case CEE_MUL:
                case CEE_DIV:
                case CEE_DIV_UN:
                case CEE_REM:
                case CEE_REM_UN:
                case CEE_CONV_R4:
                case CEE_CONV_R8:
                case CEE_CONV_R_UN:
                {
                    _complexMathCount++;
                    break;
                }

                case CEE_CONV_OVF_I1_UN:
                case CEE_CONV_OVF_I2_UN:
                case CEE_CONV_OVF_I4_UN:
                case CEE_CONV_OVF_I8_UN:
                case CEE_CONV_OVF_U1_UN:
                case CEE_CONV_OVF_U2_UN:
                case CEE_CONV_OVF_U4_UN:
                case CEE_CONV_OVF_U8_UN:
                case CEE_CONV_OVF_I_UN:
                case CEE_CONV_OVF_U_UN:
                case CEE_CONV_OVF_I1:
                case CEE_CONV_OVF_U1:
                case CEE_CONV_OVF_I2:
                case CEE_CONV_OVF_U2:
                case CEE_CONV_OVF_I4:
                case CEE_CONV_OVF_U4:
                case CEE_CONV_OVF_I8:
                case CEE_CONV_OVF_U8:
                case CEE_ADD_OVF:
                case CEE_ADD_OVF_UN:
                case CEE_MUL_OVF:
                case CEE_MUL_OVF_UN:
                case CEE_SUB_OVF:
                case CEE_SUB_OVF_UN:
                case CEE_CKFINITE:
                {
                    _overflowMathCount++;
                    break;
                }

                case CEE_LDELEM_I1:
                case CEE_LDELEM_U1:
                case CEE_LDELEM_I2:
                case CEE_LDELEM_U2:
                case CEE_LDELEM_I4:
                case CEE_LDELEM_U4:
                case CEE_LDELEM_I8:
                case CEE_LDELEM_I:
                {
                    _intArrayLoadCount++;
                    break;
                }

                case CEE_LDELEM_R4:
                case CEE_LDELEM_R8:
                {
                    _floatArrayLoadCount++;
                    break;
                }

                case CEE_LDELEM_REF:
                {
                    _refArrayLoadCount++;
                    break;
                }

                case CEE_LDELEM:
                {
                    _structArrayLoadCount++;
                    break;
                }

                case CEE_STELEM_I:
                case CEE_STELEM_I1:
                case CEE_STELEM_I2:
                case CEE_STELEM_I4:
                case CEE_STELEM_I8:
                {
                    _intArrayStoreCount++;
                    break;
                }

                case CEE_STELEM_R4:
                case CEE_STELEM_R8:
                {
                    _floatArrayStoreCount++;
                    break;
                }

                case CEE_STELEM_REF:
                {
                    _refArrayStoreCount++;
                    break;
                }

                case CEE_STELEM:
                {
                    _structArrayStoreCount++;
                    break;
                }

                case CEE_CPOBJ:
                case CEE_LDOBJ:
                case CEE_CPBLK:
                case CEE_INITBLK:
                case CEE_STOBJ:
                {
                    _structOperationCount++;
                    break;
                }

                case CEE_CASTCLASS:
                case CEE_ISINST:
                case CEE_UNBOX:
                case CEE_BOX:
                case CEE_UNBOX_ANY:
                case CEE_LDFTN:
                case CEE_LDVIRTFTN:
                case CEE_SIZEOF:
                {
                    _objectModelCount++;
                    break;
                }

                case CEE_LDFLD:
                case CEE_LDLEN:
                case CEE_REFANYTYPE:
                case CEE_REFANYVAL:
                {
                    _fieldLoadCount++;
                    break;
                }

                case CEE_STFLD:
                {
                    _fieldStoreCount++;
                    break;
                }

                case CEE_LDSFLD:
                {
                    _staticFieldLoadCount++;
                    break;
                }

                case CEE_STSFLD:
                {
                    _staticFieldStoreCount++;
                    break;
                }

                case CEE_LDELEMA:
                case CEE_LDSFLDA:
                case CEE_LDFLDA:
                case CEE_LDSTR:
                case CEE_LDARGA:
                case CEE_LDLOCA:
                {
                    _loadAddressCount++;
                    break;
                }

                case CEE_CALL:
                case CEE_CALLI:
                case CEE_CALLVIRT:
                case CEE_NEWOBJ:
                case CEE_NEWARR:
                case CEE_JMP:
                {
                    _callCount++;
                    break;
                }

                case CEE_THROW:
                case CEE_RETHROW:
                {
                    _throwCount++;
                    break;
                }

                case CEE_RET:
                {
                    _returnCount++;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }
    }
}
