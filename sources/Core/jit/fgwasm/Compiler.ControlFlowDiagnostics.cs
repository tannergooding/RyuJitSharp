// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM && DEBUG
using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgDumpWasmControlFlow()
    {
        if (!verbose)
        {
            return;
        }

        var intervals = fgWasmIntervals ??
            throw new InvalidOperationException("Wasm control-flow intervals are not available for diagnostics.");
        var activeIntervals = new ArrayStack<WasmInterval>();
        var wasmCursor = 0;

        foreach (var block in Blocks)
        {
            var cursor = unchecked((uint)block.bbPreorderNum);
            JITDUMP($"Before {FMT_BB(block.bbNum)} at {cursor} stack is:");

            if (activeIntervals.Empty())
            {
                JITDUMP("empty");
            }
            else
            {
                foreach (var interval in activeIntervals.TopDownOrder())
                {
                    JITDUMP($" [{interval.Start()},{interval.End()}]");
                }
            }

            JITDUMP("\n");

            while (!activeIntervals.Empty() && (activeIntervals.Top().End() == cursor))
            {
                var interval = activeIntervals.Top();
                JITDUMP($"END    ({interval.End()}){interval.KindString()}\n");
                _ = activeIntervals.Pop();
            }

            if (wasmCursor < intervals.Count)
            {
                var interval = intervals[wasmCursor];
                var chain = interval.Chain();

                while (chain.Start() <= cursor)
                {
                    JITDUMP($"{interval.KindString()} ({interval.End()})\n");

                    wasmCursor++;
                    activeIntervals.Push(interval);

                    if (wasmCursor >= intervals.Count)
                    {
                        break;
                    }

                    interval = intervals[wasmCursor];
                    chain = interval.Chain();
                }
            }

            JITDUMP($"  {FMT_BB(block.bbNum)}\n");

            int FindDepth(uint targetNum, bool isBackedge, out uint match)
            {
                var height = activeIntervals.Height();
                match = 0;

                for (var i = 0; i < height; i++)
                {
                    var interval = activeIntervals.Top(i);
                    match = isBackedge ? interval.Start() : interval.End();

                    if ((match == targetNum) && (isBackedge == interval.IsLoop()))
                    {
                        return i;
                    }
                }

                JITDUMP($"Could not find {targetNum}{(isBackedge ? " (backedge)" : "")} in active control stack\n");
                assert(false, "Can't find target in control stack");

                return ~0;
            }

            switch (block.Kind)
            {
                case BBJ_RETURN:
                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                case BBJ_EHFILTERRET:
                case BBJ_EHCATCHRET:
                {
                    JITDUMP("RETURN\n");
                    break;
                }

                case BBJ_THROW:
                {
                    JITDUMP("THROW\n");
                    break;
                }

                case BBJ_CALLFINALLY:
                {
                    if (!block.isBBCallFinallyPair)
                    {
                        JITDUMP("UNREACHED\n");
                    }

                    break;
                }

                case BBJ_ALWAYS:
                case BBJ_CALLFINALLYRET:
                {
                    var succNum = unchecked((uint)block.Target.bbPreorderNum);

                    if (succNum == (cursor + 1))
                    {
                        JITDUMP("FALLTHROUGH\n");
                    }
                    else
                    {
                        var isBackedge = succNum <= cursor;
                        var depth = FindDepth(succNum, isBackedge, out var blockNum);
                        JITDUMP($"BR {depth} ({blockNum}){(isBackedge ? "be" : "")}\n");
                    }

                    break;
                }

                case BBJ_COND:
                {
                    var trueNum = unchecked((uint)block.TrueTarget.bbPreorderNum);
                    var falseNum = unchecked((uint)block.FalseTarget.bbPreorderNum);

                    if (trueNum == falseNum)
                    {
                        JITDUMP("FALLTHROUGH\n");
                        break;
                    }

                    // A contiguous true target may not have induced a Block interval; invert the condition.
                    var reverseCondition = trueNum == (cursor + 1);

                    if (reverseCondition)
                    {
                        JITDUMP("FALLTHROUGH-inv\n");
                    }
                    else
                    {
                        var isBackedge = trueNum <= cursor;
                        var depth = FindDepth(trueNum, isBackedge, out var blockNum);
                        JITDUMP($"BR_IF {depth} ({blockNum}){(isBackedge ? "be" : "")}\n");
                    }

                    if (falseNum == (cursor + 1))
                    {
                        JITDUMP("FALLTHROUGH\n");
                    }
                    else
                    {
                        var isBackedge = falseNum <= cursor;
                        var depth = FindDepth(falseNum, isBackedge, out var blockNum);
                        JITDUMP($"BR{(reverseCondition ? "_IF-inv" : "")} {depth} ({blockNum}){(isBackedge ? "be" : "")}\n");
                    }

                    break;
                }

                case BBJ_SWITCH:
                {
                    var desc = block.SwitchTargets;
                    var caseCount = desc.Cases.Length;
                    assert(desc.HasDefaultCase);

                    if (caseCount == 0)
                    {
                        JITDUMP("FALLTHROUGH\n");
                        break;
                    }

                    JITDUMP("BR_TABLE");

                    for (var caseNum = 0; caseNum < caseCount; caseNum++)
                    {
                        var caseTargetNum = unchecked((uint)desc.Cases[caseNum].DestinationBlock.bbPreorderNum);
                        var isBackedge = caseTargetNum <= cursor;
                        var depth = FindDepth(caseTargetNum, isBackedge, out var blockNum);
                        JITDUMP($"{(caseNum > 0 ? "," : "")} {depth} ({blockNum}){(isBackedge ? "be" : "")}");
                    }

                    JITDUMP("\n");
                    break;
                }

                default:
                {
                    assert(false, "Unexpected block kind");
                    break;
                }
            }

            JITDUMP("\n");
        }

        while (!activeIntervals.Empty())
        {
            var interval = activeIntervals.Pop();
            JITDUMP($"END    ({interval.End()}){interval.KindString()}\n");
        }
    }
}
#endif
