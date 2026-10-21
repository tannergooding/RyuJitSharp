// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction genGetInsForOper(genTreeOps oper, var_types type)
    {
        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARMARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Scalar operation instruction selection requires xarch or ARM.");
            return default!;
        }

        if (Configuration.TARGET_XARCH)
        {
            assert(!varTypeIsSimd(type));
        }

        if (varTypeIsFloating(type))
        {
            if (!Configuration.TARGET_ARM64)
            {
                return ins_MathOp(oper, type);
            }

            switch (oper)
            {
                case GT_ADD:
                {
                    return INS_fadd;
                }

                case GT_SUB:
                {
                    return INS_fsub;
                }

                case GT_MUL:
                {
                    return INS_fmul;
                }

                case GT_DIV:
                {
                    return INS_fdiv;
                }

                case GT_NEG:
                {
                    return INS_fneg;
                }

                default:
                {
                    NYI("Unhandled oper in genGetInsForOper() - float");
                    unreached();
                    return INS_invalid;
                }
            }
        }

        switch (oper)
        {
            case GT_ADD:
            {
                return INS_add;
            }

            case GT_AND:
            {
                return INS_and;
            }

            case GT_AND_NOT when Configuration.TARGET_ARMARCH:
            {
                return INS_bic;
            }

            case GT_DIV when Configuration.TARGET_ARMARCH:
            case GT_UDIV when Configuration.TARGET_ARMARCH:
            {
                if (Configuration.USE_HELPERS_FOR_INT_DIV)
                {
                    unreached();
                    return INS_invalid;
                }

                return oper is GT_DIV ? INS_sdiv : INS_udiv;
            }

            case GT_LSH:
            {
                return Configuration.TARGET_XARCH ? INS_shl : INS_lsl;
            }

            case GT_MUL:
            {
                return Configuration.TARGET_XARCH ? INS_imul : INS_mul;
            }

            case GT_NEG:
            {
                return Configuration.TARGET_ARM ? INS_rsb : INS_neg;
            }

            case GT_NOT:
            {
                return Configuration.TARGET_XARCH ? INS_not : INS_mvn;
            }

            case GT_OR:
            {
                return Configuration.TARGET_XARCH ? INS_or : INS_orr;
            }

            case GT_OR_NOT when Configuration.TARGET_ARM64:
            {
                return INS_orn;
            }

            case GT_ROL when Configuration.TARGET_XARCH:
            {
                return INS_rol;
            }

            case GT_ROR:
            {
                return INS_ror;
            }

            case GT_RSH:
            {
                return Configuration.TARGET_XARCH ? INS_sar : INS_asr;
            }

            case GT_RSZ:
            {
                return Configuration.TARGET_XARCH ? INS_shr : INS_lsr;
            }

            case GT_SUB:
            {
                return INS_sub;
            }

            case GT_XOR:
            {
                return Configuration.TARGET_XARCH ? INS_xor : INS_eor;
            }

            case GT_XOR_NOT when Configuration.TARGET_ARM64:
            {
                return INS_eon;
            }

            case GT_ADD_LO when !Configuration.TARGET_64BIT:
            {
                return INS_add;
            }

            case GT_ADD_HI when !Configuration.TARGET_64BIT:
            {
                return INS_adc;
            }

            case GT_SUB_LO when !Configuration.TARGET_64BIT:
            {
                return INS_sub;
            }

            case GT_SUB_HI when !Configuration.TARGET_64BIT:
            {
                return Configuration.TARGET_XARCH ? INS_sbb : INS_sbc;
            }

            case GT_LSH_HI when !Configuration.TARGET_64BIT:
            {
                return Configuration.TARGET_XARCH ? INS_shld : INS_lsl;
            }

            case GT_RSH_LO when !Configuration.TARGET_64BIT:
            {
                return Configuration.TARGET_XARCH ? INS_shrd : INS_lsr;
            }

            default:
            {
                if (Configuration.TARGET_ARM64)
                {
                    NYI("Unhandled oper in genGetInsForOper() - integer");
                }

                unreached();
                return INS_invalid;
            }
        }
    }
}
