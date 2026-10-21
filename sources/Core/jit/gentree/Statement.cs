// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class Statement
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

    private sealed class DebugData
    {
        private static ConditionalWeakTable<Statement, DebugData>? s_instances;

        internal int Id;
        internal IL_OFFSET LastILOffset;

        internal static DebugData Get(Statement statement)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(statement, static _ => new DebugData());
        }
    }

    public Statement(GenTree expr, int stmtId)
    {
        _rootNode = expr;

        if (Configuration.DEBUG)
        {
            var debugData = DebugData.Get(this);
            debugData.Id = stmtId;
            debugData.LastILOffset = BAD_IL_OFFSET;
        }
    }

    public byte CostEx => _rootNode.CostEx;

    public byte CostSz => _rootNode.CostSz;

    public ref readonly DebugInfo DebugInfo => ref _debugInfo;

    public int Id => GetDebugData().Id;

    public IL_OFFSET LastILOffset
    {
        get
        {
            return GetDebugData().LastILOffset;
        }

        set
        {
            GetDebugData().LastILOffset = value;
        }
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("Statement debug data is only available in Debug builds.");
        }

        return DebugData.Get(this);
    }

    public bool IsPhiDefnStmt => _rootNode.IsPhiDefn;

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

    /// <summary>Visit occurrences in locals-list order without expanding promoted parents.</summary>
    /// <remarks>The callback may abort, but must not change the list and continue.</remarks>
    public GenTree.VisitResult VisitLogicalLocalOccurrencesViaLocalsTreeList(
        Func<LocalOccurrence, GenTree.VisitResult> visitor)
    {
        assert(JitTls.Compiler!.fgNodeThreading == NodeThreading.AllLocals);

        foreach (var node in LocalsTreeList)
        {
            if (visitor(new LocalOccurrence(node)) == GenTree.VisitResult.Abort)
            {
                return GenTree.VisitResult.Abort;
            }
        }

        return GenTree.VisitResult.Continue;
    }

    public void SetDebugInfo(in DebugInfo debugInfo)
    {
        debugInfo.Validate();
        _debugInfo = debugInfo;
    }
}
