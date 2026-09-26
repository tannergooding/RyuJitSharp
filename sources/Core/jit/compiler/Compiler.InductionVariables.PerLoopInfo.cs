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

#if DEBUG
        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            assert(_visitedBlocks.Contains(child.Header));
        }
#endif
        info.LocalToOccurrences = [];
        _ = loop.VisitLoopBlocksReversePostOrder(block => {
            // Child loop maps already own their blocks, so each occurrence belongs to one map.
            if (!_visitedBlocks.Add(block))
            {
                return Continue;
            }

            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                foreach (var node in stmt.TreeList)
                {
                    info.HasSuspensionPoint |= (node.Oper is GT_CALL) && node.AsCall().IsAsync;
                    if (!node.Oper.IsAnyLocal)
                    {
                        continue;
                    }

                    var local = node.AsLclVarCommon();
                    info.LocalToOccurrences.TryGetValue(local.LclNum, out var previous);
                    info.LocalToOccurrences[local.LclNum] = new Occurrence(block, stmt, local, previous);
                }
            }

            return Continue;
        });

        return info;
    }

    private bool VisitLoopNestInfo(FlowGraphNaturalLoop loop, Func<LoopInfo, bool> visitor)
    {
        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            if (!VisitLoopNestInfo(child, visitor))
            {
                return false;
            }
        }

        return visitor(GetOrCreateInfo(loop));
    }

    public bool VisitOccurrences(FlowGraphNaturalLoop loop, int lclNum,
        Func<BasicBlock, Statement, GenTreeLclVarCommon, bool> visitor)
    {
        return VisitLoopNestInfo(loop, info => {
            var occurrences = info.LocalToOccurrences
                ?? throw new FatalJitException("Loop occurrences must be initialized before visiting.");
            if (!occurrences.TryGetValue(lclNum, out var occurrence))
            {
                return true;
            }

            while (occurrence is not null)
            {
                if (!visitor(occurrence.Block, occurrence.Statement, occurrence.Node))
                {
                    return false;
                }
                occurrence = occurrence.Next;
            }

            return true;
        });
    }

    public bool HasAnyOccurrences(FlowGraphNaturalLoop loop, int lclNum)
        => !VisitOccurrences(loop, lclNum, (_, _, _) => false);

    public bool VisitStatementsWithOccurrences(FlowGraphNaturalLoop loop, int lclNum,
        Func<BasicBlock, Statement, bool> visitor)
    {
        return VisitLoopNestInfo(loop, info => {
            var occurrences = info.LocalToOccurrences
                ?? throw new FatalJitException("Loop occurrences must be initialized before visiting.");
            if (!occurrences.TryGetValue(lclNum, out var occurrence))
            {
                return true;
            }

            while (occurrence is not null)
            {
                if (!visitor(occurrence.Block, occurrence.Statement))
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
        });
    }

    public bool HasSuspensionPoint(FlowGraphNaturalLoop loop)
    {
        if (!_loops.DfsTree.GetCompiler().compIsAsync)
        {
            return false;
        }

        return !VisitLoopNestInfo(loop, info => !info.HasSuspensionPoint);
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
            _ = loop.VisitLoopBlocks(block => {
                _ = _visitedBlocks.Remove(block);
                return Continue;
            });
        }
    }
}
