// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static insGroup? emitCodeGetCookie(BasicBlock block)
    {
        assert(block is not null);
        return block.bbEmitCookie;
    }

    public insGroup emitAddLabel(VARSET_TP gcVars, regMaskTP gcRefRegs, regMaskTP byrefRegs, BasicBlock? prevBlock = null)
    {
        assert(_compiler is not null);
        var currIGWasNonEmpty = emitCurIGnonEmpty();
        if (prevBlock is not null)
        {
            assert(_compiler.compCurBB is not null);
            if (_compiler.compCurBB.HasFlag(BBF_HAS_LABEL) && emitLastInsIsCallWithGC())
            {
                assert(!emitIGisInEpilog(emitLastInsIG));

                // A GC-capable call's return IP must have the same liveness regardless of how it is reached.
                if ((new regMaskTP(emitThisGCrefRegs) != gcRefRegs) || (new regMaskTP(emitThisByrefRegs) != byrefRegs) ||
                    !VarSetOps.Equal(_compiler, emitThisGCrefVars, gcVars))
                {
                    if (prevBlock.Kind is BBJ_THROW)
                    {
                        emitIns(INS_BREAKPOINT);
                    }
                    else
                    {
                        assert(prevBlock.Kind is BBJ_ALWAYS);
                        emitIns(INS_nop);
                    }
                }
            }
        }

        emitAddedLabel = true;

        if (emitCurIGnonEmpty())
        {
#if FEATURE_LOOP_ALIGN
            if (!currIGWasNonEmpty && (emitAlignLastGroup is not null) &&
                (emitAlignLastGroup.idaLoopHeadPredIG is not null) &&
                (emitAlignLastGroup.idaLoopHeadPredIG.igNext == emitCurIG))
            {
                // Padding displaced the loop head into the next group; keep nested-loop alignment removal accurate.
                emitAlignLastGroup.idaLoopHeadPredIG = emitCurIG;
            }
#endif
            emitNxtIG();
        }
        else
        {
            assert(emitCurIG is not null);
            assert((emitCurIG.igFlags & InsGroupFlags.Extend) == 0);
#if DEBUG || LATE_DISASM
            emitCurIG.igWeight = getCurrentBlockWeight();
            emitCurIG.igPerfScore = 0.0;
#endif
        }

        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, gcVars);
        VarSetOps.Assign(_compiler, ref emitInitGCrefVars, gcVars);
        emitInitGCrefRegs = (regMask)gcRefRegs;
        emitThisGCrefRegs = (regMask)gcRefRegs;
        emitInitByrefRegs = (regMask)byrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;

        assert(emitCurIG is not null);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"Label: {emitLabelString(emitCurIG)}, GCvars={VarSetOps.ToString(_compiler, gcVars)} ");
            dumpConvertedVarSet(_compiler, gcVars);
            jitprintf(", gcrefRegs=");
            printRegMaskInt(gcRefRegs);
            emitDispRegSet(gcRefRegs);
            jitprintf(", byrefRegs=");
            printRegMaskInt(byrefRegs);
            emitDispRegSet(byrefRegs);
            jitprintf("\n");
        }
#endif
        return emitCurIG;
    }

    public insGroup emitAddInlineLabel()
    {
        if (emitCurIGnonEmpty())
        {
            emitNxtIG(extend: true);
        }

        assert(emitCurIG is not null);
        return emitCurIG;
    }

    public void emitSetFirstColdIGCookie(insGroup cookie)
    {
        emitFirstColdIG = cookie;
    }

    private bool emitLastInsIsCallWithGC()
    {
        return (emitLastIns is not null) && emitLastIns.idIsCall() && !emitLastIns.idIsNoGC();
    }

#if !TARGET_XARCH && !TARGET_ARM64 && !TARGET_WASM
    public void emitIns(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED,
            $"Zero-operand instruction recording outside xarch is not ported ({ins}).");
    }
#endif
}
