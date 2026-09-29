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
        if (dst >= emitCodeBlock && dst <= emitCodeBlock + emitTotalHotCodeSize)
        {
            distance = (nuint)(dst - emitCodeBlock);
        }
        else
        {
            assert(emitFirstColdIG is not null && emitColdCodeBlock != null);
            assert(dst >= emitColdCodeBlock && dst <= emitColdCodeBlock + emitTotalColdCodeSize);
            distance = (nuint)(dst - emitColdCodeBlock + emitTotalHotCodeSize);
        }
        noway_assert(distance <= uint.MaxValue);
        return unchecked((uint)distance);
    }

    private unsafe void emitGCregLiveSet(GCInfo.GCtype gcType, regMaskTP mask, byte* dst, bool isThis)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        assert(gcType != GCT_NONE);
        assert(!isThis || (_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
            .lvaKeepAliveAndReportThis());
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
#if DEBUG
        assert(emitIssuing);
#endif
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
#if DEBUG
        assert(emitIssuing);
#endif
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
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
#endif
    }

    private unsafe void emitGCregLiveUpd(GCInfo.GCtype gcType, regNumber reg, byte* dst)
    {
#if DEBUG
        assert(emitIssuing);
#endif
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
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
#endif
    }
}
