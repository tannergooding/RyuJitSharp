// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static void emitEncodeCallGCregs(regMaskTP regmask, instrDesc id)
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Call GC register encoding is not supported for this target.");
            return;
        }

        var regs = regmask.Lower;
        uint encodeMask;

        if (Configuration.TARGET_X86)
        {
            assert(Configuration.REGNUM_BITS >= 3);
            encodeMask = 0;

            if ((regs & SRBM_X86_ESI) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_X86_EDI) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_X86_EBX) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }

            id.idReg1((regNumber)encodeMask); // Save in idReg1

        }
        else if (Configuration.TARGET_AMD64)
        {
            assert(Configuration.REGNUM_BITS >= 4);
            encodeMask = 0;

            if ((regs & SRBM_X64_ESI) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_X64_EDI) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_X64_EBX) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_X64_EBP) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }

            id.idReg1((regNumber)encodeMask); // Save in idReg1

            encodeMask = 0;

            if ((regs & SRBM_X64_R12) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_X64_R13) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_X64_R14) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_X64_R15) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }

            id.idReg2((regNumber)encodeMask); // Save in idReg2

        }
        else if (Configuration.TARGET_ARM)
        {
            assert(Configuration.REGNUM_BITS >= 4);
            encodeMask = 0;

            if ((regs & SRBM_ARM32_R4) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_ARM32_R5) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_ARM32_R6) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_ARM32_R7) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }

            id.idReg1((regNumber)encodeMask); // Save in idReg1

            encodeMask = 0;

            if ((regs & SRBM_ARM32_R8) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_ARM32_R9) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_ARM32_R10) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_ARM32_R11) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }

            id.idReg2((regNumber)encodeMask); // Save in idReg2

        }
        else if (Configuration.TARGET_ARM64)
        {
            assert(Configuration.REGNUM_BITS >= 5);
            encodeMask = 0;

            if ((regs & SRBM_ARM64_R19) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_ARM64_R20) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_ARM64_R21) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_ARM64_R22) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }
            if ((regs & SRBM_ARM64_R23) != SRBM_NONE)
            {
                encodeMask |= 0x10;
            }

            id.idReg1((regNumber)encodeMask); // Save in idReg1

            encodeMask = 0;

            if ((regs & SRBM_ARM64_R24) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_ARM64_R25) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_ARM64_R26) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_ARM64_R27) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }
            if ((regs & SRBM_ARM64_R28) != SRBM_NONE)
            {
                encodeMask |= 0x10;
            }

            id.idReg2((regNumber)encodeMask); // Save in idReg2

        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            assert(Configuration.REGNUM_BITS >= 5);
            encodeMask = 0;

            if ((regs & SRBM_LOONGARCH64_S0) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_LOONGARCH64_S1) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_LOONGARCH64_S2) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_LOONGARCH64_S3) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }
            if ((regs & SRBM_LOONGARCH64_S4) != SRBM_NONE)
            {
                encodeMask |= 0x10;
            }

            id.idReg1((regNumber)encodeMask); // Save in idReg1

            encodeMask = 0;

            if ((regs & SRBM_LOONGARCH64_S5) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_LOONGARCH64_S6) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_LOONGARCH64_S7) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_LOONGARCH64_S8) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }

            id.idReg2((regNumber)encodeMask); // Save in idReg2

        }
        else if (Configuration.TARGET_RISCV64)
        {
            assert(Configuration.REGNUM_BITS >= 6);
            encodeMask = 0;

            if ((regs & SRBM_RISCV64_S1) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_RISCV64_S2) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_RISCV64_S3) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_RISCV64_S4) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }
            if ((regs & SRBM_RISCV64_S5) != SRBM_NONE)
            {
                encodeMask |= 0x10;
            }
            if ((regs & SRBM_RISCV64_S6) != SRBM_NONE)
            {
                encodeMask |= 0x20;
            }

            id.idReg1((regNumber)encodeMask); // Save in idReg1

            encodeMask = 0;

            if ((regs & SRBM_RISCV64_S7) != SRBM_NONE)
            {
                encodeMask |= 0x01;
            }
            if ((regs & SRBM_RISCV64_S8) != SRBM_NONE)
            {
                encodeMask |= 0x02;
            }
            if ((regs & SRBM_RISCV64_S9) != SRBM_NONE)
            {
                encodeMask |= 0x04;
            }
            if ((regs & SRBM_RISCV64_S10) != SRBM_NONE)
            {
                encodeMask |= 0x08;
            }
            if ((regs & SRBM_RISCV64_S11) != SRBM_NONE)
            {
                encodeMask |= 0x10;
            }

            id.idReg2((regNumber)encodeMask); // Save in idReg2
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Call GC register encoding is not ported for this target.");
            return;
        }
    }

    private static uint emitDecodeCallGCregs(instrDesc id)
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Call GC register encoding is not supported for this target.");
            return default!;
        }

        var regmask = SRBM_NONE;
        uint encodeMask;

        if (Configuration.TARGET_X86)
        {
            assert(Configuration.REGNUM_BITS >= 3);
            encodeMask = (uint)id.idReg1();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_X86_ESI;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_X86_EDI;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_X86_EBX;
            }
        }
        else if (Configuration.TARGET_AMD64)
        {
            assert(Configuration.REGNUM_BITS >= 4);
            encodeMask = (uint)id.idReg1();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_X64_ESI;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_X64_EDI;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_X64_EBX;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_X64_EBP;
            }

            encodeMask = (uint)id.idReg2();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_X64_R12;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_X64_R13;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_X64_R14;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_X64_R15;
            }
        }
        else if (Configuration.TARGET_ARM)
        {
            assert(Configuration.REGNUM_BITS >= 4);
            encodeMask = (uint)id.idReg1();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_ARM32_R4;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_ARM32_R5;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_ARM32_R6;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_ARM32_R7;
            }

            encodeMask = (uint)id.idReg2();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_ARM32_R8;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_ARM32_R9;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_ARM32_R10;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_ARM32_R11;
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            assert(Configuration.REGNUM_BITS >= 5);
            encodeMask = (uint)id.idReg1();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_ARM64_R19;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_ARM64_R20;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_ARM64_R21;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_ARM64_R22;
            }
            if ((encodeMask & 0x10) != 0)
            {
                regmask |= SRBM_ARM64_R23;
            }

            encodeMask = (uint)id.idReg2();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_ARM64_R24;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_ARM64_R25;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_ARM64_R26;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_ARM64_R27;
            }
            if ((encodeMask & 0x10) != 0)
            {
                regmask |= SRBM_ARM64_R28;
            }
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            assert(Configuration.REGNUM_BITS >= 5);
            encodeMask = (uint)id.idReg1();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S0;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S1;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S2;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S3;
            }
            if ((encodeMask & 0x10) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S4;
            }

            encodeMask = (uint)id.idReg2();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S5;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S6;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S7;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_LOONGARCH64_S8;
            }
        }
        else if (Configuration.TARGET_RISCV64)
        {
            assert(Configuration.REGNUM_BITS >= 6);
            encodeMask = (uint)id.idReg1();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_RISCV64_S1;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_RISCV64_S2;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_RISCV64_S3;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_RISCV64_S4;
            }
            if ((encodeMask & 0x10) != 0)
            {
                regmask |= SRBM_RISCV64_S5;
            }
            if ((encodeMask & 0x20) != 0)
            {
                regmask |= SRBM_RISCV64_S6;
            }

            encodeMask = (uint)id.idReg2();

            if ((encodeMask & 0x01) != 0)
            {
                regmask |= SRBM_RISCV64_S7;
            }
            if ((encodeMask & 0x02) != 0)
            {
                regmask |= SRBM_RISCV64_S8;
            }
            if ((encodeMask & 0x04) != 0)
            {
                regmask |= SRBM_RISCV64_S9;
            }
            if ((encodeMask & 0x08) != 0)
            {
                regmask |= SRBM_RISCV64_S10;
            }
            if ((encodeMask & 0x10) != 0)
            {
                regmask |= SRBM_RISCV64_S11;
            }
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Call GC register encoding is not ported for this target.");
            return default!;
        }

        return unchecked((uint)regmask);
    }
}
