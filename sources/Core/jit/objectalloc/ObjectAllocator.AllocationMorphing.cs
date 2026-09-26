// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private readonly Dictionary<int, int> _heapLocalToStackObjLocalMap = [];
    private readonly Dictionary<int, int> _heapLocalToStackArrLocalMap = [];
    private int _stackAllocationCount;

    private sealed class AllocationCandidate(BasicBlock block, Statement statement, GenTree tree,
        int local, ObjectAllocationType kind)
    {
        public readonly BasicBlock Block = block;
        public readonly Statement Statement = statement;
        public readonly GenTree Tree = tree;
        public readonly int Local = local;
        public readonly ObjectAllocationType Kind = kind;
        public string? OnHeapReason;
        public bool BashCall;
    }

    internal unsafe bool MorphAllocObjNodes()
    {
        var compiler = CompilerInstance;
        _stackAllocationCount = 0;
        _possiblyStackPointingPointers = BitVecOps.MakeEmpty(_bitVecTraits);
        _definitelyStackPointingPointers = BitVecOps.MakeEmpty(_bitVecTraits);

        foreach (var block in compiler.Blocks)
        {
            if (!block.HasFlag(BBF_HAS_NEWOBJ) && !block.HasFlag(BBF_HAS_NEWARR))
            {
                continue;
            }

            foreach (var stmt in block.Statements)
            {
                var tree = stmt.RootNode;
                if ((tree.Oper is not GT_STORE_LCL_VAR) || (tree.Type is not TYP_REF))
                {
                    assert(!compiler.gtTreeContainsOper(tree, GT_ALLOCOBJ));
                    continue;
                }

                var store = tree.AsLclVar();
                var kind = AllocationKind(store.Data);
                if (kind is ObjectAllocationType.OAT_NONE)
                {
                    continue;
                }

                var candidate = new AllocationCandidate(block, stmt, tree, store.LclNum, kind);
                MorphAllocObjNode(candidate);
            }
        }

        return _stackAllocationCount > 0;
    }

    private void MorphAllocObjNode(AllocationCandidate candidate)
    {
        var compiler = CompilerInstance;
        if (MorphAllocObjNodeHelper(candidate))
        {
            MarkLclVarAsDefinitelyStackPointing(candidate.Local);
            MarkLclVarAsPossiblyStackPointing(candidate.Local);

            if (_enumeratorLocalToPseudoIndexMap.TryGetValue(candidate.Local, out var pseudoIndex) &&
                _cloneMap.TryGetValue(pseudoIndex, out var clone) && clone.WillClone)
            {
                JITDUMP($"Connecting stack allocated enumerator V{candidate.Local:D2} to its address var V{clone.EnumeratorLocal:D2}\n");
                AddConnGraphEdge(candidate.Local, clone.EnumeratorLocal);
                MarkLclVarAsPossiblyStackPointing(clone.EnumeratorLocal);
                MarkLclVarAsDefinitelyStackPointing(clone.EnumeratorLocal);
            }

            if (candidate.BashCall)
            {
                candidate.Statement.RootNode.BashToNOP();
            }

            compiler.optMethodFlags |= OMF_HAS_OBJSTACKALLOC;
            _stackAllocationCount++;
        }
        else
        {
            var reason = candidate.OnHeapReason
                ?? throw new FatalJitException("A rejected stack allocation requires a reason.");
#if DEBUG
            JITDUMP($"Allocating V{candidate.Local:D2} / [{candidate.Tree.TreeId:D6}] on the heap: {reason}\n");
#endif
            if (candidate.Kind is ObjectAllocationType.OAT_NEWOBJ or ObjectAllocationType.OAT_NEWOBJ_HEAP)
            {
                var store = candidate.Tree.AsLclVar();
                var newData = MorphAllocObjNodeIntoHelperCall(store.Data.AsAllocObj());
                store.DataRef = newData;
                store.Flags |= newData.Flags & GTF_ALL_EFFECT;
            }

            if (IsTrackedLocal(candidate.Local))
            {
                AddConnGraphEdgeIndex(LocalToIndex(candidate.Local), _unknownSourceIndex);
            }
        }
    }

    private bool MorphAllocObjNodeHelper(AllocationCandidate candidate)
    {
        var compiler = CompilerInstance;
        if (!_isObjectStackAllocationEnabled)
        {
            candidate.OnHeapReason = "[object stack allocation disabled]";
            return false;
        }

        if (candidate.Block.HasFlag(BBF_BACKWARD_JUMP))
        {
            candidate.OnHeapReason = "[alloc in loop]";
            return false;
        }

        if (BlockIsCloneOrWasCloned(candidate.Block))
        {
            candidate.OnHeapReason = "[allocation was cloned]";
            return false;
        }

        foreach (var clone in _cloneMap.Values)
        {
            if (!clone.WillClone || clone.GuardBlock is null ||
                clone.AllocTree == candidate.Tree.AsLclVar().Data)
            {
                continue;
            }

            var dfs = compiler._dfsTree
                ?? throw new FatalJitException("Conditional escape cloning requires a DFS tree.");
            if (!dfs.Contains(candidate.Block))
            {
                continue;
            }

            var dom = compiler._domTree
                ?? throw new FatalJitException("Conditional escape cloning requires dominators.");
            var definition = clone.DefBlock
                ?? throw new FatalJitException("Conditional escape cloning requires a definition block.");
            if (dom.Dominates(clone.GuardBlock, candidate.Block) &&
                !dom.Dominates(definition, candidate.Block))
            {
                candidate.OnHeapReason = "[on slow path of conditional escape clone]";
                return false;
            }
        }

        switch (candidate.Kind)
        {
            case ObjectAllocationType.OAT_NEWARR:
            {
                return MorphAllocObjNodeHelperArr(candidate);
            }

            case ObjectAllocationType.OAT_NEWOBJ:
            {
                return MorphAllocObjNodeHelperObj(candidate);
            }

            case ObjectAllocationType.OAT_NEWOBJ_HEAP:
            {
                candidate.OnHeapReason = "[runtime disallows]";
                return false;
            }

            default:
            {
                throw new FatalJitException("Unexpected object allocation candidate kind.");
            }
        }
    }

    private unsafe bool MorphAllocObjNodeHelperArr(AllocationCandidate candidate)
    {
        var compiler = CompilerInstance;
        assert(candidate.Block.HasFlag(BBF_HAS_NEWARR));
        if (_isR2R)
        {
            candidate.OnHeapReason = "[R2R array not yet supported]";
            return false;
        }

        var call = candidate.Tree.AsLclVar().Data.AsCall();
        var clsHnd = compiler.gtGetHelperCallClassHandle(call, out var isExact, out var isNonNull);
        var length = call.Args.GetUserArgByIndex(1)?.Node
            ?? throw new FatalJitException("A new-array helper requires a length argument.");
        var blockSize = 0;
        compiler.Metrics.NewArrayHelperCalls++;

        if (!isExact || !isNonNull)
        {
            candidate.OnHeapReason = "[array type is either non-exact or null]";
            return false;
        }

        if (!length.Oper.IsCnsIntOrI)
        {
            candidate.OnHeapReason = "[non-constant array size]";
            return false;
        }

        if (!CanAllocateLclVarOnStack(candidate.Local, clsHnd, candidate.Kind,
            length.AsIntCon().IconValue, ref blockSize, out var reason))
        {
            candidate.OnHeapReason = reason;
            return false;
        }

        JITDUMP($"Allocating V{candidate.Local:D2} on the stack\n");
        var stackLocal = MorphNewArrNodeIntoStackAlloc(call, clsHnd,
            unchecked((uint)length.AsIntCon().IconValue), blockSize, candidate.Block, candidate.Statement);
        _heapLocalToStackArrLocalMap[candidate.Local] = stackLocal;
        compiler.Metrics.StackAllocatedArrays++;
        return true;
    }

    private unsafe bool MorphAllocObjNodeHelperObj(AllocationCandidate candidate)
    {
        var compiler = CompilerInstance;
        assert(candidate.Block.HasFlag(BBF_HAS_NEWOBJ));
        var alloc = candidate.Tree.AsLclVar().Data.AsAllocObj();
        var clsHnd = alloc.ClsHnd;
        var isValueClass = compiler.info.compCompHnd->isValueClass(clsHnd);
        if (isValueClass)
        {
            compiler.Metrics.NewBoxedValueClassHelperCalls++;
        }
        else
        {
            compiler.Metrics.NewRefClassHelperCalls++;
        }

        var blockSize = 0;
        if (!CanAllocateLclVarOnStack(candidate.Local, clsHnd, candidate.Kind,
            0, ref blockSize, out var reason))
        {
            candidate.OnHeapReason = reason;
            return false;
        }

        JITDUMP($"Allocating V{candidate.Local:D2} on the stack\n");
        ClassLayout layout;
        if (isValueClass)
        {
            var boxedClass = compiler.info.compCompHnd->getTypeForBox(clsHnd);
            assert(boxedClass != NO_CLASS_HANDLE);
            layout = GetBoxedLayout(compiler.typGetObjLayout(boxedClass));
            compiler.Metrics.StackAllocatedBoxedValueClasses++;
        }
        else
        {
            layout = compiler.typGetObjLayout(clsHnd);
            compiler.Metrics.StackAllocatedRefClasses++;
        }

        var stackLocal = MorphAllocObjNodeIntoStackAlloc(alloc, layout,
            candidate.Block, candidate.Statement);
        _heapLocalToStackObjLocalMap[candidate.Local] = stackLocal;
        candidate.BashCall = true;
        return true;
    }

    private bool BlockIsCloneOrWasCloned(BasicBlock block)
    {
        if (block.bbID >= _initialMaxBlockID)
        {
            JITDUMP($"Block {FMT_BB(block.bbNum)} was cloned as part of conditional escape processing\n");
            return true;
        }

        var traits = new BitVecTraits(CompilerInstance, _initialMaxBlockID);
        foreach (var clone in _cloneMap.Values)
        {
            if (!clone.WillClone || block == clone.AllocBlock)
            {
                continue;
            }

            if (BitVecOps.IsMember(traits, clone.Blocks, block.bbID))
            {
                JITDUMP($"Block {FMT_BB(block.bbNum)} was cloned as part of conditional escape processing\n");
                return true;
            }
        }

        return false;
    }
}
