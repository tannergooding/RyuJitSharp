// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public static int emitLoadImmediate(bool doEmit, emitAttr size, regNumber reg, nint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 immediate materialization is not ported.");
    }
}
#endif
