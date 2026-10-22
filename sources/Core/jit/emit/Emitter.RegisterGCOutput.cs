// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe uint emitCurCodeOffs(byte* dst)
    {
        nuint distance;
        if (dst >= emitCodeBlock && dst <= emitCodeBlock + unchecked((uint)emitTotalHotCodeSize))
        {
            distance = (nuint)(dst - emitCodeBlock);
        }
        else
        {
            assert(emitFirstColdIG is not null && emitColdCodeBlock != null);
            assert(dst >= emitColdCodeBlock && dst <= emitColdCodeBlock + unchecked((uint)emitTotalColdCodeSize));
            distance = (nuint)(dst - emitColdCodeBlock + unchecked((uint)emitTotalHotCodeSize));
        }
        noway_assert(distance <= uint.MaxValue);
        return unchecked((uint)distance);
    }

    private unsafe void emitGCregLiveSet(GCInfo.GCtype gcType, regMaskTP mask, byte* dst, bool isThis)
    {
        if (Configuration.DEBUG)
        {
            assert(emitIssuing);
        }
        assert(gcType != GCT_NONE);
        if (Configuration.DEBUG)
        {
            bool __jitExceptionArgument298;
            bool __jitExceptionShortCircuitLeft299;
            __jitExceptionShortCircuitLeft299 = !isThis;
            if (__jitExceptionShortCircuitLeft299)
            {
                __jitExceptionArgument298 = true;
            }
            else
            {
                global::RyuJitSharp.Compiler __jitExceptionReceiver300;
                var __jitExceptionCoalesceLeft301 = _compiler;
                if (__jitExceptionCoalesceLeft301 != null)
                {
                    __jitExceptionReceiver300 = __jitExceptionCoalesceLeft301;
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                    __jitExceptionReceiver300 = default;
                }

                __jitExceptionArgument298 = __jitExceptionReceiver300.lvaKeepAliveAndReportThis();
            }

            assert(__jitExceptionArgument298);
        }
        assert(emitFullGCinfo);
        assert((new regMaskTP(emitThisGCrefRegs | emitThisByrefRegs) & mask).IsEmpty);
        var descriptor = gcInfo.gcRegPtrAllocDsc();
        descriptor.rpdGCtype = gcType;
        descriptor.rpdOffs = emitCurCodeOffs(dst);
        descriptor.rpdArg = false;
        descriptor.rpdCall = false;
        descriptor.rpdIsThis = isThis;
        descriptor.rpdCompiler.rpdAdd = (regMask)mask;
        descriptor.rpdCompiler.rpdDel = SRBM_NONE;
    }

    private unsafe void emitGCregDeadSet(GCInfo.GCtype gcType, regMaskTP mask, byte* dst)
    {
        if (Configuration.DEBUG)
        {
            assert(emitIssuing);
        }
        assert(gcType != GCT_NONE && emitFullGCinfo);
        assert((new regMaskTP(emitThisGCrefRegs | emitThisByrefRegs) & mask).IsNonEmpty);
        var descriptor = gcInfo.gcRegPtrAllocDsc();
        descriptor.rpdGCtype = gcType;
        descriptor.rpdOffs = emitCurCodeOffs(dst);
        descriptor.rpdCall = false;
        descriptor.rpdIsThis = false;
        descriptor.rpdArg = false;
        descriptor.rpdCompiler.rpdAdd = SRBM_NONE;
        descriptor.rpdCompiler.rpdDel = (regMask)mask;
    }

    private unsafe void emitGCregDeadUpd(regNumber reg, byte* dst)
    {
        if (Configuration.DEBUG)
        {
            assert(emitIssuing);
        }
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            if (emitIGisInEpilog(emitCurIG))
            {
                return;
            }

            var mask = reg.SingleTypeMask;
            if ((emitThisGCrefRegs & mask) != SRBM_NONE)
            {
                assert((emitThisByrefRegs & mask) == SRBM_NONE);
                if (emitFullGCinfo)
                {
                    emitGCregDeadSet(GCT_GCREF, new regMaskTP(mask), dst);
                }
                emitThisGCrefRegs &= ~mask;
            }
            else if ((emitThisByrefRegs & mask) != SRBM_NONE)
            {
                if (emitFullGCinfo)
                {
                    emitGCregDeadSet(GCT_BYREF, new regMaskTP(mask), dst);
                }
                emitThisByrefRegs &= ~mask;
            }
        }
    }

    private unsafe void emitGCregDeadUpdMask(regMaskTP regs, byte* addr)
    {
        if (Configuration.DEBUG)
        {
            assert(emitIssuing);
        }
        if (emitIGisInEpilog(emitCurIG))
        {
            return;
        }

        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            var gcrefRegs = new regMaskTP(emitThisGCrefRegs) & regs;
            assert(emitSyncThisObjReg == REG_NA ||
                (regMaskTP.CreateFromRegNum(emitSyncThisObjReg, emitSyncThisObjReg.SingleTypeMask) & regs).IsEmpty);

            if (gcrefRegs.IsNonEmpty)
            {
                assert((emitThisByrefRegs & (regMask)gcrefRegs) == SRBM_NONE);
                if (emitFullGCinfo)
                {
                    emitGCregDeadSet(GCT_GCREF, gcrefRegs, addr);
                }
                emitThisGCrefRegs &= ~(regMask)gcrefRegs;
            }

            var byrefRegs = new regMaskTP(emitThisByrefRegs) & regs;
            if (byrefRegs.IsNonEmpty)
            {
                assert((emitThisGCrefRegs & (regMask)byrefRegs) == SRBM_NONE);
                if (emitFullGCinfo)
                {
                    emitGCregDeadSet(GCT_BYREF, byrefRegs, addr);
                }
                emitThisByrefRegs &= ~(regMask)byrefRegs;
            }
        }
    }

    private unsafe void emitGCregLiveUpd(GCInfo.GCtype gcType, regNumber reg, byte* dst)
    {
        if (Configuration.DEBUG)
        {
            assert(emitIssuing);
        }
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            if (emitIGisInEpilog(emitCurIG))
            {
                return;
            }

            assert(gcType is GCT_GCREF or GCT_BYREF);
            var mask = reg.SingleTypeMask;
            var live = gcType == GCT_GCREF ? emitThisGCrefRegs : emitThisByrefRegs;
            var other = gcType == GCT_GCREF ? emitThisByrefRegs : emitThisGCrefRegs;
            if ((live & mask) == SRBM_NONE)
            {
                if ((other & mask) != SRBM_NONE)
                {
                    emitGCregDeadUpd(reg, dst);
                }
                if (emitFullGCinfo)
                {
                    emitGCregLiveSet(gcType, new regMaskTP(mask), dst, reg == emitSyncThisObjReg);
                }
                if (gcType == GCT_GCREF)
                {
                    emitThisGCrefRegs |= mask;
                }
                else
                {
                    emitThisByrefRegs |= mask;
                }
            }
            assert((emitThisGCrefRegs & emitThisByrefRegs) == SRBM_NONE);
        }
    }
}
