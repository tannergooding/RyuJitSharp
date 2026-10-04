// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Numerics;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isValidSimm(nint value, int bits)
    {
        var sizeBits = nint.Size * 8;
        assert(bits > 0);
        assert(bits <= sizeBits);
        if (bits == sizeBits)
        {
            return true;
        }
        else
        {
            nint max = 1;
            max <<= bits - 1;
            return (-max <= value) && (value < max);
        }
    }

    private static uint NaturalScale_helper(emitAttr size)
    {
        assert((size == EA_1BYTE) || (size == EA_2BYTE) || (size == EA_4BYTE) ||
               (size == EA_8BYTE) || (size == EA_16BYTE));
        return (uint)BitOperations.Log2((uint)size);
    }

    private static uint insGetRegisterListSize(instruction ins)
    {
        uint registerListSize = 0;
        switch (ins)
        {
            case INS_ld1:
            case INS_ld1r:
            case INS_st1:
            case INS_tbl:
            case INS_tbx:
            {
                registerListSize = 1;
                break;
            }

            case INS_ld1_2regs:
            case INS_ld2:
            case INS_ld2r:
            case INS_st1_2regs:
            case INS_st2:
            case INS_tbl_2regs:
            case INS_tbx_2regs:
            {
                registerListSize = 2;
                break;
            }

            case INS_ld1_3regs:
            case INS_ld3:
            case INS_ld3r:
            case INS_st1_3regs:
            case INS_st3:
            case INS_tbl_3regs:
            case INS_tbx_3regs:
            {
                registerListSize = 3;
                break;
            }

            case INS_ld1_4regs:
            case INS_ld4:
            case INS_ld4r:
            case INS_st1_4regs:
            case INS_st4:
            case INS_tbl_4regs:
            case INS_tbx_4regs:
            {
                registerListSize = 4;
                break;
            }

            default:
            {
                assert(false, "Unexpected instruction");
                break;
            }
        }

        return registerListSize;
    }

    private static bool isValidGeneralLSDatasize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE) || (size == EA_1BYTE);
    }

    private static bool isValidVectorLSDatasize(emitAttr size)
    {
        return (size == EA_16BYTE) || (size == EA_8BYTE) || (size == EA_4BYTE) ||
               (size == EA_2BYTE) || (size == EA_1BYTE);
    }

    private unsafe bool TryFoldPageOffsetIntoLdr(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
    {
        assert(_compiler is not null);

        if (ins != INS_ldr)
        {
            return false;
        }

        // The base register must be dead after the load so the preceding address add can be removed.
        if (reg1 != reg2)
        {
            return false;
        }

        // PAGEOFFSET_12L scales by eight, so it only supports 64-bit general-register loads.
        if ((EA_SIZE(attr) != EA_8BYTE) || !isGeneralRegister(reg1))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        if (_compiler.compGeneratingUnwindProlog || _compiler.compGeneratingUnwindEpilog)
        {
            // Keep the original instructions so their unwind effects remain reportable.
            return false;
        }

        assert(emitLastIns is not null);
        var prevId = emitLastIns;
        // The previous instruction must be the relocatable low-page add from the same adrp/add pair.
        if ((prevId.idIns() != INS_add) || (prevId.idInsFmt() != IF_DI_2A) || !prevId.idIsReloc() ||
            prevId.idIsTlsGD() || (prevId.idReg1() != reg1) || (prevId.idReg2() != reg1))
        {
            return false;
        }

        var sym = prevId.idAddr().iiaAddr;
        // Only fold when the VM guarantees the alignment required by the scaled 64-bit relocation.
        if (_compiler.eeGetAddressAlignment(sym) < 8)
        {
            return false;
        }

        // The adrp already materialized the page base; replace the add with its page-offset load.
        emitRemoveLastInstruction();

        var id = emitNewInstrSC(attr, (nint)sym);
        id.idIns(INS_ldr);
        id.idInsFmt(IF_LS_2A);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg1);
        id.idSetIsDspReloc();

        dispIns(id);
        appendToCurIG(id);
        return true;
    }

    private enum RegisterOrder
    {
        eRO_none = 0,
        eRO_ascending,
        eRO_descending,
    }

    private bool OptimizePostIndexed(instruction ins, regNumber reg, nint imm, emitAttr regAttr)
    {
        assert(ins is INS_add or INS_sub);

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        assert(emitLastIns is not null);
        if (!emitInsIsLoadOrStore(emitLastIns.idIns()))
        {
            return false;
        }

        if ((emitLastIns.idInsFmt() != IF_LS_2A) || emitLastIns.idIsTlsGD() || emitLastIns.idIsReloc())
        {
            return false;
        }

        assert(_compiler is not null);
        // Preserve the unwind encoding by leaving the original load/store instruction intact.
        if (_compiler.compGeneratingUnwindProlog || _compiler.compGeneratingUnwindEpilog)
        {
            return false;
        }

        var loadStoreDataReg = emitLastIns.idReg1();
        if (loadStoreDataReg == reg)
        {
            return false;
        }

        var loadStoreAddrReg = encodingZRtoSP(emitLastIns.idReg2());
        if (loadStoreAddrReg != reg)
        {
            return false;
        }

        switch (emitLastIns.idIns())
        {
            case INS_ldrb:
            case INS_strb:
            case INS_ldurb:
            case INS_sturb:
            case INS_ldrh:
            case INS_strh:
            case INS_ldurh:
            case INS_sturh:
            case INS_ldrsb:
            case INS_ldursb:
            case INS_ldrsh:
            case INS_ldursh:
            case INS_ldrsw:
            case INS_ldursw:
            case INS_ldr:
            case INS_str:
            case INS_ldur:
            case INS_stur:
            {
                break;
            }

            default:
            {
                return false;
            }
        }

        if (ins == INS_sub)
        {
            imm = -imm;
        }

        // Post-indexed ARM64 load/store immediates are signed 9-bit byte offsets.
        if ((imm < -256) || (imm >= 256))
        {
            return false;
        }

        var newIns = emitLastIns.idIns();
        emitAttr newAttr;

        switch (emitLastIns.idGCref())
        {
            case GCT_BYREF:
            {
                newAttr = EA_BYREF;
                break;
            }

            case GCT_GCREF:
            {
                newAttr = EA_GCREF;
                break;
            }

            default:
            {
                newAttr = emitLastIns.idOpSize();
                break;
            }
        }

        emitRemoveLastInstruction();

        var id = emitNewInstrCns(newAttr, imm);
        id.idIns(newIns);
        id.idInsFmt(IF_LS_2C);
        id.idInsOpt(INS_OPTS_POST_INDEX);
        id.idReg1(loadStoreDataReg);
        id.idReg2(encodingSPtoZR(loadStoreAddrReg));

        if (EA_IS_BYREF(regAttr))
        {
            id.idGCrefReg2(GCT_BYREF);
        }
        else if (EA_IS_GCREF(regAttr))
        {
            id.idGCrefReg2(GCT_GCREF);
        }

        dispIns(id);
        appendToCurIG(id);
        return true;
    }

    private bool OptimizeLdrStr(instruction ins, emitAttr reg1Attr, regNumber reg1, regNumber reg2,
        nint imm, emitAttr size, insFormat fmt, bool localVar = false, int varx = -1, int offs = -1
#if DEBUG
        , bool useRsvdReg = false
#endif
        )
    {
        assert(ins is INS_ldr or INS_str);

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        assert(emitLastIns is not null);
        if (emitLastIns.idIns() != ins)
        {
            return false;
        }

        if (IsRedundantLdStr(ins, reg1, reg2, imm, size, fmt))
        {
            return true;
        }

        reg2 = encodingZRtoSP(reg2);

        if (ReplaceLdrStrWithPairInstr(ins, reg1Attr, reg1, reg2, imm, size, fmt, localVar, varx, offs))
        {
#if DEBUG
            assert(!useRsvdReg);
#endif
            return true;
        }

        if (IsOptimizableLdrToMov(ins, reg1, reg2, imm, size, fmt))
        {
            emitIns_Mov(INS_mov, reg1Attr, reg1, emitLastIns.idReg1(), canSkip: true);
            return true;
        }

        return false;
    }

    private bool IsRedundantLdStr(instruction ins, regNumber reg1, regNumber reg2, nint imm,
        emitAttr size, insFormat fmt)
    {
        if ((ins != INS_ldr) && (ins != INS_str))
        {
            return false;
        }

        assert(emitLastIns is not null);
        var prevReg1 = emitLastIns.idReg1();
        var prevReg2 = emitLastIns.idReg2();
        var lastInsFmt = emitLastIns.idInsFmt();
        var prevSize = emitLastIns.idOpSize();
        var prevImm = emitGetInsSC(emitLastIns);

        if (((fmt != IF_LS_2A) && (fmt != IF_LS_2B)) || (fmt != lastInsFmt) || (prevSize != size))
        {
            return false;
        }

        if ((ins == INS_ldr) && (emitLastIns.idIns() == INS_str))
        {
            if (size != EA_8BYTE)
            {
                return false;
            }

            if ((prevReg1 == reg1) && (prevReg2 == reg2) && (imm == prevImm))
            {
                JITDUMP($"\n -- suppressing 'ldr reg{reg1} [reg{reg2}, #{imm}]' as previous " +
                    $"'str reg{prevReg1} [reg{prevReg2}, #{prevImm}] was from same location.\n");
                return true;
            }
        }
        else if ((ins == INS_str) && (emitLastIns.idIns() == INS_ldr))
        {
            if ((reg1 != reg2) && (prevReg1 == reg1) && (prevReg2 == reg2) && (imm == prevImm) &&
                (reg1 != REG_ZR))
            {
                JITDUMP($"\n -- suppressing 'str reg{reg1} [reg{reg2}, #{imm}]' as previous " +
                    $"'ldr reg{prevReg1} [reg{prevReg2}, #{prevImm}] was from same location.\n");
                return true;
            }
        }

        return false;
    }

    private bool ReplaceLdrStrWithPairInstr(instruction ins, emitAttr reg1Attr, regNumber reg1, regNumber reg2,
        nint imm, emitAttr size, insFormat fmt, bool localVar = false, int varx = -1, int offs = -1)
    {
        var optimizationOrder = IsOptimizableLdrStrWithPair(ins, reg1, reg2, imm, size, fmt);
        if (optimizationOrder == RegisterOrder.eRO_none)
        {
            return false;
        }

        assert(emitLastIns is not null);
        var prevReg1 = emitLastIns.idReg1();
#if DEBUG
        var previousDebugInfo = emitLastIns.idDebugOnlyInfo();
        assert(previousDebugInfo is not null);
        var prevVarRefsOffs = previousDebugInfo.idVarRefOffs;
        var newVarRefsOffs = unchecked((uint)emitVarRefOffs);
#endif

        var prevImm = emitGetInsSC(emitLastIns);
        var optIns = (ins == INS_ldr) ? INS_ldp : INS_stp;
        var prevImmSize = (emitLastIns.idInsFmt() == IF_LS_2C) ? prevImm : (prevImm * (nint)size);
        var newImmSize = (fmt == IF_LS_2C) ? imm : (imm * (nint)size);

        var prevOffset = -1;
        var prevLclVarNum = -1;
        if (emitLastIns.idIsLclVar())
        {
            prevOffset = unchecked((int)emitLastIns.idAddr().iiaLclVar.lvaOffset());
            prevLclVarNum = emitLastIns.idAddr().iiaLclVar.lvaVarNum();
        }

        if (!localVar)
        {
            assert((varx == -1) && (offs == -1));
        }

        emitAttr prevReg1Attr;
        switch (emitLastIns.idGCref())
        {
            case GCT_GCREF:
            {
                prevReg1Attr = EA_GCREF;
                break;
            }

            case GCT_BYREF:
            {
                prevReg1Attr = EA_BYREF;
                break;
            }

            default:
            {
                prevReg1Attr = emitLastIns.idOpSize();
                break;
            }
        }

        emitRemoveLastInstruction();

        // A pair of zero stores has the same memory effect as one 64-bit zero store.
        if ((ins == INS_str) && (reg1 == REG_ZR) && (prevReg1 == REG_ZR) && (size == EA_4BYTE))
        {
            var offset = (optimizationOrder == RegisterOrder.eRO_ascending) ? prevImmSize : newImmSize;
            emitIns_R_R_I(INS_str, EA_8BYTE, REG_ZR, reg2, offset, INS_OPTS_NONE);
            return true;
        }

        if (optimizationOrder == RegisterOrder.eRO_ascending)
        {
            emitIns_R_R_R_I_LdStPair(optIns, prevReg1Attr, reg1Attr, prevReg1, reg1, reg2, prevImmSize,
                prevLclVarNum, varx, prevOffset, offs
#if DEBUG
                , prevVarRefsOffs, newVarRefsOffs
#endif
            );
        }
        else
        {
            emitIns_R_R_R_I_LdStPair(optIns, reg1Attr, prevReg1Attr, reg1, prevReg1, reg2, newImmSize,
                varx, prevLclVarNum, offs, prevOffset
#if DEBUG
                , newVarRefsOffs, prevVarRefsOffs
#endif
            );
        }

        return true;
    }

    private RegisterOrder IsOptimizableLdrStrWithPair(instruction ins, regNumber reg1, regNumber reg2, nint imm,
        emitAttr size, insFormat fmt)
    {
        if ((ins != INS_ldr) && (ins != INS_str))
        {
            return RegisterOrder.eRO_none;
        }

        assert(emitLastIns is not null);
        if (ins != emitLastIns.idIns())
        {
            return RegisterOrder.eRO_none;
        }

        var prevReg1 = emitLastIns.idReg1();
        var prevReg2 = encodingZRtoSP(emitLastIns.idReg2());
        var lastInsFmt = emitLastIns.idInsFmt();
        var prevSize = emitLastIns.idOpSize();
        var prevImm = emitGetInsSC(emitLastIns);

        // Normalize raw byte offsets before comparing adjacency and pair-instruction range.
        var scale = NaturalScale_helper(size);
        var scaleMask = ((nint)1 << (int)scale) - 1;
        if (fmt == IF_LS_2C)
        {
            if ((imm & scaleMask) != 0)
            {
                return RegisterOrder.eRO_none;
            }

            imm >>= (int)scale;
        }

        if (lastInsFmt == IF_LS_2C)
        {
            if ((prevImm & scaleMask) != 0)
            {
                return RegisterOrder.eRO_none;
            }

            prevImm >>= (int)scale;
        }

        if ((imm < -64) || (imm > 63) || (prevImm < -64) || (prevImm > 63))
        {
            return RegisterOrder.eRO_none;
        }

        if ((reg1 == REG_SP) || (prevReg1 == REG_SP) ||
            (isGeneralRegisterOrZR(reg1) != isGeneralRegisterOrZR(prevReg1)))
        {
            return RegisterOrder.eRO_none;
        }

        var compatibleFmt = (lastInsFmt == fmt) || (lastInsFmt == IF_LS_2B && fmt == IF_LS_2A) ||
            (lastInsFmt == IF_LS_2A && fmt == IF_LS_2B);
        if (!compatibleFmt)
        {
            return RegisterOrder.eRO_none;
        }

        if (emitInsIsLoad(ins) && (prevReg1 == prevReg2))
        {
            return RegisterOrder.eRO_none;
        }

        if (emitInsIsLoad(ins) && (reg1 == prevReg1))
        {
            return RegisterOrder.eRO_none;
        }

        if (prevSize != size)
        {
            return RegisterOrder.eRO_none;
        }

        RegisterOrder optimizationOrder;
        if (imm == (prevImm + 1))
        {
            optimizationOrder = RegisterOrder.eRO_ascending;
        }
        else if (imm == (prevImm - 1))
        {
            optimizationOrder = RegisterOrder.eRO_descending;
        }
        else
        {
            return RegisterOrder.eRO_none;
        }

        if ((reg2 != prevReg2) || !isGeneralRegisterOrSP(reg2))
        {
            return RegisterOrder.eRO_none;
        }

        assert(_compiler is not null);
        // Do not change instruction sizes while unwind data is being generated.
        if (_compiler.compGeneratingUnwindProlog || _compiler.compGeneratingUnwindEpilog)
        {
            return RegisterOrder.eRO_none;
        }

        return optimizationOrder;
    }

    private bool IsOptimizableLdrToMov(instruction ins, regNumber reg1, regNumber reg2, nint imm,
        emitAttr size, insFormat fmt)
    {
        if (ins != INS_ldr)
        {
            return false;
        }

        assert(emitLastIns is not null);
        if (ins != emitLastIns.idIns())
        {
            return false;
        }

        var prevReg1 = emitLastIns.idReg1();
        var prevReg2 = encodingZRtoSP(emitLastIns.idReg2());
        var lastInsFmt = emitLastIns.idInsFmt();
        var prevSize = emitLastIns.idOpSize();
        var prevImm = emitGetInsSC(emitLastIns);

        if ((reg2 != prevReg2) || !isGeneralRegisterOrSP(reg2))
        {
            return false;
        }

        if (prevImm != imm)
        {
            return false;
        }

        if (!isGeneralRegister(reg1) || !isGeneralRegister(prevReg1))
        {
            return false;
        }

        if (lastInsFmt != fmt)
        {
            return false;
        }

        if (prevReg1 == prevReg2)
        {
            return false;
        }

        if (prevSize != size)
        {
            return false;
        }

        return true;
    }
}
#endif
