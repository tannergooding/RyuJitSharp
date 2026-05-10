// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public readonly ref partial struct BBSuccBlockList
{
    private readonly ReadOnlySpan<FlowEdge> m_succs;
    private readonly succsInlineArray m_succsInline;
    private readonly int m_succCount;

    public BBSuccBlockList(BasicBlock block)
    {
        switch (block.Kind)
        {
            case BBJ_THROW:
            case BBJ_RETURN:
            case BBJ_EHFAULTRET:
            {
                m_succs = [];
                m_succCount = 0;
                break;
            }

            case BBJ_CALLFINALLY:
            case BBJ_CALLFINALLYRET:
            case BBJ_ALWAYS:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            case BBJ_LEAVE:
            {
                m_succsInline[0] = block.TargetEdge;
                m_succCount = 1;
                break;
            }

            case BBJ_COND:
            {
                // If the true/false successors are identical, then only include
                // them once in the iteration (this is the same behavior as NumSucc()/GetSucc()).
                if (block.TrueEdge == block.FalseEdge)
                {
                    m_succsInline[0] = block.FalseEdge;
                    m_succCount = 1;
                }
                else
                {
                    m_succsInline[0] = block.FalseEdge;
                    m_succsInline[1] = block.TrueEdge;
                    m_succCount = 2;
                }
                break;
            }

            case BBJ_EHFINALLYRET:
            {
                // We don't use the m_succs in-line data; use the existing successor table in the block.
                // We must tolerate iterating successors early in the system, before EH_FINALLYRET successors have
                // been computed.
                var ehfTargets = block.EhfTargets;
                m_succs = (ehfTargets is not null) ? ehfTargets.Succs : [];
                m_succCount = m_succs.Length;
                break;
            }

            case BBJ_SWITCH:
            {
                // We don't use the m_succs in-line data for switches; use the existing jump table in the block.
                m_succs = block.SwitchTargets.Succs;
                m_succCount = m_succs.Length;
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
        var succs = (m_succCount <= 2) ? m_succsInline : m_succs;
        return new BlockEnumerator(succs[0..m_succCount]);
    }

    [InlineArray(2)]
    private struct succsInlineArray
    {
        public FlowEdge e0;
    }
}
