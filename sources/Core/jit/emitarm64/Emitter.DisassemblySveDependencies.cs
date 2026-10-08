// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    // These implementations remain in emitarm64sve.cpp, outside the ARM64 formatter slice.
    private static void emitDispInsSveHelp(instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitDispInsSveHelp is not ported.");
    }

    private static double emitDecodeSmallFloatImm(nint imm, instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 emitDecodeSmallFloatImm is not ported.");
    }

#if DEBUG || LATE_DISASM
    private static void getInsSveExecutionCharacteristics(instrDesc id, ref insExecutionCharacteristics result)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 getInsSveExecutionCharacteristics is not ported.");
    }
#endif
}
#endif
