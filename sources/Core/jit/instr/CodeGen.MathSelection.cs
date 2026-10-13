// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction ins_MathOp(genTreeOps oper, var_types type)
    {
        if (Configuration.TARGET_XARCH)
        {
            switch (oper)
            {
                case GT_ADD:
                {
                    return type == TYP_DOUBLE ? INS_addsd : INS_addss;
                }

                case GT_SUB:
                {
                    return type == TYP_DOUBLE ? INS_subsd : INS_subss;
                }

                case GT_MUL:
                {
                    return type == TYP_DOUBLE ? INS_mulsd : INS_mulss;
                }

                case GT_DIV:
                {
                    return type == TYP_DOUBLE ? INS_divsd : INS_divss;
                }

                default:
                {
                    unreached();
                    return INS_invalid;
                }
            }
        }

        if (Configuration.TARGET_ARM)
        {
            switch (oper)
            {
                case GT_ADD:
                {
                    return INS_vadd;
                }

                case GT_SUB:
                {
                    return INS_vsub;
                }

                case GT_MUL:
                {
                    return INS_vmul;
                }

                case GT_DIV:
                {
                    return INS_vdiv;
                }

                case GT_NEG:
                {
                    return INS_vneg;
                }

                default:
                {
                    unreached();
                    throw new FatalJitException("Invalid floating math operator.");
                }
            }
        }

        throw new FatalJitException(CORJIT_SKIPPED,
            "Floating-point math instruction selection is not implemented for this target.");
    }
}
