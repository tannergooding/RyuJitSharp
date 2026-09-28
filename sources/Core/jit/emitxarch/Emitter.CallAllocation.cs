// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if UNIX_AMD64_ABI
using static RyuJitSharp.GCInfo.GCtype;
#endif

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private instrDesc emitNewInstrCallInd(int argCnt, nint disp, ReadOnlySpan<nint> GCvars,
        regMaskTP gcrefRegs, regMaskTP byrefRegs, emitAttr retSize,
#if UNIX_AMD64_ABI
        emitAttr secondRetSize,
#endif
        bool hasAsyncRet)
    {
        assert(_compiler is not null);
        if (retSize == EA_UNKNOWN)
        {
            retSize = EA_PTRSIZE;
        }

        var large = !VarSetOps.IsEmpty(_compiler, GCvars) ||
            ((gcrefRegs & CallScratchRegisters) != RBM_NONE) || (byrefRegs != RBM_NONE) ||
            (disp < AM_DISP_MIN) || (disp > AM_DISP_MAX) || !instrDesc.fitsInSmallCns(argCnt) ||
            (argCnt < 0) ||
#if UNIX_AMD64_ABI
            (EA_IS_GCREF(secondRetSize) || EA_IS_BYREF(secondRetSize)) ||
#endif
            hasAsyncRet;

        if (large)
        {
            var id = emitAllocAnyInstr<instrDescCGCA>(72, retSize);
            id.idSetIsLargeCall();
            VarSetOps.Assign(_compiler, ref id.idcGCvars, GCvars);
            id.idcGcrefRegs = gcrefRegs;
            id.idcByrefRegs = byrefRegs;
            id.idcArgCnt = unchecked((uint)argCnt);
            id.idcDisp = disp;
#if UNIX_AMD64_ABI
            emitSetSecondRetRegGCType(id, secondRetSize);
#endif
            id.hasAsyncContinuationRet(hasAsyncRet);
            return id;
        }
        else
        {
            var id = emitNewInstrCns(retSize, argCnt);
            assert(!id.idIsLargeCns());
            id.idSetIsCall();
            id.idAddr().iiaAddrMode.amDisp = (int)disp;
            assert(id.idAddr().iiaAddrMode.amDisp == disp);
            assert((gcrefRegs & CallScratchRegisters) == RBM_NONE);
            emitEncodeCallGCregs(gcrefRegs, id);
            return id;
        }
    }

    private instrDesc emitNewInstrCallDir(int argCnt, ReadOnlySpan<nint> GCvars,
        regMaskTP gcrefRegs, regMaskTP byrefRegs, emitAttr retSize,
#if UNIX_AMD64_ABI
        emitAttr secondRetSize,
#endif
        bool hasAsyncRet)
    {
        assert(_compiler is not null);
        if (retSize == EA_UNKNOWN)
        {
            retSize = EA_PTRSIZE;
        }

        var large = !VarSetOps.IsEmpty(_compiler, GCvars) ||
            ((gcrefRegs & CallScratchRegisters) != RBM_NONE) || (byrefRegs != RBM_NONE) ||
            !instrDesc.fitsInSmallCns(argCnt) || (argCnt < 0) ||
#if UNIX_AMD64_ABI
            (EA_IS_GCREF(secondRetSize) || EA_IS_BYREF(secondRetSize)) ||
#endif
            hasAsyncRet;

        if (large)
        {
            var id = emitAllocAnyInstr<instrDescCGCA>(72, retSize);
            id.idSetIsLargeCall();
            VarSetOps.Assign(_compiler, ref id.idcGCvars, GCvars);
            id.idcGcrefRegs = gcrefRegs;
            id.idcByrefRegs = byrefRegs;
            id.idcDisp = 0;
            id.idcArgCnt = unchecked((uint)argCnt);
#if UNIX_AMD64_ABI
            emitSetSecondRetRegGCType(id, secondRetSize);
#endif
            id.hasAsyncContinuationRet(hasAsyncRet);
            return id;
        }
        else
        {
            var id = emitNewInstrCns(retSize, argCnt);
            assert(!id.idIsLargeCns());
            id.idSetIsCall();
            assert((gcrefRegs & CallScratchRegisters) == RBM_NONE);
            emitEncodeCallGCregs(gcrefRegs, id);
            return id;
        }
    }

#if UNIX_AMD64_ABI
    private static void emitSetSecondRetRegGCType(instrDescCGCA id, emitAttr secondRetSize)
    {
        if (EA_IS_GCREF(secondRetSize))
        {
            id.idSecondGCref(GCT_GCREF);
        }
        else if (EA_IS_BYREF(secondRetSize))
        {
            id.idSecondGCref(GCT_BYREF);
        }
        else
        {
            id.idSecondGCref(GCT_NONE);
        }
    }
#endif

    private static void emitEncodeCallGCregs(regMaskTP gcRefRegs, instrDesc id)
    {
        var regs = gcRefRegs.Lower;
        uint reg1 = 0;
        uint reg2 = 0;

        if ((regs & SRBM_ESI) != 0)
        {
            reg1 |= 1;
        }
        if ((regs & SRBM_EDI) != 0)
        {
            reg1 |= 2;
        }
        if ((regs & SRBM_EBX) != 0)
        {
            reg1 |= 4;
        }
        if ((regs & SRBM_EBP) != 0)
        {
            reg1 |= 8;
        }

        if ((regs & SRBM_R12) != 0)
        {
            reg2 |= 1;
        }
        if ((regs & SRBM_R13) != 0)
        {
            reg2 |= 2;
        }
        if ((regs & SRBM_R14) != 0)
        {
            reg2 |= 4;
        }
        if ((regs & SRBM_R15) != 0)
        {
            reg2 |= 8;
        }

        id.idReg1((regNumber)reg1);
        id.idReg2((regNumber)reg2);
    }

    private static uint emitDecodeCallGCregs(instrDesc id)
    {
        var reg1 = (uint)id.idReg1();
        var reg2 = (uint)id.idReg2();
        var regs = SRBM_NONE;

        if ((reg1 & 1) != 0)
        {
            regs |= SRBM_ESI;
        }
        if ((reg1 & 2) != 0)
        {
            regs |= SRBM_EDI;
        }
        if ((reg1 & 4) != 0)
        {
            regs |= SRBM_EBX;
        }
        if ((reg1 & 8) != 0)
        {
            regs |= SRBM_EBP;
        }

        if ((reg2 & 1) != 0)
        {
            regs |= SRBM_R12;
        }
        if ((reg2 & 2) != 0)
        {
            regs |= SRBM_R13;
        }
        if ((reg2 & 4) != 0)
        {
            regs |= SRBM_R14;
        }
        if ((reg2 & 8) != 0)
        {
            regs |= SRBM_R15;
        }

        return (uint)regs;
    }
#endif
}
