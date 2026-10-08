// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_XARCH && !TARGET_ARMARCH && !TARGET_WASM && !TARGET_RISCV64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCall(GenTreeCall call)
    {
#if TARGET_LOONGARCH64
        genCallLoongArch64(call);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Call generation requires xarch.");
#endif
    }

    public unsafe void genCallInstruction(GenTreeCall call, int stackArgBytes = 0)
    {
#if TARGET_LOONGARCH64
        genCallInstructionLoongArch64(call);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Call-instruction generation requires xarch.");
#endif
    }
}
#endif
