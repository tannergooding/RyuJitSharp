// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    public regNumber emitInsBinary(instruction ins, emitAttr attr, GenTree dst, GenTree src)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 binary instruction emission is not ported.");
    }

    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int imm,
        insFlags flags, insOpts opt = INS_OPTS_NONE)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 register-register-immediate instruction emission is not ported.");
    }

    public void emitIns_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber reg3,
        int imm, insFlags flags = INS_FLAGS_DONT_CARE, insOpts opt = INS_OPTS_NONE)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 register-register-register-immediate instruction emission is not ported.");
    }
#endif

    public regNumber emitInsTernary(instruction ins, emitAttr attr, GenTree dst, GenTree src1, GenTree src2)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM ternary instruction emission is not ported.");
    }
}
#endif
