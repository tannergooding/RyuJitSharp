// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

#if DUMP_FLOWGRAPHS
[NonParallelizable]
internal static unsafe class FlowGraphRegionDumpTests
{
    [Test]
    public static void EhRegionsReparentEarlierIntervalsAndSortFiltersLexically()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN,
                BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = blocks[1], ebdTryLast = blocks[2],
                    ebdFilter = blocks[3], ebdHndBeg = blocks[4], ebdHndLast = blocks[4],
                    ebdHandlerType = EH_HANDLER_FILTER
                },
                new EHblkDsc {
                    ebdTryBeg = blocks[0], ebdTryLast = blocks[5],
                    ebdHndBeg = blocks[6], ebdHndLast = blocks[7],
                    ebdHandlerType = EH_HANDLER_FINALLY
                }
            ];
            compiler.compHndBBtabCount = 2;

            var ordinals = Ordinals(blocks);
            using var writer = new StringWriter();
            compiler.fgDumpFlowGraphEH(writer, ordinals);

            Assert.That(writer.ToString(), Is.EqualTo(
                "    subgraph cluster_0 {\n" +
                "        label = \"EH#1 try\";\n" +
                "        color = red;\n" +
                "        BB01;\n" +
                "        subgraph cluster_1 {\n" +
                "            label = \"EH#0 try\";\n" +
                "            color = red;\n" +
                "            BB02;BB03;\n" +
                "        }\n" +
                "        subgraph cluster_2 {\n" +
                "            label = \"EH#0 filter\";\n" +
                "            color = red;\n" +
                "            BB04;\n" +
                "        }\n" +
                "        subgraph cluster_3 {\n" +
                "            label = \"EH#0 filter-hnd\";\n" +
                "            color = red;\n" +
                "            BB05;\n" +
                "        }\n" +
                "        BB06;\n" +
                "    }\n" +
                "    subgraph cluster_4 {\n" +
                "        label = \"EH#1 finally\";\n" +
                "        color = red;\n" +
                "        BB07;BB08;\n" +
                "    }\n\n"));
        });
    }

    [Test]
    public static void EqualEhTryRangesNestInInsertionOrder()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = blocks[0], ebdTryLast = blocks[1],
                    ebdHndBeg = blocks[2], ebdHndLast = blocks[2],
                    ebdHandlerType = EH_HANDLER_CATCH
                },
                new EHblkDsc {
                    ebdTryBeg = blocks[0], ebdTryLast = blocks[1],
                    ebdHndBeg = blocks[3], ebdHndLast = blocks[3],
                    ebdHandlerType = EH_HANDLER_FAULT_WAS_FINALLY
                }
            ];
            compiler.compHndBBtabCount = 2;

            using var writer = new StringWriter();
            compiler.fgDumpFlowGraphEH(writer, Ordinals(blocks));

            Assert.That(writer.ToString(), Is.EqualTo(
                "    subgraph cluster_0 {\n" +
                "        label = \"EH#0 try\";\n" +
                "        color = red;\n" +
                "        subgraph cluster_1 {\n" +
                "            label = \"EH#1 try\";\n" +
                "            color = red;\n" +
                "            BB01;BB02;\n" +
                "        }\n" +
                "\n    }\n" +
                "    subgraph cluster_2 {\n" +
                "        label = \"EH#0 catch\";\n" +
                "        color = red;\n" +
                "        BB03;\n" +
                "    }\n" +
                "    subgraph cluster_3 {\n" +
                "        label = \"EH#1 fault-was-finally\";\n" +
                "        color = red;\n" +
                "        BB04;\n" +
                "    }\n\n"));
        });
    }

    [Test]
    public static void EhRegionsUseLexicalOrdinalsInsteadOfBlockNumbers()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            (blocks[0].bbNum, blocks[2].bbNum) = (blocks[2].bbNum, blocks[0].bbNum);
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = blocks[0], ebdTryLast = blocks[0],
                    ebdHndBeg = blocks[2], ebdHndLast = blocks[2],
                    ebdHandlerType = EH_HANDLER_FAULT
                },
                new EHblkDsc {
                    ebdTryBeg = blocks[1], ebdTryLast = blocks[1],
                    ebdHndBeg = blocks[2], ebdHndLast = blocks[2],
                    ebdHandlerType = EH_HANDLER_CATCH
                }
            ];
            compiler.compHndBBtabCount = 2;

            using var writer = new StringWriter();
            compiler.fgDumpFlowGraphEH(writer, Ordinals(blocks));

            Assert.That(writer.ToString(), Is.EqualTo(
                "    subgraph cluster_0 {\n" +
                "        label = \"EH#0 try\";\n" +
                "        color = red;\n" +
                "        BB03;\n" +
                "    }\n" +
                "    subgraph cluster_1 {\n" +
                "        label = \"EH#1 try\";\n" +
                "        color = red;\n" +
                "        BB02;\n" +
                "    }\n" +
                "    subgraph cluster_2 {\n" +
                "        label = \"EH#0 fault\";\n" +
                "        color = red;\n" +
                "        subgraph cluster_3 {\n" +
                "            label = \"EH#1 catch\";\n" +
                "            color = red;\n" +
                "            BB01;\n" +
                "        }\n" +
                "\n    }\n\n"));
        });
    }

    [Test]
    public static void NestedLoopsUseIndependentClusterNumbersAndNativeWhitespace()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_COND, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            Jump(blocks[0], blocks[1]);
            blocks[1].SetCond(Connect(blocks[1], blocks[2]), Connect(blocks[1], blocks[5]));
            blocks[2].SetCond(Connect(blocks[2], blocks[2]), Connect(blocks[2], blocks[3]));
            blocks[3].SetCond(Connect(blocks[3], blocks[3]), Connect(blocks[3], blocks[4]));
            Jump(blocks[4], blocks[1]);
            compiler._loops = FlowGraphNaturalLoops.Find(compiler.fgComputeDfs(false));

            using var writer = new StringWriter();
            compiler.fgDumpFlowGraphLoops(writer);

            Assert.That(writer.ToString(), Is.EqualTo(
                "    subgraph cluster_0 {\n" +
                "        label = \"L00\";\n" +
                "        color = blue;\n" +
                "        BB02;\n" +
                "        subgraph cluster_1 {\n" +
                "            label = \"L01\";\n" +
                "            color = blue;\n" +
                "            BB03;\n" +
                "        }\n" +
                "        \n" +
                "        subgraph cluster_2 {\n" +
                "            label = \"L02\";\n" +
                "            color = blue;\n" +
                "            BB04;\n" +
                "        }\n" +
                "        BB05;\n" +
                "    }\n"));
        });
    }

    private static uint[] Ordinals(BasicBlock[] blocks)
    {
        var maxBlockNumber = 0;
        foreach (var block in blocks)
        {
            maxBlockNumber = Math.Max(maxBlockNumber, block.bbNum);
        }

        var ordinals = new uint[maxBlockNumber + 1];
        for (var i = 0; i < blocks.Length; i++)
        {
            ordinals[blocks[i].bbNum] = (uint)(i + 1);
        }

        return ordinals;
    }

    private static BasicBlock[] Blocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, kinds[i]);
            blocks[i].bbRefs = i == 0 ? 1 : 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
                blocks[i].Prev = blocks[i - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        return blocks;
    }

    private static FlowEdge Connect(BasicBlock source, BasicBlock target)
    {
        var edge = new FlowEdge(source, target, target.bbPreds) { Likelihood = 0.5 };
        target.bbPreds = edge;
        target.bbRefs++;
        return edge;
    }

    private static void Jump(BasicBlock source, BasicBlock target)
    {
        var edge = Connect(source, target);
        edge.Likelihood = 1;
        source.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(FlowGraphRegionDumpTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
