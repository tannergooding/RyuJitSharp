// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockVisit;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

internal sealed class PerLoopInfo
{
    private sealed class Occurrence(BasicBlock block, Statement statement, GenTreeLclVarCommon node,
        Occurrence? next)
    {
        public BasicBlock Block { get; } = block;
        public Statement Statement { get; } = statement;
        public GenTreeLclVarCommon Node { get; } = node;
        public Occurrence? Next { get; } = next;
    }

    private sealed class LoopInfo
    {
        public Dictionary<int, Occurrence?>? LocalToOccurrences;
        public bool HasSuspensionPoint;
    }

    private readonly FlowGraphNaturalLoops _loops;
    private readonly LoopInfo[] _info;
    private readonly HashSet<BasicBlock> _visitedBlocks = [];

    internal interface ILoopInfoVisitor<TSelf>
        where TSelf : ILoopInfoVisitor<TSelf>, allows ref struct
    {
        bool Visit(FlowGraphNaturalLoop loop);
    }

    internal interface IOccurrenceVisitor<TSelf>
        where TSelf : struct, IOccurrenceVisitor<TSelf>
    {
        bool Visit(BasicBlock block, Statement statement, GenTreeLclVarCommon node);
    }

    internal interface IStatementOccurrenceVisitor<TSelf>
        where TSelf : struct, IStatementOccurrenceVisitor<TSelf>
    {
        bool Visit(BasicBlock block, Statement statement);
    }

    private readonly struct DelegateOccurrenceVisitor(
        Func<BasicBlock, Statement, GenTreeLclVarCommon, bool> visitor)
        : IOccurrenceVisitor<DelegateOccurrenceVisitor>
    {
        public bool Visit(BasicBlock block, Statement statement, GenTreeLclVarCommon node)
            => visitor(block, statement, node);
    }

    private readonly struct DelegateStatementOccurrenceVisitor(Func<BasicBlock, Statement, bool> visitor)
        : IStatementOccurrenceVisitor<DelegateStatementOccurrenceVisitor>
    {
        public bool Visit(BasicBlock block, Statement statement) => visitor(block, statement);
    }

    private readonly struct AnyOccurrenceVisitor : IOccurrenceVisitor<AnyOccurrenceVisitor>
    {
        public bool Visit(BasicBlock block, Statement statement, GenTreeLclVarCommon node) => false;
    }

    private readonly struct SuspensionPointVisitor(PerLoopInfo owner) : ILoopInfoVisitor<SuspensionPointVisitor>
    {
        public bool Visit(FlowGraphNaturalLoop loop) => !owner.GetOrCreateInfo(loop).HasSuspensionPoint;
    }

    private readonly struct OccurrenceBuilder(PerLoopInfo owner, LoopInfo info) : IBasicBlockVisitor<OccurrenceBuilder>
    {
        public BasicBlockVisit Visit(BasicBlock block)
        {
            // Child loop maps already own their blocks, so each occurrence belongs to one map.
            if (!owner._visitedBlocks.Add(block))
            {
                return BasicBlockVisit.Continue;
            }

            for (var statement = block.GetFirstNonPhiDef(); statement is not null; statement = statement.NextStmt)
            {
                foreach (var node in statement.TreeList)
                {
                    info.HasSuspensionPoint |= (node.Oper is GT_CALL) && node.AsCall().IsAsync;
                    if (!node.Oper.IsAnyLocal)
                    {
                        continue;
                    }

                    var local = node.AsLclVarCommon();
                    _ = info.LocalToOccurrences!.TryGetValue(local.LclNum, out var previous);
                    info.LocalToOccurrences[local.LclNum] = new Occurrence(block, statement, local, previous);
                }
            }

            return BasicBlockVisit.Continue;
        }
    }

    private readonly struct RemoveVisitedBlockVisitor(PerLoopInfo owner) : IBasicBlockVisitor<RemoveVisitedBlockVisitor>
    {
        public BasicBlockVisit Visit(BasicBlock block)
        {
            _ = owner._visitedBlocks.Remove(block);
            return BasicBlockVisit.Continue;
        }
    }

