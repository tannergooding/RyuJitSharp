// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_LOONGARCH64
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, regNumber addrReg,
        CORINFO_FIELD_HANDLE fieldHandle, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 embedded-data instruction recording is not ported.");
    }
#else
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, regNumber addrReg,
        CORINFO_FIELD_HANDLE fieldHandle)
    {
        var id = emitNewInstr(attr);
        id.idSetRelocFlags(attr);
        id.idIns(ins);
        assert(reg != REG_R0);
        id.idReg1(reg);
        id.idInsOpt(INS_OPTS_RC);
        id.idCodeSize(8);

        // Inline JIT data is resolved after code and data are placed together, so no patch is needed.
        id.idSetIsBound();

        assert(addrReg == REG_NA);
        id.idAddr().iiaFieldHnd = fieldHandle;

        dispIns(id);
        appendToCurIG(id);
    }
#endif

}
#endif
