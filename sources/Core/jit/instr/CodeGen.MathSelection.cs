// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction ins_MathOp(genTreeOps oper, var_types type)
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
}
#endif