    private ref struct OccurrenceLoopInfoVisitor<TVisitor> : ILoopInfoVisitor<OccurrenceLoopInfoVisitor<TVisitor>>
        where TVisitor : struct, IOccurrenceVisitor<TVisitor>
    {
        private readonly PerLoopInfo _owner;
        private ref TVisitor _visitor;
        private readonly int _lclNum;

        public OccurrenceLoopInfoVisitor(PerLoopInfo owner, int lclNum, ref TVisitor visitor)
        {
            _owner = owner;
            _lclNum = lclNum;
            _visitor = ref visitor;
        }

        public bool Visit(FlowGraphNaturalLoop loop)
        {
            var occurrences = _owner.GetOrCreateInfo(loop).LocalToOccurrences;
            if (occurrences is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.LoopOccurrencesMustBeInitializedBeforeVisiting);
                return default!;
            }

            if (!occurrences.TryGetValue(_lclNum, out var occurrence))
            {
                return true;
            }

            while (occurrence is not null)
            {
                if (!_visitor.Visit(occurrence.Block, occurrence.Statement, occurrence.Node))
                {
                    return false;
                }

                occurrence = occurrence.Next;
            }

            return true;
        }
    }

    private ref struct StatementLoopInfoVisitor<TVisitor> : ILoopInfoVisitor<StatementLoopInfoVisitor<TVisitor>>
        where TVisitor : struct, IStatementOccurrenceVisitor<TVisitor>
    {
        private readonly PerLoopInfo _owner;
        private ref TVisitor _visitor;
        private readonly int _lclNum;

        public StatementLoopInfoVisitor(PerLoopInfo owner, int lclNum, ref TVisitor visitor)
        {
            _owner = owner;
            _lclNum = lclNum;
            _visitor = ref visitor;
        }

        public bool Visit(FlowGraphNaturalLoop loop)
        {
            var occurrences = _owner.GetOrCreateInfo(loop).LocalToOccurrences;
            if (occurrences is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.LoopOccurrencesMustBeInitializedBeforeVisiting);
                return default!;
            }

            if (!occurrences.TryGetValue(_lclNum, out var occurrence))
            {
                return true;
            }

            while (occurrence is not null)
            {
                if (!_visitor.Visit(occurrence.Block, occurrence.Statement))
                {
                    return false;
                }

                var statement = occurrence.Statement;
                do
                {
                    occurrence = occurrence.Next;
                } while ((occurrence is not null) && (occurrence.Statement == statement));
            }

            return true;
        }
    }

    public PerLoopInfo(FlowGraphNaturalLoops loops)
    {
        _loops = loops;
        _info = new LoopInfo[loops.NumLoops];
        for (var index = 0; index < _info.Length; index++)
        {
            _info[index] = new LoopInfo();
        }
    }

    private LoopInfo GetOrCreateInfo(FlowGraphNaturalLoop loop)
    {
        var info = _info[loop.Index];
        if (info.LocalToOccurrences is not null)
        {
            return info;
        }

        if (Configuration.DEBUG)
        {
            for (var child = loop.Child; child is not null; child = child.Sibling)
            {
                assert(_visitedBlocks.Contains(child.Header));
            }
        }
        info.LocalToOccurrences = [];
        var visitor = new OccurrenceBuilder(this, info);
        _ = loop.VisitLoopBlocksReversePostOrder(ref visitor);

        return info;
    }

    private bool VisitLoopNestInfo<TVisitor>(FlowGraphNaturalLoop loop, ref TVisitor visitor)
        where TVisitor : ILoopInfoVisitor<TVisitor>, allows ref struct
    {
        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            if (!VisitLoopNestInfo(child, ref visitor))
            {
                return false;
            }
        }

        return visitor.Visit(loop);
    }

    public bool VisitOccurrences(FlowGraphNaturalLoop loop, int lclNum,
        Func<BasicBlock, Statement, GenTreeLclVarCommon, bool> visitor)
    {
        var adapter = new DelegateOccurrenceVisitor(visitor);
        return VisitOccurrences(loop, lclNum, ref adapter);
    }

    internal bool VisitOccurrences<TVisitor>(
        FlowGraphNaturalLoop loop, int lclNum, ref TVisitor visitor)
        where TVisitor : struct, IOccurrenceVisitor<TVisitor>
    {
        var adapter = new OccurrenceLoopInfoVisitor<TVisitor>(this, lclNum, ref visitor);
        return VisitLoopNestInfo(loop, ref adapter);
    }

    public bool HasAnyOccurrences(FlowGraphNaturalLoop loop, int lclNum)
    {
        var visitor = new AnyOccurrenceVisitor();
        return !VisitOccurrences(loop, lclNum, ref visitor);
    }

    public bool VisitStatementsWithOccurrences(FlowGraphNaturalLoop loop, int lclNum,
        Func<BasicBlock, Statement, bool> visitor)
    {
        var adapter = new DelegateStatementOccurrenceVisitor(visitor);
        return VisitStatementsWithOccurrences(loop, lclNum, ref adapter);
    }

    internal bool VisitStatementsWithOccurrences<TVisitor>(
        FlowGraphNaturalLoop loop, int lclNum, ref TVisitor visitor)
        where TVisitor : struct, IStatementOccurrenceVisitor<TVisitor>
    {
        var adapter = new StatementLoopInfoVisitor<TVisitor>(this, lclNum, ref visitor);
        return VisitLoopNestInfo(loop, ref adapter);
    }

    public bool HasSuspensionPoint(FlowGraphNaturalLoop loop)
    {
        if (!_loops.DfsTree.GetCompiler().compIsAsync)
        {
            return false;
        }

        var visitor = new SuspensionPointVisitor(this);
        return !VisitLoopNestInfo(loop, ref visitor);
    }

    public void Invalidate(FlowGraphNaturalLoop loop)
    {
        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            Invalidate(child);
        }

        var info = _info[loop.Index];
        if (info.LocalToOccurrences is not null)
        {
            info.LocalToOccurrences = null;
            info.HasSuspensionPoint = false;
            var visitor = new RemoveVisitedBlockVisitor(this);
            _ = loop.VisitLoopBlocks(ref visitor);
        }
    }
}
