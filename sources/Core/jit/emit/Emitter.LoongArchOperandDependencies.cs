// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int imm1, int imm2)
    {
        RequireLoongArch64Target();

        emitInsRRIIloongArch64(ins, attr, reg1, reg2, imm1, imm2, INS_OPTS_NONE);
    }

    public void emitIns_J(instruction ins, BasicBlock target, int regs)
    {
        RequireLoongArch64Target();

        emitInsJLoongArch64(ins, target, regs);
    }

    public void emitIns_I_I(instruction ins, emitAttr attr, nint cc, nint offs)
    {
        RequireLoongArch64Target();

        emitInsIILoongArch64(ins, attr, cc, offs);
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
        RequireLoongArch64Target();

        assert(attrReg2 == EA_UNKNOWN);
        emitInsRRRIloongArch64(ins, attr, reg1, reg2, reg3, imm, opt);
    }
}
