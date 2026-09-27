// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;

namespace RyuJitSharp;

public partial class Compiler
{
#if DUMP_FLOWGRAPHS
    private sealed class RegionGraph
    {
        private sealed class Region(string name, BasicBlock start, BasicBlock end)
        {
            public Region? Next;
            public Region? Child;
            public readonly string Name = name;
            public readonly BasicBlock Start = start;
            public readonly BasicBlock End = end;
        }

        private readonly uint[] _ordinals;
        private readonly Region _root;

        public RegionGraph(Compiler compiler, uint[] ordinals)
        {
            assert(compiler.fgFirstBB is not null);
            assert(compiler.fgLastBB is not null);

            _ordinals = ordinals;
            _root = new Region("Root", compiler.fgFirstBB, compiler.fgLastBB);
        }

        private uint Ordinal(BasicBlock block) => _ordinals[block.bbNum];

        public void Insert(string name, BasicBlock start, BasicBlock end)
        {
            JITDUMP($"Insert region: {name}, type: EH, start: {FMT_BB(start.bbNum)}, end: {FMT_BB(end.bbNum)}\n");

            var newRegion = new Region(name, start, end);
            var newStartOrdinal = Ordinal(start);
            var newEndOrdinal = Ordinal(end);
            var current = _root;
            var currentStartOrdinal = Ordinal(current.Start);
            var currentEndOrdinal = Ordinal(current.End);

            assert(newStartOrdinal <= newEndOrdinal);
            assert(currentStartOrdinal <= currentEndOrdinal);
            assert(newStartOrdinal >= currentStartOrdinal);
            assert(newEndOrdinal <= currentEndOrdinal);

            while (true)
            {
                Region? previous = null;
                var child = current.Child;
                var descend = false;

                while (child is not null)
                {
                    var childStartOrdinal = Ordinal(child.Start);
                    var childEndOrdinal = Ordinal(child.End);
                    assert(childStartOrdinal <= childEndOrdinal);
                    assert(currentStartOrdinal <= childStartOrdinal);
                    assert(childEndOrdinal <= currentEndOrdinal);

                    if (newEndOrdinal < childStartOrdinal)
                    {
                        newRegion.Next = child;
                        SetChildOrNext(current, previous, newRegion);
                        return;
                    }
                    else if ((newStartOrdinal >= childStartOrdinal) && (newEndOrdinal <= childEndOrdinal))
                    {
                        current = child;
                        currentStartOrdinal = childStartOrdinal;
                        currentEndOrdinal = childEndOrdinal;
                        descend = true;
                        break;
                    }
                    else if (newStartOrdinal <= childStartOrdinal)
                    {
                        // Reparent every consecutive child covered by the new interval.
                        var last = child;
                        var endChild = child.Next;
                        while (endChild is not null)
                        {
                            var endStartOrdinal = Ordinal(endChild.Start);
                            var endEndOrdinal = Ordinal(endChild.End);
                            assert(endStartOrdinal <= endEndOrdinal);

                            if (newEndOrdinal < endStartOrdinal)
                            {
                                break;
                            }

                            last = endChild;
                            endChild = endChild.Next;
                        }

                        newRegion.Next = endChild;
                        SetChildOrNext(current, previous, newRegion);
                        newRegion.Child = child;
                        last.Next = null;
                        return;
                    }

                    previous = child;
                    child = child.Next;
                }

                if (!descend)
                {
                    SetChildOrNext(current, previous, newRegion);
                    return;
                }
            }
        }

        private static void SetChildOrNext(Region parent, Region? previous, Region value)
        {
            if (previous is null)
            {
                parent.Child = value;
            }
            else
            {
                previous.Next = value;
            }
        }

#if DEBUG
        private static void DumpRegionNode(Region region, int indent)
        {
            var spaces = new string(' ', indent);
            jitprintf($"{spaces}======\n");
            jitprintf($"{spaces}Type: {(region.Name == "Root" ? "Root" : "EH")}\n");
            jitprintf($"{spaces}Name: {region.Name}\n");
            jitprintf($"{spaces}Range: {FMT_BB(region.Start.bbNum)}..{FMT_BB(region.End.bbNum)}\n");

            for (var child = region.Child; child is not null; child = child.Next)
            {
                DumpRegionNode(child, indent + 2);
            }
        }

        public void Dump()
        {
            jitprintf("Region graph:\n");
            DumpRegionNode(_root, 0);
            jitprintf("\n");
        }

        private void Verify(Region region)
        {
            var startOrdinal = Ordinal(region.Start);
            var endOrdinal = Ordinal(region.End);
            assert(startOrdinal <= endOrdinal);

            var child = region.Child;
            if (child is not null)
            {
                var childStartOrdinal = Ordinal(child.Start);
                var childEndOrdinal = Ordinal(child.End);
                assert(childStartOrdinal <= childEndOrdinal);
                assert(startOrdinal <= childStartOrdinal);

                while (true)
                {
                    Verify(child);
                    var lastEndOrdinal = childEndOrdinal;
                    child = child.Next;
                    if (child is null)
                    {
                        break;
                    }

                    childStartOrdinal = Ordinal(child.Start);
                    childEndOrdinal = Ordinal(child.End);
                    assert(childStartOrdinal <= childEndOrdinal);
                    assert(lastEndOrdinal < childStartOrdinal);
                }

                assert(childEndOrdinal <= endOrdinal);
            }
        }

