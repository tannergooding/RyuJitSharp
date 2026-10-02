// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.regMask;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe uint emitOutputCallArm64Core(insGroup ig, byte* dst, instrDesc id, uint code)
    {
        assert(_compiler is not null);
        const byte CallInstructionSize = sizeof(uint);
        regMaskTP gcrefRegs;
        regMaskTP byrefRegs;
        VARSET_TP GCvars = [];

        if (id.idIsLargeCall())
        {
            var call = (instrDescCGCA)id;
            gcrefRegs = call.idcGcrefRegs;
            byrefRegs = call.idcByrefRegs;
            VarSetOps.Assign(_compiler, ref GCvars, call.idcGCvars);
        }
        else
        {
            assert(!id.idIsLargeDsp());
            assert(!id.idIsLargeCns());

            gcrefRegs = new regMaskTP((regMask)emitDecodeCallGCregs(id));
            byrefRegs = RBM_NONE;
            VarSetOps.AssignNoCopy(_compiler, ref GCvars, VarSetOps.MakeEmpty(_compiler));
        }

        // Stack variables die at the call's start, including non-returning THROW
        // helpers; return registers become live only after the instruction.
        emitUpdateLiveGCvars(GCvars, dst);
#if DEBUG
        if (_compiler.verbose || _compiler.opts.disasmWithGC)
        {
            emitDispGCVarDelta();
        }
#endif

        uint outputInstrSize = emitOutputLong(dst, code);
        dst = unchecked(dst + outputInstrSize);
        assert(outputInstrSize == CallInstructionSize);

        if (id.idGCref() == GCT_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET);
        }
        else if (id.idGCref() == GCT_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET);
        }

        if (id.idIsLargeCall())
        {
            var call = (instrDescCGCA)id;
            if (call.idSecondGCref() == GCT_GCREF)
            {
                gcrefRegs |= new regMaskTP(SRBM_INTRET_1);
            }
            else if (call.idSecondGCref() == GCT_BYREF)
            {
                byrefRegs |= new regMaskTP(SRBM_INTRET_1);
            }

            if (call.hasAsyncContinuationRet())
            {
                gcrefRegs |= new regMaskTP(REG_ASYNC_CONTINUATION_RET.SingleTypeMask);
            }
        }

        if (gcrefRegs != new regMaskTP(emitThisGCrefRegs))
        {
            emitUpdateLiveGCregs(GCT_GCREF, gcrefRegs, dst);
        }
        if (byrefRegs != new regMaskTP(emitThisByrefRegs))
        {
            emitUpdateLiveGCregs(GCT_BYREF, byrefRegs, dst);
        }

        if (!id.idIsNoGC())
        {
            emitStackPop(dst, true, CallInstructionSize, 0);
            if (!emitFullGCinfo)
            {
                emitRecordGCcall(dst, CallInstructionSize);
            }
        }

        return CallInstructionSize;
    }
}
#endif
