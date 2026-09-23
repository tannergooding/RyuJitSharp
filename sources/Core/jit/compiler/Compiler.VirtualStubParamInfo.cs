// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public sealed class VirtualStubParamInfo
    {
        public VirtualStubParamInfo()
        {
#if TARGET_X86
            Reg = REG_EAX;
            RegMask = RBM_EAX;
#elif TARGET_AMD64
            Reg = REG_R11;
            RegMask = RBM_R11;
#elif TARGET_ARM
            Reg = REG_R12;
            RegMask = RBM_R12;
#elif TARGET_ARM64
            Reg = REG_R11;
            RegMask = RBM_R11;
#elif TARGET_LOONGARCH64
            Reg = REG_T8;
            RegMask = RBM_T8;
#elif TARGET_RISCV64
            Reg = REG_T5;
            RegMask = RBM_T5;
#elif TARGET_WASM
            Reg = REG_NA;
            RegMask = RBM_NONE;
#else
#error Unsupported or unset target architecture
#endif
        }

        public regNumber Reg { get; }

        public regMaskTP RegMask { get; }
    }
}
