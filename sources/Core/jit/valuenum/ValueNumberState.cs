// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ValueNumberState
{
    private readonly Compiler _compiler;
    private readonly BitVecTraits _blockTraits;
    private readonly BitVec _provenUnreachableBlocks;

    public ValueNumberState(Compiler compiler)
    {
        _compiler = compiler;
        _blockTraits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
        _provenUnreachableBlocks = BitVecOps.MakeEmpty(_blockTraits);
    }

    public void SetUnreachable(BasicBlock block)
    {
        BitVecOps.AddElemD(_blockTraits, _provenUnreachableBlocks, block.bbNum);
    }

    public bool IsReachable(BasicBlock block)
    {
        assert(_compiler._dfsTree is not null);
        return _compiler._dfsTree.Contains(block)
            && !BitVecOps.IsMember(_blockTraits, _provenUnreachableBlocks, block.bbNum);
    }

    public bool IsReachableThroughPred(BasicBlock block, BasicBlock predecessor)
    {
        if (!IsReachable(predecessor))
        {
            return false;
        }

        if ((predecessor.Kind is not BBJ_COND) || (predecessor.TrueEdge == predecessor.FalseEdge))
        {
            return true;
        }

        var statement = predecessor.LastStmt;
        assert(statement is not null);
        var lastTree = statement.RootNode;
        assert(lastTree.Oper is GT_JTRUE);
        var condition = lastTree.AsUnOp().Op1;

        // Native relies on RBO to eventually eliminate the branch proven by
        // liberal VNs; reachability here retains that cross-phase dependency.
        assert(_compiler.vnStore is not null);
        var store = _compiler.vnStore;
        var normalVN = store.VNNormalValue(condition._vnPair.Liberal);
        if (!store.IsVNConstant(normalVN))
        {
            return true;
        }

        var isTaken = normalVN != store.VNZeroForType(TYP_INT);
        var unreachableSuccessor = isTaken ? predecessor.FalseTarget : predecessor.TrueTarget;
        return block != unreachableSuccessor;
    }
}
