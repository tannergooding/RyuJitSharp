// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_R_I_LdStPair(instruction ins, emitAttr attr, emitAttr attr2,
        regNumber reg1, regNumber reg2, regNumber reg3, nint imm,
        int varx1 = -1, int varx2 = -1, int offs1 = -1, int offs2 = -1
#if DEBUG
        , uint var1RefsOffs = unchecked((uint)BAD_IL_OFFSET), uint var2RefsOffs = unchecked((uint)BAD_IL_OFFSET)
#endif
        )
    {
        assert((ins == INS_stp) || (ins == INS_ldp));
        var size = EA_SIZE(attr);
        int scale;

        if (isVectorRegister(reg1))
        {
            assert(isValidVectorLSPDatasize(size));
            assert(isVectorRegister(reg2));
            scale = (int)NaturalScale_helper(size);
            assert((scale >= 2) && (scale <= 4));
        }
        else
        {
            assert(isValidGeneralDatasize(size));
            assert(isGeneralRegisterOrZR(reg2));
            scale = (size == EA_8BYTE) ? 3 : 2;
        }

        reg3 = encodingSPtoZR(reg3);

        var fmt = IF_LS_3C;
        nint mask = (1 << scale) - 1; // Low bits must be zero to encode the scaled immediate.
        if (imm == 0)
        {
            fmt = IF_LS_3B;
        }
        else
        {
            if ((imm & mask) == 0)
            {
                imm >>= scale;
            }
            else
            {
                // Unlike emitIns_S_S_R_R, this caller cannot supply an unaligned offset.
                unreached();
            }
        }

        var validVar1 = varx1 != -1;
        var validVar2 = varx2 != -1;
        instrDesc id;
        if (validVar1 && validVar2)
        {
            id = emitNewInstrLclVarPair(attr, imm);
            id.idAddr().iiaLclVar.initLclVarAddr(varx1, unchecked((uint)offs1));
            id.idSetIsLclVar();
            emitGetLclVarPairLclVar2(id).initLclVarAddr(varx2, unchecked((uint)offs2));
        }
        else
        {
            id = emitNewInstrCns(attr, imm);
            if (validVar1)
            {
                id.idAddr().iiaLclVar.initLclVarAddr(varx1, unchecked((uint)offs1));
                id.idSetIsLclVar();
            }
            if (validVar2)
            {
                id.idAddr().iiaLclVar.initLclVarAddr(varx2, unchecked((uint)offs2));
                id.idSetIsLclVar();
            }
        }

        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);

        // The second register's GC type is independent of the descriptor operand size.
        if (EA_IS_GCREF(attr2))
        {
            id.idGCrefReg2(GCT_GCREF);
        }
        else if (EA_IS_BYREF(attr2))
        {
            id.idGCrefReg2(GCT_BYREF);
        }
        else
        {
            id.idGCrefReg2(GCT_NONE);
        }

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = var1RefsOffs;
        debugInfo.idVarRefOffs2 = var2RefsOffs;
#endif
        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
