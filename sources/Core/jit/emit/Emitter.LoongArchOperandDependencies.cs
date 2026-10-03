// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber reg3)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 three-register instruction recording is not ported.");
    }

    public void emitIns_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int imm1, int imm2)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 two-register/two-immediate instruction recording is not ported.");
    }

    public void emitIns_I_I(instruction ins, emitAttr attr, nint cc, nint offs)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 condition-code/immediate instruction recording is not ported.");
    }

    public void emitIns_R_R_R_I(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        nint imm,
        insOpts opt = insOpts.INS_OPTS_NONE,
        emitAttr attrReg2 = emitAttr.EA_UNKNOWN)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED,
            "LoongArch64 three-register/immediate instruction recording is not ported.");
    }
}
#endif
