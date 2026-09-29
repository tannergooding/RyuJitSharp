// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool IsBitTestInstruction(instruction ins)
        => ins is INS_bt or INS_bts or INS_btr or INS_btc;

    private static bool IsApxConditionalInstruction(instruction ins)
        => false;

    private static bool HasRegularWideImmediateForm(instruction ins)
        => (prefixFlags(ins) & INS_FLAGS_HasSBit) != 0;

    private static regNumber inst3opImulReg(instruction ins)
    {
        assert(instrIs3opImul(ins));
        return (regNumber)(ins - INS_imul_AX);
    }

    private static bool insNeedsRRIb(instruction ins)
        => ins == INS_imul;

    private unsafe ulong insEncodeRRIb(instrDesc id, regNumber reg, emitAttr size)
    {
        assert(size == EA_4BYTE && insNeedsRRIb(id.idIns()));
        ulong code = 0x69C0;
        var regcode = insEncodeReg012(id, reg, size, &code);
        return code | regcode | ((ulong)regcode << 3);
    }

    private unsafe ulong insEncodeMIreg(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        assert((code & 0xC000) == 0);
        code |= 0xC000;
        code |= (ulong)insEncodeReg012(id, reg, size, &code) << 8;
        return code;
    }

    private unsafe ulong insEncodeOpreg(instrDesc id, regNumber reg, emitAttr size)
    {
        var code = (ulong)insCodeRR(id.idIns());
        code |= insEncodeReg012(id, reg, size, &code);
        return code;
    }

    public static ulong insEncodeRMreg(instrDesc id, ulong code)
    {
        if ((code & 0xFF00) == 0)
        {
            assert((code & 0xC000) == 0);
            code |= 0xC000;
        }

        return code;
    }

    private static regNumber getSseShiftRegNumber(instruction ins)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 SIMD immediate shift opcode selection is not ported.");

    private ulong AddRex2Prefix(instruction ins, ulong code)
    {
        assert(IsRex2EncodableInstruction(ins));

        code |= 0xD50000000000UL;
        if (IsLegacyMap1(code))
        {
            code |= 0x008000000000UL;
        }

        return code;
    }

}
#endif
