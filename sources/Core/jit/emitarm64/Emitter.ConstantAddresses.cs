// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, regNumber addrReg,
        CORINFO_FIELD_HANDLE fldHnd, int offs)
    {
        assert(offs >= 0);
        assert(instrDesc.fitsInSmallCns(offs));
        assert(_compiler is not null);

        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        var id = emitNewInstrJmp();
        switch (ins)
        {
            case INS_adr:
            {
                fmt = IF_LARGEADR;
                assert(isGeneralRegister(reg));
                assert(isValidGeneralDatasize(size));
                break;
            }

            case INS_ldr:
            {
                fmt = IF_LARGELDC;
                if (isVectorRegister(reg))
                {
                    assert(isValidVectorLSDatasize(size));
                    assert(isGeneralRegister(reg) || (addrReg != REG_NA));
                }
                else
                {
                    assert(isGeneralRegister(reg));
                    assert(isValidGeneralDatasize(size));
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(fmt != IF_NONE);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idSmallCns(offs);
        id.idOpSize(size);
        id.idSetRelocFlags(attr);
        id.idAddr().iiaFieldHnd = fldHnd;
        id.idSetIsBound();
        id.idReg1(reg);
        if (addrReg != REG_NA)
        {
            id.idReg2(addrReg);
        }
        id.idjShort = false;

        // Relocatable and cold-code references cannot assume adjacent code and data.
        id.idjKeepLong = EA_IS_RELOC(attr);
        if (!id.idjKeepLong)
        {
            assert(_compiler.compCurBB is not null);
            id.idjKeepLong = _compiler.fgIsBlockCold(_compiler.compCurBB);
        }
#if DEBUG
        if (_compiler.opts.compLongAddress)
        {
            id.idjKeepLong = true;
        }
#endif

        if (!id.idjKeepLong)
        {
            id.idjIG = emitCurIG;
            id.idjOffs = unchecked((uint)emitCurIGsize);
            id.idjNext = emitCurIGjmpList;
            emitCurIGjmpList = id;
#if EMITTER_STATS
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif
        }

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
