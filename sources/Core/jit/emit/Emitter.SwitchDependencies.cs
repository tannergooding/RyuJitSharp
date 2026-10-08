// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_LOONGARCH64
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, regNumber addrReg,
        CORINFO_FIELD_HANDLE fieldHandle, int offset)
    {
        assert(offset >= 0);
        assert(instrDesc.fitsInSmallCns(offset));

        var id = emitNewInstr(attr);
        id.idSetRelocFlags(attr);
        id.idIns(ins);
        assert(reg != REG_R0);
        id.idReg1(reg);
        id.idSmallCns(offset);
        id.idInsOpt(INS_OPTS_RC);

        var compiler = _compiler
            ?? throw new FatalJitException("LoongArch64 embedded-data recording requires an active compiler.");
        if (compiler.opts.compReloc || id.idIsReloc())
        {
            id.idSetIsDspReloc();
            id.idCodeSize(8);
        }
        else
        {
            id.idCodeSize(12);
        }

        if (EA_IS_GCREF(attr))
        {
            id.idGCref(GCT_GCREF);
            id.idOpSize(EA_PTRSIZE);
        }
        else if (EA_IS_BYREF(attr))
        {
            id.idGCref(GCT_BYREF);
            id.idOpSize(EA_PTRSIZE);
        }

        id.idSetIsBound();
        assert(addrReg == REG_NA);
        id.idAddr().iiaFieldHnd = fieldHandle;
        appendToCurIG(id);
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
