// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void spillIntArgRegsToShadowSlots()
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Varargs shadow-slot homing requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(emitGeneratingPrologOrFuncletProlog());
#if TARGET_X86
        // targetx86.cpp defines intArgRegs as ECX, EDX.
        const int maxRegArg = 2;
#else
        const int maxRegArg = MAX_REG_ARG;
#endif
        for (var argNum = 0; argNum < maxRegArg; argNum++)
        {
            // Shadow slots begin immediately above the return address, before any prolog pushes.
            var offset = (argNum + 1) * TARGET_POINTER_SIZE;
            var id = emitNewInstrAmd(EA_PTRSIZE, offset);
            id.idIns(INS_mov);
            id.idInsFmt(emitInsModeFormat(INS_mov, IF_ARD_RRD));
            id.idAddr().iiaAddrMode.amBaseReg = REG_SPBASE;
            id.idAddr().iiaAddrMode.amIndxReg = REG_NA;
            id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeScale(1);
            assert(emitGetInsAmdAny(id) == offset);
#if TARGET_X86
            id.idReg1(argNum == 0 ? REG_ECX : REG_EDX);
#else
            id.idReg1(IntArgRegs[argNum]);
#endif
            var size = emitInsSizeAM(id, insCodeMR(INS_mov));
            id.idCodeSize(size);
            emitCurIGsize = unchecked(emitCurIGsize + (int)size);
        }
#endif
    }
}
