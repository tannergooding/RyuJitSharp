// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, morph.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private PhaseStatus fgPromoteStructs()
    {
        if (!opts.OptEnabled(CLFLG_STRUCTPROMOTE))
        {
            JITDUMP("  promotion opt flag not enabled\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (fgNoStructPromotion)
        {
            JITDUMP("  promotion disabled by JitNoStructPromotion\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (compStressCompile(STRESS_NO_OLD_PROMOTION, 10))
        {
            JITDUMP("  skipping due to stress\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        if (info.compIsVarArgs)
        {
            JITDUMP("  promotion disabled because of varargs\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf("\nlvaTable before fgPromoteStructs\n");
            lvaTableDump();
        }
#endif

        // Promotion allocates field locals; visit only the original table entries.
        var startLvaCount = lvaCount;
        var tooManyLocalsReported = false;
        var madeChanges = false;

        // Inlining can cache conservative SIMD information. Reconsider it for this phase.
        assert(structPromotionHelper is not null);
        structPromotionHelper.Clear();

        for (var lclNum = 0; lclNum < startLvaCount; lclNum++)
        {
            var promotedVar = false;
            ref var varDsc = ref lvaGetDesc(lclNum);

            if (varTypeIsSimdOrMask(varDsc.Type) || varDsc.IsBitcastToSimd())
            {
                varDsc.lvRegStruct = true;
            }
            else if (lvaHaveManyLocals())
            {
                if (!tooManyLocalsReported)
                {
                    JITDUMP("Stopped promoting struct fields, due to too many locals.\n");
                }
                tooManyLocalsReported = true;
            }
            else if (varTypeIsStruct(varDsc.Type))
            {
                promotedVar = structPromotionHelper.TryPromoteStructVar(lclNum);
            }

            madeChanges |= promotedVar;

            if (!promotedVar && varTypeIsSimd(varDsc.Type) && !varDsc.lvFieldAccessed)
            {
                varDsc.lvRegStruct = true;
            }
        }

#if DEBUG
        if (verbose && madeChanges)
        {
            jitprintf("\nlvaTable after fgPromoteStructs\n");
            lvaTableDump();
        }
#endif

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
