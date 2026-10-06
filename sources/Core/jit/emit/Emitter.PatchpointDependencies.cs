// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    public void emitIns_R(instruction ins, emitAttr attr, regNumber reg)
    {
        recordArm32InsR(ins, attr, reg);
    }
#else
    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, nint imm)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target two-register-immediate instruction recording is not implemented.");
    }
#endif

#if TARGET_RISCV64
    public void emitLoadImmediateAddress(emitAttr attr, regNumber reg, nint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 address constant materialization is not ported.");
    }
#endif
}
#endif
