// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool IsAvx512OnlyInstruction(instruction ins)
    {
        return Configuration.TARGET_XARCH &&
            InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_AVX512_INSTRUCTION, LAST_AVX512_INSTRUCTION);
    }

    private static bool isLowSimdReg(regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        var first = Configuration.TARGET_AMD64 ? REG_X64_XMM0 : REG_X86_XMM0;
        var last = Configuration.TARGET_AMD64 ? REG_X64_XMM15 : REG_X86_XMM7;

        return (reg >= first) && (reg <= last);
    }

    private static ulong insEncodeMRreg(instrDesc id, ulong code)
    {
        if ((code & 0xFF00) == 0)
        {
            assert((code & 0xC000) == 0);
            code |= 0xC000;
        }

        return code;
    }

    private int emitGetInsCDinfo(instrDesc id)
    {
        if (id.idIsLargeCall())
        {
            return unchecked((int)((instrDescCGCA)id).idcArgCnt);
        }

        assert(!id.idIsLargeDsp());
        assert(!id.idIsLargeCns());
        var cns = emitGetInsCns(id);
        noway_assert(unchecked((int)cns) == cns);

        return unchecked((int)cns);
    }

    private uint emitGetInsCIargs(instrDesc id)
    {
        if (id.idIsLargeCall())
        {
            return ((instrDescCGCA)id).idcArgCnt;
        }

        assert(!id.idIsLargeDsp());
        assert(!id.idIsLargeCns());
        var cns = emitGetInsCns(id);
        if (Configuration.TARGET_X86)
        {
            assert(unchecked((int)cns) == cns);
        }
        else
        {
            assert(unchecked((uint)cns) == unchecked((nuint)cns));
        }

        return unchecked((uint)cns);
    }

    private static unsafe byte* emitCodeWithInstructionSize(byte* before, byte* after, byte* size)
    {
        assert(after >= before);
        assert((nuint)(after - before) <= byte.MaxValue);
        *size = checked((byte)(after - before));

        return after;
    }

    private static bool emitAlignInstHasNoCode(instrDesc id)
    {
        return id.idIns() == INS_align && id.idCodeSize() == 0;
    }

    private static bool emitJmpInstHasNoCode(instrDesc id)
    {
        var result = id.idIns() == INS_jmp && ((instrDescJmp)id).idjIsRemovableJmpCandidate;
        if (Configuration.TARGET_AMD64)
        {
            assert(!result || id.idCodeSize() == 0
                || (((instrDescJmp)id).idjIsAfterCallBeforeEpilog && id.idCodeSize() == 1));
        }
        else
        {
            assert(!result || id.idCodeSize() == 0);
        }

        return result;
    }

    private static bool emitInstHasNoCode(instrDesc id)
    {
        return emitAlignInstHasNoCode(id) || emitJmpInstHasNoCode(id);
    }

    private static bool IsThreeOperandImulInstruction(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        // x86 encodes the accumulator-through-DI forms; AMD64 adds R8-R31.
        var lastInstruction = Configuration.TARGET_AMD64 ? INS_imul_31 : INS_imul_DI;
        return InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_imul_AX, lastInstruction);
    }
}
