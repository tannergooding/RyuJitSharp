// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    // Native gcinfo.h encodes pointer properties in the low two stack-offset bits.
    private const uint byref_OFFSET_FLAG = 1;
    private const uint OFFSET_MASK = 3;

    private unsafe void emitGCvarLiveSet(int offs, GCInfo.GCtype gcType, byte* addr, nint disp = -1)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        assert((offs % TARGET_POINTER_SIZE) == 0);
        assert(gcType is GCT_GCREF or GCT_BYREF);
        if (disp == -1)
        {
            disp = (offs - emitGCrFrameOffsMin) / TARGET_POINTER_SIZE;
        }
        assert((nuint)disp < (uint)emitGCrFrameOffsCnt);
        var descriptor = new GCInfo.varPtrDsc
        {
            vpdBegOfs = emitCurCodeOffs(addr),
#if DEBUG
            vpdEndOfs = 0xFACEDEAD,
#endif
            vpdVarNum = unchecked((uint)offs),
        };
        if (gcType == GCT_BYREF)
        {
            descriptor.vpdVarNum |= byref_OFFSET_FLAG;
        }
        if (gcInfo.gcVarPtrLast is null)
        {
            assert(gcInfo.gcVarPtrList is null);
            gcInfo.gcVarPtrList = gcInfo.gcVarPtrLast = descriptor;
        }
        else
        {
            assert(gcInfo.gcVarPtrList is not null);
            gcInfo.gcVarPtrLast.vpdNext = descriptor;
            gcInfo.gcVarPtrLast = descriptor;
        }

        assert(emitGCrFrameLiveTab is not null);
        assert(emitGCrFrameLiveTab[disp] is null);
        emitGCrFrameLiveTab[disp] = descriptor;
        emitThisGCrefVset = false;
    }

    private unsafe void emitGCvarDeadSet(int offs, byte* addr, nint disp = -1)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        assert((offs % sizeof(int)) == 0);
        if (disp == -1)
        {
            disp = (offs - emitGCrFrameOffsMin) / TARGET_POINTER_SIZE;
        }
        assert((uint)disp < (uint)emitGCrFrameOffsCnt);
        assert(emitGCrFrameLiveTab is not null);
        var descriptor = emitGCrFrameLiveTab[disp];
        emitGCrFrameLiveTab[disp] = null;
        assert(descriptor is not null);
        assert((descriptor.vpdVarNum & ~OFFSET_MASK) == unchecked((uint)offs));
#if DEBUG
        assert(descriptor.vpdEndOfs == 0xFACEDEAD);
#endif
        descriptor.vpdEndOfs = emitCurCodeOffs(addr);
        emitThisGCrefVset = false;
    }

    private unsafe void emitGCvarLiveUpd(int offs, int varNum, GCInfo.GCtype gcType, byte* addr
#if DEBUG
        , uint actualVarNum
#endif
        )
    {
        assert(_compiler is not null);
        assert((offs % sizeof(int)) == 0);
        assert(gcType is GCT_GCREF or GCT_BYREF);
#if FEATURE_FIXED_OUT_ARGS
        if (unchecked((uint)varNum) == _compiler.lvaOutgoingArgSpaceVar)
        {
            if (emitFullGCinfo)
            {
                var descriptor = gcInfo.gcRegPtrAllocDsc();
                descriptor.rpdGCtype = gcType;
                descriptor.rpdOffs = emitCurCodeOffs(addr);
                descriptor.rpdArg = true;
                descriptor.rpdCall = false;
                noway_assert((uint)offs <= ushort.MaxValue);
                descriptor.rpdCallData.rpdPtrArg = (ushort)offs;
                descriptor.rpdArgType = GCInfo.rpdArgType_t.rpdARG_PUSH;
                descriptor.rpdIsThis = false;
            }
        }
        else
#endif
        {
            if ((offs >= emitGCrFrameOffsMin) && (offs < emitGCrFrameOffsMax))
            {
                if (varNum != int.MaxValue)
                {
                    var isTracked = false;
                    if (varNum >= 0)
                    {
                        isTracked = _compiler.lvaIsGCTracked(in _compiler.lvaGetDesc(varNum));
                    }
                    if (!isTracked)
                    {
                        assert(!emitContTrkPtrLcls);
                        return;
                    }
                }

                var disp = (offs - emitGCrFrameOffsMin) / TARGET_POINTER_SIZE;
                assert(disp < emitGCrFrameOffsCnt);
                assert(emitGCrFrameLiveTab is not null);
                if (emitGCrFrameLiveTab[disp] is null)
                {
                    emitGCvarLiveSet(offs, gcType, addr, disp);
#if DEBUG
                    if ((_compiler.verbose || _compiler.opts.disasmWithGC) && (actualVarNum < _compiler.lvaCount)
                        && _compiler.lvaGetDesc((int)actualVarNum).lvTracked)
                    {
                        VarSetOps.AddElemD(_compiler, debugThisGCrefVars, _compiler.lvaGetDesc((int)actualVarNum)._varIndex);
                    }
#endif
                }
            }
        }
    }

    private unsafe void emitGCvarDeadUpd(int offs, byte* addr
#if DEBUG
        , uint varNum
#endif
        )
    {
#if DEBUG
        assert(emitIssuing);
#endif
        assert(_compiler is not null);
        assert((offs % sizeof(int)) == 0);
        if ((offs >= emitGCrFrameOffsMin) && (offs < emitGCrFrameOffsMax))
        {
            var disp = (offs - emitGCrFrameOffsMin) / TARGET_POINTER_SIZE;
            assert(disp < emitGCrFrameOffsCnt);
            assert(emitGCrFrameLiveTab is not null);
            if (emitGCrFrameLiveTab[disp] is not null)
            {
                assert(!_compiler.lvaKeepAliveAndReportThis() || (offs != emitSyncThisObjOffs));
                emitGCvarDeadSet(offs, addr, disp);
#if DEBUG
                if ((_compiler.verbose || _compiler.opts.disasmWithGC) && (varNum < _compiler.lvaCount)
                    && _compiler.lvaGetDesc((int)varNum).lvTracked)
                {
                    VarSetOps.RemoveElemD(_compiler, debugThisGCrefVars, _compiler.lvaGetDesc((int)varNum)._varIndex);
                }
#endif
            }
        }
    }
#endif
}
