// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
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
