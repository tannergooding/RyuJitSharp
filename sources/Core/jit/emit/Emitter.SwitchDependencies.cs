// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_LOONGARCH64
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, regNumber addrReg,
        CORINFO_FIELD_HANDLE fieldHandle, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 embedded-data instruction recording is not ported.");
    }
#else
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, regNumber addrReg,
        CORINFO_FIELD_HANDLE fieldHandle)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 embedded-data instruction recording is not ported.");
    }
#endif

    public void emitIns_R_L(instruction ins, emitAttr attr, BasicBlock target, regNumber reg)
    {
#if TARGET_LOONGARCH64
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 block-relative address recording is not ported.");
#else
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 block-relative address recording is not ported.");
#endif
    }
}
#endif
