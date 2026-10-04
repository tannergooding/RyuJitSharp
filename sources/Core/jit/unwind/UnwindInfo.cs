// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public sealed class UnwindInfo
{
#if DEBUG
    public bool uwiAddingNOP;
#endif

    public UnwindInfo()
    {
        throw new FatalJitException(CorJitResult.CORJIT_SKIPPED, "UnwindInfo construction is not ported.");
    }

    public void InitUnwindInfo(Compiler compiler, emitLocation? startLoc, emitLocation? endLoc)
    {
        throw new FatalJitException(CorJitResult.CORJIT_SKIPPED, "UnwindInfo::InitUnwindInfo is not ported.");
    }

    public emitLocation? GetCurrentEmitterLocation()
    {
        throw new FatalJitException(CorJitResult.CORJIT_SKIPPED, "UnwindInfo::GetCurrentEmitterLocation is not ported.");
    }

    public void AddCode(byte b1)
    {
        throw new FatalJitException(CorJitResult.CORJIT_SKIPPED, "UnwindInfo::AddCode is not ported.");
    }

    public void AddCode(byte b1, byte b2)
    {
        throw new FatalJitException(CorJitResult.CORJIT_SKIPPED, "UnwindInfo::AddCode is not ported.");
    }

    public void AddCode(byte b1, byte b2, byte b3, byte b4)
    {
        throw new FatalJitException(CorJitResult.CORJIT_SKIPPED, "UnwindInfo::AddCode is not ported.");
    }
}
#endif
