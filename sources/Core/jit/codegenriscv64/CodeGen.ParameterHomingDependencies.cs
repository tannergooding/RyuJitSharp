// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public regNumber rsGetRsvdReg()
    {
        noway_assert((_regSet.rsMaskResvd & new regMaskTP(SRBM_OPT_RSVD)).IsNonEmpty);

        return REG_OPT_RSVD;
    }
}
#endif
