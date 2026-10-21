// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private static bool TreeIsNonExceptionalGCSafePoint(GenTree tree)
    {
        if (tree.Oper.IsCall)
        {
            return !tree.AsCall().IsHelperCall() || !tree.AsCall().HelperNum.IsNoGC;
        }

        return tree.OperIs(GT_GCPOLL);
    }

    public PhaseStatus fgWasmFindGCRefCandidates()
    {
        assert(Configuration.TARGET_WASM);

        if (lvaTrackedCount == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var liveAcrossGC = VarSetOps.MakeEmpty(this);
        var liveInto = VarSetOps.MakeEmpty(this);
        var lifeUpdater = new TreeLifeUpdater(this, forCodeGen: false);
        VarSetOps.AssignNoCopy(this, ref compCurLife, VarSetOps.MakeEmpty(this));

        foreach (var block in Blocks)
        {
            var ehLive = VarSetOps.MakeEmpty(this);
            if (block.HasPotentialEHSuccs(this))
            {
                MemoryKindSet memoryLive = default;
                fgAddHandlerLiveVars(block, ehLive, ref memoryLive);
            }

            compCurLifeTree = null;
            VarSetOps.Assign(this, ref compCurLife, block.bbLiveIn);
            foreach (var tree in block)
            {
                VarSetOps.Assign(this, ref liveInto, compCurLife);

                if (TreeIsNonExceptionalGCSafePoint(tree))
                {
                    VarSetOps.UnionD(this, liveAcrossGC, liveInto);
                    JITDUMP($"GC safepoint in {FMT_BB(block.bbNum)}:\n");
                    DISPNODE(tree);
                    JITDUMP("Live across: ");
                    if (Configuration.DEBUG && verbose)
                    {
                        dumpConvertedVarSet(this, liveInto);
                    }
                    JITDUMP("\n");
                }
                else if (tree.MayThrow(this))
                {
                    VarSetOps.IntersectionD(this, liveInto, ehLive);
                    VarSetOps.UnionD(this, liveAcrossGC, liveInto);
                    JITDUMP($"EH-only GC safepoint in {FMT_BB(block.bbNum)}:\n");
                    DISPNODE(tree);
                    JITDUMP("Live across: ");
                    if (Configuration.DEBUG && verbose)
                    {
                        dumpConvertedVarSet(this, liveInto);
                    }
                    JITDUMP("\n");
                }

                lifeUpdater.UpdateLife(tree);
            }

            if (block.Kind is BBJ_CALLFINALLY)
            {
                var liveAcross = VarSetOps.Intersection(this, compCurLife, block.bbLiveOut);
                VarSetOps.UnionD(this, liveAcrossGC, liveAcross);
            }
        }

        JITDUMP("All vars live across GC safepoints:\n");
        if (Configuration.DEBUG && verbose)
        {
            dumpConvertedVarSet(this, liveAcrossGC);
        }
        JITDUMP("\n");

        var candidates = VarSetOps.MakeEmpty(this);
        m_wasmGCRefCandidates = candidates;
        for (var varIndex = 0; varIndex < lvaTrackedCount; varIndex++)
        {
            ref var varDsc = ref lvaGetDesc(lvaTrackedIndexToLclNum(varIndex));
            if (varTypeIsGC(varDsc.Type) && !VarSetOps.IsMember(this, liveAcrossGC, varIndex))
            {
                VarSetOps.AddElemD(this, candidates, varIndex);
            }
        }

        JITDUMP("Identified GC-ref candidates:\n");
        if (Configuration.DEBUG && verbose)
        {
            dumpConvertedVarSet(this, candidates);
        }

        return PhaseStatus.MODIFIED_NOTHING;
    }
}
