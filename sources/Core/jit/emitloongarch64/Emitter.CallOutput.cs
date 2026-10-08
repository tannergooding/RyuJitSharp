// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System.Runtime.CompilerServices;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe uint emitOutputCallLoongArch64(byte* dst, instrDesc id)
    {
        var compiler = _compiler
            ?? throw new FatalJitException("LoongArch64 call output requires an active compiler.");
        regMaskTP gcrefRegs;
        regMaskTP byrefRegs;
        VARSET_TP gcVars = [];

        if (id.idIsLargeCall())
        {
            var callDescriptor = (instrDescCGCA)id;
            gcrefRegs = callDescriptor.idcGcrefRegs;
            byrefRegs = callDescriptor.idcByrefRegs;
            VarSetOps.Assign(compiler, ref gcVars, callDescriptor.idcGCvars);
        }
        else
        {
            assert(!id.idIsLargeDsp());
            assert(!id.idIsLargeCns());
            gcrefRegs = new regMaskTP((regMask)emitDecodeCallGCregs(id));
            byrefRegs = default;
            VarSetOps.AssignNoCopy(compiler, ref gcVars, VarSetOps.MakeEmpty(compiler));
        }

        emitUpdateLiveGCvars(gcVars, dst);
#if DEBUG
        if (compiler.opts.disasmWithGC)
        {
            emitDispGCVarDelta();
        }
#endif

        assert(id.idIns() == INS_jirl);
        var originalDst = dst;
        if (id.idIsCallRegPtr())
        {
            var code = emitInsCode(id.idIns());
            code |= unchecked((uint)id.idReg4());
            code |= unchecked((uint)id.idReg3()) << 5;
            emitOutputLoongArch64Instruction(dst, code);
        }
        else if (id.idIsReloc())
        {
            emitOutputLoongArch64Instruction(dst, 0x1e000000 | unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET));
            var address = unchecked((nuint)id.idAddr().iiaAddr);
            var linkRegister = unchecked((int)(address & 1));
            address = unchecked(address - (nuint)linkRegister);
            assert((address & 3) == 0);

            dst += sizeof(uint);
            emitGCregDeadUpd(REG_DEFAULT_HELPER_CALL_TARGET, dst);
            emitOutputLoongArch64Instruction(dst, 0x4c000000 |
                (unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET) << 5) | unchecked((uint)linkRegister));
            emitRecordRelocation(dst - sizeof(uint), unchecked((void*)address), CorInfoReloc.LOONGARCH64_JIR);
        }
        else
        {
            var immediate = unchecked((nint)id.idAddr().iiaAddr);
            assert(unchecked((nuint)(immediate >> 32)) <= 0x7ffff);
            var linkRegister = unchecked((uint)immediate) & 1;
            immediate = unchecked(immediate - (nint)linkRegister);

            var code = emitInsCode(INS_lu12i_w);
            code |= unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET);
            code |= (unchecked((uint)(immediate >> 12)) & 0xfffff) << 5;
            emitOutputLoongArch64Instruction(dst, code);
            dst += sizeof(uint);
            emitGCregDeadUpd(REG_DEFAULT_HELPER_CALL_TARGET, dst);

            code = emitInsCode(INS_ori);
            code |= unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET);
            code |= unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET) << 5;
            code |= (unchecked((uint)immediate) & 0xfff) << 10;
            emitOutputLoongArch64Instruction(dst, code);
            dst += sizeof(uint);

            code = emitInsCode(INS_lu32i_d);
            code |= unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET);
            code |= (unchecked((uint)(immediate >> 32)) & 0x7ffff) << 5;
            emitOutputLoongArch64Instruction(dst, code);
            dst += sizeof(uint);

            code = emitInsCode(INS_jirl);
            code |= linkRegister;
            code |= unchecked((uint)REG_DEFAULT_HELPER_CALL_TARGET) << 5;
            emitOutputLoongArch64Instruction(dst, code);
        }

        dst += sizeof(uint);
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
            var callDescriptor = (instrDescCGCA)id;
            if (callDescriptor.idSecondGCref() == GCT_GCREF)
            {
                gcrefRegs |= new regMaskTP(SRBM_INTRET_1);
            }
            else if (callDescriptor.idSecondGCref() == GCT_BYREF)
            {
                byrefRegs |= new regMaskTP(SRBM_INTRET_1);
            }
            if (callDescriptor.hasAsyncContinuationRet())
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
            emitStackPop(dst, true, sizeof(uint), 0);
            if (!emitFullGCinfo)
            {
                emitRecordGCcall(dst, sizeof(uint));
            }
        }

        assert(dst > originalDst);
        return id.idCodeSize();
    }

    private unsafe void emitOutputLoongArch64Instruction(byte* dst, uint code)
    {
        assert(dst is not null);
        Unsafe.WriteUnaligned(unchecked(dst + writeableOffset), code);
    }
}
#endif
