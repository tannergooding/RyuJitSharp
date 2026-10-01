// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_SETCC(GenCondition condition, var_types type, regNumber dstReg)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM condition-result instruction generation is not ported.");
    }
}
#endif
