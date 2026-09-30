// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static instruction ins_MathOp(genTreeOps oper, var_types type)
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
}
#endif
