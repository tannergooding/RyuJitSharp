// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_ARM
    public void unwindNop(uint codeSizeInBytes)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind NOP encoding is not ported.");
    }
#elif TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
    public void unwindNop()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Unwind NOP encoding for this target is not ported.");
    }
#endif
}
