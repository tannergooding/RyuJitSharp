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

    private static int GetXarchInstructionOrdinal(instruction ins, string unsupportedMessage)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED    );
            return default!;
        }

        return InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins);
    }

    public static nuint insCode(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Opcode lookup outside xarch is not implemented.");
        var codes = insCodes;
        assert((uint)ordinal < (uint)codes.Length);
        assert(codes[ordinal] != BAD_CODE);
        return unchecked((nuint)codes[ordinal]);
    }

    public static nuint insCodeACC(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Accumulator opcode lookup outside xarch is not implemented.");
        var codes = insCodesACC;
        assert((uint)ordinal < (uint)codes.Length);
        assert(codes[ordinal] != BAD_CODE);
        return unchecked((nuint)codes[ordinal]);
    }

    public static nuint insCodeRR(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Register opcode lookup outside xarch is not implemented.");
        var codes = insCodesRR;
        assert((uint)ordinal < (uint)codes.Length);
        assert(codes[ordinal] != BAD_CODE);
        return unchecked((nuint)codes[ordinal]);
    }

    public static nuint insCodeRM(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Register-memory opcode lookup outside xarch is not implemented.");
        var codes = insCodesRM;
        assert((uint)ordinal < (uint)codes.Length);
        assert(codes[ordinal] != BAD_CODE);
        return unchecked((nuint)codes[ordinal]);
    }

    public static bool hasCodeRM(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Register-memory opcode lookup outside xarch is not implemented.");
        var codes = insCodesRM;
        assert((uint)ordinal < (uint)codes.Length);
        return codes[ordinal] != BAD_CODE;
    }

    public static nuint insCodeMI(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Memory-immediate opcode lookup outside xarch is not implemented.");
        var codes = insCodesMI;
        assert((uint)ordinal < (uint)codes.Length);
        assert(codes[ordinal] != BAD_CODE);
        return unchecked((nuint)codes[ordinal]);
    }

    public static bool hasCodeMI(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Memory-immediate opcode lookup outside xarch is not implemented.");
        var codes = insCodesMI;
        assert((uint)ordinal < (uint)codes.Length);
        return codes[ordinal] != BAD_CODE;
    }

    public static nuint insCodeMR(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Memory-register opcode lookup outside xarch is not implemented.");
        var codes = insCodesMR;
        assert((uint)ordinal < (uint)codes.Length);
        assert(codes[ordinal] != BAD_CODE);
        return unchecked((nuint)codes[ordinal]);
    }

    public static bool hasCodeMR(instruction ins)
    {
        var ordinal = GetXarchInstructionOrdinal(ins, "Memory-register opcode lookup outside xarch is not implemented.");
        var codes = insCodesMR;
        assert((uint)ordinal < (uint)codes.Length);
        return codes[ordinal] != BAD_CODE;
    }
}
