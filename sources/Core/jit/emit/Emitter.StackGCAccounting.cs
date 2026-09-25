// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using GCtype = RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private static bool emitStackNeedsGC(GCInfo.GCtype type)
    {
        assert(type is GCT_NONE or GCT_GCREF or GCT_BYREF);
        return type is GCT_GCREF or GCT_BYREF;
    }
#endif

#if EMIT_TRACK_STACK_DEPTH && TARGET_AMD64
    private const int MAX_SIMPLE_STK_DEPTH = sizeof(uint) * 8;
#endif

    internal unsafe void emitStackPush(byte* addr, GCInfo.GCtype gcType)
    {
#if !EMIT_TRACK_STACK_DEPTH || !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack GC accounting requires Windows AMD64.");
#else
        assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
        if (emitSimpleStkUsed)
        {
            assert(!emitFullGCinfo);
            assert(emitCurStackLvl / sizeof(int) < MAX_SIMPLE_STK_DEPTH);
            u1.emitSimpleStkMask = unchecked((u1.emitSimpleStkMask << 1) | (emitStackNeedsGC(gcType) ? 1 : 0));
            u1.emitSimpleByrefStkMask = unchecked((u1.emitSimpleByrefStkMask << 1) | (gcType == GCT_BYREF ? 1 : 0));
            assert((u1.emitSimpleStkMask & u1.emitSimpleByrefStkMask) == u1.emitSimpleByrefStkMask);
        }
        else
        {
            emitStackPushLargeStk(addr, gcType);
        }

        emitCurStackLvl = unchecked(emitCurStackLvl + sizeof(int));
#endif
    }

    internal unsafe void emitStackPushN(byte* addr, uint count)
    {
#if !EMIT_TRACK_STACK_DEPTH || !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack GC accounting requires Windows AMD64.");
#else
        assert(count != 0);
        if (emitSimpleStkUsed)
        {
            assert(!emitFullGCinfo);
            u1.emitSimpleStkMask = unchecked(u1.emitSimpleStkMask << (int)count);
            u1.emitSimpleByrefStkMask = unchecked(u1.emitSimpleByrefStkMask << (int)count);
        }
        else
        {
            emitStackPushLargeStk(addr, GCT_NONE, count);
        }

        emitCurStackLvl = unchecked(emitCurStackLvl + (int)unchecked(count * sizeof(int)));
#endif
    }

    internal unsafe void emitStackPop(byte* addr, bool isCall, byte callInstrSize, uint count = 1)
    {
#if !EMIT_TRACK_STACK_DEPTH || !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack GC accounting requires Windows AMD64.");
#else
        assert((uint)emitCurStackLvl / sizeof(int) >= count);
        assert(!isCall || callInstrSize > 0);

        if (count != 0)
        {
            if (emitSimpleStkUsed)
            {
                assert(!emitFullGCinfo);
                var remaining = count;
                do
                {
                    u1.emitSimpleStkMask = (int)(unchecked((uint)u1.emitSimpleStkMask) >> 1);
                    u1.emitSimpleByrefStkMask = (int)(unchecked((uint)u1.emitSimpleByrefStkMask) >> 1);
                } while (--remaining != 0);
            }
            else
            {
                emitStackPopLargeStk(addr, isCall, callInstrSize, count);
            }
            emitCurStackLvl = unchecked(emitCurStackLvl - (int)unchecked(count * sizeof(int)));
        }
        else
        {
            assert(isCall);
            if (emitFullGCinfo || (codeGen.IsFullPtrRegMapRequired && !codeGen.Interruptible && isCall))
            {
                emitStackPopLargeStk(addr, isCall, callInstrSize, 0);
            }
        }
#endif
    }

    private unsafe void emitStackPushLargeStk(byte* addr, GCInfo.GCtype gcType, uint count = 1)
    {
#if EMIT_TRACK_STACK_DEPTH && TARGET_AMD64 && WINDOWS_AMD64_ABI
        var level = (uint)emitCurStackLvl / sizeof(int);
        assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
        assert(count != 0 && !emitSimpleStkUsed);

        do
        {
            noway_assert(u2.emitArgTrackTab != null && u2.emitArgTrackTop != null);
            assert(u2.emitArgTrackTop == u2.emitArgTrackTab + level);
            noway_assert(u2.emitArgTrackTop < u2.emitArgTrackTab + emitMaxStackDepth);
            *u2.emitArgTrackTop++ = (byte)gcType;

            if (emitFullArgInfo || emitStackNeedsGC(gcType))
            {
                if (emitFullGCinfo)
                {
                    var descriptor = gcInfo.gcRegPtrAllocDsc();
                    descriptor.rpdGCtype = gcType;
                    descriptor.rpdOffs = emitCurCodeOffs(addr);
                    descriptor.rpdArg = true;
                    descriptor.rpdCall = false;
                    if (level > ushort.MaxValue)
                    {
                        IMPL_LIMITATION("Too many/too big arguments to encode GC information");
                    }
                    descriptor.rpdCallData.rpdPtrArg = (ushort)level;
                    descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_PUSH;
                    descriptor.rpdIsThis = false;
                }

                u2.emitGcArgTrackCnt = unchecked((ushort)(u2.emitGcArgTrackCnt + 1));
            }
            level = unchecked(level + 1);
            noway_assert(level != 0);
        } while (--count != 0);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Large-stack GC push accounting requires Windows AMD64.");
#endif
    }

    private unsafe void emitStackPopLargeStk(byte* addr, bool isCall, byte callInstrSize, uint count = 1)
    {
#if EMIT_GENERATE_GCINFO && EMIT_TRACK_STACK_DEPTH && TARGET_AMD64 && WINDOWS_AMD64_ABI
#if DEBUG
        assert(emitIssuing);
#endif
        assert(!emitSimpleStkUsed);
        var argRecCnt = 0u;
        for (var remaining = count; remaining != 0; remaining--)
        {
            assert(u2.emitArgTrackTop > u2.emitArgTrackTab);
            var gcType = (GCtype)(*--u2.emitArgTrackTop);
            assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
            if (emitFullArgInfo || emitStackNeedsGC(gcType))
            {
                argRecCnt++;
            }
        }

        assert(u2.emitArgTrackTop >= u2.emitArgTrackTab);
        assert(u2.emitArgTrackTop == u2.emitArgTrackTab + (uint)emitCurStackLvl / sizeof(int) - count);
        noway_assert(argRecCnt <= ushort.MaxValue);
        noway_assert(argRecCnt <= u2.emitGcArgTrackCnt);
        u2.emitGcArgTrackCnt = (ushort)(u2.emitGcArgTrackCnt - argRecCnt);

        var gcrefRegs = unchecked((uint)emitThisGCrefRegs) >> (int)REG_INT_FIRST;
        var byrefRegs = unchecked((uint)emitThisByrefRegs) >> (int)REG_INT_FIRST;
        assert(new regMaskTP((regMask)((ulong)gcrefRegs << (int)REG_INT_FIRST)) ==
            new regMaskTP(emitThisGCrefRegs));
        assert(new regMaskTP((regMask)((ulong)byrefRegs << (int)REG_INT_FIRST)) ==
            new regMaskTP(emitThisByrefRegs));

        var isCallRelatedPop = argRecCnt > 1;
        var descriptor = gcInfo.gcRegPtrAllocDsc();
        descriptor.rpdGCtype = GCT_GCREF;
        descriptor.rpdOffs = emitCurCodeOffs(addr);
        descriptor.rpdCall = isCall || isCallRelatedPop;
#if !JIT32_GCENCODER
        if (descriptor.rpdCall)
        {
            assert(isCall || callInstrSize == 0);
            descriptor.rpdCallInstrSize = callInstrSize;
        }
#endif
        descriptor.rpdCallData.rpdCallGCrefRegs = gcrefRegs;
        descriptor.rpdCallData.rpdCallByrefRegs = byrefRegs;
        descriptor.rpdArg = true;
        descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_POP;
        descriptor.rpdCallData.rpdPtrArg = (ushort)argRecCnt;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Large-stack GC pop accounting requires Windows AMD64 GC info.");
#endif
    }

    internal unsafe void emitStackKillArgs(byte* addr, uint count, byte callInstrSize)
    {
#if !EMIT_TRACK_STACK_DEPTH || !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack GC accounting requires Windows AMD64.");
#else
        assert(count != 0);
        if (emitSimpleStkUsed)
        {
            assert(!emitFullGCinfo);
            assert((uint)emitCurStackLvl / sizeof(int) >= count);
            for (var level = 0u; level < count; level++)
            {
                u1.emitSimpleStkMask &= unchecked((int)~(1u << (int)level));
                u1.emitSimpleByrefStkMask &= unchecked((int)~(1u << (int)level));
            }
            return;
        }

        var argTrackTop = u2.emitArgTrackTop;
        var gcCnt = 0u;
        for (var i = 0u; i < count; i++)
        {
            assert(argTrackTop > u2.emitArgTrackTab);
            --argTrackTop;
            var gcType = (GCtype)(*argTrackTop);
            assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
            if (emitStackNeedsGC(gcType))
            {
                *argTrackTop = (byte)GCT_NONE;
                gcCnt++;
            }
        }
        noway_assert(gcCnt <= ushort.MaxValue);

        if (!emitFullArgInfo)
        {
            noway_assert(gcCnt <= u2.emitGcArgTrackCnt);
            u2.emitGcArgTrackCnt = (ushort)(u2.emitGcArgTrackCnt - gcCnt);
        }
        if (!emitFullGCinfo)
        {
            return;
        }

        if (gcCnt != 0)
        {
            var descriptor = gcInfo.gcRegPtrAllocDsc();
            descriptor.rpdGCtype = GCT_GCREF;
            descriptor.rpdOffs = emitCurCodeOffs(addr);
            descriptor.rpdArg = true;
            descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_KILL;
            descriptor.rpdCallData.rpdPtrArg = (ushort)gcCnt;
        }
        emitStackPopLargeStk(addr, true, callInstrSize, 0);
#endif
    }
}
