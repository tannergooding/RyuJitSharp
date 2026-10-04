// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_ARM
    public void unwindBegProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind prolog recording is not ported.");
    }

    public void unwindEndProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind prolog recording is not ported.");
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 frame-register unwind recording is not ported.");
    }

    public void unwindReserve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind reservation is not ported.");
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind emission is not ported.");
    }

    public void unwindNop(uint codeSizeInBytes)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind NOP encoding is not ported.");
    }
#elif TARGET_ARM64
    public void unwindBegProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind prolog recording is not ported.");
    }

    public void unwindEndProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind prolog recording is not ported.");
    }

    public void unwindReserve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind reservation is not ported.");
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind emission is not ported.");
    }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
    public void unwindNop()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Unwind NOP encoding for this target is not ported.");
    }
#endif
}
