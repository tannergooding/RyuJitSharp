// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_X86
    public unsafe bool isTrivialPointerSizedStruct(CORINFO_CLASS_HANDLE clsHnd)
    {
        NYI("Compiler::isTrivialPointerSizedStruct (compiler.cpp), x86 ABI nested value-class classification");
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "x86 trivial pointer-sized struct classification is not ported.");
    }
#endif

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    public unsafe ref CORINFO_FPSTRUCT_LOWERING GetFpStructLowering(CORINFO_CLASS_HANDLE structHandle)
    {
        NYI("Compiler::GetFpStructLowering (compiler.cpp), RISC-V64/LoongArch64 ABI runtime lowering cache");
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "RISC-V64/LoongArch64 floating-point struct lowering is not ported.");
    }
#endif
}
