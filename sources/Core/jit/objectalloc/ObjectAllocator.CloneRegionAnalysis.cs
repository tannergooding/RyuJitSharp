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
        global::RyuJitSharp.BasicBlock __jitExceptionResult482;
        var __jitExceptionCoalesceLeft483 = info.AllocBlock;
        if (__jitExceptionCoalesceLeft483 != null)
        {
            __jitExceptionResult482 = __jitExceptionCoalesceLeft483;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingEnumeratorAllocationBlock);
            __jitExceptionResult482 = default;
        }
        var allocBlock = __jitExceptionResult482;

        if (info.HasConflictingRedefinition)
        {
            JITDUMP($"V{info.Local:D2} has a later definition that may reach its guarded uses\n");
            return false;
        }

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
        global::System.Collections.Generic.Dictionary<int, global::RyuJitSharp.ObjectAllocator.EnumeratorVar> __jitExceptionResult484;
        var __jitExceptionCoalesceLeft485 = info.AppearanceMap;
        if (__jitExceptionCoalesceLeft485 != null)
        {
            __jitExceptionResult484 = __jitExceptionCoalesceLeft485;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingEnumeratorAppearances);
            __jitExceptionResult484 = default;
        }
        var appearanceMap = __jitExceptionResult484;
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

        global::RyuJitSharp.ObjectAllocator.EnumeratorVarAppearance __jitExceptionResult486;
        var __jitExceptionCoalesceLeft487 = variable.Def;
        if (__jitExceptionCoalesceLeft487 != null)
        {
            __jitExceptionResult486 = __jitExceptionCoalesceLeft487;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingEnumeratorDefinition);
            __jitExceptionResult486 = default;
        }
        var def = __jitExceptionResult486;
        var defBlock = def.Block;
        var defStmt = def.Stmt;
        if (Configuration.DEBUG)
        {
            JITDUMP($"V{info.Local:D2} has single def in {FMT_BB(defBlock.bbNum)} at " +
                $"[{TreeIdForDump(defStmt.RootNode):D6}]\n");
        }
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
        global::RyuJitSharp.FlowGraphDominatorTree __jitExceptionResult488;
        var __jitExceptionCoalesceLeft489 = compiler._domTree;
        if (__jitExceptionCoalesceLeft489 != null)
        {
            __jitExceptionResult488 = __jitExceptionCoalesceLeft489;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ConditionalEscapeRequiresDominators);
            __jitExceptionResult488 = default;
        }
        var domTree = __jitExceptionResult488;
        if (domTree.Dominates(allocBlock, defBlock))
        {
            JITDUMP($"Unexpected, alloc site {FMT_BB(allocBlock.bbNum)} dominates def block {FMT_BB(defBlock.bbNum)}. " +
                "We will clone anyways.\n");
        }
        else
        {
            global::RyuJitSharp.BasicBlock __jitExceptionResult112;
            var __jitExceptionCoalesceLeft113 = defBlock.bbIDom;
            if (__jitExceptionCoalesceLeft113 != null)
            {
                __jitExceptionResult112 = __jitExceptionCoalesceLeft113;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ConditionalAllocationSDefinitionHasNoDominator);
                __jitExceptionResult112 = default;
            }
            var possibleGuardBlock = __jitExceptionResult112;
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
            global::System.Collections.Generic.List<global::RyuJitSharp.ObjectAllocator.EnumeratorVarAppearance> __jitExceptionResult114;
            var __jitExceptionCoalesceLeft115 = enumeratorVar.Appearances;
            if (__jitExceptionCoalesceLeft115 != null)
            {
                __jitExceptionResult114 = __jitExceptionCoalesceLeft115;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingEnumeratorAppearanceList);
                __jitExceptionResult114 = default;
            }
            var appearances = __jitExceptionResult114;

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
        global::RyuJitSharp.GenTree __jitExceptionResult490;
        var __jitExceptionCoalesceLeft491 = info.AllocTree;
        if (__jitExceptionCoalesceLeft491 != null)
        {
            __jitExceptionResult490 = __jitExceptionCoalesceLeft491;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingEnumeratorAllocationTree);
            __jitExceptionResult490 = default;
        }
        var allocTree = __jitExceptionResult490;
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

            global::System.Collections.Generic.List<global::RyuJitSharp.ObjectAllocator.EnumeratorVarAppearance> __jitExceptionResult116;
            var __jitExceptionCoalesceLeft117 = enumeratorVar.Appearances;
            if (__jitExceptionCoalesceLeft117 != null)
            {
                __jitExceptionResult116 = __jitExceptionCoalesceLeft117;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingAllocationTempAppearances);
                __jitExceptionResult116 = default;
            }
            var appearances = __jitExceptionResult116;
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

            global::System.Collections.Generic.List<global::RyuJitSharp.ObjectAllocator.EnumeratorVarAppearance> __jitExceptionResult118;
            var __jitExceptionCoalesceLeft119 = enumeratorVar.Appearances;
            if (__jitExceptionCoalesceLeft119 != null)
            {
                __jitExceptionResult118 = __jitExceptionCoalesceLeft119;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.MissingUseTempAppearances);
                __jitExceptionResult118 = default;
            }
            var appearances = __jitExceptionResult118;
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
        global::RyuJitSharp.FlowGraphDfsTree __jitExceptionResult492;
        var __jitExceptionCoalesceLeft493 = compiler._dfsTree;
        if (__jitExceptionCoalesceLeft493 != null)
        {
            __jitExceptionResult492 = __jitExceptionCoalesceLeft493;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ConditionalEscapeRequiresDFS);
            __jitExceptionResult492 = default;
        }
        var dfsTree = __jitExceptionResult492;

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
