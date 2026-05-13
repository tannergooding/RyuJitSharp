// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class Statement
{
    // The root of the expression tree.
    // Note: It will be the last node in evaluation order.
    private GenTree _rootNode;

    // The tree list head (for forward walks in evaluation order).
    // The value is `null` until we have set the sequencing of the nodes.
    private GenTree? _treeList;

    // The tree list tail. Only valid when locals are linked (fgNodeThreading
    // == AllLocals), in which case this is the last local.
    // When all nodes are linked (fgNodeThreading == AllTrees), _rootNode
    // should be considered the last node.
    private GenTree? _treeListEnd;

    // The statement nodes are doubly-linked. The first statement node in a block points
    // to the last node in the block via its `_prev` link. Note that the last statement node
    // does not point to the first: it has `_next is null`; that is, the list is not fully circular.
    private Statement? _next;
    private Statement? _prev;

    private DebugInfo _debugInfo;

#if DEBUG
    /// <summary>The instr offset at the end of this statement.</summary>
    private IL_OFFSET _lastILOffset;

    private int _stmtId;
#endif

    public Statement(GenTree expr, int stmtId)
    {
        _rootNode = expr;

#if DEBUG
        _lastILOffset = BAD_IL_OFFSET;
        _stmtId = stmtId;
#endif
    }

    public byte CostEx => _rootNode.CostEx;

    public byte CostSz => _rootNode.CostSz;

    public ref readonly DebugInfo DebugInfo => ref _debugInfo;

#if DEBUG
    public int Id => _stmtId;

    public bool IsPhiDefnStmt => _rootNode.IsPhiDefn;

    public IL_OFFSET LastILOffset
    {
        get
        {
            return _lastILOffset;
        }

        set
        {
            _lastILOffset = value;
        }
    }
#endif

    public LocalsGenTreeList LocalsTreeList
    {
        get
        {
            assert(Debugger.IsAttached || (JitTls.Compiler!.fgNodeThreading == NodeThreading.AllLocals));
            return new LocalsGenTreeList(this);
        }
    }

    public Statement? NextStmt
    {
        get
        {
            return _next;
        }

        set
        {
            _next = value;
        }
    }

    public Statement? PrevStmt
    {
        get
        {
            return _prev;
        }

        set
        {
            _prev = value;
        }
    }

    public GenTree RootNode
    {
        get
        {
            return _rootNode;
        }

        set
        {
            _rootNode = value;
        }
    }

    public ref GenTree RootNodeRef => ref _rootNode;

    public GenTreeList TreeList
    {
        get
        {
            assert(Debugger.IsAttached || (JitTls.Compiler!.fgNodeThreading == NodeThreading.AllTrees));
            return new GenTreeList(_treeList);
        }
    }

    public GenTree? TreeListBegin
    {
        get
        {
            return _treeList;
        }

        set
        {
            _treeList = value;
        }
    }

    public ref GenTree? TreeListBeginRef => ref _treeList;

    public GenTree? TreeListEnd
    {
        get
        {
            return _treeListEnd;
        }

        set
        {
            _treeListEnd = value;
        }
    }

    public ref GenTree? TreeListEndRef => ref _treeListEnd;

    public void SetDebugInfo(in DebugInfo debugInfo)
    {
        debugInfo.Validate();
        _debugInfo = debugInfo;
    }
}
