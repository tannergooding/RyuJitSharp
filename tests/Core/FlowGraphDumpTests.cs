// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Compiler;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Phases;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowGraphDumpTests
{
    [TestCase(false, "a<>&\"', :;|*", "a&lt;&gt;&amp;&quot;', :;|*")]
    [TestCase(true, "a<>&\"', :;|*", "a~lt~~gt~~amp~~quot~'___~semi~~bar~~star~")]
    public static void EscapeMappingsAreNative(bool file, string value, string expected)
    {
        Assert.That(fgProcessEscapes(value, file), Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EscapeFreeStringsAreReused(bool file)
    {
        var value = new string('a', 13);
        Assert.That(fgProcessEscapes(value, file), Is.SameAs(value));
    }

    [TestCase(0.0, "\"  0.000\"")]
    [TestCase(-0.0, "\" -0.000\"")]
    [TestCase(0.01, "\"  0.010\"")]
    [TestCase(0.001, "\"0.00100\"")]
    [TestCase(0.0001, "\"0.00010\"")]
    [TestCase(0.00001, "\"1.000000E-05\"")]
    [TestCase(1e-100, "\"1.000000E-100\"")]
    [TestCase(double.PositiveInfinity, "\"    inf\"")]
    public static void NativeQuotedWeights(double value, string expected)
    {
        using var writer = new StringWriter(CultureInfo.GetCultureInfo("fr-FR"));
        fgWriteGraphDouble(writer, value);
        Assert.That(writer.ToString(), Is.EqualTo(expected));
    }

    [TestCase(PhasePosition.PrePhase, null, null, false)]
    [TestCase(PhasePosition.PrePhase, "", "*", false)]
    [TestCase(PhasePosition.PrePhase, "*suffix", null, true)]
    [TestCase(PhasePosition.PrePhase, "xIMPORTATIONy", null, true)]
    [TestCase(PhasePosition.PrePhase, "importation", null, false)]
    [TestCase(PhasePosition.PostPhase, null, null, false)]
    [TestCase(PhasePosition.PostPhase, "*", null, false)]
    [TestCase(PhasePosition.PostPhase, null, "*suffix", true)]
    [TestCase(PhasePosition.PostPhase, null, "xIMPORTATIONy", true)]
    [TestCase(PhasePosition.PostPhase, null, "", false)]
    public static void PhaseSelectionUsesEnumSuffixes(PhasePosition position, string? pre, string? post, bool expected)
    {
        Assert.That(fgFlowGraphPhaseSelected(PHASE_IMPORTATION, position, pre, post), Is.EqualTo(expected));
        Assert.That(fgFlowGraphPhaseSelected(PHASE_DETERMINE_FIRST_COLD_BLOCK, PhasePosition.PostPhase, null, null), Is.True);
    }

    [TestCase(GT_EQ, "==")]
    [TestCase(GT_NE, "!=")]
    [TestCase(GT_LT, "<")]
    [TestCase(GT_LE, "<=")]
    [TestCase(GT_GE, ">=")]
    [TestCase(GT_GT, ">")]
    public static void CompactComparisons(genTreeOps op, string name)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            using var writer = new StringWriter();
            var tree = new GenTreeOp(op, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, -7));
            fgDumpTree(writer, tree);
            Assert.That(writer.ToString(), Is.EqualTo($"V00 {name} -7"));
        });
    }

    [TestCase("array", "V00.Length")]
    [TestCase("md-length", "V00.GetLength(2)")]
    [TestCase("md-bound", "V00.GetLowerBound(2)")]
    [TestCase("double", "1.23457e+20")]
    [TestCase("zero", "-0")]
    [TestCase("fallback", "[ADD]")]
    public static void CompactOperands(string kind, string expected)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            GenTree tree = kind switch {
                "array" => new GenTreeArrLen(TYP_INT, array, 8),
                "md-length" => new GenTreeMDArr(GT_MDARR_LENGTH, array, 2, 3),
                "md-bound" => new GenTreeMDArr(GT_MDARR_LOWER_BOUND, array, 2, 3),
                "double" => new GenTreeDblCon(TYP_DOUBLE, 1.23456789e20),
                "zero" => new GenTreeDblCon(TYP_DOUBLE, -0.0),
                _ => new GenTreeOp(GT_ADD, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2)),
            };
            using var writer = new StringWriter();
            fgDumpTree(writer, tree);
            Assert.That(writer.ToString(), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DotEdgesUseLexicalOrdinalsWithoutRenumbering(bool predecessors)
    {
        WithGraph(compiler => {
            var entry = compiler.fgFirstBB;
            assert(entry is not null);
            var body = entry.Next;
            var exit = compiler.fgLastBB;
            assert(body is not null);
            assert(exit is not null);
            entry.bbNum = 9;
            body.bbNum = 2;
            exit.bbNum = 7;
            compiler.fgBBNumMax = 9;
            compiler.fgPredsComputed = predecessors;
            Constrained(ref JitConfig) = 1;
            using var writer = new StringWriter();
            compiler.fgWriteFlowGraph(writer, PHASE_IMPORTATION, PhasePosition.PostPhase, true);
            var text = writer.ToString();
            Assert.That(text, Does.StartWith("digraph FlowGraph {\n    graph [label = \"M\\nafter\\nImportation\"];\n"));
            Assert.That(text, Does.Contain("    BB09 [label = \"BB09\\n\", shape = \"house\"];\n"));
            Assert.That(text, Does.Contain("    BB07 [label = \"BB07\", shape = \"invhouse\"];\n"));
            Assert.That(text, Does.Contain("    BB09 -> BB02 [style=\"invis\", weight=25];\n"));
            Assert.That(text, Does.Contain(predecessors
                ? "    BB09 -> BB02 [color=blue, weight=20, label=\"   0.50\"];\n"
                : "    BB09 -> BB02 [color=blue]\n"));
            Assert.That(text, Does.Contain(predecessors
                ? "    BB02 -> BB09 [color=green, label=\"   1.00\"];\n"
                : "    BB02 -> BB09 [color=green]\n"));
            Assert.That(text, Does.EndWith("}\n"));
            Assert.That(new[] { entry.bbNum, body.bbNum, exit.bbNum }, Is.EqualTo((int[])[9, 2, 7]));
        });
    }

    [Test]
    public static void XmlPreservesNativeLabelsEscapingWeightsAndEdges()
    {
        WithGraph(compiler => {
            compiler.info.compFullName = "C<T>:M<&>()";
            compiler.info.compClassName = "C<T>";
            compiler.info.compMethodName = "M<&>";
            compiler.fgCalledCount = 100;
            compiler.fgFirstColdBlock = compiler.fgLastBB;
            assert(compiler.fgFirstBB is not null);
            assert(compiler.fgLastBB is not null);
            compiler.fgFirstBB.bbCodeOffs = -1;
            compiler.fgLastBB.SetFlags(BBF_HAS_NEWARR | BBF_HAS_NEWOBJ);
            using var writer = new StringWriter();
            compiler.fgWriteFlowGraph(writer, PHASE_IMPORTATION, PhasePosition.PostPhase, false);
            var text = writer.ToString();
            Assert.That(text, Does.Contain("name=\"C&lt;T&gt;:M&lt;&amp;&gt;()\""));
            var xml = XElement.Parse(text);
            Assert.That((string?)xml.Attribute("name"), Is.EqualTo("C<T>:M<&>()"));
            Assert.That((string?)xml.Attribute("firstColdBlock"), Is.EqualTo("3"));
            var blocks = xml.Elements("blocks").Single().Elements("block").ToArray();
            Assert.That(blocks.Select(block => (string?)block.Attribute("jumpKind")), Is.EqualTo((string[])["SWITCH", "ALWAYS", "NONE"]));
            Assert.That((string?)blocks[0].Attribute("startOffset"), Is.EqualTo("-1"));
            Assert.That((string?)blocks[2].Attribute("callsNewArr"), Is.EqualTo("true"));
            Assert.That((string?)blocks[2].Attribute("callsNew"), Is.EqualTo("true"));
            Assert.That((string?)blocks[0].Attribute("weight"), Is.EqualTo("  1.000"));
            var edges = xml.Elements("edges").Single().Elements("edge").ToArray();
            Assert.That(edges.Select(edge => (string?)edge.Attribute("id")), Is.EqualTo((string[])["1", "2", "3"]));
            Assert.That((string?)edges[1].Attribute("out"), Is.EqualTo("  0.500"));
            Assert.That((string?)edges[1].Attribute("in"), Is.EqualTo("  0.500"));
            Assert.That(edges[0].Attribute("out"), Is.Null);
        });
    }

    [Test]
    public static void UndefinedNativeXmlSwitchEntryFailsExplicitly()
    {
        WithGraph(compiler => {
            assert(compiler.fgFirstBB is not null);
            compiler.fgFirstBB.Kind = BBJ_SWITCH;
            using var writer = new StringWriter();
            Assert.That(() => compiler.fgWriteFlowGraph(writer, PHASE_IMPORTATION, PhasePosition.PostPhase, false),
                Throws.TypeOf<FatalJitException>().With.Message.Contains("B350"));
        });
    }

    [TestCase(BBJ_RETURN, "invhouse")]
    [TestCase(BBJ_THROW, "trapezium")]
    [TestCase(BBJ_ALWAYS, "note")]
    public static void DotBlockShapesAndSelectedFlags(BBKinds kind, string shape)
    {
        WithGraph(compiler => {
            var block = compiler.fgLastBB;
            assert(block is not null);
            assert(compiler.fgFirstBB is not null);
            block.SetKindAndTargetEdge(kind, kind == BBJ_ALWAYS ? new FlowEdge(block, compiler.fgFirstBB, null) : null);
            block.SetFlags(BBF_INTERNAL | BBF_LOOP_ALIGN);
            BlockFlags(ref JitConfig) = 1;
            BlockId(ref JitConfig) = 1;
            using var writer = new StringWriter();
            compiler.fgWriteFlowGraph(writer, PHASE_IMPORTATION, PhasePosition.PrePhase, true);
            Assert.That(writer.ToString(), Does.Contain(
                $"    BB03 [label = \"{block.dspToString()} [A]\", shape = \"{shape}\"];\n"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PerMethodFilenamesCropUtf8BytesAndReserveCollisionSuffix(bool multibyte)
    {
        WithGraph(compiler => {
            compiler.info.compFullName = new string(multibyte ? '\u00e9' : 'a', 300);
            var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "flowgraph-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(directory);
            try
            {
                WithFileConfig(compiler, "all", directory, () => {
                    using var writer = compiler.fgOpenFlowGraphFile(out var borrowed, PHASE_DETERMINE_FIRST_COLD_BLOCK, PhasePosition.PostPhase, "dot");
                    Assert.That(writer, Is.Not.Null);
                    Assert.That(borrowed, Is.False);
                    const string suffix = "-post-DETERMINE_FIRST_COLD_BLOCK-FullOpts";
                    var byteLimit = 240 - suffix.Length - "~999.dot".Length - 1;
                    var prefix = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(compiler.info.compFullName).AsSpan(0, byteLimit));
                    Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName),
                        Is.EqualTo((string[])[prefix + suffix + ".dot"]));
                });
            }
            finally
            {
                foreach (var path in Directory.GetFiles(directory))
                {
                    File.Delete(path);
                }
                Directory.Delete(directory);
            }
        });
    }

    [Test]
    public static void MemorySsaIncludesPhiArgumentsHavocAndOutgoingValue()
    {
        WithGraph(compiler => {
            MemorySsa(ref JitConfig) = 1;
            compiler.fgSsaPassesCompleted = 1;
            var first = compiler.AllocMemorySsaNum();
            var second = compiler.AllocMemorySsaNum();
            compiler.GetMemoryPerSsaData(first)._vnPair.SetBoth(0x12);
            compiler.GetMemoryPerSsaData(second)._vnPair.SetBoth(0x34);
            var block = compiler.fgFirstBB;
            assert(block is not null);
            block.bbMemorySsaNumIn[(int)MemoryKind.GcHeap] = first;
            block.bbMemorySsaNumOut[(int)MemoryKind.GcHeap] = second;
            block.bbMemorySsaPhiFunc[(int)MemoryKind.GcHeap] =
                new BasicBlock.MemoryPhiArg(first, new BasicBlock.MemoryPhiArg(second));
            block.bbMemoryHavoc = 1;
            using var writer = new StringWriter();
            compiler.fgWriteFlowGraph(writer, PHASE_IMPORTATION, PhasePosition.PostPhase, true);
            Assert.That(writer.ToString(), Does.Contain($"MI {first} $12 = PHI({first} $12,{second} $34)\\n** HAVOC **\\nMO {second} $34"));
        });
    }

    [TestCase("stdout")]
    [TestCase("all")]
    [TestCase("append")]
    public static void SelectedOutputRespectsOwnershipAppendAndExclusiveCreation(string file)
    {
        WithGraph(compiler => {
            using var memory = new MemoryStream();
            using var borrowed = new StreamWriter(memory, new UTF8Encoding(false), leaveOpen: true);
            var previous = s_jitstdout;
            s_jitstdout = borrowed;
            var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "flowgraph-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(directory);
            try
            {
                WithFileConfig(compiler, file, directory, () => {
                    Dot(ref JitConfig) = 1;
                    Assert.That(compiler.fgDumpFlowGraph(PHASE_DETERMINE_FIRST_COLD_BLOCK, PhasePosition.PostPhase), Is.True);
                    Assert.That(compiler.fgDumpFlowGraph(PHASE_DETERMINE_FIRST_COLD_BLOCK, PhasePosition.PostPhase), Is.True);
                    if (file == "stdout")
                    {
                        borrowed.Write("still open");
                        borrowed.Flush();
                        var text = Encoding.UTF8.GetString(memory.ToArray());
                        Assert.That(text, Does.EndWith("}\n\nstill open"));
                        Assert.That(Directory.GetFiles(directory), Is.Empty);
                    }
                    else
                    {
                        var paths = Directory.GetFiles(directory);
                        Assert.That(paths.Length, Is.EqualTo(file == "all" ? 2 : 1));
                        if (file == "all")
                        {
                            Assert.That(paths.Select(Path.GetFileName), Does.Contain(
                                "C_M-post-DETERMINE_FIRST_COLD_BLOCK-FullOpts~2.dot"));
                        }
                        var text = string.Concat(paths.Select(File.ReadAllText));
                        Assert.That(text.Split("digraph FlowGraph").Length - 1, Is.EqualTo(2));
                        foreach (var path in paths)
                        {
                            using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        }
                    }
                });
            }
            finally
            {
                s_jitstdout = previous;
                foreach (var path in Directory.GetFiles(directory))
                {
                    File.Delete(path);
                }
                Directory.Delete(directory);
            }
        });
    }

    [TestCase("hash")]
    [TestCase("single-block")]
    [TestCase("tier0")]
    [TestCase("profiled")]
    [TestCase("hot")]
    [TestCase("cold")]
    [TestCase("jit")]
    public static void FileSelectionGatesDoNotCreateFiles(string gate)
    {
        WithGraph(compiler =>
            WithFileConfig(compiler, gate is "hash" or "single-block" or "tier0" ? "stdout" : gate, null, () => {
                if (gate == "hash")
                {
                    Hash(ref JitConfig) ^= 1;
                }
                else if (gate == "single-block")
                {
                    compiler.fgBBcount = 1;
                }
                else if (gate == "tier0")
                {
                    compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_TIER0);
                }
                using var writer = compiler.fgOpenFlowGraphFile(out var borrowed, PHASE_DETERMINE_FIRST_COLD_BLOCK,
                    PhasePosition.PostPhase, "dot");
                Assert.That(writer, Is.Null);
                Assert.That(borrowed, Is.False);
            }));
    }

    private static void WithGraph(Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            Methods(ref JitConfig) = new JitConfigValues.MethodSet(null, null);
            compiler.info.compFullName = "C:M";
            compiler.info.compClassName = "C";
            compiler.info.compMethodName = "M";
            compiler.fgCalledCount = 100;
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var body = BasicBlock.New(compiler, BBJ_RETURN);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = body;
            body.Next = exit;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = exit;
            foreach (var block in new[] { entry, body, exit })
            {
                block.bbWeight = 100;
            }
            var toBody = new FlowEdge(entry, body, null);
            var toExit = new FlowEdge(entry, exit, null);
            var back = new FlowEdge(body, entry, null);
            entry.SetCond(toBody, toExit);
            toBody.Likelihood = 0.5;
            toExit.Likelihood = 0.5;
            body.SetKindAndTargetEdge(BBJ_ALWAYS, back);
            back.Likelihood = 1;
            entry.bbPreds = back;
            body.bbPreds = toBody;
            exit.bbPreds = toExit;
            compiler.fgPredsComputed = true;
            action(compiler);
        });
    }

    private static void WithFileConfig(Compiler compiler, string file, string? directory, Action action)
    {
        var filePointer = Marshal.StringToCoTaskMemUTF8(file);
        var directoryPointer = Marshal.StringToCoTaskMemUTF8(directory);
        var previous = JitConfig;
        try
        {
            Hash(ref JitConfig) = compiler.info.compMethodHash();
            FileName(ref JitConfig) = (byte*)filePointer;
            DirectoryName(ref JitConfig) = (byte*)directoryPointer;
            action();
        }
        finally
        {
            JitConfig = previous;
            Marshal.FreeCoTaskMem(filePointer);
            Marshal.FreeCoTaskMem(directoryPointer);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgHash")]
    private static extern ref int Hash(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFg")]
    private static extern ref JitConfigValues.MethodSet Methods(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgDot")]
    private static extern ref int Dot(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgConstrained")]
    private static extern ref int Constrained(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgMemorySsa")]
    private static extern ref int MemorySsa(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgBlockFlags")]
    private static extern ref int BlockFlags(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgBlockID")]
    private static extern ref int BlockId(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgFile")]
    private static extern ref byte* FileName(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDumpFgDir")]
    private static extern ref byte* DirectoryName(ref JitConfigValues values);
}
#endif
