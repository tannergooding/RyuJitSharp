// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        RequireLoongArch64Target();

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
        if (Configuration.DEBUG && compiler.opts.disasmWithGC)
        {
            emitDispGCVarDelta();
        }

        assert(id.idIns() == INS_jirl);
        var originalDst = dst;
        if (id.idIsCallRegPtr())
        {
            var code = emitInsCodeLoongArch64(id.idIns());
            code |= unchecked((uint)id.idReg4());
            code |= unchecked((uint)id.idReg3()) << 5;
            emitOutputLoongArch64Instruction(dst, code);
        }
        else if (id.idIsReloc())
        {
            emitOutputLoongArch64Instruction(dst, 0x1e000000 | unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister));
            var address = unchecked((nuint)id.idAddr().iiaAddr);
            var linkRegister = unchecked((int)(address & 1));
            address = unchecked(address - (nuint)linkRegister);
            assert((address & 3) == 0);

            dst += sizeof(uint);
            emitGCregDeadUpd(TargetRuntimeMetadata.DefaultHelperCallTargetRegister, dst);
            emitOutputLoongArch64Instruction(dst, 0x4c000000 |
                (unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister) << 5) | unchecked((uint)linkRegister));
            emitRecordRelocation(dst - sizeof(uint), unchecked((void*)address), CorInfoReloc.LOONGARCH64_JIR);
        }
        else
        {
            var immediate = unchecked((nint)id.idAddr().iiaAddr);
            assert(unchecked((nuint)(immediate >> 32)) <= 0x7ffff);
            var linkRegister = unchecked((uint)immediate) & 1;
            immediate = unchecked(immediate - (nint)linkRegister);

            var code = emitInsCodeLoongArch64(INS_lu12i_w);
            code |= unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister);
            code |= (unchecked((uint)(immediate >> 12)) & 0xfffff) << 5;
            emitOutputLoongArch64Instruction(dst, code);
            dst += sizeof(uint);
            emitGCregDeadUpd(TargetRuntimeMetadata.DefaultHelperCallTargetRegister, dst);

            code = emitInsCodeLoongArch64(INS_ori);
            code |= unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister);
            code |= unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister) << 5;
            code |= (unchecked((uint)immediate) & 0xfff) << 10;
            emitOutputLoongArch64Instruction(dst, code);
            dst += sizeof(uint);

            code = emitInsCodeLoongArch64(INS_lu32i_d);
            code |= unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister);
            code |= (unchecked((uint)(immediate >> 32)) & 0x7ffff) << 5;
            emitOutputLoongArch64Instruction(dst, code);
            dst += sizeof(uint);

            code = emitInsCodeLoongArch64(INS_jirl);
            code |= linkRegister;
            code |= unchecked((uint)TargetRuntimeMetadata.DefaultHelperCallTargetRegister) << 5;
            emitOutputLoongArch64Instruction(dst, code);
        }

        dst += sizeof(uint);
        if (id.idGCref() == GCT_GCREF)
        {
            gcrefRegs |= TargetRuntimeMetadata.IntegerReturnRegisterMask;
        }
        else if (id.idGCref() == GCT_BYREF)
        {
            byrefRegs |= TargetRuntimeMetadata.IntegerReturnRegisterMask;
        }

        if (id.idIsLargeCall())
        {
            var callDescriptor = (instrDescCGCA)id;
            if (callDescriptor.idSecondGCref() == GCT_GCREF)
            {
                gcrefRegs |= TargetRuntimeMetadata.IntegerReturnRegister1Mask;
            }
            else if (callDescriptor.idSecondGCref() == GCT_BYREF)
            {
                byrefRegs |= TargetRuntimeMetadata.IntegerReturnRegister1Mask;
            }
            if (callDescriptor.hasAsyncContinuationRet())
            {
                gcrefRegs |= TargetRuntimeMetadata.AsyncContinuationReturnRegisterMask;
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
