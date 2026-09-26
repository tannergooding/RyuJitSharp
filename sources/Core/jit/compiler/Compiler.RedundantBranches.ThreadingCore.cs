// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optJumpThreadCore(JumpThreadInfo info)
    {
        assert(info.NumPreds == info.NumTruePreds + info.NumFalsePreds + info.NumAmbiguousPreds);
        if ((info.NumTruePreds == 0) && (info.NumFalsePreds == 0))
        {
            return false;
        }

        assert(vnStore is not null);
        foreach (var phiUse in info.PhiUses)
        {
            if (phiUse.ReplacementSsaNum == SsaConfig.RESERVED_SSA_NUM)
            {
                continue;
            }

            var use = phiUse.Use;
            var localNumber = use.LclNum;
            assert(use.SsaNum != phiUse.ReplacementSsaNum);
            ref var replacement = ref lvaGetDesc(localNumber).GetPerSsaData(phiUse.ReplacementSsaNum);
            use.SsaNum = phiUse.ReplacementSsaNum;
            if (use._vnPair != replacement._vnPair)
            {
                var newVNPair = replacement._vnPair;
                if (use.Oper is GT_LCL_FLD)
                {
                    var field = use.AsLclFld();
                    newVNPair = vnStore.VNPairForLoad(
                        replacement._vnPair, lvaLclValueSize(localNumber),
                        field.Type, field.LclOffs, field.ValueSize);
                }
                else
                {
                    assert(use.Oper is GT_LCL_VAR);
                }

                use._vnPair = newVNPair;
                // The rewritten local's value also flows through COMMA right operands.
                foreach (var comma in phiUse.CommaParents)
                {
                    comma._vnPair = vnStore.VNPWithExc(
                        comma.Op2._vnPair, vnStore.VNPExceptionSet(comma.Op1._vnPair));
                }
            }

            replacement.AddUse(phiUse.Block);
        }

        for (var index = info.PhiDefsToRemove.Count - 1; index >= 0; index--)
        {
            fgRemoveStmt(info.Block, info.PhiDefsToRemove[index]);
        }

        var setNoCseIn = false;
        if (info.IsPhiBased)
        {
            // Bypassing a memory phi must not let CSE propagate the old memory state.
            foreach (var kind in new AllMemoryKinds())
            {
                if ((kind is ByrefExposed) && byrefStatesMatchGcHeapStates)
                {
                    continue;
                }

                if (info.Block.bbMemorySsaPhiFunc[(int)kind] is not null)
                {
                    setNoCseIn = true;
                    break;
                }
            }
        }

        var modifiedProfile = false;
        // Save the next predecessor before redirecting, as redirecting edits this list.
        for (var edge = info.Block.bbPreds; edge is not null;)
        {
            var next = edge.NextPredEdge;
            var predecessor = edge.SourceBlock;
            if (info.AmbiguousPreds.Contains(predecessor))
            {
                if (setNoCseIn)
                {
                    info.Block.SetFlags(BBF_NO_CSE_IN);
                }

                edge = next;
                continue;
            }

            var target = info.TruePreds.Contains(predecessor) ? info.TrueTarget : info.FalseTarget;
            fgReplaceJumpTarget(predecessor, info.Block, target);
            if (setNoCseIn)
            {
                target.SetFlags(BBF_NO_CSE_IN);
            }

            if (predecessor.hasProfileWeight)
            {
                target.increaseBBProfileWeight(edge.LikelyWeight);
                modifiedProfile = true;
            }

            edge = next;
        }

        if (modifiedProfile)
        {
            JITDUMP($"RBO: {FMT_BB(info.Block.bbNum)} is now unreachable, and flow into its successors needs to be removed. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            fgPgoConsistent = false;
        }

        var ambiguousBlock = info.AmbiguousVNBlock;
        if ((ambiguousBlock is not null) && (info.Block.Kind is BBJ_COND) &&
            (info.Block.GetUniquePred(this) == ambiguousBlock))
        {
            var statement = info.Block.LastStmt;
            assert(statement is not null && statement.RootNode.Oper is GT_JTRUE);
            var compare = statement.RootNode.AsUnOp().Op1;
            assert(compare.Oper.IsCompare);
            vnStore.VNUnpackExc(compare._vnPair.Liberal, out _, out var exceptions);
            compare._vnPair.Liberal = vnStore.VNWithExc(info.AmbiguousVN, exceptions);
            // The sharpened VN only describes the surviving predecessor; old dominator
            // information cannot propagate it into the successors reached by threaded edges.
            info.Block.SetFlags(BBF_STALE_PREDICATE);
        }

        Metrics.JumpThreadingsPerformed++;
        fgModified = true;
        return true;
    }
}