        public void Verify()
        {
            foreach (var ordinal in _ordinals)
            {
                assert(ordinal < _ordinals.Length);
            }

            assert(_root.Next is null);
            Verify(_root);
        }
#endif

        public void Output(TextWriter writer)
        {
            var clusterNumber = 0;
            for (var child = _root.Child; child is not null; child = child.Next)
            {
                OutputRegion(writer, child, ref clusterNumber, 4);
            }

            writer.Write('\n');
        }

        private static void OutputRegion(TextWriter writer, Region region, ref int clusterNumber, int indent)
        {
            writer.Write($"{new string(' ', indent)}subgraph cluster_{clusterNumber} {{\n");
            indent += 4;
            writer.Write($"{new string(' ', indent)}label = \"{region.Name}\";\n");
            writer.Write($"{new string(' ', indent)}color = red;\n");
            clusterNumber++;

            var needIndent = true;
            var current = region.Start;
            var end = region.End.Next;
            var child = region.Child;
            var childStart = child?.Start;
            var totalChildren = 0;
            var childCount = 0;
            for (var next = child; next is not null; next = next.Next)
            {
                totalChildren++;
            }

            while (current != end)
            {
                while ((current != childStart) && (current != end))
                {
                    assert(current is not null);
                    writer.Write($"{(needIndent ? new string(' ', indent) : "")}{FMT_BB(current.bbNum)};");
                    needIndent = false;
                    current = current.Next;
                }

                if (current == end)
                {
                    break;
                }

                assert(current is not null);
                assert(child is not null);
                if (!needIndent)
                {
                    writer.Write('\n');
                }

                OutputRegion(writer, child, ref clusterNumber, indent);
                needIndent = true;
                childCount++;
                current = child.End.Next;
                child = child.Next;
                childStart = child?.Start;
            }

            indent -= 4;
            writer.Write($"\n{new string(' ', indent)}}}\n");
            assert(childCount == totalChildren);
        }
    }

    internal void fgDumpFlowGraphEH(TextWriter writer, uint[] ordinals)
    {
        var regions = new RegionGraph(this, ordinals);

        for (var index = 0; index < compHndBBtabCount; index++)
        {
            ref readonly var descriptor = ref compHndBBtab[index];
            regions.Insert($"EH#{index} try", descriptor.ebdTryBeg, descriptor.ebdTryLast);

            var handlerType = descriptor.ebdHandlerType switch {
                EH_HANDLER_CATCH => "catch",
                EH_HANDLER_FILTER => "filter-hnd",
                EH_HANDLER_FAULT => "fault",
                EH_HANDLER_FINALLY => "finally",
                EH_HANDLER_FAULT_WAS_FINALLY => "fault-was-finally",
                _ => ""
            };
            regions.Insert($"EH#{index} {handlerType}", descriptor.ebdHndBeg, descriptor.ebdHndLast);

            if (descriptor.HasFilter)
            {
                regions.Insert($"EH#{index} filter",
                    descriptor.ebdFilter ?? throw new InvalidOperationException("Filter region has no first block."),
                    descriptor.ebdHndBeg.Prev ?? throw new InvalidOperationException("Filter region has no last block."));
            }
        }

#if DEBUG
        if (verbose)
        {
            regions.Dump();
        }

        regions.Verify();
#endif
        regions.Output(writer);
    }

    private sealed class FlowGraphLoopDumper(FlowGraphNaturalLoops loops, TextWriter writer)
    {
        private readonly BitVecTraits _traits = loops.DfsTree.PostOrderTraits();
        private readonly BitVec _outputBlocks = BitVecOps.MakeEmpty(loops.DfsTree.PostOrderTraits());
        private int _indent = 4;
        private int _loopIndex;

        public void Output(FlowGraphNaturalLoop loop)
        {
            writer.Write($"{new string(' ', _indent)}subgraph cluster_{_loopIndex++} {{\n");
            _indent += 4;
            writer.Write($"{new string(' ', _indent)}label = \"L{loop.Index:D2}\";\n");
            writer.Write($"{new string(' ', _indent)}color = blue;\n");
            writer.Write(new string(' ', _indent));

            _ = loop.VisitLoopBlocksReversePostOrder(block => {
                if (BitVecOps.IsMember(_traits, _outputBlocks, block.bbPostorderNum))
                {
                    return BasicBlockVisit.Continue;
                }

                if (block != loop.Header)
                {
                    var childLoop = loops.GetLoopByHeader(block);
                    if (childLoop is not null)
                    {
                        writer.Write('\n');
                        Output(childLoop);
                        writer.Write($"\n{new string(' ', _indent)}");
                        return BasicBlockVisit.Continue;
                    }
                }

                writer.Write($"{FMT_BB(block.bbNum)};");
                BitVecOps.AddElemD(_traits, _outputBlocks, block.bbPostorderNum);
                return BasicBlockVisit.Continue;
            });

            _indent -= 4;
            writer.Write($"\n{new string(' ', _indent)}}}");
        }
    }

    internal void fgDumpFlowGraphLoops(TextWriter writer)
    {
        var loops = _loops ?? throw new InvalidOperationException("Flow graph loop dumping requires natural loops.");
        var dumper = new FlowGraphLoopDumper(loops, writer);

        foreach (var loop in loops.InReversePostOrder())
        {
            if (loop.Parent is null)
            {
                dumper.Output(loop);
                writer.Write('\n');
            }
        }
    }
#endif
}
