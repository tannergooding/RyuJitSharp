// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private void DoAnalysis()
    {
        assert(_isObjectStackAllocationEnabled);
        assert(!_analysisDone);

        PrepareAnalysis();

        if (_bvCount > 0)
        {
            _escapingPointers = BitVecOps.MakeEmpty(_bitVecTraits);
            _definitelyUsedPointers = BitVecOps.MakeEmpty(_bitVecTraits);
            _connGraphAdjacencyMatrix = new nint[_bvCount][];

            if (CanHavePseudos())
            {
                var compiler = CompilerInstance;
                assert(compiler._dfsTree is not null);
                assert(compiler._domTree is null);
                compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            }

            for (var index = 0; index < _bvCount; index++)
            {
                _connGraphAdjacencyMatrix[index] = BitVecOps.MakeEmpty(_bitVecTraits);
            }

            MarkEscapingVarsAndBuildConnGraph();
            ComputeEscapingNodes(_bitVecTraits, _escapingPointers);

            // A value flowing outside the method is also a nontrivial use.
            BitVecOps.UnionD(_bitVecTraits, _definitelyUsedPointers, _escapingPointers);
            ComputeConnGraphClosure(_bitVecTraits, _definitelyUsedPointers, "used");
        }

#if DEBUG
        if (JitConfig.JitObjectStackAllocationDumpConnGraph > 0)
        {
            JITDUMP("digraph ConnectionGraph {\n");
            var adjacencyMatrix = _connGraphAdjacencyMatrix;
            assert(adjacencyMatrix is not null);

            for (var index = 0; index < _bvCount; index++)
            {
                BitVecOps.VisitBits(_bitVecTraits, adjacencyMatrix[index], source => {
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(source);
                    }
                    JITDUMP(" -> ");
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(index);
                    }
                    JITDUMP(";\n");
                    return true;
                });

                if (CanIndexEscape(index))
                {
                    if (CompilerInstance.verbose)
                    {
                        DumpIndex(index);
                    }
                    JITDUMP(" -> E;\n");
                }
            }
            JITDUMP("}\n");
        }
