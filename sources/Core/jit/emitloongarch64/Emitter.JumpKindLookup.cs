// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public static emitJumpKind emitInsToJumpKindLoongArch64(instruction ins)
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LoongArch64 jump-kind mapping is not supported for this target.");
        }

        throw new FatalJitException(
            CORJIT_SKIPPED, "emitInsToJumpKind-----unimplemented on LOONGARCH64 yet----");
    }

    public void emitIns_J_R_LoongArch64(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "LoongArch64 register jump recording is not supported for this target.");
        }

        throw new FatalJitException(
            CORJIT_SKIPPED, "emitIns_J_R-----unimplemented/unused on LOONGARCH64 yet----");
    }
}
