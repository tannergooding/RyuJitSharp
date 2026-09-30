// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_LOONGARCH64
    public const regNumber REG_FLOATRET = REG_F0;
#else
    public const regNumber REG_FLOATRET = REG_FA0;
#endif
}
#endif
