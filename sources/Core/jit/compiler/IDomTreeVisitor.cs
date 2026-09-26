// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public interface IDomTreeVisitor<TSelf>
    where TSelf : struct, IDomTreeVisitor<TSelf>, allows ref struct
{
    protected static void WalkTree(ref TSelf self, Compiler compiler, FlowGraphDominatorTree tree)
    {
        WalkTree(ref self, compiler, tree.Nodes);
    }

    // Child, sibling and immediate-dominator links permit a nonrecursive,
    // nonallocating walk with both entry and exit callbacks.
    protected static void WalkTree(ref TSelf self, Compiler compiler, DomTreeNode[] tree)
    {
        self.Begin();

        for (BasicBlock? next, block = compiler.fgFirstBB; block is not null; block = next)
        {
            self.PreOrderVisit(block);
            next = tree[block.bbPostorderNum].firstChild;

            if (next is not null)
            {
                assert(next.bbIDom == block);
                continue;
            }

            do
            {
                self.PostOrderVisit(block);
                next = tree[block.bbPostorderNum].nextSibling;

                if (next is not null)
                {
                    assert(next.bbIDom == block.bbIDom);
                    break;
                }

                block = block.bbIDom;
            }
            while (block is not null);
        }

        self.End();
    }

    // Explicit hooks, even empty ones, avoid boxing a struct to call a default
    // interface implementation.
    void Begin();

    void PreOrderVisit(BasicBlock block);

    void PostOrderVisit(BasicBlock block);

    void End();

    void WalkTree(FlowGraphDominatorTree tree);
}
