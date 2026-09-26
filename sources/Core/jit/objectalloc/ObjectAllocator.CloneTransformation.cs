// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private struct ReplaceVisitor : IGenTreeVisitor<ReplaceVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool DoLclVarsOnly => true;

        private readonly CloneInfo _info;
        private readonly int _newLclNum;
        private readonly GenTreeStack _ancestors;

        public bool MadeChanges;

        public ReplaceVisitor(CloneInfo info, int newLclNum)
        {
            _info = info;
            _newLclNum = newLclNum;
            _ancestors = [];
            MadeChanges = false;
        }

        public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use.AsLclVarCommon();
            if (node.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR)
            {
                var appearances = _info.AppearanceMap
                    ?? throw new InvalidOperationException("Missing enumerator appearance map.");
                if (appearances.TryGetValue(node.LclNum, out var variable) && !variable.IsInitialAllocTemp)
                {
                    node.LclNum = _newLclNum;
                }

                // The native visitor counts every scalar local it walks, even when it is not rewritten.
                MadeChanges = true;
            }

            return Compiler.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
            => Compiler.WALK_CONTINUE;

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ReplaceVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private unsafe void CloneAndSpecialize(CloneInfo info)
    {
        var compiler = CompilerInstance;
        assert(info.CanClone && info.WillClone);
        var allocBlock = info.AllocBlock ?? throw new InvalidOperationException("Missing allocation block.");
        var blocksToClone = info.BlocksToClone ?? throw new InvalidOperationException("Missing blocks to clone.");
        var appearanceMap = info.AppearanceMap ?? throw new InvalidOperationException("Missing enumerator appearances.");
        JITDUMP($"\nCloning to ensure allocation at {FMT_BB(allocBlock.bbNum)} does not escape\n");

        var map = new Dictionary<BasicBlock, BasicBlock>();
        var insertionPoint = allocBlock;
        var enclosingEHRegion = compiler.ehGetMostNestedRegionIndex(insertionPoint, out var inTry);
        if (enclosingEHRegion != 0)
        {
            ref var enclosing = ref compiler.ehGetDsc(checked((ushort)(enclosingEHRegion - 1)));
            insertionPoint = inTry ? enclosing.ebdTryLast : enclosing.ebdHndLast;
            JITDUMP($"Will insert new blocks at end of enclosing EH#{enclosingEHRegion - 1} " +
                $"{(inTry ? "try" : "handler")} region {FMT_BB(insertionPoint.bbNum)}\n");
        }
        else
        {
            JITDUMP($"Will insert new blocks after allocation block {FMT_BB(insertionPoint.bbNum)}\n");
        }

        var oldLast = insertionPoint;
        var originalScale = Math.Max(0.0, 1.0 - info.ProfileScale);

        foreach (var block in blocksToClone)
        {
            if (map.ContainsKey(block))
            {
                continue;
            }

            if (compiler.bbIsTryBeg(block))
            {
                var cloneTryInfo = new CloneTryInfo(compiler)
                {
                    Map = map,
                    AddEdges = false,
                    ProfileScale = info.ProfileScale,
                    ScaleOriginalBlockProfile = true,
                };
                if (compiler.fgCloneTryRegion(block, cloneTryInfo, ref insertionPoint) is null)
                {
                    throw new FatalJitException("The selected try region could not be cloned.");
                }
                continue;
            }

            var newBlock = compiler.fgNewBBafter(BBJ_ALWAYS, insertionPoint, extendRegion: false);
            JITDUMP($"Adding {FMT_BB(newBlock.bbNum)} (copy of {FMT_BB(block.bbNum)}) " +
                $"after {FMT_BB(insertionPoint.bbNum)}\n");
            BasicBlock.CloneBlockState(compiler, newBlock, block);

            assert(newBlock.bbRefs == 0);
            newBlock.scaleBBWeight(info.ProfileScale);
            block.scaleBBWeight(originalScale);
            map.Add(block, newBlock);
            insertionPoint = newBlock;
        }

        foreach (var block in blocksToClone)
        {
            if (!map.TryGetValue(block, out var newBlock))
            {
                throw new InvalidOperationException("Missing cloned block.");
            }

            assert(!newBlock.HasInitializedTarget);
            JITDUMP($"Updating targets: {FMT_BB(block.bbNum)} mapped to {FMT_BB(newBlock.bbNum)}\n");
            compiler.optSetMappedBlockTargets(block, newBlock, map);
        }

        if (enclosingEHRegion != 0)
        {
            var postCloneRegion = compiler.ehGetMostNestedRegionIndex(allocBlock, out var postCloneInTry);
            assert(postCloneRegion >= enclosingEHRegion && inTry == postCloneInTry);
            foreach (ref var clause in new EHClauses(compiler, checked((ushort)(enclosingEHRegion - 1))))
            {
                if (clause.ebdTryLast == oldLast)
                {
                    compiler.fgSetTryEnd(ref clause, insertionPoint);
                }
                if (clause.ebdHndLast == oldLast)
                {
                    compiler.fgSetHndEnd(ref clause, insertionPoint);
                }
            }
        }

        var newEnumeratorLocal = compiler.lvaGrabTemp(false, "fast-path enumerator");
        info.EnumeratorLocal = newEnumeratorLocal;
        ref var enumeratorDsc = ref compiler.lvaGetDesc(newEnumeratorLocal);
        enumeratorDsc.Type = TYP_REF;
        enumeratorDsc.lvSingleDef = true;
        compiler.lvaSetClass(newEnumeratorLocal, info.Type, true);
        enumeratorDsc.lvTracked = true;
        enumeratorDsc._varIndex = unchecked((ushort)_nextLocalIndex);
        var trackedToLocal = compiler.lvaTrackedToVarNum
            ?? throw new InvalidOperationException("Missing tracked-local map.");
        assert(enumeratorDsc._varIndex < trackedToLocal.Length);
        trackedToLocal[enumeratorDsc._varIndex] = newEnumeratorLocal;
        var adjacencyMatrix = _connGraphAdjacencyMatrix
            ?? throw new InvalidOperationException("Missing connection graph.");
        adjacencyMatrix[enumeratorDsc._varIndex] = BitVecOps.MakeEmpty(_bitVecTraits);
        _nextLocalIndex++;
        assert(_maxPseudos > 0 && enumeratorDsc._varIndex < _firstPseudoIndex);
        JITDUMP($"Tracking V{newEnumeratorLocal:D2} via 0x{enumeratorDsc._varIndex:x2}\n");

        var visitor = new ReplaceVisitor(info, newEnumeratorLocal);
        var defStmts = new Stack<Statement>();

        foreach (var (lclNum, variable) in appearanceMap)
        {
            assert(variable is not null);
            var appearances = variable.Appearances
                ?? throw new InvalidOperationException("Missing enumerator appearance list.");
            foreach (var appearance in appearances)
            {
                if (variable.IsInitialAllocTemp)
                {
                    continue;
                }

                var newBlock = allocBlock;
                if (appearance.Block == allocBlock)
                {
                    JITDUMP($"Updating V{lclNum:D2} {(appearance.IsDef ? "def" : "use")} in " +
                        $"{FMT_BB(newBlock.bbNum)} (allocation block) to V{newEnumeratorLocal:D2}\n");
                }
                else
                {
                    if (!map.TryGetValue(appearance.Block, out var clonedBlock))
                    {
                        throw new InvalidOperationException("Missing clone for enumerator appearance.");
                    }
                    newBlock = clonedBlock;
                    JITDUMP($"Updating V{lclNum:D2} {(appearance.IsDef ? "def" : "use")} in " +
                        $"{FMT_BB(newBlock.bbNum)} (clone of {FMT_BB(appearance.Block.bbNum)}) " +
                        $"to V{newEnumeratorLocal:D2}\n");
                }

                var clonedStmt = newBlock.FirstStmt;
                foreach (var stmt in appearance.Block.Statements)
                {
                    if (stmt == appearance.Stmt)
                    {
                        if (clonedStmt is null)
                        {
                            throw new InvalidOperationException("Missing cloned statement.");
                        }
                        JITDUMP("Before\n");
                        visitor.MadeChanges = false;
                        _ = visitor.WalkTree(ref clonedStmt.RootNodeRef, null);
                        JITDUMP("After\n");
                        assert(visitor.MadeChanges);
                        if (appearance.IsDef)
                        {
                            defStmts.Push(clonedStmt);
                        }
                        break;
                    }
                    clonedStmt = clonedStmt?.NextStmt;
                }

                if (!appearance.IsGuard)
                {
                    continue;
                }

                SpecializeGuard(appearance.Block, keepFastPath: false);
                SpecializeGuard(newBlock, keepFastPath: true);
            }
        }

        while (defStmts.Count != 0)
        {
            var defStmt = defStmts.Pop();
            var root = defStmt.RootNode;
            if ((root.Oper is GT_STORE_LCL_VAR) && (root.AsLclVar().Data.Oper is GT_LCL_VAR) &&
                (root.AsLclVar().LclNum == root.AsLclVar().Data.AsLclVar().LclNum))
            {
                JITDUMP($"Bashing self-copy [{TreeIdForDump(root):D6}] to NOP\n");
                root.BashToNOP();
            }
        }

        var firstBlock = blocksToClone[0];
        if (!map.TryGetValue(firstBlock, out var firstClonedBlock))
        {
            throw new InvalidOperationException("Missing first cloned block.");
        }
        compiler.fgRedirectEdge(ref allocBlock.TargetEdgeRef, firstClonedBlock);

        var allocTree = info.AllocTree ?? throw new InvalidOperationException("Missing allocation tree.");
        if ((allocTree.Flags & GTF_ALLOCOBJ_EMPTY_STATIC) != 0)
        {
            JITDUMP($"Anticipating the empty-collection static enumerator opt for " +
                $"[{TreeIdForDump(allocTree):D6}], so not adjusting profile in the initial GDV region\n");
            return;
        }

        JITDUMP($"Profile data needs more repair. Data " +
            $"{(compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
        if (compiler.fgPgoConsistent)
        {
            compiler.fgPgoConsistent = false;
        }
    }

    private void SpecializeGuard(BasicBlock block, bool keepFastPath)
    {
        var compiler = CompilerInstance;
        var guard = new GuardInfo();
        var relop = IsGuard(block, guard)
            ?? throw new InvalidOperationException("Missing guard in cloned enumerator region.");
        var keepTrueEdge = keepFastPath ? relop.Oper is GT_EQ : relop.Oper is GT_NE;
        var retainedEdge = keepTrueEdge ? block.TrueEdge : block.FalseEdge;
        var removedEdge = keepTrueEdge ? block.FalseEdge : block.TrueEdge;

        JITDUMP($"Modifying {(keepFastPath ? "fast" : "slow")} path GDV guard " +
            $"{FMT_BB(block.bbNum)} to always branch to {FMT_BB(retainedEdge.DestinationBlock.bbNum)}\n");
        compiler.fgRemoveRefPred(removedEdge);
        block.SetKindAndTargetEdge(BBJ_ALWAYS, retainedEdge);
        var stmt = block.LastStmt ?? throw new InvalidOperationException("Missing guard statement.");
        stmt.RootNode = relop;
        compiler.fgRepairProfileCondToUncond(block, retainedEdge, removedEdge);
    }

    private void CloneAndSpecialize()
    {
        var numberOfClonedRegions = 0;
        foreach (var info in _cloneMap.Values)
        {
            if (!info.WillClone)
            {
                continue;
            }

            CloneAndSpecialize(info);
            numberOfClonedRegions++;
        }

        assert(numberOfClonedRegions == _regionsToClone);
    }
}
