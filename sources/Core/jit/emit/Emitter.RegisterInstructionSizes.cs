// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool emitVerifyEncodable(instruction ins, emitAttr size, regNumber reg1) =>
        emitVerifyEncodable(ins, size, reg1, TargetRuntimeMetadata.NoRegister);

    public static bool emitVerifyEncodable(instruction ins, emitAttr size, regNumber reg1, regNumber reg2)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.InstructionEncodabilityChecksRequireXarch);
            return default!;
        }

        if (!Configuration.TARGET_X86)
        {
            return true;
        }

        if (size != EA_1BYTE)
        {
            return true;
        }

        var requireByteRegister =
            (ins is not (INS_movsx or INS_movzx)) &&
            (!Configuration.FEATURE_HW_INTRINSICS || (ins != INS_crc32));
        if (requireByteRegister && !IsX86ByteRegister(reg1))
        {
            return false;
        }

        if ((reg2 != TargetRuntimeMetadata.NoRegister) && !IsX86ByteRegister(reg2))
        {
            return false;
        }

        return true;
    }

    private static bool IsX86ByteRegister(regNumber reg) =>
        reg is REG_X86_EAX or REG_X86_ECX or REG_X86_EDX or REG_X86_EBX;

    public static bool instrIs3opImul(instruction ins) => IsThreeOperandImulInstruction(ins);

    public static bool instrIsExtendedReg3opImul(instruction ins) =>
        Configuration.TARGET_AMD64 &&
        InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_imul_08, INS_imul_31);

    public static bool IsKInstruction(instruction ins) => (prefixFlags(ins) & KInstruction) != 0;

    public static bool IsKInstructionWithLBit(instruction ins) => (prefixFlags(ins) & KInstructionWithLBit) != 0;

    public ulong AddVexPrefix(instruction ins, ulong code, emitAttr attr)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.VEXOutputRequiresXarch);
            return default!;
        }

        // Carry the three-byte VEX encoding until emission, when all conditions
        // for shortening it to the preferred two-byte form are known.
        assert(IsVexEncodableInstruction(ins));
        assert(!hasVexPrefix(code));
        assert((code & 0xFFFFFF00000000UL) == 0);
        code |= 0xC4E07800000000UL;

        if ((attr == EA_32BYTE) || IsKInstructionWithLBit(ins))
        {
            code |= 0x00000400000000UL;
        }

        return code;
    }

    public static ulong insEncodeRMreg(instrDesc id, ulong code)
    {
        // A zero byte at 0xFF00 holds ModRM. Otherwise ModRM follows the opcode.
        if ((code & 0xFF00) == 0)
        {
            assert((code & 0xC000) == 0);
            code |= 0xC000;
        }

        return code;
    }

    public uint emitInsSizeRR(instrDesc id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.XarchRegisterRegisterSizingIsNotAvailableForThisTarget);
            return default!;
        }

        var ins = id.idIns();
        ulong code = hasCodeRM(ins) ? insCodeRM(ins) : insCodeMR(ins);

        if (IsKInstruction(ins))
        {
            code = AddVexPrefix(ins, code, EA_SIZE(id.idOpSize()));
        }

        var sz = emitGetAdjustedSize(id, code);
        var includeRexPrefixSize = true;

        if (!hasRexPrefix(code))
        {
            var reg1 = id.idReg1();
            var reg2 = id.idReg2();
            var attr = id.idOpSize();

            if ((TakesRexWPrefix(id) && ((ins != INS_xor) || (reg1 != reg2))) ||
                IsExtendedReg(reg1, attr) || IsExtendedReg(reg2, attr))
            {
                sz += emitGetRexPrefixSize(id, ins);
                includeRexPrefixSize = false;
            }
        }

        if ((code & 0xFF00) != 0)
        {
            sz += (IsSimdInstruction(ins) || TakesEvexPrefix(id))
                ? emitInsSize(id, code, includeRexPrefixSize)
                : 5u;
        }
        else
        {
            sz += emitInsSize(id, insEncodeRMreg(id, code), includeRexPrefixSize);
        }
        return sz;
    }
}
