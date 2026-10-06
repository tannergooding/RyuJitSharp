// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_S_R(instruction ins, emitAttr attr, regNumber reg, int varNum, int offset)
    {
#if TARGET_ARM
        recordArm32InsSR(ins, attr, reg, varNum, offset);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Target local-stack store recording is not implemented.");
#endif
    }
}
#endif
