// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class CodeGen
{
    public regNumber rsGetRsvdReg()
    {
        if (Configuration.TARGET_ARM64)
        {
            return rsGetRsvdRegArm64();
        }

        if (Configuration.TARGET_RISCV64)
        {
            noway_assert((_regSet.rsMaskResvd & new regMaskTP(SRBM_OPT_RSVD_RISCV64)).IsNonEmpty);

            return REG_OPT_RSVD_RISCV64;
        }

        throw new FatalJitException(CORJIT_SKIPPED, Configuration.TARGET_ARM
            ? "ARM32 reserved-register selection is not ported."
            : "Reserved-register selection is not ported for this target.");
    }

    public regNumber rsGetRsvdRegArm64()
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "The reserved ARM64 code-generation register requires ARM64.");
        }

        var reserved = REG_ARM64_IP1;
        noway_assert((_regSet.rsMaskResvd & regMaskTP.CreateFromRegNum(reserved, reserved.SingleTypeMask)).IsNonEmpty);

        return reserved;
    }
}
