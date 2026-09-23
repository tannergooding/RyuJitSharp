// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class FlowGraphDfsTree
{
    private readonly Compiler _compiler;
    private readonly BasicBlock[] _postOrder;
    private readonly int _postOrderCount;

    public FlowGraphDfsTree(Compiler compiler, BasicBlock[] postOrder, int postOrderCount, bool hasCycle, bool profileAware, bool forWasm = false)
    {
        _compiler = compiler;
        _postOrder = postOrder;
        _postOrderCount = postOrderCount;
        HasCycle = hasCycle;
        IsProfileAware = profileAware;
        IsForWasm = forWasm;
    }

    public Compiler GetCompiler() => _compiler;

    public BasicBlock[] GetPostOrder() => _postOrder;

    public int PostOrderCount => _postOrderCount;

    public BasicBlock GetPostOrder(int index)
    {
        assert((uint)index < (uint)_postOrderCount);
        return _postOrder[index];
    }

    public BitVecTraits PostOrderTraits() => new BitVecTraits(_compiler, _postOrderCount);

    public bool HasCycle { get; }

    public bool IsProfileAware { get; }

    public bool IsForWasm { get; }

#if DEBUG
    public void Dump()
    {
        jitprintf($"DFS tree. {(HasCycle ? "Has cycle" : "No cycle")}.\n");
        jitprintf("PO RPO -> BB [pre, post]\n");

        for (var i = 0; i < _postOrderCount; i++)
        {
            var rpoNum = _postOrderCount - i - 1;
            var block = _postOrder[i];
            jitprintf($"{i:D2} {rpoNum:D2} -> {FMT_BB(block.bbNum)}[{block.bbPreorderNum}, {block.bbPostorderNum}]\n");
        }
    }
#endif

    public bool Contains(BasicBlock block) =>
        ((uint)block.bbPostorderNum < (uint)_postOrderCount) && (GetPostOrder(block.bbPostorderNum) == block);

    public bool IsAncestor(BasicBlock ancestor, BasicBlock descendant)
    {
        assert(Contains(ancestor) && Contains(descendant));
        return (ancestor.bbPreorderNum <= descendant.bbPreorderNum) &&
               (descendant.bbPostorderNum <= ancestor.bbPostorderNum);
    }
}
