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
            JITDUMP($"{FMT_BB(info.Block.bbNum)} only has ambiguous preds, not jump threading\n");
            return false;
        }

        JITDUMP("Optimizing via jump threading\n");
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
#if DEBUG
            JITDUMP($"Updating [{use.TreeId:D6}] in {FMT_BB(phiUse.Block.bbNum)} " +
                $"from u:{use.SsaNum} to u:{phiUse.ReplacementSsaNum}\n");
#endif
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

#if DEBUG
                if (verbose)
                {
                    JITDUMP($"Updating [{use.TreeId:D6}] VN from ");
                    vnpPrint(use._vnPair, 1);
                    JITDUMP(" to ");
                    vnpPrint(newVNPair, 1);
                    JITDUMP("\n");
                }
#endif

                use._vnPair = newVNPair;
                // The rewritten local's value also flows through COMMA right operands.
                foreach (var comma in phiUse.CommaParents)
                {
#if DEBUG
                    JITDUMP($" Updating COMMA parent VN [{comma.TreeId:D6}]\n");
#endif
                    comma._vnPair = vnStore.VNPWithExc(
                        comma.Op2._vnPair, vnStore.VNPExceptionSet(comma.Op1._vnPair));
                }
            }

            replacement.AddUse(phiUse.Block);
        }

        for (var index = info.PhiDefsToRemove.Count - 1; index >= 0; index--)
        {
            JITDUMP($"Removing redundant phi def from {FMT_BB(info.Block.bbNum)}\n");
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
                    JITDUMP($"{FMT_BB(info.Block.bbNum)} has {kind} memory phi; will be marking blocks with BBF_NO_CSE_IN\n");
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
                if (setNoCseIn && !info.Block.HasFlag(BBF_NO_CSE_IN))
                {
                    JITDUMP($"{FMT_BB(info.Block.bbNum)} => BBF_NO_CSE_IN\n");
                    info.Block.SetFlags(BBF_NO_CSE_IN);
                }

                edge = next;
                continue;
            }

            var isTruePred = info.TruePreds.Contains(predecessor);
            var target = isTruePred ? info.TrueTarget : info.FalseTarget;
            JITDUMP($"Jump flow from pred {FMT_BB(predecessor.bbNum)} -> {FMT_BB(info.Block.bbNum)} " +
                $"implies predicate {(isTruePred ? "true" : "false")}; we can safely redirect flow to be " +
                $"{FMT_BB(predecessor.bbNum)} -> {FMT_BB(target.bbNum)}\n");
            fgReplaceJumpTarget(predecessor, info.Block, target);
            if (setNoCseIn && !target.HasFlag(BBF_NO_CSE_IN))
            {
                JITDUMP($"{FMT_BB(target.bbNum)} => BBF_NO_CSE_IN\n");
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
            JITDUMP($"{FMT_BB(info.Block.bbNum)} has just one remaining predecessor {FMT_BB(ambiguousBlock.bbNum)}\n");
            var statement = info.Block.LastStmt;
            assert(statement is not null && statement.RootNode.Oper is GT_JTRUE);
            var compare = statement.RootNode.AsUnOp().Op1;
            assert(compare.Oper.IsCompare);
            var oldVN = compare._vnPair.Liberal;
            vnStore.VNUnpackExc(oldVN, out _, out var exceptions);
            compare._vnPair.Liberal = vnStore.VNWithExc(info.AmbiguousVN, exceptions);
            // The sharpened VN only describes the surviving predecessor; old dominator
            // information cannot propagate it into the successors reached by threaded edges.
            info.Block.SetFlags(BBF_STALE_PREDICATE);
#if DEBUG
            JITDUMP($"Updating [{compare.TreeId:D6}] liberal VN from ${oldVN:x} to ${compare._vnPair.Liberal:x}\n");
#endif
        }

        Metrics.JumpThreadingsPerformed++;
        fgModified = true;
        return true;
    }
}
