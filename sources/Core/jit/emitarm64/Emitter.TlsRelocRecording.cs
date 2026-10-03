// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_Add_Add_Tls_Reloc(emitAttr attr, regNumber targetReg, regNumber reg, nint imm
#if DEBUG
        , GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        var size = EA_SIZE(attr);

        assert(_compiler is not null);
        assert(_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI));
        assert(TargetOS.IsWindows);
        assert(isValidGeneralDatasize(size));
        assert(EA_IS_CNS_SEC_RELOC(attr));

        var fmt = IF_DI_2A;
        var id = emitNewInstrCns(attr, 0);

        id.idIns(INS_add);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_LSL12);
        id.idAddr().iiaAddr = (byte*)imm;
        id.idReg1(targetReg);
        id.idReg2(reg);
        // TLS relocations always materialize an 8-byte address.
        id.idOpSize(EA_8BYTE);
        id.idSetTlsGD();
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idMemCookie = imm;
        debugInfo.idFlags = gtFlags;
#endif
        dispIns(id);
        appendToCurIG(id);

        // Only the first ADD carries the section-relocation marker.
        id = emitNewInstrCns(size, 0);
        id.idIns(INS_add);
        id.idInsFmt(fmt);
        id.idAddr().iiaAddr = (byte*)imm;
        id.idReg1(targetReg);
        id.idReg2(reg);
        id.idOpSize(EA_8BYTE);
        id.idSetTlsGD();
#if DEBUG
        debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idMemCookie = imm;
        debugInfo.idFlags = gtFlags;
#endif
        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
