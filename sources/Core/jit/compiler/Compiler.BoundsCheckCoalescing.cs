// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.ValueNumStore;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class BoundsCheckCandidate(GenTreeBoundsChk check, ValueNum lengthVN, int offset)
    {
        public GenTreeBoundsChk Check { get; } = check;
        public ValueNum LengthVN { get; } = lengthVN;
        public int Offset = offset;
    }

    private bool IsBoundsCheckCoalesceBarrier(GenTree node, bool blockHasEHSuccs)
    {
        if (node.RequiresCallFlag(this))
        {
            return true;
        }

        var effects = node.OperEffects(this, out var exceptions);
        if ((effects & (GTF_CALL | GTF_ORDER_SIDEEFF)) != 0)
        {
            return true;
        }

        if (((effects & GTF_EXCEPT) != 0) &&
            ((exceptions & ~ExceptionSetFlags.IndexOutOfRangeException) != ExceptionSetFlags.None))
        {
            return true;
        }

        if ((effects & GTF_ASG) != 0)
        {
            if (!node.Oper.IsLocalStore)
            {
                return true;
            }
            if (!blockHasEHSuccs)
            {
                return false;
            }

            ref var local = ref lvaGetDesc(node.AsLclVarCommon().LclNum);
            return !local.lvTracked || local.IsLiveInOutOfHandler;
        }

        return false;
    }

    public PhaseStatus optBoundsCheckCoalesce()
    {
        if (!MethodHasBoundsChecks)
        {
            JITDUMP("Method has no bounds checks\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (fgSsaPassesCompleted == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var store = vnStore ?? throw new FatalJitException("Bounds-check coalescing requires value numbering.");
        var modified = false;
        var candidates = new List<BoundsCheckCandidate>();
        var groupMap = new Dictionary<(int BarrierCount, ValueNum LengthVN), int>();

        foreach (var block in Blocks)
        {
            candidates.Clear();
            groupMap.Clear();
            var barrierCount = 0;
            var blockHasEHSuccs = block.HasPotentialEHSuccs(this);

            foreach (var stmt in block.Statements)
            {
                foreach (var node in stmt.TreeList)
                {
                    if (node.Oper is GT_BOUNDS_CHECK)
                    {
                        var check = node.AsBoundsChk();
                        if (check.ThrowKind is not SCK_RNGCHK_FAIL)
                        {
                            barrierCount++;
                            continue;
                        }

                        var index = check.Index;
                        if (!index.IsIntCnsFitsInI32)
                        {
                            continue;
                        }

                        var offset = (int)index.AsIntCon().IconValue;
                        if (offset < 0)
                        {
                            continue;
                        }

                        var length = check.ArrayLength.EffectiveVal;
                        var lengthVN = store.VNNormalValue(length._vnPair.Conservative);
                        if (lengthVN == NoVN)
                        {
                            continue;
                        }

                        var key = (barrierCount, lengthVN);
                        if (!groupMap.TryGetValue(key, out var headIndex))
                        {
                            groupMap.Add(key, candidates.Count);
                            candidates.Add(new BoundsCheckCandidate(check, lengthVN, offset));
                            continue;
                        }

                        var head = candidates[headIndex];
#if DEBUG
                        JITDUMP($"BC coalesce in {FMT_BB(block.bbNum)}: [{check.TreeId:D6}] " +
                            $"(offset {offset}) is redundant given [{head.Check.TreeId:D6}]\n");
#endif
                        if (offset > head.Offset)
                        {
                            head.Offset = offset;
                        }
                        continue;
                    }

                    if (IsBoundsCheckCoalesceBarrier(node, blockHasEHSuccs))
                    {
                        barrierCount++;
                    }
                }
            }

            foreach (var head in candidates)
            {
                var index = head.Check.Index.AsIntCon();
                var original = (int)index.IconValue;
                if (head.Offset == original)
                {
                    continue;
                }

#if DEBUG
                JITDUMP($"BC coalesce in {FMT_BB(block.bbNum)}: strengthen " +
                    $"[{head.Check.TreeId:D6}] offset {original} -> {head.Offset} (lenVN ${head.LengthVN:x})\n");
#endif
                index.IconValue = head.Offset;
                index._vnPair.SetBoth(store.VNForIntCon(head.Offset));
                modified = true;
            }
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
