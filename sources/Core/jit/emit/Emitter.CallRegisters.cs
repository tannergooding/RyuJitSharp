// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static void emitEncodeCallGCregs(regMaskTP regmask, instrDesc id)
    {
#if HAS_FIXED_REGISTER_SET
        var regs = regmask.Lower;
        uint encodeMask;

#if TARGET_X86
        assert(REGNUM_BITS >= 3);
        encodeMask = 0;

        if ((regs & SRBM_ESI) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_EDI) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_EBX) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }

        id.idReg1((regNumber)encodeMask); // Save in idReg1

#elif TARGET_AMD64
        assert(REGNUM_BITS >= 4);
        encodeMask = 0;

        if ((regs & SRBM_ESI) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_EDI) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_EBX) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_EBP) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }

        id.idReg1((regNumber)encodeMask); // Save in idReg1

        encodeMask = 0;

        if ((regs & SRBM_R12) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_R13) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_R14) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_R15) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }

        id.idReg2((regNumber)encodeMask); // Save in idReg2

#elif TARGET_ARM
        assert(REGNUM_BITS >= 4);
        encodeMask = 0;

        if ((regs & SRBM_R4) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_R5) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_R6) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_R7) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }

        id.idReg1((regNumber)encodeMask); // Save in idReg1

        encodeMask = 0;

        if ((regs & SRBM_R8) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_R9) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_R10) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_R11) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }

        id.idReg2((regNumber)encodeMask); // Save in idReg2

#elif TARGET_ARM64
        assert(REGNUM_BITS >= 5);
        encodeMask = 0;

        if ((regs & SRBM_R19) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_R20) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_R21) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_R22) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }
        if ((regs & SRBM_R23) != SRBM_NONE)
        {
            encodeMask |= 0x10;
        }

        id.idReg1((regNumber)encodeMask); // Save in idReg1

        encodeMask = 0;

        if ((regs & SRBM_R24) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_R25) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_R26) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_R27) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }
        if ((regs & SRBM_R28) != SRBM_NONE)
        {
            encodeMask |= 0x10;
        }

        id.idReg2((regNumber)encodeMask); // Save in idReg2

#elif TARGET_LOONGARCH64
        assert(REGNUM_BITS >= 5);
        encodeMask = 0;

        if ((regs & SRBM_S0) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_S1) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_S2) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_S3) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }
        if ((regs & SRBM_S4) != SRBM_NONE)
        {
            encodeMask |= 0x10;
        }

        id.idReg1((regNumber)encodeMask); // Save in idReg1

        encodeMask = 0;

        if ((regs & SRBM_S5) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_S6) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_S7) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_S8) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }

        id.idReg2((regNumber)encodeMask); // Save in idReg2

#elif TARGET_RISCV64
        assert(REGNUM_BITS >= 6);
        encodeMask = 0;

        if ((regs & SRBM_S1) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_S2) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_S3) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_S4) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }
        if ((regs & SRBM_S5) != SRBM_NONE)
        {
            encodeMask |= 0x10;
        }
        if ((regs & SRBM_S6) != SRBM_NONE)
        {
            encodeMask |= 0x20;
        }

        id.idReg1((regNumber)encodeMask); // Save in idReg1

        encodeMask = 0;

        if ((regs & SRBM_S7) != SRBM_NONE)
        {
            encodeMask |= 0x01;
        }
        if ((regs & SRBM_S8) != SRBM_NONE)
        {
            encodeMask |= 0x02;
        }
        if ((regs & SRBM_S9) != SRBM_NONE)
        {
            encodeMask |= 0x04;
        }
        if ((regs & SRBM_S10) != SRBM_NONE)
        {
            encodeMask |= 0x08;
        }
        if ((regs & SRBM_S11) != SRBM_NONE)
        {
            encodeMask |= 0x10;
        }

        id.idReg2((regNumber)encodeMask); // Save in idReg2

#else
        throw new FatalJitException(CORJIT_SKIPPED, "Call GC register encoding is not ported for this target.");
#endif
#endif // HAS_FIXED_REGISTER_SET
    }

#if HAS_FIXED_REGISTER_SET
    private static uint emitDecodeCallGCregs(instrDesc id)
    {
        var regmask = SRBM_NONE;
        uint encodeMask;

#if TARGET_X86
        assert(REGNUM_BITS >= 3);
        encodeMask = (uint)id.idReg1();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_ESI;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_EDI;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_EBX;
        }
#elif TARGET_AMD64
        assert(REGNUM_BITS >= 4);
        encodeMask = (uint)id.idReg1();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_ESI;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_EDI;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_EBX;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_EBP;
        }

        encodeMask = (uint)id.idReg2();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_R12;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_R13;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_R14;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_R15;
        }

#elif TARGET_ARM
        assert(REGNUM_BITS >= 4);
        encodeMask = (uint)id.idReg1();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_R4;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_R5;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_R6;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_R7;
        }

        encodeMask = (uint)id.idReg2();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_R8;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_R9;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_R10;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_R11;
        }

#elif TARGET_ARM64
        assert(REGNUM_BITS >= 5);
        encodeMask = (uint)id.idReg1();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_R19;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_R20;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_R21;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_R22;
        }
        if ((encodeMask & 0x10) != 0)
        {
            regmask |= SRBM_R23;
        }

        encodeMask = (uint)id.idReg2();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_R24;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_R25;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_R26;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_R27;
        }
        if ((encodeMask & 0x10) != 0)
        {
            regmask |= SRBM_R28;
        }

#elif TARGET_LOONGARCH64
        assert(REGNUM_BITS >= 5);
        encodeMask = (uint)id.idReg1();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_S0;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_S1;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_S2;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_S3;
        }
        if ((encodeMask & 0x10) != 0)
        {
            regmask |= SRBM_S4;
        }

        encodeMask = (uint)id.idReg2();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_S5;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_S6;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_S7;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_S8;
        }

#elif TARGET_RISCV64
        assert(REGNUM_BITS >= 6);
        encodeMask = (uint)id.idReg1();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_S1;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_S2;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_S3;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_S4;
        }
        if ((encodeMask & 0x10) != 0)
        {
            regmask |= SRBM_S5;
        }
        if ((encodeMask & 0x20) != 0)
        {
            regmask |= SRBM_S6;
        }

        encodeMask = (uint)id.idReg2();

        if ((encodeMask & 0x01) != 0)
        {
            regmask |= SRBM_S7;
        }
        if ((encodeMask & 0x02) != 0)
        {
            regmask |= SRBM_S8;
        }
        if ((encodeMask & 0x04) != 0)
        {
            regmask |= SRBM_S9;
        }
        if ((encodeMask & 0x08) != 0)
        {
            regmask |= SRBM_S10;
        }
        if ((encodeMask & 0x10) != 0)
        {
            regmask |= SRBM_S11;
        }

#else
        throw new FatalJitException(CORJIT_SKIPPED, "Call GC register encoding is not ported for this target.");
#endif

        return unchecked((uint)regmask);
    }
#endif
}
