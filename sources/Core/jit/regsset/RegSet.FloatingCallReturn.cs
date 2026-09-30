// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial struct RegSet
{
    public void rsSpillFPStack(GenTreeCall call)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "x86 floating-point call return stack spilling is not yet ported.");
    }
}
#endif
