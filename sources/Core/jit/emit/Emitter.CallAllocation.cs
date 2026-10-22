// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Emitter.EmitterStatistics;

namespace RyuJitSharp;

public partial class Emitter
{
    private regMaskTP CallScratchRegisters
    {
        get
        {
            if (!Configuration.TARGET_XARCH)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.CallScratchRegistersAreOnlyDefinedForXarch);
                return default!;
            }

            assert(_compiler is not null);
            return new regMaskTP(
                _compiler.SRBM_INT_CALLEE_TRASH | _compiler.SRBM_FLT_CALLEE_TRASH, _compiler.SRBM_MSK_CALLEE_TRASH);
        }
    }

    private instrDescCGCA emitAllocInstrCGCA(emitAttr attr)
    {
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIDescCGCACnt = unchecked(emitTotalIDescCGCACnt + 1);
        }
        return emitAllocAnyInstr<instrDescCGCA>(unchecked((nuint)instrDescCGCA.NativeSize), attr);
    }

    private instrDesc emitNewInstrCallInd(int argCnt, nint disp, ReadOnlySpan<nint> GCvars,
        regMaskTP gcrefRegs, regMaskTP byrefRegs, emitAttr retSize, emitAttr secondRetSize, bool hasAsyncRet)
    {
        assert(_compiler is not null);
        if (retSize == EA_UNKNOWN)
        {
            retSize = EA_PTRSIZE;
        }

        var gcRefRegsInScratch = (gcrefRegs & RBM_CALLEE_TRASH) != RBM_NONE;
        var large = !VarSetOps.IsEmpty(_compiler, GCvars) || gcRefRegsInScratch || (byrefRegs != RBM_NONE) ||
            (Configuration.TARGET_XARCH && ((disp < AddressModeConstants.AM_DISP_MIN) || (disp > AddressModeConstants.AM_DISP_MAX))) ||
            (argCnt > ID_MAX_SMALL_CNS) || (argCnt < 0) ||
            (Configuration.MULTIREG_HAS_SECOND_GC_RET &&
                (EA_IS_GCREF(secondRetSize) || EA_IS_BYREF(secondRetSize))) ||
            hasAsyncRet;

        if (large)
        {
            var id = emitAllocInstrCGCA(retSize);
            id.idSetIsLargeCall();
            VarSetOps.Assign(_compiler, ref id.idcGCvars, GCvars);
            id.idcGcrefRegs = gcrefRegs;
            id.idcByrefRegs = byrefRegs;
            id.idcArgCnt = unchecked((uint)argCnt);
            id.idcDisp = disp;
            emitSetSecondRetRegGCType(id, secondRetSize);
            id.hasAsyncContinuationRet(hasAsyncRet);
            return id;
        }
        else
        {
            var id = emitNewInstrCns(retSize, argCnt);
            assert(!id.idIsLargeCns());
            id.idSetIsCall();
            if (Configuration.TARGET_XARCH)
            {
                id.idAddr().iiaAddrMode.amDisp = (int)disp;
                assert(id.idAddr().iiaAddrMode.amDisp == disp);
            }
            assert((gcrefRegs & RBM_CALLEE_TRASH) == RBM_NONE);
            emitEncodeCallGCregs(gcrefRegs, id);
            return id;
        }
    }

    private instrDesc emitNewInstrCallDir(int argCnt, ReadOnlySpan<nint> GCvars,
        regMaskTP gcrefRegs, regMaskTP byrefRegs, emitAttr retSize, emitAttr secondRetSize, bool hasAsyncRet)
    {
        assert(_compiler is not null);
        if (retSize == EA_UNKNOWN)
        {
            retSize = EA_PTRSIZE;
        }

        var gcRefRegsInScratch = (gcrefRegs & RBM_CALLEE_TRASH) != RBM_NONE;
        var large = !VarSetOps.IsEmpty(_compiler, GCvars) || gcRefRegsInScratch || (byrefRegs != RBM_NONE) ||
            (argCnt > ID_MAX_SMALL_CNS) || (argCnt < 0) ||
            (Configuration.MULTIREG_HAS_SECOND_GC_RET &&
                (EA_IS_GCREF(secondRetSize) || EA_IS_BYREF(secondRetSize))) ||
            hasAsyncRet;

        if (large)
        {
            var id = emitAllocInstrCGCA(retSize);
            id.idSetIsLargeCall();
            VarSetOps.Assign(_compiler, ref id.idcGCvars, GCvars);
            id.idcGcrefRegs = gcrefRegs;
            id.idcByrefRegs = byrefRegs;
            id.idcDisp = 0;
            id.idcArgCnt = unchecked((uint)argCnt);
            emitSetSecondRetRegGCType(id, secondRetSize);
            id.hasAsyncContinuationRet(hasAsyncRet);
            return id;
        }
        else
        {
            var id = emitNewInstrCns(retSize, argCnt);
            assert(!id.idIsLargeCns());
            id.idSetIsCall();
            assert((gcrefRegs & RBM_CALLEE_TRASH) == RBM_NONE);
            emitEncodeCallGCregs(gcrefRegs, id);
            return id;
        }
    }

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
}
