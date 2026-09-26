// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class FlowGraphDominatorTree
{
    private readonly FlowGraphDfsTree _dfsTree;
    private readonly uint[] _preorderNum;
    private readonly uint[] _postorderNum;

    internal DomTreeNode[] Nodes { get; }

    private FlowGraphDominatorTree(FlowGraphDfsTree dfsTree, DomTreeNode[] nodes, uint[] preorderNum, uint[] postorderNum)
    {
        _dfsTree = dfsTree;
        Nodes = nodes;
        _preorderNum = preorderNum;
        _postorderNum = postorderNum;
    }

    public FlowGraphDfsTree GetDfsTree() => _dfsTree;

    private static BasicBlock IntersectDom(BasicBlock finger1, BasicBlock finger2)
    {
        while (finger1 != finger2)
        {
            while (finger1.bbPostorderNum < finger2.bbPostorderNum)
            {
                assert(finger1.bbIDom is not null);
                finger1 = finger1.bbIDom;
            }

            while (finger2.bbPostorderNum < finger1.bbPostorderNum)
            {
                assert(finger2.bbIDom is not null);
                finger2 = finger2.bbIDom;
            }
        }

        return finger1;
    }

    public BasicBlock Intersect(BasicBlock block1, BasicBlock block2) => IntersectDom(block1, block2);

    public bool Dominates(BasicBlock dominator, BasicBlock dominated)
    {
        assert(_dfsTree.Contains(dominator) && _dfsTree.Contains(dominated));

        // A lies on B's path to the root exactly when A's traversal interval
        // encloses B's. Equality includes a block's dominance of itself.
        return (_preorderNum[dominator.bbPostorderNum] <= _preorderNum[dominated.bbPostorderNum]) &&
               (_postorderNum[dominator.bbPostorderNum] >= _postorderNum[dominated.bbPostorderNum]);
    }

#if DEBUG
    public void Dump()
    {
        foreach (var block in _dfsTree.GetCompiler().Blocks)
        {
            if (!_dfsTree.Contains(block) || (Nodes[block.bbPostorderNum].firstChild is null))
            {
                continue;
            }

            jitprintf($"{FMT_BB(block.bbNum)} : ");

            for (var child = Nodes[block.bbPostorderNum].firstChild; child is not null; child = Nodes[child.bbPostorderNum].nextSibling)
            {
                jitprintf($"{FMT_BB(child.bbNum)} ");
            }

            jitprintf("\n");
        }

        jitprintf("\n");
    }
#endif

    // Requires a unique root, with no incoming edges or enclosing try.
    // Immediate dominators live on BasicBlock, so trees cannot coexist across rebuilds.
    public static FlowGraphDominatorTree Build(FlowGraphDfsTree dfsTree)
    {
        var compiler = dfsTree.GetCompiler();
        var postOrder = dfsTree.GetPostOrder();
        var count = dfsTree.PostOrderCount;
        compiler._blockToEHPreds = null;
        compiler._dominancePreds = null;

        assert(compiler.fgFirstBB is not null);
        assert((compiler.fgFirstBB.bbPreds is null) && !compiler.fgFirstBB.hasTryIndex);
        assert(postOrder[count - 1] == compiler.fgFirstBB);
        compiler.fgFirstBB.bbIDom = null;

        // Cooper, Harvey and Kennedy's iterative algorithm, in reverse postorder.
        // On the first pass only predecessors already visited have valid dominators.
        uint iterations = 0;
        bool changed;

        do
        {
            changed = false;

            for (var i = count - 1; i > 0; i--)
            {
                var poNum = i - 1;
                var block = postOrder[poNum];
                BasicBlock? immediateDominator = null;

                for (var edge = compiler.BlockDominancePreds(block); edge is not null; edge = edge.NextPredEdge)
                {
                    var predecessor = edge.SourceBlock;

                    if (!dfsTree.Contains(predecessor))
                    {
                        continue;
                    }

                    if ((iterations == 0) && (predecessor.bbPostorderNum <= poNum))
                    {
                        continue;
                    }

                    immediateDominator = immediateDominator is null
                        ? predecessor : IntersectDom(immediateDominator, predecessor);
                }

                assert(immediateDominator is not null);

                if (block.bbIDom != immediateDominator)
                {
                    changed = true;
                    block.bbIDom = immediateDominator;
                }
            }

            iterations++;
        }
        while (changed && dfsTree.HasCycle);

        var nodes = new DomTreeNode[count];

        // Prepending in postorder yields siblings in reverse postorder.
        // The root has no parent or siblings.
        for (var i = 0; i < count - 1; i++)
        {
            var block = postOrder[i];
            var parent = block.bbIDom;
            assert(parent is not null);
            assert(dfsTree.Contains(block) && dfsTree.Contains(parent));
            nodes[i].nextSibling = nodes[parent.bbPostorderNum].firstChild;
            nodes[parent.bbPostorderNum].firstChild = block;
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf("After computing the dominance tree:\n");

            for (var i = count; i > 0; i--)
            {
                var poNum = i - 1;

                if (nodes[poNum].firstChild is null)
                {
                    continue;
                }

                jitprintf($"{FMT_BB(postOrder[poNum].bbNum)} :");

                for (var child = nodes[poNum].firstChild; child is not null; child = nodes[child.bbPostorderNum].nextSibling)
                {
                    jitprintf($" {FMT_BB(child.bbNum)}");
                }

                jitprintf("\n");
            }

            jitprintf("\n");
        }
#endif

        var preorderNum = new uint[count];
        var postorderNum = new uint[count];
        var visitor = new NumberDomTreeVisitor(compiler, preorderNum, postorderNum);
        visitor.WalkTree(nodes);

        return new FlowGraphDominatorTree(dfsTree, nodes, preorderNum, postorderNum);
    }

    private struct NumberDomTreeVisitor(Compiler compiler, uint[] preorderNum, uint[] postorderNum) : IDomTreeVisitor<NumberDomTreeVisitor>
    {
        private uint _preNum;
        private uint _postNum;

        public readonly void Begin()
        {
        }

        public void PreOrderVisit(BasicBlock block)
        {
            preorderNum[block.bbPostorderNum] = _preNum++;
        }

        public void PostOrderVisit(BasicBlock block)
        {
            postorderNum[block.bbPostorderNum] = _postNum++;
        }

        public readonly void End()
        {
        }

        public void WalkTree(DomTreeNode[] nodes) => IDomTreeVisitor<NumberDomTreeVisitor>.WalkTree(ref this, compiler, nodes);

        public void WalkTree(FlowGraphDominatorTree tree) => IDomTreeVisitor<NumberDomTreeVisitor>.WalkTree(ref this, compiler, tree);
    }
}
