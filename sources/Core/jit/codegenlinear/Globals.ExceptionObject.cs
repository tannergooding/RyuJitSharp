// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_X86
    public const regNumber REG_EXCEPTION_OBJECT = REG_EAX;
    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_EAX;
#elif TARGET_ARM
    public const regNumber REG_EXCEPTION_OBJECT = REG_R0;
    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_R0;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
    public const regNumber REG_EXCEPTION_OBJECT = REG_A0;
    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_A0;
#else
    public const regNumber REG_EXCEPTION_OBJECT = REG_NA;
    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_NONE;
#endif
}
#endif
