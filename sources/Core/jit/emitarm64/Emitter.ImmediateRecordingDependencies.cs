// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static void emitInsSve_I(instruction ins, emitAttr attr, nint imm)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE immediate-only instruction recording is not ported.");

    public void emitInsSve_R(instruction ins, emitAttr attr, regNumber reg, insOpts opt = INS_OPTS_NONE)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE single-register instruction recording is not ported.");

    public void emitInsSve_R_I(instruction ins, emitAttr attr, regNumber reg, nint imm,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE register-immediate instruction recording is not ported.");
}
#endif
