// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_ARM
    private regMaskTP genStackAllocRegisterMask(uint frameSize, regMaskTP maskCalleeSavedFloat)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM stack allocation register mask is not ported.");
    }
#endif

#if TARGET_ARM64
    private void genUnknownSizeFrame()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unknown-size frame setup is not ported.");
    }
#endif
}
#endif
