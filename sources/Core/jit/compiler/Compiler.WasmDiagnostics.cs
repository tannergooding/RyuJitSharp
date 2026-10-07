// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_WASM && DEBUG
    public void fgDumpWasmControlFlowDot()
    {
        if (!verbose)
        {
            return;
        }

        var intervals = fgWasmIntervals ??
            throw new InvalidOperationException("Wasm control-flow intervals are not available for diagnostics.");
        var activeIntervals = new ArrayStack<WasmInterval>();
        var wasmCursor = 0;
        JITDUMP("\ndigraph WASM {\n");

        foreach (var block in Blocks)
        {
            var cursor = block.bbPreorderNum;

            while (!activeIntervals.Empty() && (activeIntervals.Top().End() == cursor))
            {
                JITDUMP("  }\n");
                _ = activeIntervals.Pop();
            }

            if (wasmCursor < intervals.Count)
            {
                var interval = intervals[wasmCursor];
                var chain = interval.Chain();

                while (chain.Start() <= cursor)
                {
                    JITDUMP(
                        $"  subgraph cluster_{chain.Start()}_{interval.End()}{interval.KindString()} {{\n");

                    if (interval.IsLoop())
                    {
                        JITDUMP("    color=red;\n");
                    }
                    else if (interval.IsTry())
                    {
                        JITDUMP("    color=blue;\n");
                    }
                    else
                    {
                        JITDUMP("    color=black;\n");
                    }

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

            JITDUMP($"    {FMT_BB(block.bbNum)};\n");
        }

        while (!activeIntervals.Empty())
        {
            _ = activeIntervals.Pop();
            JITDUMP("  }\n");
        }

        foreach (var block in Blocks)
        {
            if (block.Kind is BBJ_CALLFINALLY)
            {
                if (block.isBBCallFinallyPair)
                {
                    var next = block.Next ??
                        throw new InvalidOperationException("A paired Wasm call-finally block has no following block.");
                    JITDUMP($"   {FMT_BB(block.bbNum)} -> {FMT_BB(next.bbNum)} [style=dotted];\n");
                }
            }
            else
            {
                foreach (var succ in block.Succs)
                {
                    JITDUMP($"   {FMT_BB(block.bbNum)} -> {FMT_BB(succ.bbNum)};\n");
                }
            }
        }

        JITDUMP("}\n");
    }
#endif
}
