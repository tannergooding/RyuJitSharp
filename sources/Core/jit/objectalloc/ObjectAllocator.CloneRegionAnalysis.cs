// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private bool CheckCanClone(CloneInfo info)
    {
        var compiler = CompilerInstance;
        assert(!info.CheckedCanClone);
        JITDUMP($"** Seeing if we can clone to guarantee non-escape under V{info.Local:D2}\n");
        var allocBlock = info.AllocBlock ?? throw new InvalidOperationException("Missing enumerator allocation block.");

        if (!allocBlock.HasTarget)
        {
            JITDUMP($"allocation block {FMT_BB(allocBlock.bbNum)} is a {allocBlock.Kind}, and so has no unique target edge\n");
            return false;
        }

        if (allocBlock.HasFlag(BBF_BACKWARD_JUMP))
        {
            JITDUMP($"allocation block {FMT_BB(allocBlock.bbNum)} is (possibly) in a loop\n");
            return false;
        }

        if (!allocBlock.hasProfileWeight)
        {
            JITDUMP($"alloc block {FMT_BB(allocBlock.bbNum)} was not profiled\n");
            return false;
        }

        const weight_t thinProfile = 0.1;
        var allocWeight = allocBlock.getBBWeight(compiler);
        if (allocWeight < thinProfile)
        {
            JITDUMP($"alloc block {FMT_BB(allocBlock.bbNum)} relative profile weight too low: " +
                $"{FMT_WT(allocWeight)} < {FMT_WT(thinProfile)}\n");
            return false;
        }

        // T0 through Tv convey the allocation to V's sole definition; U locals copy V
        // afterward. Tv can also have an empty-static definition outside the allocation
        // path, so its final use and the allocation predecessor have special dominance rules.
        var appearanceMap = info.AppearanceMap ?? throw new InvalidOperationException("Missing enumerator appearances.");
        if (!appearanceMap.TryGetValue(info.Local, out var variable))
        {
            JITDUMP($"Unexpected: no appearance info for V{info.Local:D2}\n");
            return false;
        }

        if (variable.HasMultipleDefs)
        {
            JITDUMP($"Unexpected: V{info.Local:D2} multiply defined\n");
            return false;
        }

        var def = variable.Def ?? throw new InvalidOperationException("Missing enumerator definition.");
        var defBlock = def.Block;
        var defStmt = def.Stmt;
#if DEBUG
        JITDUMP($"V{info.Local:D2} has single def in {FMT_BB(defBlock.bbNum)} at [{defStmt.RootNode.TreeId:D6}]\n");
#endif
        info.DefBlock = defBlock;

        var toVisit = new Stack<BasicBlock>();
        var visited = new List<BasicBlock>();
        var toVisitTryEntry = new List<BasicBlock>();
        var traits = new BitVecTraits(compiler, _initialMaxBlockID);
        var visitedBlocks = BitVecOps.MakeEmpty(traits);
        toVisit.Push(allocBlock);
        BitVecOps.AddElemD(traits, visitedBlocks, allocBlock.bbID);

        var searchCount = 0u;
        const uint searchLimit = 25;
        while (toVisit.Count > 0)
        {
            var block = toVisit.Pop();

            if (searchCount > searchLimit)
            {
                JITDUMP("Too many blocks between alloc and def block\n");
                return false;
            }

            if (block != allocBlock)
            {
                visited.Add(block);
            }

            if (!BasicBlock.sameEHRegion(allocBlock, block))
            {
                JITDUMP($"Unexpected: new EH region at {FMT_BB(block.bbNum)}\n");
                return false;
            }

            if (block == defBlock)
            {
                continue;
            }

            JITDUMP($"walking through {FMT_BB(block.bbNum)}\n");
            _ = block.VisitRegularSuccs(compiler, successor => {
                if (BitVecOps.TryAddElemD(traits, visitedBlocks, successor.bbID))
                {
                    toVisit.Push(successor);
                }

                return BasicBlockVisit.Continue;
            });
        }

        JITDUMP($"def block {FMT_BB(defBlock.bbNum)} post-dominates allocation site {FMT_BB(allocBlock.bbNum)}\n");
        JITDUMP($"allocation side cloning: {unchecked((nuint)(visited.Count - 1))} blocks\n");

        // The GDV hammock is assumed to prevent a normal path bypassing defBlock.
        var domTree = compiler._domTree ?? throw new InvalidOperationException("Conditional escape requires dominators.");
        if (domTree.Dominates(allocBlock, defBlock))
        {
            JITDUMP($"Unexpected, alloc site {FMT_BB(allocBlock.bbNum)} dominates def block {FMT_BB(defBlock.bbNum)}. " +
                "We will clone anyways.\n");
        }
        else
        {
            var possibleGuardBlock = defBlock.bbIDom ??
                throw new InvalidOperationException("Conditional allocation's definition has no dominator.");
            var guard = new GuardInfo();
            if (IsGuard(possibleGuardBlock, guard) is not null)
            {
                JITDUMP("Conditional allocation is guarded by a GDV\n");
                info.GuardBlock = possibleGuardBlock;
            }
            else
            {
                JITDUMP("Conditional allocation is not guarded by a GDV\n");
            }
        }

        foreach (var (lclNum, enumeratorVar) in appearanceMap)
        {
            var appearances = enumeratorVar.Appearances ??
                throw new InvalidOperationException("Missing enumerator appearance list.");

            foreach (var appearance in appearances)
            {
                if (!appearance.IsDef && (appearance.Stmt == appearance.Block.LastStmt))
                {
                    var guard = new GuardInfo();
                    appearance.IsGuard = IsGuard(appearance.Block, guard) is not null;
                }

                if (lclNum == info.Local)
                {
                    continue;
                }

                if (defBlock.bbPostorderNum < appearance.Block.bbPostorderNum)
                {
                    enumeratorVar.IsAllocTemp = true;
                }
                else if (defBlock.bbPostorderNum == appearance.Block.bbPostorderNum)
                {
                    if (defStmt == appearance.Stmt)
                    {
                        enumeratorVar.IsAllocTemp = true;
                        enumeratorVar.IsFinalAllocTemp = true;
                    }
                    else if (LatestStatement(defStmt, appearance.Stmt) == appearance.Stmt)
                    {
                        enumeratorVar.IsUseTemp = true;
                    }
                    else
                    {
                        enumeratorVar.IsAllocTemp = true;
                    }
                }
                else
                {
                    enumeratorVar.IsUseTemp = true;
                }

                if (appearance.IsGuard)
                {
                    JITDUMP($"Unexpected: {(enumeratorVar.IsAllocTemp ? "alloc" : "use")} temp " +
                        $"V{appearance.LclNum:D2} is GDV guard at {FMT_BB(appearance.Block.bbNum)}\n");
                    return false;
                }
            }

            if (enumeratorVar.IsAllocTemp && enumeratorVar.IsUseTemp)
            {
                JITDUMP($"Unexpected: temp V{lclNum:D2} has appearances both before and after main var assignment in " +
                    $"{FMT_BB(defBlock.bbNum)}\n");
                return false;
            }

            if (enumeratorVar.IsAllocTemp || enumeratorVar.IsUseTemp)
            {
                JITDUMP($"Temp V{lclNum:D2} is a {(enumeratorVar.IsAllocTemp ? "alloc" : "use")} temp");
                if (enumeratorVar.IsInitialAllocTemp)
                {
                    JITDUMP(" [initial]");
                }
                if (enumeratorVar.IsFinalAllocTemp)
                {
                    JITDUMP(" [final]");
                }
                JITDUMP("\n");
            }
        }

        var domCheckBlock = allocBlock;
        var domCheckBlockName = "alloc";
        var allocTree = info.AllocTree ?? throw new InvalidOperationException("Missing enumerator allocation tree.");
        if ((allocTree.Flags & GTF_ALLOCOBJ_EMPTY_STATIC) != 0)
        {
            var uniquePred = domCheckBlock.GetUniquePred(compiler);
            if (uniquePred is not null)
            {
                domCheckBlock = uniquePred;
                domCheckBlockName = "alloc-pred";
            }
        }

        foreach (var enumeratorVar in appearanceMap.Values)
        {
            if (!enumeratorVar.IsAllocTemp)
            {
                continue;
            }

            var appearances = enumeratorVar.Appearances ??
                throw new InvalidOperationException("Missing allocation-temp appearances.");
            foreach (var appearance in appearances)
            {
                if (enumeratorVar.IsFinalAllocTemp && (appearance.Block == defBlock) && !appearance.IsDef)
                {
                    continue;
                }

                if (!domTree.Dominates(domCheckBlock, appearance.Block))
                {
                    JITDUMP($"Alloc temp V{appearance.LclNum:D2} {(appearance.IsDef ? "def" : "use")} in " +
                        $"{FMT_BB(appearance.Block.bbNum)} not dominated by {domCheckBlockName} " +
                        $"{FMT_BB(domCheckBlock.bbNum)}\n");
                    return false;
                }
            }
        }

        foreach (var (lclNum, enumeratorVar) in appearanceMap)
        {
            if (enumeratorVar.IsAllocTemp)
            {
                continue;
            }

            var appearances = enumeratorVar.Appearances ??
                throw new InvalidOperationException("Missing use-temp appearances.");
            foreach (var appearance in appearances)
            {
                var appearanceBlock = appearance.Block;
                if (!domTree.Dominates(defBlock, appearanceBlock))
                {
                    JITDUMP($"{(enumeratorVar.IsUseTemp ? "Use temp" : "")}V{lclNum:D2} " +
                        $"{(appearance.IsDef ? "def" : "use")} in {FMT_BB(appearanceBlock.bbNum)} not dominated by " +
                        $"def {FMT_BB(defBlock.bbNum)}\n");
                    return false;
                }

                if (BitVecOps.TryAddElemD(traits, visitedBlocks, appearanceBlock.bbID))
                {
                    toVisit.Push(appearanceBlock);
                }
            }
        }

        JITDUMP("The defBlock dominates the right set of enumerator var uses\n");
        var dfsTree = compiler._dfsTree ?? throw new InvalidOperationException("Conditional escape requires DFS.");

        while (toVisit.Count > 0)
        {
            var block = toVisit.Pop();
            visited.Add(block);

            if (compiler.bbIsTryBeg(block))
            {
                toVisitTryEntry.Add(block);
            }

            JITDUMP($"walking back through {FMT_BB(block.bbNum)}\n");
            for (var predEdge = compiler.BlockPredsWithEH(block); predEdge is not null; predEdge = predEdge.NextPredEdge)
            {
                var predBlock = predEdge.SourceBlock;
                if (!dfsTree.Contains(predBlock))
                {
                    JITDUMP($"Unreachable pred block {FMT_BB(predBlock.bbNum)} for {FMT_BB(block.bbNum)}\n");
                    return false;
                }

                assert(domTree.Dominates(defBlock, predBlock));
                if (BitVecOps.TryAddElemD(traits, visitedBlocks, predBlock.bbID))
                {
                    toVisit.Push(predBlock);
                }
            }
        }

        JITDUMP($"total cloning including all enumerator uses: {unchecked((nuint)(visited.Count - 1))} blocks\n");

        // Native orders outer try entries before nested ones; the shared
        // feasibility helper expands each selected region without cloning it.
        var cloneInfo = new CloneTryInfo(compiler);
        var tryBlocks = new List<BasicBlock>();
        cloneInfo.BlocksToClone = tryBlocks;
        toVisitTryEntry.Sort((left, right) => right.TryIndex.CompareTo(left.TryIndex));

        foreach (var block in toVisitTryEntry)
        {
            if (BitVecOps.IsMember(traits, cloneInfo.Visited, block.bbID))
            {
                continue;
            }

            // The no-insertion mode must check full EH clone feasibility without mutation.
            var result = compiler.fgCloneTryRegionFeasibility(block, cloneInfo);
            if (result is null)
            {
                return false;
            }
        }

        foreach (var block in tryBlocks)
        {
            if (BitVecOps.TryAddElemD(traits, visitedBlocks, block.bbID))
            {
                visited.Add(block);
            }
        }

        visited.Sort((left, right) => right.bbPostorderNum.CompareTo(left.bbPostorderNum));
        assert(defBlock.hasProfileWeight);

        var weightForClone = 0.0;
        foreach (var predEdge in defBlock.PredEdges)
        {
            if (BitVecOps.IsMember(traits, visitedBlocks, predEdge.SourceBlock.bbID))
            {
                weightForClone += predEdge.LikelyWeight;
            }
        }

        var scaleFactor = Math.Max(1.0, weightForClone / defBlock.bbWeight);
        info.ProfileScale = scaleFactor;
        JITDUMP($"Profile weight for clone {FMT_WT(weightForClone)} overall {FMT_WT(defBlock.bbWeight)}, " +
            $"will scale clone at {FMT_WT(scaleFactor)}\n");

        info.BlocksToClone = visited;
        info.Blocks = visitedBlocks;
        info.CanClone = true;
        JITDUMP($"total cloning including all uses and subsequent EH: {BitVecOps.Count(traits, visitedBlocks)} blocks\n");
        compiler.Metrics.EnumeratorGDVCanCloneToEnsureNoEscape++;
        return true;
    }
}
