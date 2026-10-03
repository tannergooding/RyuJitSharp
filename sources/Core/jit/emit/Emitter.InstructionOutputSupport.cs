// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    private static bool IsAvx512OnlyInstruction(instruction ins)
    {
        return ins >= FIRST_AVX512_INSTRUCTION && ins <= LAST_AVX512_INSTRUCTION;
    }

    private static bool isLowSimdReg(regNumber reg)
    {
#if TARGET_AMD64
        return (reg >= REG_XMM0) && (reg <= REG_XMM15);
#else
        return (reg >= REG_XMM0) && (reg <= REG_XMM7);
#endif
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
#if TARGET_X86
        assert(unchecked((int)cns) == cns);
#else
        assert(unchecked((uint)cns) == unchecked((nuint)cns));
#endif

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
#if TARGET_AMD64
        assert(!result || id.idCodeSize() == 0
            || (((instrDescJmp)id).idjIsAfterCallBeforeEpilog && id.idCodeSize() == 1));
#else
        assert(!result || id.idCodeSize() == 0);
#endif

        return result;
    }

    private static bool emitInstHasNoCode(instrDesc id)
    {
        return emitAlignInstHasNoCode(id) || emitJmpInstHasNoCode(id);
    }

#endif
}
