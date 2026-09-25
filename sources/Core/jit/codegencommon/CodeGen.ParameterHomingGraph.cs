// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private sealed class RegNode(regNumber reg)
    {
        public regNumber Reg = reg;
        public regNumber CopiedReg = REG_NA;
        public RegNodeEdge? Incoming;
        public RegNodeEdge? Outgoing;
    }

    private sealed class RegNodeEdge(RegNode from, RegNode to, var_types type, uint offset)
    {
        public RegNode From = from;
        public RegNode To = to;
        public var_types Type = type;
        public uint DestOffset = offset;
        public RegNodeEdge? NextIncoming;
    }

    private sealed class RegGraph
    {
        private readonly List<RegNode> _nodes = [];

        public RegNode GetOrAdd(regNumber reg)
        {
            foreach (var node in _nodes)
            {
                if (node.Reg == reg)
                {
                    return node;
                }
            }

            var result = new RegNode(reg);
            _nodes.Add(result);
            return result;
        }

        public void AddEdge(RegNode from, RegNode to, var_types type, uint destOffset)
        {
            assert(type != TYP_STRUCT);
            var edge = new RegNodeEdge(from, to, type, destOffset);
            assert(from.Outgoing is null);
            from.Outgoing = edge;
            edge.NextIncoming = to.Incoming;
            to.Incoming = edge;
        }

        public RegNode? FindNodeToProcess()
        {
            RegNode? lastNode = null;
            foreach (var node in _nodes)
            {
                if (node.Incoming is null)
                {
                    continue;
                }
                if (node.Outgoing is null)
                {
                    return node;
                }

                lastNode = node;
            }

            return lastNode;
        }

        public void RemoveIncomingEdges(RegNode node, ref regMaskTP busyRegs)
        {
            for (var edge = node.Incoming; edge is not null; edge = edge.NextIncoming)
            {
                assert(ReferenceEquals(edge.From.Outgoing, edge));
                edge.From.Outgoing = null;
                var source = edge.From.CopiedReg != REG_NA ? edge.From.CopiedReg : edge.From.Reg;
                busyRegs &= ~regMaskTP.CreateFromRegNum(source, source.SingleTypeMask);
            }

            node.Incoming = null;
        }

#if DEBUG
        public void Dump()
        {
            jitprintf($"{_nodes.Count} registers in register parameter interference graph\n");
            foreach (var node in _nodes)
            {
                jitprintf($"  {node.Reg.Name}");
                for (var edge = node.Incoming; edge is not null; edge = edge.NextIncoming)
                {
                    jitprintf($"\n    <- {edge.From.Reg.Name} ({edge.Type.Name})");
                    if (edge.DestOffset != 0)
                    {
                        jitprintf($" (offset: {edge.DestOffset})");
                    }
                }

                jitprintf("\n");
            }
        }

        public void Validate()
        {
            foreach (var node in _nodes)
            {
                for (var incoming = node.Incoming; incoming is not null; incoming = incoming.NextIncoming)
                {
                    var start = incoming.DestOffset;
                    var end = unchecked(start + (uint)incoming.Type.Size);
                    for (var other = incoming.NextIncoming; other is not null; other = other.NextIncoming)
                    {
                        var otherStart = other.DestOffset;
                        var otherEnd = unchecked(otherStart + (uint)other.Type.Size);
                        if ((otherEnd > start) && (otherStart < end))
                        {
                            assert(false, "Detected conflicting incoming edges when homing parameter registers");
                        }
                    }
                }
            }
        }
#endif
    }
}
#endif
