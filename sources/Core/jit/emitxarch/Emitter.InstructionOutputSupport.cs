// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    internal static nint emitGetInsSC(instrDesc id)
    {
        return id.idIsLargeCns() ? ((instrDescCns)id).idcCnsVal : id.idSmallCns();
    }

    private static bool IsAvx512OnlyInstruction(instruction ins)
    {
        return ins >= FIRST_AVX512_INSTRUCTION && ins <= LAST_AVX512_INSTRUCTION;
    }

    private static bool isLowSimdReg(regNumber reg)
    {
        return reg >= REG_XMM0 && reg <= REG_XMM15;
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
        assert(unchecked((uint)cns) == unchecked((nuint)cns));

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
        assert(!result || id.idCodeSize() == 0
            || (((instrDescJmp)id).idjIsAfterCallBeforeEpilog && id.idCodeSize() == 1));

        return result;
    }

    private static bool emitInstHasNoCode(instrDesc id)
    {
        return emitAlignInstHasNoCode(id) || emitJmpInstHasNoCode(id);
    }

#if DEBUG
    private unsafe void emitRecordCallSite(uint instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle)
    {
        assert(_compiler is not null);
        if (callSig == null && methodHandle != null && Compiler.eeGetHelperNum(methodHandle) == CORINFO_HELP_UNDEF)
        {
            _compiler.eeGetMethodSig(methodHandle, out var sigInfo);
            callSig = &sigInfo;
        }
        emitCmpHandle->recordCallSite(unchecked((int)instrOffset), callSig, methodHandle);
    }
#endif
#endif
}
