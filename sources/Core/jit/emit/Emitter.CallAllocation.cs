// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if MULTIREG_HAS_SECOND_GC_RET
using static RyuJitSharp.GCInfo.GCtype;
#endif

namespace RyuJitSharp;

public partial class Emitter
{
#if !TARGET_WASM
#if EMITTER_STATS
    private static uint emitTotalIDescCGCACnt;
#endif

#if TARGET_X86
    private regMaskTP CallScratchRegisters
    {
        get
        {
            assert(_compiler is not null);
            return new regMaskTP(
                _compiler.SRBM_INT_CALLEE_TRASH | _compiler.SRBM_FLT_CALLEE_TRASH, _compiler.SRBM_MSK_CALLEE_TRASH);
        }
    }
#endif

    private instrDescCGCA emitAllocInstrCGCA(emitAttr attr)
    {
#if EMITTER_STATS
        emitTotalIDescCGCACnt = unchecked(emitTotalIDescCGCACnt + 1);
#endif
        return emitAllocAnyInstr<instrDescCGCA>(unchecked((nuint)instrDescCGCA.NativeSize), attr);
    }

    private instrDesc emitNewInstrCallInd(int argCnt, nint disp, ReadOnlySpan<nint> GCvars,
        regMaskTP gcrefRegs, regMaskTP byrefRegs, emitAttr retSize,
#if MULTIREG_HAS_SECOND_GC_RET
        emitAttr secondRetSize,
#endif
        bool hasAsyncRet)
    {
        assert(_compiler is not null);
        if (retSize == EA_UNKNOWN)
        {
            retSize = EA_PTRSIZE;
        }

        var gcRefRegsInScratch = (gcrefRegs & RBM_CALLEE_TRASH) != RBM_NONE;
        var large = !VarSetOps.IsEmpty(_compiler, GCvars) || gcRefRegsInScratch || (byrefRegs != RBM_NONE) ||
#if TARGET_XARCH
            (disp < AM_DISP_MIN) || (disp > AM_DISP_MAX) ||
#endif
            (argCnt > ID_MAX_SMALL_CNS) || (argCnt < 0) ||
#if MULTIREG_HAS_SECOND_GC_RET
            (EA_IS_GCREF(secondRetSize) || EA_IS_BYREF(secondRetSize)) ||
#endif
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
#if MULTIREG_HAS_SECOND_GC_RET
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
#if TARGET_XARCH
            id.idAddr().iiaAddrMode.amDisp = (int)disp;
            assert(id.idAddr().iiaAddrMode.amDisp == disp);
#endif
            assert((gcrefRegs & RBM_CALLEE_TRASH) == RBM_NONE);
            emitEncodeCallGCregs(gcrefRegs, id);
            return id;
        }
    }

    private instrDesc emitNewInstrCallDir(int argCnt, ReadOnlySpan<nint> GCvars,
        regMaskTP gcrefRegs, regMaskTP byrefRegs, emitAttr retSize,
#if MULTIREG_HAS_SECOND_GC_RET
        emitAttr secondRetSize,
#endif
        bool hasAsyncRet)
    {
        assert(_compiler is not null);
        if (retSize == EA_UNKNOWN)
        {
            retSize = EA_PTRSIZE;
        }

        var gcRefRegsInScratch = (gcrefRegs & RBM_CALLEE_TRASH) != RBM_NONE;
        var large = !VarSetOps.IsEmpty(_compiler, GCvars) || gcRefRegsInScratch || (byrefRegs != RBM_NONE) ||
            (argCnt > ID_MAX_SMALL_CNS) || (argCnt < 0) ||
#if MULTIREG_HAS_SECOND_GC_RET
            (EA_IS_GCREF(secondRetSize) || EA_IS_BYREF(secondRetSize)) ||
#endif
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
#if MULTIREG_HAS_SECOND_GC_RET
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
            assert((gcrefRegs & RBM_CALLEE_TRASH) == RBM_NONE);
            emitEncodeCallGCregs(gcrefRegs, id);
            return id;
        }
    }

#if MULTIREG_HAS_SECOND_GC_RET
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
#endif
}
