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
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 instruction-to-jump-kind mapping is not implemented.");
    }
}
#endif
