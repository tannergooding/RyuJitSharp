// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction genGetInsForOper(genTreeOps oper, var_types type)
    {
        var ins = INS_invalid;

        if (varTypeIsFloating(type))
        {
            switch (oper)
            {
                case GT_ADD:
                {
                    ins = INS_fadd;
                    break;
                }

                case GT_SUB:
                {
                    ins = INS_fsub;
                    break;
                }

                case GT_MUL:
                {
                    ins = INS_fmul;
                    break;
                }

                case GT_DIV:
                {
                    ins = INS_fdiv;
                    break;
                }

                case GT_NEG:
                {
                    ins = INS_fneg;
                    break;
                }

                default:
                {
                    NYI("Unhandled operator in genGetInsForOper() - float");
                    unreached();
                    break;
                }
            }
        }
        else
        {
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

                case GT_MUL:
                {
                    ins = INS_mul;
                    break;
                }

                case GT_LSH:
                {
                    ins = INS_lsl;
                    break;
                }

                case GT_NEG:
                {
                    ins = INS_neg;
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

                case GT_OR_NOT:
                {
                    ins = INS_orn;
                    break;
                }

                case GT_ROR:
                {
                    ins = INS_ror;
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

                case GT_XOR_NOT:
                {
                    ins = INS_eon;
                    break;
                }

                default:
                {
                    NYI("Unhandled operator in genGetInsForOper() - integer");
                    unreached();
                    break;
                }
            }
        }

        return ins;
    }

    public static insOpts ShiftOpToInsOpts(genTreeOps shiftOp)
    {
        switch (shiftOp)
        {
            case GT_LSH:
            {
                return INS_OPTS_LSL;
            }

            case GT_RSH:
            {
                return INS_OPTS_ASR;
            }

            case GT_RSZ:
            {
                return INS_OPTS_LSR;
            }

            default:
            {
                NO_WAY("expected a shift-op");
                return INS_OPTS_NONE;
            }
        }
    }
}
#endif