#endif
        _analysisDone = true;
    }

    private struct BuildConnGraphVisitor : IGenTreeVisitor<BuildConnGraphVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool DoPostOrder => true;
        public static bool DoLclVarsOnly => true;
        public static bool ComputeStack => true;

        private readonly ObjectAllocator _allocator;
        private readonly BasicBlock _block;
        private readonly Statement _stmt;
        private readonly GenTreeStack _ancestors;

        public BuildConnGraphVisitor(ObjectAllocator allocator, BasicBlock block, Statement stmt)
        {
            _allocator = allocator;
            _block = block;
            _stmt = stmt;
            _ancestors = [];
        }

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            var lclNum = tree.AsLclVarCommon().LclNum;
            var compiler = _allocator.CompilerInstance;
            ref var lclDsc = ref compiler.lvaGetDesc(lclNum);

            if (!_allocator.IsTrackedLocal(lclNum))
            {
                return Compiler.fgWalkResult.WALK_CONTINUE;
            }

            var lclIndex = _allocator.LocalToIndex(lclNum);
            if (_allocator.CanIndexEscape(lclIndex))
            {
                return Compiler.fgWalkResult.WALK_CONTINUE;
            }

            if (tree.Oper.IsLocalStore)
            {
                _allocator.CheckForGuardedAllocationOrCopy(_block, _stmt, ref use, user, lclNum);
            }
            else if (tree.Oper is GT_LCL_VAR)
            {
                assert(_ancestors.Peek() == tree);
                _allocator.AnalyzeParentStack(_ancestors, lclIndex, _block);
            }
            else if ((tree.Oper is GT_LCL_ADDR) && (lclDsc.Type is TYP_STRUCT))
            {
                assert(_ancestors.Peek() == tree);
                _allocator.AnalyzeParentStack(_ancestors, lclIndex, _block);
            }
            else if (tree.Oper is GT_LCL_FLD)
            {
                JITDUMP($"V{lclNum:D2} local field at [{TreeIdForDump(tree):D6}]\n");
                _allocator.MarkLclVarAsEscaping(lclNum);
            }
            else
            {
                assert((tree.Oper is GT_LCL_ADDR) && (lclDsc.Type is not TYP_STRUCT));
                JITDUMP($"V{lclNum:D2} address taken at [{TreeIdForDump(tree):D6}]\n");
                _allocator.MarkLclVarAsEscaping(lclNum);
            }

            if (!_allocator.CanIndexEscape(lclIndex) && !tree.Oper.IsLocalStore)
            {
                _allocator.RecordAppearance(lclNum, _block, _stmt, ref use, user);
            }

            return Compiler.fgWalkResult.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            var allocator = _allocator;
            var compiler = allocator.CompilerInstance;

            if (tree.Oper.IsLocalStore)
            {
                var local = tree.AsLclVarCommon();
                var lclNum = local.LclNum;

                if (allocator.IsTrackedLocal(lclNum) && !allocator.CanLclVarEscape(lclNum))
                {
                    if (!allocator._storeAddressToIndexMap.TryGetValue(tree, out var info) || !info.Connected)
                    {
                        var data = local.Data;
                        var allocationKind = allocator.AllocationKind(data);
                        var valueIsUnknown = (allocationKind is ObjectAllocationType.OAT_NEWOBJ_HEAP) ||
                            ((allocationKind is ObjectAllocationType.OAT_NONE) && !data.IsIntegralConst(0));

                        if (valueIsUnknown)
                        {
                            JITDUMP($"V{lclNum:D2} value unknown at [{TreeIdForDump(tree):D6}]\n");
                            allocator.AddConnGraphEdgeIndex(allocator.LocalToIndex(lclNum), allocator._unknownSourceIndex);
                        }
                    }
                    else
                    {
                        JITDUMP($" ... Already connected at [{TreeIdForDump(tree):D6}]\n");
                    }
                }
                else
                {
                    JITDUMP($" ... Not a GC store at [{TreeIdForDump(tree):D6}]\n");
                }
            }
            else if (tree.Oper is GT_STOREIND or GT_STORE_BLK)
            {
                var isGcStore = allocator.IsTrackedType(tree.Type);
                if (isGcStore && (tree.Oper is GT_STORE_BLK))
                {
                    isGcStore = tree.AsBlk().Layout.HasGCPtr;
                }

                if (isGcStore)
                {
                    if (allocator._storeAddressToIndexMap.TryGetValue(tree, out var info) && !info.Connected)
                    {
                        assert(info.Index != BAD_VAR_NUM);
                        var destination = info.Index;
                        JITDUMP(" ... Unmodelled GC store to");
#if DEBUG
                        if (compiler.verbose)
                        {
                            allocator.DumpIndex(destination);
                        }
#endif
                        JITDUMP($" at [{TreeIdForDump(tree):D6}]\n");

                        if (!tree.AsIndir().Data.IsIntegralConst(0))
                        {
                            allocator.AddConnGraphEdgeIndex(destination, allocator._unknownSourceIndex);
#if DEBUG
                            if (compiler.verbose)
                            {
                                allocator.DumpIndex(destination);
                            }
#endif
                            JITDUMP($" ... value unknown at [{TreeIdForDump(tree):D6}]\n");
                        }
                        else
                        {
                            JITDUMP($" ... Store of nullptr(s) at [{TreeIdForDump(tree):D6}]\n");
                        }

                        info.Connected = true;
                    }
                    else if (info is null)
                    {
                        JITDUMP($" ... No store info for [{TreeIdForDump(tree):D6}]\n");
                    }
                    else
                    {
                        JITDUMP($" ... Already connected at [{TreeIdForDump(tree):D6}]\n");
                    }
                }
                else
                {
                    JITDUMP($" ... Not a GC store at [{TreeIdForDump(tree):D6}]\n");
                }
            }

            return Compiler.fgWalkResult.WALK_CONTINUE;
        }

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<BuildConnGraphVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private void MarkEscapingVarsAndBuildConnGraph()
    {
        var compiler = CompilerInstance;

        for (var lclNum = 0; lclNum < compiler.lvaCount; lclNum++)
        {
            if (!IsTrackedLocal(lclNum))
            {
                continue;
            }

            ref var lclDsc = ref compiler.lvaGetDesc(lclNum);
            var bvIndex = LocalToIndex(lclNum);

            if (lclDsc.IsAddressExposed)
            {
                JITDUMP($"   V{lclNum:D2} is address exposed\n");
                MarkIndexAsEscaping(bvIndex);
                continue;
            }

            if (lclNum == compiler.info.compRetBuffArg)
            {
                JITDUMP($"   V{lclNum:D2} is retbuff\n");
                MarkIndexAsEscaping(bvIndex);
                continue;
            }

#if FEATURE_IMPLICIT_BYREFS
            if (lclDsc.lvIsParam && lclDsc.IsImplicitByRef)
            {
                JITDUMP($"   V{lclNum:D2} is an implicit byref param\n");
                MarkIndexAsEscaping(bvIndex);
                continue;
            }
#endif
            if (lclDsc.lvIsParam || lclDsc.lvIsOSRLocal)
            {
                AddConnGraphEdgeIndex(bvIndex, _unknownSourceIndex);
            }
        }

        MarkIndexAsEscaping(_unknownSourceIndex);
        var dfs = compiler._dfsTree;
        assert(dfs is not null);

        for (var index = dfs.PostOrderCount; index != 0; index--)
        {
            var block = dfs.GetPostOrder(index - 1);
            foreach (var stmt in block.Statements)
            {
                var visitor = new BuildConnGraphVisitor(this, block, stmt);
                visitor.WalkTree(ref stmt.RootNodeRef, null);
            }
        }
    }
}
