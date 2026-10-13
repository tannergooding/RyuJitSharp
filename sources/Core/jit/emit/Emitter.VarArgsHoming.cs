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
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Varargs shadow-slot homing requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(emitGeneratingPrologOrFuncletProlog());
        var maxRegArg = Configuration.TARGET_X86 ? 2 : Configuration.MAX_REG_ARG;
        for (var argNum = 0; argNum < maxRegArg; argNum++)
        {
            // Shadow slots begin immediately above the return address, before any prolog pushes.
            var offset = (argNum + 1) * Configuration.TARGET_POINTER_SIZE;
            var id = emitNewInstrAmd(EA_PTRSIZE, offset);
            id.idIns(INS_mov);
            id.idInsFmt(emitInsModeFormat(INS_mov, IF_XARCH_ARD_RRD));
            id.idAddr().iiaAddrMode.amBaseReg = TargetRuntimeMetadata.StackPointerBaseRegister;
            id.idAddr().iiaAddrMode.amIndxReg = TargetRuntimeMetadata.NoRegister;
            id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeScale(1);
            assert(emitGetInsAmdAny(id) == offset);
            id.idReg1(Configuration.TARGET_X86
                ? argNum == 0 ? REG_X86_ECX : REG_X86_EDX
                : IntArgRegs[argNum]);
            var size = emitInsSizeAM(id, insCodeMR(INS_mov));
            id.idCodeSize(size);
            emitCurIGsize = unchecked(emitCurIGsize + (int)size);
        }
    }
}
