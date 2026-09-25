// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insGroupPlaceholderType;

namespace RyuJitSharp;

public partial class Emitter
{
    private const int MAX_PLACEHOLDER_IG_SIZE = 256;

    public void emitCreatePlaceholderIG(insGroupPlaceholderType igType, BasicBlock block, VARSET_TP gcVars,
        regMaskTP gcRefRegs, regMaskTP byrefRegs, bool last)
    {
#if !TARGET_AMD64 || EMITTER_STATS
        throw new FatalJitException(CORJIT_SKIPPED, "Placeholder groups require AMD64 without emitter allocation statistics.");
#else
        assert(_compiler is not null);
        var extend = igType is IGPT_EPILOG or IGPT_FUNCLET_EPILOG;
        if (extend)
        {
            emitOutputPreEpilogNOP();
        }

        if (emitCurIGnonEmpty())
        {
            emitNxtIG(extend);
        }

        if (!extend)
        {
            VarSetOps.Assign(_compiler, ref emitThisGCrefVars, gcVars);
            VarSetOps.Assign(_compiler, ref emitInitGCrefVars, gcVars);
            emitThisGCrefRegs = emitInitGCrefRegs = (regMask)gcRefRegs;
            emitThisByrefRegs = emitInitByrefRegs = (regMask)byrefRegs;
        }

        var placeholder = emitCurIG;
        assert(placeholder is not null);
        placeholder.igFlags |= InsGroupFlags.Placeholder;

        // An empty reused group may still have the preceding function's index.
        placeholder.igFuncIdx = _compiler.compCurrFuncIdx;
        var data = new insPlaceholderGroupData
        {
            igPhType = igType,
            igPhBB = block,
            igPhPrevGCrefRegs = new(emitPrevGCrefRegs),
            igPhPrevByrefRegs = new(emitPrevByrefRegs),
            igPhInitGCrefRegs = new(emitInitGCrefRegs),
            igPhInitByrefRegs = new(emitInitByrefRegs),
        };
        VarSetOps.Assign(_compiler, ref data.igPhPrevGCrefVars, emitPrevGCrefVars);
        VarSetOps.Assign(_compiler, ref data.igPhInitGCrefVars, emitInitGCrefVars);
        placeholder.igPhData = data;

        if (igType == IGPT_EPILOG)
        {
            placeholder.igFlags |= InsGroupFlags.Epilog;
        }
        else if (igType == IGPT_FUNCLET_PROLOG)
        {
            placeholder.igFlags |= InsGroupFlags.FuncletProlog;
        }
        else if (igType == IGPT_FUNCLET_EPILOG)
        {
            placeholder.igFlags |= InsGroupFlags.FuncletEpilog;
        }
        placeholder.igFlags |= InsGroupFlags.OutOfOrderHead;

        if (emitPlaceholderList is not null)
        {
            assert(emitPlaceholderLast is not null);
            assert(emitPlaceholderLast.igPhData is not null);
            emitPlaceholderLast.igPhData.igPhNext = placeholder;
        }
        else
        {
            emitPlaceholderList = placeholder;
        }
        emitPlaceholderLast = placeholder;

        // Reserve the native maximum until out-of-order prolog/epilog generation determines the size.
        emitCurIGsize = unchecked(emitCurIGsize + MAX_PLACEHOLDER_IG_SIZE);
        emitLastSavedIGWasNoGC = true;
        emitCurCodeOffset = unchecked(emitCurCodeOffset + emitCurIGsize);

        if (_compiler.opts.compDbgInfo)
        {
            if (igType == IGPT_FUNCLET_PROLOG)
            {
                codeGen.genIPmappingAdd(IPmappingDscKind.Prolog, default, true);
            }
            else if (igType == IGPT_FUNCLET_EPILOG)
            {
                codeGen.genIPmappingAdd(IPmappingDscKind.Epilog, default, true);
            }
        }

        if (last)
        {
            emitCurIG = null;
        }
        else
        {
            if (extend)
            {
                // Fast tailcall argument setup relies on an epilog ending its no-GC region.
                emitNoGCRequestCount = 0;
                emitNoGCIG = false;
            }

            emitNewIG();

            // The placeholder's final GC state is not known until its code is generated.
            emitForceStoreGCState = true;
            assert(emitCurIG is not null);
            emitCurIG.igFlags &= ~IGF_PROPAGATE_MASK;
        }

        emitLastIns = null;
        emitLastInsIG = null;

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("*************** After placeholder IG creation\n");
            emitDispIGlist(displayInstructions: false);
        }
#endif
#endif
    }

#if TARGET_AMD64
    internal bool emitIsLastInsCall()
    {
        return (emitLastIns is not null) && (emitLastIns.idIns() == INS_call);
    }

    private void emitOutputPreEpilogNOP()
    {
        if (emitIsLastInsCall())
        {
            emitIns(INS_nop);
        }
    }
#endif
}
