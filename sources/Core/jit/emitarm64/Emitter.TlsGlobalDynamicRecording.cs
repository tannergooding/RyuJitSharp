// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_Adrp_Ldr_Add(emitAttr attr, regNumber reg1, regNumber reg2, nint addr
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        assert(_compiler is not null);
        assert(_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI));
        assert(TargetOS.IsUnix);
        assert(EA_IS_RELOC(attr));
        assert(EA_IS_CNS_TLSGD_RELOC(attr));

        var size = EA_SIZE(attr);
        var id = emitNewInstrJmp();

        id.idIns(INS_adrp);
        id.idInsFmt(IF_DI_1E);
        id.idInsOpt(INS_OPTS_NONE);
        id.idOpSize(size);
        id.idAddr().iiaAddr = (byte*)addr;
        id.idReg1(reg1);
        id.idSetIsDspReloc();
        id.idSetTlsGD();
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idMemCookie = unchecked((nint)targetHandle);
        debugInfo.idFlags = gtFlags;
#endif

        dispIns(id);
        appendToCurIG(id);

        emitIns_R_R_I(INS_ldr, attr, reg2, reg1, addr);

        var addId = emitNewInstr(attr);
        assert(id.idIsReloc());
        addId.idIns(INS_add);
        addId.idInsFmt(IF_DI_2A);
        addId.idInsOpt(INS_OPTS_NONE);
        addId.idOpSize(size);
        addId.idAddr().iiaAddr = (byte*)addr;
        addId.idReg1(reg1);
        addId.idReg2(reg1);
        addId.idSetTlsGD();

        dispIns(addId);
        appendToCurIG(addId);
    }
}
#endif
