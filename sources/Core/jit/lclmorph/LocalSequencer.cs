// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct LocalSequencer : IGenTreeVisitor<LocalSequencer>
{
    public static bool DoPostOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly Compiler _compiler;
    private readonly GenTreeStack _ancestors;
    private GenTree? _prevNode;

    public LocalSequencer(Compiler compiler)
    {
        _compiler = compiler;
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
    {
        var node = use;

        if (node.Oper.IsAnyLocal)
        {
            SequenceLocal(node.AsLclVarCommon());
        }

        if (node.Oper.IsCall)
        {
            SequenceCall(node.AsCall());
        }
        return Compiler.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<LocalSequencer>.WalkTree(ref this, ref use, user, _ancestors);

    /// <summary>Start sequencing a statement. Must be called before other members are called for a specified statement.</summary>
    /// <param name="stmt">the statement</param>
    public void Start(Statement stmt)
    {
        // We use the root node as a 'sentinel' node that will keep the head and tail of the sequenced list.
        var rootNode = stmt.RootNode;
        {
            rootNode.Prev = null;
            rootNode.Next = null;
        }
        _prevNode = rootNode;
    }

    /// <summary>Finish sequencing a statement. Should be called after sub nodes of the statement have been visited and sequenced.</summary>
    /// <param name="stmt">the statement</param>
    public readonly void Finish(Statement stmt)
    {
        var rootNode = stmt.RootNode;

        var firstNode = rootNode.Next;
        var lastNode = _prevNode;

        if (firstNode is null)
        {
            lastNode = null;
        }
        else
        {
            assert(lastNode is not null);

            // Clear the links on the sentinel in case it didn't end up in the list.
            if (rootNode != lastNode)
            {
                assert(rootNode.Prev is null);
                rootNode.Next = null;
            }

            lastNode.Next = null;
            firstNode.Prev = null;
        }

        stmt.TreeListBegin = firstNode;
        stmt.TreeListEnd = lastNode;
    }

    /// <summary>Add a local to the list.</summary>
    /// <param name="lcl">the local</param>
    public void SequenceLocal(GenTreeLclVarCommon lcl)
    {
        assert(_prevNode is not null);
        lcl.Prev = _prevNode;

        _prevNode.Next = lcl;
        _prevNode = lcl;
    }

    /// <summary>Post-process a call that may define a local.</summary>
    /// <param name="call">the call</param>
    /// <remarks>calls may also define a local that we would like to see after all other operands of the call have been evaluated.</remarks>
    public void SequenceCall(GenTreeCall call)
    {
        var sequencer = this;
        _ = call.VisitPhysicalLocalDefNodes(_compiler, node => {
            sequencer.MoveNodeToEnd(node.AsLclVarCommon());
            return GenTree.VisitResult.Continue;
        });
        this = sequencer;
    }

    /// <summary>Fully sequence a statement.</summary>
    /// <param name="stmt">the statement</param>
    public void Sequence(Statement stmt)
    {
        Start(stmt);
        _ = WalkTree(ref stmt.RootNodeRef, user: null);
        Finish(stmt);
    }

    /// <summary>Move a node from its current position in the linked list to the end.</summary>
    /// <param name="node">The node</param>
    private void MoveNodeToEnd(GenTreeLclVarCommon node)
    {
        if ((_prevNode == node) || (node.Next is null))
        {
            return;
        }

        var prev = node.Prev;
        var next = node.Next;

        // Should have sentinel always, even as the first local.
        assert(prev is not null);

        prev.Next = next;
        next.Prev = prev;

        SequenceLocal(node);
    }
}
