// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public readonly ref partial struct BBSuccBlockList
{
    private readonly ReadOnlySpan<FlowEdge> _succs;
    private readonly InlineArray2<FlowEdge> _succsInline;
    private readonly int _succCount;

    public BBSuccBlockList(BasicBlock block)
    {
        switch (block.Kind)
        {
            case BBJ_THROW:
            case BBJ_RETURN:
            case BBJ_EHFAULTRET:
            {
                _succs = [];
                _succCount = 0;
                break;
            }

            case BBJ_CALLFINALLY:
            case BBJ_CALLFINALLYRET:
            case BBJ_ALWAYS:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            case BBJ_LEAVE:
            {
                _succsInline[0] = block.TargetEdge;
                _succCount = 1;
                break;
            }

            case BBJ_COND:
            {
                // If the true/false successors are identical, then only include
                // them once in the iteration (this is the same behavior as NumSucc()/GetSucc()).
                if (block.TrueEdge == block.FalseEdge)
                {
                    _succsInline[0] = block.FalseEdge;
                    _succCount = 1;
                }
                else
                {
                    _succsInline[0] = block.FalseEdge;
                    _succsInline[1] = block.TrueEdge;
                    _succCount = 2;
                }
                break;
            }

            case BBJ_EHFINALLYRET:
            {
                // We don't use the _succs in-line data; use the existing successor table in the block.
                // We must tolerate iterating successors early in the system, before EH_FINALLYRET successors have
                // been computed.
                var ehfTargets = block.EhfTargets;
                _succs = (ehfTargets is not null) ? ehfTargets.Succs : [];
                _succCount = _succs.Length;
                break;
            }

            case BBJ_SWITCH:
            {
                // We don't use the _succs in-line data for switches; use the existing jump table in the block.
                _succs = block.SwitchTargets.Succs;
                _succCount = _succs.Length;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    [UnscopedRef]
    public readonly BlockEnumerator GetEnumerator()
    {
        var succs = (_succCount <= 2) ? _succsInline : _succs;
        return new BlockEnumerator(succs[0.._succCount]);
    }
}
