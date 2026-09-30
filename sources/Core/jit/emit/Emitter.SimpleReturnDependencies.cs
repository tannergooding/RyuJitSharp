// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target two-register instruction recording is not implemented.");
    }

#if TARGET_RISCV64
    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber reg3)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V three-register instruction recording is not implemented.");
    }
#endif
}
#endif
