// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;

namespace RyuJitSharp;

/// <summary>Checks lclVar semantics.</summary>
/// <remarks>Specifically, ensure that an unaliasable lclVar is not redefined between the point at which a use appears in linear order and the point at which it is used by its user. This ensures that it is always safe to treat a lclVar use as happening at the user (rather than at the lclVar node).</remarks>
public sealed class CheckLclVarSemanticsHelper
{
    private Compiler _compiler;
    private LIR.Range _range;
    private Dictionary<GenTree, bool> _unusedDefs;
    private Dictionary<int, Dictionary<GenTree, GenTree>> _unusedLclVarReads;
    private Stack<Dictionary<GenTree, GenTree>> _lclVarReadsMapsCache;

    /// <summary>Init arguments for the helper.</summary>
    /// <param name="compiler">A compiler context.</param>
    /// <param name="range">a range to do the check.</param>
    /// <param name="unusedDefs">map of defs that do no have users.</param>
    /// <remarks>This needs unusedDefs because unused lclVar reads may otherwise appear as outstanding reads and produce false indications that a write to a lclVar occurs while outstanding reads of that lclVar exist.</remarks>
    public CheckLclVarSemanticsHelper(Compiler compiler, LIR.Range range, Dictionary<GenTree, bool> unusedDefs)
    {
        _compiler = compiler;
        _range = range;
        _unusedDefs = unusedDefs;
        _unusedLclVarReads = [with(capacity: 16)];
        _lclVarReadsMapsCache = [];
    }

    /// <summary>do the check.</summary>
    /// <returns>'true' if the Local variables semantics for the specified range is legal.</returns>
    public bool Check()
    {
        foreach (var node in _range)
        {
            if (!node.IsContained)
            {
                // a contained node reads operands in the parent.
                UseNodeOperands(node);
            }

            var nodeInfo = new AliasSet.NodeInfo(_compiler, node);

            if (nodeInfo.IsLclVarRead && node.IsValue && !_unusedDefs.ContainsKey(node))
            {
                PushLclVarRead(nodeInfo);
            }

            if (nodeInfo.IsLclVarWrite)
            {
                // If this node is a lclVar write, it must not alias a lclVar with an outstanding read
                if (_unusedLclVarReads.TryGetValue(nodeInfo.LclNum, out var reads))
                {
                    foreach (var read in reads)
                    {
                        var readNode = read.Key;
                        var readInfo = new AliasSet.NodeInfo(_compiler, readNode);

                        assert(readInfo.IsLclVarRead && (readInfo.LclNum == nodeInfo.LclNum));

                        var readStart = readInfo.LclOffs;
                        var readEnd = readStart + readNode.Type.Size;

                        var writeStart = nodeInfo.LclOffs;
                        var writeEnd = writeStart + node.Type.StSz;

                        if ((readEnd > writeStart) && (writeEnd > readStart))
                        {
                            JITDUMP($"Write to local overlaps outstanding read (write: {writeStart}..{writeEnd}, read: {readStart}..{readEnd})\n");

                            var found = _range.TryGetUse(readNode, out var use);
                            var user = found ? use.User() : null;

                            foreach (var rangeNode in _range)
                            {
                                string prefix;

                                if (rangeNode == readNode)
                                {
                                    prefix = "read:  ";
                                }
                                else if (rangeNode == node)
                                {
                                    prefix = "write: ";
                                }
                                else if (rangeNode == user)
                                {
                                    prefix = "user:  ";
                                }
                                else
                                {
                                    prefix = "       ";
                                }
                                _compiler.gtDispLIRNode(rangeNode, prefix);
                            }

                            NO_WAY("Write to unaliased local overlaps outstanding read");
                            break;
                        }
                    }
                }
            }
        }
        return true;
    }

    /// <summary>mark the node's operands as used.</summary>
    /// <param name="node">the node to use operands from.</param>
    private void UseNodeOperands(GenTree node)
    {
        foreach (var operand in node.Operands)
        {
            if (operand.IsContained)
            {
                UseNodeOperands(operand);
            }

            var operandInfo = new AliasSet.NodeInfo(_compiler, operand);

            if (operandInfo.IsLclVarRead)
            {
                PopLclVarRead(operandInfo);
            }
        }
    }

    /// <summary>add a local def the list of outstanding reads.</summary>
    /// <param name="defInfo">the node info representing the def.</param>
    private void PushLclVarRead(in AliasSet.NodeInfo defInfo)
    {
        if (!_unusedLclVarReads.TryGetValue(defInfo.LclNum, out var reads))
        {
            if (_lclVarReadsMapsCache.Count != 0)
            {
                reads = _lclVarReadsMapsCache.Pop();
            }
            else
            {
                reads = [];
            }
            _unusedLclVarReads[defInfo.LclNum] = reads;
        }
        reads[defInfo.Node] = defInfo.Node;
    }

    /// <summary>remove a local def from the list of outstanding reads.</summary>
    /// <remarks>the node info representing the def.</remarks>
    private void PopLclVarRead(in AliasSet.NodeInfo defInfo)
    {
        assert(_unusedLclVarReads.TryGetValue(defInfo.LclNum, out var reads));
        assert(reads.Remove(defInfo.Node), "Could not find consumed local in unusedLclVarReads");

        if (reads.Count == 0)
        {
            _ = _unusedLclVarReads.Remove(defInfo.LclNum);
            _lclVarReadsMapsCache.Push(reads);
        }
    }
}
#endif
