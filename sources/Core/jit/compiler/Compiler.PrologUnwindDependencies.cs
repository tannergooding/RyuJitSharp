// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_ARM
    public void unwindPushMaskInt(regMaskTP mask)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM integer push-mask unwind recording is not ported.");
    }
#endif

    public void unwindPadding()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target prolog unwind padding is not ported.");
    }
}
#endif
