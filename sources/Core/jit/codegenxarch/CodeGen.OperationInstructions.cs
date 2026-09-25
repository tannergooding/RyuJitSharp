// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction genGetInsForOper(genTreeOps oper, var_types type)
    {
        assert(!varTypeIsSimd(type));
        if (varTypeIsFloating(type))
        {
            return ins_MathOp(oper, type);
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

            case GT_LSH:
            {
                return INS_shl;
            }

            case GT_MUL:
            {
                return INS_imul;
            }

            case GT_NEG:
            {
                return INS_neg;
            }

            case GT_NOT:
            {
                return INS_not;
            }

            case GT_OR:
            {
                return INS_or;
            }

            case GT_ROL:
            {
                return INS_rol;
            }

            case GT_ROR:
            {
                return INS_ror;
            }

            case GT_RSH:
            {
                return INS_sar;
            }

            case GT_RSZ:
            {
                return INS_shr;
            }

            case GT_SUB:
            {
                return INS_sub;
            }

            case GT_XOR:
            {
                return INS_xor;
            }

#if !TARGET_64BIT
            case GT_ADD_LO:
            {
                return INS_add;
            }

            case GT_ADD_HI:
            {
                return INS_adc;
            }

            case GT_SUB_LO:
            {
                return INS_sub;
            }

            case GT_SUB_HI:
            {
                return INS_sbb;
            }

            case GT_LSH_HI:
            {
                return INS_shld;
            }

            case GT_RSH_LO:
            {
                return INS_shrd;
            }
#endif

            default:
            {
                unreached();
                return INS_invalid;
            }
        }
    }
}
#endif
