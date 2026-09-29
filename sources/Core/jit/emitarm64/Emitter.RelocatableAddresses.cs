// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_R_AI(instruction ins, emitAttr attr, regNumber ireg, nint addr
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        assert(EA_IS_RELOC(attr));
        var size = EA_SIZE(attr);
        var fmt = IF_DI_1E;
        var needAdd = false;
        var id = emitNewInstrJmp();

        switch (ins)
        {
            case INS_adrp:
            {
                needAdd = true;
                break;
            }

            case INS_adr:
            {
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idOpSize(size);
        id.idAddr().iiaAddr = (byte*)addr;
        id.idReg1(ireg);
        id.idSetIsDspReloc();
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idMemCookie = unchecked((nint)targetHandle);
        debugInfo.idFlags = gtFlags;
#endif

        dispIns(id);
        appendToCurIG(id);

        if (needAdd)
        {
            ins = INS_add;
            fmt = IF_DI_2A;
            var addId = emitNewInstr(attr);
            assert(addId.idIsReloc());

            addId.idIns(ins);
            addId.idInsFmt(fmt);
            addId.idInsOpt(INS_OPTS_NONE);
            addId.idOpSize(size);
            addId.idAddr().iiaAddr = (byte*)addr;
            addId.idReg1(ireg);
            addId.idReg2(ireg);

            dispIns(addId);
            appendToCurIG(addId);
        }
    }
}
#endif
