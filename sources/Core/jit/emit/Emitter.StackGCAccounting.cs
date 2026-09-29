// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using GCtype = RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool emitStackNeedsGC(GCInfo.GCtype type)
    {
        assert(type is GCT_NONE or GCT_GCREF or GCT_BYREF);
        return type is GCT_GCREF or GCT_BYREF;
    }

#if EMIT_TRACK_STACK_DEPTH
    private const int MAX_SIMPLE_STK_DEPTH = sizeof(uint) * 8;
#endif

    internal unsafe void emitStackPush(byte* addr, GCInfo.GCtype gcType)
    {
#if EMIT_TRACK_STACK_DEPTH
        assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
        if (emitSimpleStkUsed)
        {
            assert(!emitFullGCinfo);
            assert(unchecked((uint)emitCurStackLvl) / sizeof(int) < MAX_SIMPLE_STK_DEPTH);
            u1.emitSimpleStkMask = unchecked((int)((unchecked((uint)u1.emitSimpleStkMask) << 1) |
                (emitStackNeedsGC(gcType) ? 1u : 0u)));
            u1.emitSimpleByrefStkMask = unchecked((int)((unchecked((uint)u1.emitSimpleByrefStkMask) << 1) |
                (gcType == GCT_BYREF ? 1u : 0u)));
            assert((u1.emitSimpleStkMask & u1.emitSimpleByrefStkMask) == u1.emitSimpleByrefStkMask);
        }
        else
        {
            emitStackPushLargeStk(addr, gcType);
        }

        emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) + sizeof(int)));
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-depth tracking is disabled for this target.");
#endif
    }

    internal unsafe void emitStackPushN(byte* addr, uint count)
    {
#if EMIT_TRACK_STACK_DEPTH
        assert(count != 0);
        if (emitSimpleStkUsed)
        {
            assert(!emitFullGCinfo);
            u1.emitSimpleStkMask = unchecked((int)(unchecked((uint)u1.emitSimpleStkMask) << (int)count));
            u1.emitSimpleByrefStkMask = unchecked((int)(unchecked((uint)u1.emitSimpleByrefStkMask) << (int)count));
        }
        else
        {
            emitStackPushLargeStk(addr, GCT_NONE, count);
        }

        emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) + unchecked(count * sizeof(int))));
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-depth tracking is disabled for this target.");
#endif
    }

    internal unsafe void emitStackPop(byte* addr, bool isCall, byte callInstrSize, uint count = 1)
    {
#if EMIT_TRACK_STACK_DEPTH
        assert(unchecked((uint)emitCurStackLvl) / sizeof(int) >= count);
        assert(!isCall || callInstrSize > 0);

        if (count != 0)
        {
            if (emitSimpleStkUsed)
            {
                assert(!emitFullGCinfo);
                var remaining = count;
                do
                {
                    u1.emitSimpleStkMask = unchecked((int)(unchecked((uint)u1.emitSimpleStkMask) >> 1));
                    u1.emitSimpleByrefStkMask = unchecked((int)(unchecked((uint)u1.emitSimpleByrefStkMask) >> 1));
                } while (--remaining != 0);
            }
            else
            {
                emitStackPopLargeStk(addr, isCall, callInstrSize, count);
            }
            emitCurStackLvl = unchecked((int)(unchecked((uint)emitCurStackLvl) - unchecked(count * sizeof(int))));
        }
        else
        {
            assert(isCall);
            if (emitFullGCinfo
#if !JIT32_GCENCODER
                || (codeGen.IsFullPtrRegMapRequired && !codeGen.Interruptible && isCall)
#endif
                )
            {
                emitStackPopLargeStk(addr, isCall, callInstrSize, 0);
            }
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-depth tracking is disabled for this target.");
#endif
    }

    private unsafe void emitStackPushLargeStk(byte* addr, GCInfo.GCtype gcType, uint count = 1)
    {
#if EMIT_TRACK_STACK_DEPTH
        var level = unchecked((uint)emitCurStackLvl) / sizeof(int);
        assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
        assert(count != 0 && !emitSimpleStkUsed);

        do
        {
            noway_assert(u2.emitArgTrackTab != null && u2.emitArgTrackTop != null);
            assert(u2.emitArgTrackTop == u2.emitArgTrackTab + level);
            noway_assert(u2.emitArgTrackTop < u2.emitArgTrackTab + emitMaxStackDepth);
            *u2.emitArgTrackTop++ = (byte)gcType;
            assert(u2.emitArgTrackTop <= u2.emitArgTrackTab + emitMaxStackDepth);

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
                    descriptor.rpdCallData.rpdPtrArg = unchecked((ushort)level);
                    descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_PUSH;
                    descriptor.rpdIsThis = false;
                }

                u2.emitGcArgTrackCnt = unchecked((ushort)(u2.emitGcArgTrackCnt + 1));
            }
            level = unchecked(level + 1);
            assert(level != 0);
        } while (--count != 0);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-depth tracking is disabled for this target.");
