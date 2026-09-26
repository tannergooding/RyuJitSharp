// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, rangecheckcloning.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private const int MinRangeChecksPerGroup = 4;
    private const int MaxRangeChecksPerGroup = 64;
    private const uint RangeCheckCloningBudgetMultiplier = 40;
    // A tie within one statement depends on JitHashTable's bucket and chain order (jithashtable.cpp).
    private static readonly int[] RangeCheckCloningHashPrimes = [
        9, 23, 59, 131, 239, 433, 761, 1399, 2473, 4327, 7499, 12973, 22433,
        46559, 96581, 200341, 415517, 861719, 1787021, 3705617, 7684087,
        15933877, 33040633, 68513161, 142069021, 294594427, 733045421,
    ];

    private readonly record struct BoundCheckLocation(Statement Stmt, GenTreeBoundsChk Check, int StmtIdx);

    private sealed class BoundsCheckInfo(in BoundCheckLocation location)
    {
        public Statement Stmt { get; } = location.Stmt;
        public GenTreeBoundsChk Check { get; } = location.Check;
        public ValueNum LenVN { get; private set; }
        public ValueNum IdxVN { get; private set; }
        public int Offset { get; private set; }
        public int StmtIdx { get; } = location.StmtIdx;

        public bool Initialize(Compiler comp)
        {
            var store = comp.vnStore ?? throw new FatalJitException("Range-check cloning requires value numbering.");
            IdxVN = store.VNNormalValue(Check.Index._vnPair.Conservative);
            LenVN = store.VNNormalValue(Check.ArrayLength._vnPair.Conservative);
            if ((IdxVN == ValueNumStore.NoVN) || (LenVN == ValueNumStore.NoVN))
            {
                return false;
            }

            if (Check.Index.IsIntCnsFitsInI32)
            {
                Offset = (int)Check.Index.AsIntCon().IconValue;
                IdxVN = store.VNZeroForType(TYP_INT);
            }
            else
            {
                if (store.TypeOfVN(IdxVN) is not TYP_INT)
                {
                    return false;
                }

                var baseIndexVN = IdxVN;
                store.PeelOffsetsI32(ref baseIndexVN, out var offset);
                IdxVN = baseIndexVN;
                Offset = offset;
                assert(IdxVN != ValueNumStore.NoVN);
            }
            assert(store.TypeOfVN(IdxVN) is TYP_INT);

            return Offset >= 0;
        }
    }

    private struct BoundsChecksVisitor(Statement stmt, int stmtIdx, List<BoundCheckLocation> locations)
        : IGenTreeVisitor<BoundsChecksVisitor>
    {
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPreOrder => true;
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
            => (use.Flags & GTF_EXCEPT) == 0 ? WALK_SKIP_SUBTREES : WALK_CONTINUE;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if ((use is GenTreeBoundsChk check) && (check.ThrowKind is SCK_RNGCHK_FAIL))
            {
                locations.Add(new BoundCheckLocation(stmt, check, stmtIdx));
            }

            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<BoundsChecksVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private bool DoesRangeCheckCloningComplexityExceed(List<BoundsCheckInfo> checks)
    {
        var first = checks[0].Stmt;
        var last = checks[^1].Stmt;
#if DEBUG
        JITDUMP($"Checking complexity from {FMT_STMT(first.Id)} to {FMT_STMT(last.Id)}\n");
#endif

        assert(checks.Count <= MaxRangeChecksPerGroup);
        var budget = (uint)checks.Count * RangeCheckCloningBudgetMultiplier;
        JITDUMP($"\tBudget: {budget} nodes.\n");

        for (var current = first; current != last; current = current.NextStmt
            ?? throw new FatalJitException("The final range check is not reachable from the first."))
        {
            uint actual = 0;
            if (gtComplexityExceeds(current.RootNode, budget, _ => {
                actual++;
                return 1;
            }))
            {
                JITDUMP("\tExceeded budget!");
                return true;
            }

#if DEBUG
            JITDUMP($"\t\tSubtracting {actual} from budget in {FMT_STMT(current.Id)} statement\n");
#endif
            budget -= actual;
        }

        JITDUMP($"Complexity is within budget: {budget}\n");
        return false;
    }

    private static int RangeCheckCloningBucket((ValueNum Index, ValueNum Length) key, int count)
    {
        var hash = unchecked((uint)(key.Index ^ (key.Length << 16)));
        return (int)(hash % (uint)count);
    }

    private static List<(ValueNum Index, ValueNum Length)>[] GrowRangeCheckCloningBuckets(
        List<(ValueNum Index, ValueNum Length)>[] buckets, int groupCount)
    {
        var requestedSize = Math.Max(7, groupCount * 3 / 2 * 4 / 3);
        var newSize = 0;
        foreach (var prime in RangeCheckCloningHashPrimes)
        {
            if (prime >= requestedSize)
            {
                newSize = prime;
                break;
            }
        }
        if (newSize == 0)
        {
            throw new FatalJitException("Range-check cloning exhausted the native hash table sizes.");
        }

        var resized = new List<(ValueNum Index, ValueNum Length)>[newSize];
        foreach (var bucket in buckets)
        {
            if (bucket is null)
            {
                continue;
            }
            foreach (var key in bucket)
            {
                var index = RangeCheckCloningBucket(key, newSize);
                resized[index] ??= [];
                resized[index].Insert(0, key);
            }
        }
        return resized;
    }

    public unsafe PhaseStatus optRangeCheckCloning()
    {
        if (!MethodHasBoundsChecks)
        {
            JITDUMP("Current method has no bounds checks\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.jitFlags->IsSet(JitFlags.JIT_FLAG_SIZE_OPT))
        {
            JITDUMP("Optimized for size - bail out.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var modified = false;
        var locations = new List<BoundCheckLocation>();
        var groupsByVN = new Dictionary<(ValueNum Index, ValueNum Length), List<BoundsCheckInfo>>();
        var buckets = Array.Empty<List<(ValueNum Index, ValueNum Length)>>();

        for (var block = fgFirstBB; block is not null; block = block.Next)
        {
            if (!block.HasFlag(BBF_MAY_HAVE_BOUNDS_CHECKS) || block.isRunRarely || block.Kind is BBJ_THROW)
            {
                continue;
            }

            locations.Clear();
            groupsByVN.Clear();
            buckets = [];
            var stmtIdx = -1;
            foreach (var stmt in block.Statements)
            {
                stmtIdx++;
                if (block.HasTerminator && (stmt == block.LastStmt))
                {
#if JIT32_GCENCODER
                    break;
#else
                    if ((block.Kind is not BBJ_RETURN) || (block == genReturnBB))
                    {
                        break;
                    }
#endif
                }

                var visitor = new BoundsChecksVisitor(stmt, stmtIdx, locations);
                _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
            }

            if (locations.Count < MinRangeChecksPerGroup)
            {
                JITDUMP("Not enough bounds checks in the block - bail out.\n");
                continue;
            }

            foreach (var location in locations)
            {
                var info = new BoundsCheckInfo(location);
                if (!info.Initialize(this))
                {
                    continue;
                }

                var key = (info.IdxVN, info.LenVN);
                if (groupsByVN.Count == (buckets.Length * 3 / 4))
                {
                    buckets = GrowRangeCheckCloningBuckets(buckets, groupsByVN.Count);
                }
                if (!groupsByVN.TryGetValue(key, out var group))
                {
                    group = [];
                    groupsByVN.Add(key, group);
                    var index = RangeCheckCloningBucket(key, buckets.Length);
                    buckets[index] ??= [];
                    buckets[index].Insert(0, key);
                }
                if (group.Count < MaxRangeChecksPerGroup)
                {
                    group.Add(info);
                }
            }

            if (groupsByVN.Count == 0)
            {
                JITDUMP("No bounds checks in the block - bail out.\n");
                continue;
            }

            var suitable = new List<List<BoundsCheckInfo>>();
            foreach (var bucket in buckets)
            {
                if (bucket is null)
                {
                    continue;
                }
                foreach (var key in bucket)
                {
                    var group = groupsByVN[key];
                    if ((group.Count >= MinRangeChecksPerGroup) && !DoesRangeCheckCloningComplexityExceed(group))
                    {
                        suitable.Add(group);
                    }
                }
            }
            if (suitable.Count == 0)
            {
                JITDUMP("No suitable group of bounds checks in the block - bail out.\n");
                continue;
            }

            var firstGroup = suitable[^1];
            var lastGroup = suitable[^1];
            foreach (var group in suitable)
            {
                if (group[0].StmtIdx < firstGroup[0].StmtIdx)
                {
                    firstGroup = group;
                }
                if (group[^1].StmtIdx > lastGroup[^1].StmtIdx)
                {
                    lastGroup = group;
                }
            }

            var lastStmt = firstGroup[^1].Stmt;
            if ((firstGroup[^1].StmtIdx < lastGroup[^1].StmtIdx) &&
                (firstGroup[^1].StmtIdx >= lastGroup[0].StmtIdx))
            {
                lastStmt = lastGroup[^1].Stmt;
            }

#if DEBUG
            JITDUMP($"Cloning bounds checks in {FMT_BB(block.bbNum)} from " +
                $"{FMT_STMT(firstGroup[0].Stmt.Id)} to {FMT_STMT(lastStmt.Id)}\n");
#endif

            var next = optRangeCheckCloningDoClone(block, firstGroup, lastStmt);
            block = next.Prev ?? throw new FatalJitException("The cloned fast path lost its predecessor.");
            modified = true;
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
