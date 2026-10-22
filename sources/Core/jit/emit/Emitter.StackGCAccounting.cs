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

    private const int MAX_SIMPLE_STK_DEPTH = sizeof(uint) * 8;

    internal unsafe void emitStackPush(byte* addr, GCInfo.GCtype gcType)
    {
        if (!Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget);
            return;
        }

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
    }

    internal unsafe void emitStackPushN(byte* addr, uint count)
    {
        if (!Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget);
            return;
        }

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
    }

    internal unsafe void emitStackPop(byte* addr, bool isCall, byte callInstrSize, uint count = 1)
    {
        if (!Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget);
            return;
        }

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
                || (!Configuration.JIT32_GCENCODER && codeGen.IsFullPtrRegMapRequired
                    && !codeGen.Interruptible && isCall))
            {
                emitStackPopLargeStk(addr, isCall, callInstrSize, 0);
            }
        }
    }

    private unsafe void emitStackPushLargeStk(byte* addr, GCInfo.GCtype gcType, uint count = 1)
    {
        if (!Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget);
            return;
        }

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
    }

    private unsafe void emitStackPopLargeStk(byte* addr, bool isCall, byte callInstrSize, uint count = 1)
    {
        if (!Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget);
            return;
        }

        if (Configuration.DEBUG)
        {
            assert(emitIssuing);
        }
        if (Configuration.JIT32_GCENCODER)
        {
            assert(!emitSimpleStkUsed);
        }
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
        if (Configuration.DEBUG)
        {
            assert(argRecCnt <= ushort.MaxValue);
        }
        // ClrSafeInt::Value returns zero after overflow if its Debug assertion is ignored.
        var argRecValue = argRecCnt <= ushort.MaxValue ? unchecked((ushort)argRecCnt) : (ushort)0;
        u2.emitGcArgTrackCnt = unchecked((ushort)(u2.emitGcArgTrackCnt - argRecValue));

        if (Configuration.JIT32_GCENCODER && !emitFullGCinfo)
        {
            return;
        }

        var gcrefRegs = unchecked((uint)emitThisGCrefRegs) >> (int)TargetRuntimeMetadata.IntegerRegisterFirst;
        var byrefRegs = unchecked((uint)emitThisByrefRegs) >> (int)TargetRuntimeMetadata.IntegerRegisterFirst;
        assert(new regMaskTP((regMask)((ulong)gcrefRegs << (int)TargetRuntimeMetadata.IntegerRegisterFirst)) ==
            new regMaskTP(emitThisGCrefRegs));
        assert(new regMaskTP((regMask)((ulong)byrefRegs << (int)TargetRuntimeMetadata.IntegerRegisterFirst)) ==
            new regMaskTP(emitThisByrefRegs));

        if (Configuration.JIT32_GCENCODER)
        {
            var reportedRegs = unchecked((uint)(TargetRuntimeMetadata.IntegerCalleeSavedMask |
                TargetRuntimeMetadata.FramePointerBaseRegister.SingleTypeMask)) >> (int)TargetRuntimeMetadata.IntegerRegisterFirst;
            gcrefRegs &= reportedRegs;
            byrefRegs &= reportedRegs;

            if (Configuration.DEBUG)
            {
                assert(argRecCnt <= ushort.MaxValue);
            }
            if (argRecValue == 0)
            {
                if (emitFullyInt || (gcrefRegs == 0 && byrefRegs == 0 && u2.emitGcArgTrackCnt == 0))
                {
                    return;
                }
            }
        }

        if (Configuration.DEBUG)
        {
            assert(argRecCnt <= ushort.MaxValue);
        }
        var isCallRelatedPop = argRecValue > 1;
        var descriptor = gcInfo.gcRegPtrAllocDsc();
        descriptor.rpdGCtype = GCT_GCREF;
        descriptor.rpdOffs = emitCurCodeOffs(addr);
        descriptor.rpdCall = isCall || isCallRelatedPop;
        if (!Configuration.JIT32_GCENCODER && descriptor.rpdCall)
        {
            assert(isCall || callInstrSize == 0);
            descriptor.rpdCallInstrSize = callInstrSize;
        }
        descriptor.rpdCallData.rpdCallGCrefRegs = gcrefRegs;
        descriptor.rpdCallData.rpdCallByrefRegs = byrefRegs;
        descriptor.rpdArg = true;
        descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_POP;
        if (Configuration.DEBUG)
        {
            assert(argRecCnt <= ushort.MaxValue);
        }
        descriptor.rpdCallData.rpdPtrArg = argRecValue;
    }

    internal unsafe void emitStackKillArgs(byte* addr, uint count, byte callInstrSize)
    {
        if (!Configuration.EMIT_TRACK_STACK_DEPTH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget);
            return;
        }

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
            if (Configuration.DEBUG)
            {
                assert(gcCnt <= ushort.MaxValue);
            }
            u2.emitGcArgTrackCnt = unchecked((ushort)(u2.emitGcArgTrackCnt - gcCntValue));
        }
        if (!emitFullGCinfo)
        {
            return;
        }

        if (Configuration.DEBUG)
        {
            assert(gcCnt <= ushort.MaxValue);
        }
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
    }
}