#endif
    }

    private unsafe void emitStackPopLargeStk(byte* addr, bool isCall, byte callInstrSize, uint count = 1)
    {
#if EMIT_GENERATE_GCINFO && EMIT_TRACK_STACK_DEPTH
#if DEBUG
        assert(emitIssuing);
#endif
#if JIT32_GCENCODER
        assert(!emitSimpleStkUsed);
#endif
        uint argRecCnt = 0;
        for (var remaining = count; remaining != 0; remaining--)
        {
            assert(u2.emitArgTrackTop > u2.emitArgTrackTab);
            var gcType = (GCtype)(*--u2.emitArgTrackTop);
            assert(gcType is GCT_NONE or GCT_GCREF or GCT_BYREF);
            if (emitFullArgInfo || emitStackNeedsGC(gcType))
            {
                argRecCnt = unchecked(argRecCnt + 1);
            }
        }

        assert(u2.emitArgTrackTop >= u2.emitArgTrackTab);
        assert(u2.emitArgTrackTop == u2.emitArgTrackTab + unchecked((uint)emitCurStackLvl) / sizeof(int) - count);
        noway_assert(argRecCnt <= ushort.MaxValue);
#if DEBUG
        assert(argRecCnt <= ushort.MaxValue);
#endif
        // ClrSafeInt::Value returns zero after overflow if its Debug assertion is ignored.
        var argRecValue = argRecCnt <= ushort.MaxValue ? unchecked((ushort)argRecCnt) : (ushort)0;
        u2.emitGcArgTrackCnt = unchecked((ushort)(u2.emitGcArgTrackCnt - argRecValue));

#if JIT32_GCENCODER
        if (!emitFullGCinfo)
        {
            return;
        }
#endif

        var gcrefRegs = unchecked((uint)emitThisGCrefRegs) >> (int)REG_INT_FIRST;
        var byrefRegs = unchecked((uint)emitThisByrefRegs) >> (int)REG_INT_FIRST;
        assert(new regMaskTP((regMask)((ulong)gcrefRegs << (int)REG_INT_FIRST)) ==
            new regMaskTP(emitThisGCrefRegs));
        assert(new regMaskTP((regMask)((ulong)byrefRegs << (int)REG_INT_FIRST)) ==
            new regMaskTP(emitThisByrefRegs));

#if JIT32_GCENCODER
        uint reportedRegs = unchecked((uint)(SRBM_INT_CALLEE_SAVED | SRBM_EBP)) >> (int)REG_INT_FIRST;
        gcrefRegs &= reportedRegs;
        byrefRegs &= reportedRegs;

#if DEBUG
        assert(argRecCnt <= ushort.MaxValue);
#endif
        if (argRecValue == 0)
        {
#if !FPO_INTERRUPTIBLE
            if (emitFullyInt || (gcrefRegs == 0 && byrefRegs == 0 && u2.emitGcArgTrackCnt == 0))
            {
                return;
            }
#else
            return;
#endif
        }
#endif

#if DEBUG
        assert(argRecCnt <= ushort.MaxValue);
#endif
        var isCallRelatedPop = argRecValue > 1;
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
#if DEBUG
        assert(argRecCnt <= ushort.MaxValue);
#endif
        descriptor.rpdCallData.rpdPtrArg = argRecValue;
#elif !EMIT_TRACK_STACK_DEPTH
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-depth tracking is disabled for this target.");
#endif
    }

    internal unsafe void emitStackKillArgs(byte* addr, uint count, byte callInstrSize)
    {
#if EMIT_TRACK_STACK_DEPTH
        assert(count != 0);
        if (emitSimpleStkUsed)
        {
            assert(!emitFullGCinfo);
            assert(unchecked((uint)emitCurStackLvl) / sizeof(int) >= count);
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
                gcCnt = unchecked(gcCnt + 1);
            }
        }
        noway_assert(gcCnt <= ushort.MaxValue);
        var gcCntValue = gcCnt <= ushort.MaxValue ? unchecked((ushort)gcCnt) : (ushort)0;

        if (!emitFullArgInfo)
        {
#if DEBUG
            assert(gcCnt <= ushort.MaxValue);
#endif
            u2.emitGcArgTrackCnt = unchecked((ushort)(u2.emitGcArgTrackCnt - gcCntValue));
        }
        if (!emitFullGCinfo)
        {
            return;
        }

#if DEBUG
        assert(gcCnt <= ushort.MaxValue);
#endif
        if (gcCntValue != 0)
        {
            var descriptor = gcInfo.gcRegPtrAllocDsc();
            descriptor.rpdGCtype = GCT_GCREF;
            descriptor.rpdOffs = emitCurCodeOffs(addr);
            descriptor.rpdArg = true;
            descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_KILL;
            descriptor.rpdCallData.rpdPtrArg = gcCntValue;
        }
        emitStackPopLargeStk(addr, true, callInstrSize, 0);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-depth tracking is disabled for this target.");
#endif
    }
}
