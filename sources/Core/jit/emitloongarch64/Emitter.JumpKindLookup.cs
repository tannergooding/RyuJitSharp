// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    public static emitJumpKind emitInsToJumpKind(instruction ins)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED, "emitInsToJumpKind-----unimplemented on LOONGARCH64 yet----");
    }

    public void emitIns_J_R(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED, "emitIns_J_R-----unimplemented/unused on LOONGARCH64 yet----");
    }
}
#endif
