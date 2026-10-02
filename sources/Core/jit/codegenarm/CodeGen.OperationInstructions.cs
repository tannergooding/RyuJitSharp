// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction genGetInsForOper(genTreeOps oper, var_types type)
    {
        if (varTypeIsFloating(type))
        {
            return ins_MathOp(oper, type);
        }

        var ins = INS_invalid;
        switch (oper)
        {
            case GT_ADD:
            {
                ins = INS_add;
                break;
            }

            case GT_AND:
            {
                ins = INS_and;
                break;
            }

            case GT_AND_NOT:
            {
                ins = INS_bic;
                break;
            }

            case GT_MUL:
            {
                ins = INS_mul;
                break;
            }

#if !USE_HELPERS_FOR_INT_DIV
            case GT_DIV:
            {
                ins = INS_sdiv;
                break;
            }

            case GT_UDIV:
            {
                ins = INS_udiv;
                break;
            }
#endif

            case GT_LSH:
            {
                ins = INS_lsl;
                break;
            }

            case GT_NEG:
            {
                ins = INS_rsb;
                break;
            }

            case GT_NOT:
            {
                ins = INS_mvn;
                break;
            }

            case GT_OR:
            {
                ins = INS_orr;
                break;
            }

            case GT_RSH:
            {
                ins = INS_asr;
                break;
            }

            case GT_RSZ:
            {
                ins = INS_lsr;
                break;
            }

            case GT_SUB:
            {
                ins = INS_sub;
                break;
            }

            case GT_XOR:
            {
                ins = INS_eor;
                break;
            }

            case GT_ROR:
            {
                ins = INS_ror;
                break;
            }

            case GT_ADD_LO:
            {
                ins = INS_add;
                break;
            }

            case GT_ADD_HI:
            {
                ins = INS_adc;
                break;
            }

            case GT_SUB_LO:
            {
                ins = INS_sub;
                break;
            }

            case GT_SUB_HI:
            {
                ins = INS_sbc;
                break;
            }

            case GT_LSH_HI:
            {
                ins = INS_lsl;
                break;
            }

            case GT_RSH_LO:
            {
                ins = INS_lsr;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return ins;
    }
}
#endif
