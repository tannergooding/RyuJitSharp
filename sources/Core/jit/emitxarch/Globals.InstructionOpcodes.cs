// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public const uint BAD_CODE = 0xFFFFFFFF;

    public static bool isPrefix(byte b)
    {
        assert(b != 0);
        assert(b != 0x67);
        assert(b != 0x65);
        assert(b != 0x64);
        assert(b != 0xF0);
        assert(b != 0x2E);
        assert(b != 0x3E);
        assert(b != 0x26);
        assert(b != 0x36);

        // Only SSE size prefixes are packed into the opcode; segment/lock prefixes are separate.
        return b is 0xF2 or 0xF3 or 0x66;
    }

    public static nuint insCode(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodes.Length);
        assert(insCodes[(int)ins] != BAD_CODE);
        return insCodes[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Opcode lookup outside xarch is not implemented.");
#endif
    }

    public static nuint insCodeACC(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesACC.Length);
        assert(insCodesACC[(int)ins] != BAD_CODE);
        return insCodesACC[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Accumulator opcode lookup outside xarch is not implemented.");
#endif
    }

    public static nuint insCodeRR(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesRR.Length);
        assert(insCodesRR[(int)ins] != BAD_CODE);
        return insCodesRR[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Register opcode lookup outside xarch is not implemented.");
#endif
    }

    public static nuint insCodeRM(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesRM.Length);
        assert(insCodesRM[(int)ins] != BAD_CODE);
        return (nuint)insCodesRM[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Register-memory opcode lookup outside xarch is not implemented.");
#endif
    }

    public static bool hasCodeRM(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesRM.Length);
        return insCodesRM[(int)ins] != BAD_CODE;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Register-memory opcode lookup outside xarch is not implemented.");
#endif
    }

    public static nuint insCodeMI(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesMI.Length);
        assert(insCodesMI[(int)ins] != BAD_CODE);
        return (nuint)insCodesMI[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Memory-immediate opcode lookup outside xarch is not implemented.");
#endif
    }

    public static bool hasCodeMI(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesMI.Length);
        return insCodesMI[(int)ins] != BAD_CODE;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Memory-immediate opcode lookup outside xarch is not implemented.");
#endif
    }

    public static nuint insCodeMR(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesMR.Length);
        assert(insCodesMR[(int)ins] != BAD_CODE);
        return insCodesMR[(int)ins];
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Memory-register opcode lookup outside xarch is not implemented.");
#endif
    }

    public static bool hasCodeMR(instruction ins)
    {
#if TARGET_XARCH
        assert((uint)ins < (uint)insCodesMR.Length);
        return insCodesMR[(int)ins] != BAD_CODE;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Memory-register opcode lookup outside xarch is not implemented.");
#endif
    }
}
