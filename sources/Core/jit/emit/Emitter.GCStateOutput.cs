// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private unsafe void emitUpdateLiveGCvars(VARSET_TP vars, byte* addr)
    {
        assert(_compiler is not null);
#if DEBUG
        assert(emitIssuing);
#endif
        if (emitIGisInEpilog(emitCurIG))
        {
            return;
        }
        if (emitThisGCrefVset && VarSetOps.Equal(_compiler, emitThisGCrefVars, vars))
        {
            return;
        }

#if DEBUG
        if (_compiler.verbose || _compiler.opts.disasmWithGC)
        {
            VarSetOps.Assign(_compiler, ref debugThisGCrefVars, vars);
        }
#endif
        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, vars);
        if (emitGCrFrameOffsCnt != 0)
        {
            for (var num = 0; num < emitTrkVarCnt; num++)
            {
                var val = emitGCrFrameOffsTab[num];
                if (val != -1)
                {
                    var offs = val & ~(int)OFFSET_MASK;
                    if (VarSetOps.IsMember(_compiler, vars, num))
                    {
                        var gcType = (val & byref_OFFSET_FLAG) != 0 ? GCT_BYREF : GCT_GCREF;
                        emitGCvarLiveUpd(offs, int.MaxValue, gcType, addr
#if DEBUG
                            , (uint)num
#endif
                            );
                    }
                    else
                    {
                        emitGCvarDeadUpd(offs, addr
#if DEBUG
                            , (uint)num
#endif
                            );
                    }
                }
            }
        }
        emitThisGCrefVset = true;
    }

    private unsafe void emitUpdateLiveGCregs(GCInfo.GCtype gcType, regMaskTP regs, byte* addr)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        if (emitIGisInEpilog(emitCurIG))
        {
            return;
        }
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        assert(gcType is GCT_GCREF or GCT_BYREF);
        ref var current = ref (gcType == GCT_GCREF ? ref emitThisGCrefRegs : ref emitThisByrefRegs);
        ref var other = ref (gcType == GCT_GCREF ? ref emitThisByrefRegs : ref emitThisGCrefRegs);
        assert(new regMaskTP(current) != regs);

        if (emitFullGCinfo)
        {
            var dead = new regMaskTP(current) & ~regs;
            var life = ~new regMaskTP(current) & regs;
            assert((dead | life).IsNonEmpty);
            assert((dead & life).IsEmpty);

            var changed = (ulong)(regMask)(dead | life);
            do
            {
                var reg = (regNumber)BitOperations.TrailingZeroCount(changed);
                var bit = new regMaskTP(reg.SingleTypeMask);
                if ((life & bit).IsNonEmpty)
                {
                    emitGCregLiveUpd(gcType, reg, addr);
                }
                else
                {
                    emitGCregDeadUpd(reg, addr);
                }
                changed &= changed - 1;
            }
            while (changed != 0);

            assert(new regMaskTP(current) == regs);
        }
        else
        {
            other &= ~(regMask)regs;
            current = (regMask)regs;
        }
        assert((emitThisGCrefRegs & emitThisByrefRegs) == SRBM_NONE);
#endif
    }
#endif
}
