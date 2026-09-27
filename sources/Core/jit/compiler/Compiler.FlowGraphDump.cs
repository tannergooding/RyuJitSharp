// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DUMP_FLOWGRAPHS
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    private static readonly Lazy<StreamWriter> s_flowGraphStderr = new(
        () => new JitTextWriter(Console.OpenStandardError(), leaveOpen: true) { AutoFlush = true });

    // Keep the pinned native labels, including the stale entries (B350).
    private static readonly string[] s_flowGraphKinds = ["EHFINALLYRET", "EHFILTERRET", "EHCATCHRET", "THROW", "RETURN", "NONE",
        "ALWAYS", "LEAVE", "CALLFINALLY", "COND", "SWITCH"];

    internal static string fgProcessEscapes(string value, bool fileName)
    {
        StringBuilder? builder = null;
        for (var i = 0; i < value.Length; i++)
        {
            var replacement = fileName
                ? value[i] switch {
                    ' ' or ':' or ',' => "_",
                    '<' => "~lt~",
                    '>' => "~gt~",
                    ';' => "~semi~",
                    '|' => "~bar~",
                    '&' => "~amp~",
                    '"' => "~quot~",
                    '*' => "~star~",
                    _ => null,
                }
                : value[i] switch {
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '&' => "&amp;",
                    '"' => "&quot;",
                    _ => null,
                };

            if (replacement is not null)
            {
                builder ??= new StringBuilder(value.Length).Append(value.AsSpan(0, i));
                _ = builder.Append(replacement);
            }
            else
            {
                _ = builder?.Append(value[i]);
            }
        }

        return builder?.ToString() ?? value;
    }

    internal static void fgDumpTree(TextWriter writer, GenTree tree)
    {
        if (tree.Oper.IsCompare)
        {
            var op = tree.Oper switch {
                GT_EQ => "==",
                GT_NE => "!=",
                GT_LT => "<",
                GT_LE => "<=",
                GT_GE => ">=",
                GT_GT => ">",
                _ => tree.Oper.Name,
            };
            fgDumpTree(writer, tree.AsOp().Op1);
            writer.Write($" {op} ");
            fgDumpTree(writer, tree.AsOp().Op2);
        }
        else if (tree.Oper.IsCnsIntOrI)
        {
            writer.Write(tree.AsIntCon().IconValue.ToString(CultureInfo.InvariantCulture));
        }
        else if (tree.Oper.IsCnsFltOrDbl)
        {
            writer.Write(formatFloat(tree.AsDblCon().DconVal, "g6"));
        }
        else if (tree.Oper.IsLocal)
        {
            writer.Write($"V{tree.AsLclVarCommon().LclNum:D2}");
        }
        else if (tree.Oper == GT_ARR_LENGTH)
        {
            fgDumpTree(writer, tree.AsArrLen().ArrRef);
            writer.Write(".Length");
        }
        else if (tree.Oper is GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND)
        {
            var array = tree.AsMDArr();
            fgDumpTree(writer, array.ArrRef);
            writer.Write(tree.Oper == GT_MDARR_LENGTH ? ".GetLength(" : ".GetLowerBound(");
            writer.Write(array.Dim.ToString(CultureInfo.InvariantCulture));
            writer.Write(')');
        }
        else
        {
            writer.Write($"[{tree.Oper.Name}]");
        }
    }

    internal static void fgWriteGraphDouble(TextWriter writer, double value)
    {
        assert(value >= 0);
        string text;
        if ((value >= 0.010) || (value == 0))
        {
            text = formatFloat(value, "F3");
        }
        else if (value >= 0.00010)
        {
            text = formatFloat(value, "F5");
        }
        else
        {
            text = formatFloat(value, "E6");
            // The CRT uses at least two exponent digits; .NET's E format uses three.
            var exponent = text.IndexOf('E', StringComparison.Ordinal);
            if (text[exponent + 2] == '0')
            {
                text = text.Remove(exponent + 2, 1);
            }
        }

        writer.Write('"');
        writer.Write(text.PadLeft(7));
        writer.Write('"');
    }

    internal static bool fgFlowGraphPhaseSelected(Phases phase, PhasePosition pos, string? pre, string? post)
    {
        var phaseName = phase.ToString().AsSpan("PHASE_".Length);
        if (pos == PhasePosition.PrePhase)
        {
            return (pre is not null) && (pre.StartsWith('*', StringComparison.Ordinal) || pre.AsSpan().Contains(phaseName, StringComparison.Ordinal));
        }

        assert(pos == PhasePosition.PostPhase);
        if (post is null)
        {
            return (pre is null) && (phase == PHASE_DETERMINE_FIRST_COLD_BLOCK);
        }

        return post.StartsWith('*', StringComparison.Ordinal) || post.AsSpan().Contains(phaseName, StringComparison.Ordinal);
    }

    internal unsafe StreamWriter? fgOpenFlowGraphFile(out bool dontClose, Phases phase, PhasePosition pos, string type)
    {
        dontClose = false;
        if (fgBBcount <= 1)
        {
            return null;
        }

        string? filename = null;
        string? pathname = null;
        string? pre = null;
        var post = "*";
#if DEBUG
        var dump = JitConfig.JitDumpFg.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args);
        dump |= unchecked((uint)JitConfig.JitDumpFgHash) == unchecked((uint)info.compMethodHash());
        if (opts.IsTier0)
        {
            dump &= JitConfig.JitDumpFgTier0 > 0;
        }

        if (!dump)
        {
            return null;
        }

        filename = Marshal.PtrToStringUTF8((nint)JitConfig.JitDumpFgFile);
        pathname = Marshal.PtrToStringUTF8((nint)JitConfig.JitDumpFgDir);
        pre = Marshal.PtrToStringUTF8((nint)JitConfig.JitDumpFgPrePhase);
        post = Marshal.PtrToStringUTF8((nint)JitConfig.JitDumpFgPhase);
#endif

        if (!fgFlowGraphPhaseSelected(phase, pos, pre, post))
        {
            return null;
        }

        return fgOpenFlowGraphOutput(out dontClose, phase, pos, type, filename, pathname);
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "Returns ownership to the caller except for borrowed standard streams.")]
    private unsafe StreamWriter? fgOpenFlowGraphOutput(
        out bool dontClose, Phases phase, PhasePosition pos, string type, string? filename, string? pathname)
    {
        assert(fgFirstBB is not null);
        dontClose = false;
        filename ??= "default";
        var perMethod = false;
        switch (filename)
        {
            case "profiled":
            {
                if (!fgFirstBB.hasProfileWeight)
                {
                    return null;
                }
                perMethod = true;
                break;
            }

            case "hot":
            case "cold":
            case "jit":
            {
                var region = filename switch {
                    "hot" => CORINFO_REGION_HOT,
                    "cold" => CORINFO_REGION_COLD,
                    _ => CORINFO_REGION_JIT,
                };
                if (info.compMethodInfo->regionKind != region)
                {
                    return null;
                }
                perMethod = true;
                break;
            }

            case "all":
            {
                perMethod = true;
                break;
            }

            case "stdout":
            {
                dontClose = true;
                return jitstdout();
            }

            case "stderr":
            {
                dontClose = true;
                return s_flowGraphStderr.Value;
            }
        }

        Exception? error;
        if (perMethod)
        {
            var phaseName = phase.ToString()["PHASE_".Length..];
            var position = pos == PhasePosition.PrePhase ? "pre" : "post";
            var suffix = $"-{position}-{phaseName}-{compGetTieringName(true)}";
            var escaped = Encoding.UTF8.GetBytes(fgProcessEscapes(info.compFullName, fileName: true));
            // Native reserves 20 bytes of MAX_PATH_FNAME, "~999", extension, and NUL.
            var limit = 260 - 20 - Encoding.UTF8.GetByteCount(suffix) - 4 - 1 - Encoding.UTF8.GetByteCount(type) - 1;
            if (escaped.Length > limit)
            {
                escaped = escaped.AsSpan(0, limit).ToArray();
            }
            var stem = Encoding.UTF8.GetString(escaped) + suffix;
            error = null;
            for (var number = 1; number < 1000; number++)
            {
                var name = stem + (number == 1 ? "" : $"~{number}") + "." + type;
                filename = pathname is null ? name : pathname + "\\" + name;
                var file = fgTryOpenGraphFile(filename, FileMode.CreateNew, out error);
                if (file is not null)
                {
                    return file;
                }
            }
        }
        else
        {
            filename = (pathname is null ? "" : pathname + "\\") + filename + "." + type;
            var file = fgTryOpenGraphFile(filename, FileMode.Append, out error);
            if (file is not null)
            {
                return file;
            }
        }

        JITDUMP($"Unable to open flow graph file '{filename}': {error?.Message}\n");
        return null;
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "JitTextWriter takes stream ownership; failed transfers are disposed in finally.")]
    private static JitTextWriter? fgTryOpenGraphFile(string filename, FileMode mode, out Exception? error)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(filename, mode, FileAccess.Write, FileShare.ReadWrite);
            var writer = new JitTextWriter(stream, leaveOpen: false);
            stream = null;
            error = null;
            return writer;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = exception;
            return null;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The dontClose result distinguishes borrowed standard streams from owned files.")]
    public bool fgDumpFlowGraph(Phases phase, PhasePosition pos)
    {
#if DEBUG
        var dot = JitConfig.JitDumpFgDot != 0;
#else
        const bool dot = true;
#endif
        var writer = fgOpenFlowGraphFile(out var dontClose, phase, pos, dot ? "dot" : "fgx");
        if (writer is null)
        {
            return false;
        }

        try
        {
            JITDUMP($"Writing out flow graph {(pos == PhasePosition.PrePhase ? "before" : "after")} phase {phase.Name}\n");
            fgWriteFlowGraph(writer, phase, pos, dot);
            if (dontClose)
            {
                writer.Write('\n');
                writer.Flush();
            }
        }
        finally
        {
            if (!dontClose)
            {
                writer.Dispose();
            }
        }

        return true;
    }

    internal unsafe void fgWriteFlowGraph(TextWriter writer, Phases phase, PhasePosition pos, bool dot)
    {
#if DEBUG
        var includeEH = (JitConfig.JitDumpFgEH != 0) && !compIsForInlining;
        var includeLoops = (JitConfig.JitDumpFgLoops != 0) && !compIsForInlining;
        var constrained = JitConfig.JitDumpFgConstrained != 0;
        var useBlockId = JitConfig.JitDumpFgBlockID != 0;
        var displayFlags = JitConfig.JitDumpFgBlockFlags != 0;
        var displayMemorySsa = JitConfig.JitDumpFgMemorySsa != 0;
        var synthesizeCounts = JitConfig.JitSynthesizeCounts > 0;
#else
        const bool includeEH = false;
        const bool includeLoops = false;
        const bool constrained = true;
        const bool displayFlags = false;
        const bool displayMemorySsa = false;
        const bool synthesizeCounts = false;
#endif
        assert(fgFirstBB is not null);
        var divisor = BasicBlock.getCalledCount(this);
        if (dot)
        {
            writer.Write("digraph FlowGraph {\n");
            writer.Write($"    graph [label = \"{info.compMethodName}{(compIsForInlining ? "\\n(inlinee)" : "")}" +
                $"\\n{(pos == PhasePosition.PrePhase ? "before" : "after")}\\n{phase.Name}\"];\n");
            writer.Write("    node [shape = \"Box\"];\n");
        }
        else
        {
            var region = info.compMethodInfo->regionKind switch {
                CORINFO_REGION_HOT => "HOT",
                CORINFO_REGION_COLD => "COLD",
                CORINFO_REGION_JIT => "JIT",
                _ => "NONE",
            };
            writer.Write("<method");
            writer.Write($"\n    name=\"{fgProcessEscapes(info.compFullName, false)}\"");
            writer.Write($"\n    className=\"{fgProcessEscapes(info.compClassName, false)}\"");
            writer.Write($"\n    methodName=\"{fgProcessEscapes(info.compMethodName, false)}\"");
            writer.Write($"\n    ngenRegion=\"{region}\"");
            writer.Write($"\n    bytesOfIL=\"{unchecked((int)info.compILCodeSize)}\"");
            writer.Write($"\n    localVarCount=\"{lvaCount}\"");
            if (fgHaveProfileWeights)
            {
                writer.Write($"\n    calledCount=\"{formatFloat(fgCalledCount, "F6")}\"");
                writer.Write("\n    profileData=\"true\"");
            }
            if (compHndBBtabCount > 0)
            {
                writer.Write("\n    hasEHRegions=\"true\"");
            }
            if (fgHasLoops)
            {
                writer.Write("\n    hasLoops=\"true\"");
            }
            if (fgFirstColdBlock is not null)
            {
                writer.Write($"\n    firstColdBlock=\"{fgFirstColdBlock.bbNum}\"");
            }
            writer.Write(">\n    <blocks");
            writer.Write($"\n        blockCount=\"{fgBBcount}\">");
        }

        var ordinals = new uint[fgBBNumMax + 1];
        uint ordinal = 1;
        foreach (var block in Blocks)
        {
            ordinals[block.bbNum] = ordinal++;
        }

        foreach (var block in Blocks)
        {
            if (dot)
            {
                writer.Write($"    BB{block.bbNum:D2} [label = \"");
#if DEBUG
                writer.Write(useBlockId ? block.dspToString() : $"BB{block.bbNum:D2}");
#else
                writer.Write($"BB{block.bbNum:D2}");
#endif
                if (displayFlags)
                {
                    var isTry = bbIsTryBeg(block);
                    var isFunclet = fgFuncletsCreated && bbIsFuncletBeg(block);
                    if (isTry || isFunclet || block.HasFlag(BBF_LOOP_ALIGN))
                    {
                        writer.Write(" [");
                        if (isTry)
                        {
                            writer.Write('T');
                        }
                        if (isFunclet)
                        {
                            writer.Write('F');
                        }
                        if (block.HasFlag(BBF_LOOP_ALIGN))
                        {
                            writer.Write('A');
                        }
                        writer.Write(']');
                    }
                }

                if (displayMemorySsa && (fgSsaPassesCompleted > 0))
                {
                    fgWriteFlowGraphMemorySsa(writer, block);
                }
                if (block.Kind == BBJ_COND)
                {
                    writer.Write("\\n");
                    if (block.LastStmt is Statement statement)
                    {
                        var tree = statement.RootNode;
                        noway_assert(tree.Oper == GT_JTRUE);
                        fgDumpTree(writer, tree.AsUnOp().Op1);
                    }
                }
                if (block.hasProfileWeight || synthesizeCounts)
                {
                    writer.Write("\\n\\n" + formatFloat(block.getBBWeight(this) / BB_UNITY_WEIGHT, "F2").PadLeft(7));
                }

                writer.Write('"');
                if (block == fgFirstBB)
                {
                    writer.Write(", shape = \"house\"");
                }
                else if (block.Kind == BBJ_RETURN)
                {
                    writer.Write(", shape = \"invhouse\"");
                }
                else if (block.Kind == BBJ_THROW)
                {
                    writer.Write(", shape = \"trapezium\"");
                }
                else if (block.HasFlag(BBF_INTERNAL))
                {
                    writer.Write(", shape = \"note\"");
                }
                writer.Write("];\n");
            }
            else
            {
                if ((uint)block.Kind >= (uint)s_flowGraphKinds.Length)
                {
                    throw new FatalJitException(CORJIT_INTERNALERROR,
                        "Native XML flow-graph jump-kind table has no defined entry for this block kind (B350).");
                }

                writer.Write($"\n        <block\n            id=\"{block.bbNum}\"");
                writer.Write($"\n            ordinal=\"{ordinals[block.bbNum]}\"");
                writer.Write($"\n            jumpKind=\"{s_flowGraphKinds[(int)block.Kind]}\"");
                if (block.hasTryIndex)
                {
                    writer.Write("\n            inTry=\"true\"");
                }
                if (block.hasHndIndex)
                {
                    writer.Write("\n            inHandler=\"true\"");
                }
                if (fgFirstBB.hasProfileWeight && !block.HasFlag(BBF_COLD))
                {
                    writer.Write("\n            hot=\"true\"");
                }
                if (block.HasFlag(BBF_HAS_NEWARR))
                {
                    writer.Write("\n            callsNewArr=\"true\"");
                }
                if (block.HasFlag(BBF_HAS_NEWOBJ))
                {
                    writer.Write("\n            callsNew=\"true\"");
                }

                var root = "n/a";
                if ((block.IsLIR || (block.LastStmt is not null)) && (block.GetLastNode() is GenTree last))
                {
                    root = last.Oper.Name;
                }
                writer.Write("\n            weight=");
                fgWriteGraphDouble(writer, block.bbWeight / divisor);
                writer.Write($"\n            startOffset=\"{unchecked((int)block.bbCodeOffs)}\"");
                writer.Write($"\n            rootTreeOp=\"{root}\"");
                writer.Write($"\n            endOffset=\"{unchecked((int)block.bbCodeOffsEnd)}\">");
                writer.Write("\n        </block>");
            }
        }

        if (!dot)
        {
            writer.Write("\n    </blocks>\n    <edges>");
        }

        if (fgPredsComputed)
        {
            var edgeNumber = 1;
            foreach (var target in Blocks)
            {
                var targetDivisor = target.bbWeight == BB_ZERO_WEIGHT ? 1 : target.bbWeight;
                foreach (var edge in target.PredEdges)
                {
                    var source = edge.SourceBlock;
                    var sourceDivisor = source.bbWeight == BB_ZERO_WEIGHT ? 1 : source.bbWeight;
                    var weight = edge.LikelyWeight;
                    if (dot)
                    {
                        writer.Write($"    BB{source.bbNum:D2} -> BB{target.bbNum:D2} [");
                        if (ordinals[source.bbNum] > ordinals[target.bbNum])
                        {
                            writer.Write("color=green, ");
                        }
                        else if ((ordinals[source.bbNum] + 1) == ordinals[target.bbNum])
                        {
                            writer.Write("color=blue, weight=20, ");
                        }
                        writer.Write($"label=\"{formatFloat(weight / divisor, "F2"),7}\"];\n");
                    }
                    else
                    {
                        writer.Write($"\n        <edge\n            id=\"{edgeNumber}\"");
                        writer.Write($"\n            source=\"{source.bbNum}\"\n            target=\"{target.bbNum}\"");
                        if (source.Kind == BBJ_SWITCH)
                        {
                            if (edge.DupCount >= 2)
                            {
                                writer.Write($"\n            switchCases=\"{edge.DupCount}\"");
                            }
                            if (source.SwitchTargets.DefaultCase.DestinationBlock == target)
                            {
                                writer.Write("\n            switchDefault=\"true\"");
                            }
                        }
                        writer.Write("\n            weight=");
                        fgWriteGraphDouble(writer, weight / divisor);
                        if (weight > 0)
                        {
                            if (weight < source.bbWeight)
                            {
                                writer.Write("\n            out=");
                                fgWriteGraphDouble(writer, weight / sourceDivisor);
                            }
                            if (weight < target.bbWeight)
                            {
                                writer.Write("\n            in=");
                                fgWriteGraphDouble(writer, weight / targetDivisor);
                            }
                        }
                        writer.Write(">\n        </edge>");
                    }
                    edgeNumber++;
                }
            }
        }

        if (dot)
        {
            foreach (var source in Blocks)
            {
                if (constrained && !source.IsLast)
                {
                    writer.Write($"    BB{source.bbNum:D2} -> BB{source.Next.bbNum:D2} [style=\"invis\", weight=25];\n");
                }
                if (fgPredsComputed)
                {
                    continue;
                }
                foreach (var target in source.Succs)
                {
                    writer.Write($"    BB{source.bbNum:D2} -> BB{target.bbNum:D2}");
                    if (ordinals[source.bbNum] > ordinals[target.bbNum])
                    {
                        writer.Write(" [color=green]\n");
                    }
                    else if ((ordinals[source.bbNum] + 1) == ordinals[target.bbNum])
                    {
                        writer.Write(" [color=blue]\n");
                    }
                    else
                    {
                        writer.Write(";\n");
                    }
                }
            }

            if (includeEH && (compHndBBtabCount > 0))
            {
                fgDumpFlowGraphEH(writer, ordinals);
            }
            if (includeLoops && (_loops is not null))
            {
                fgDumpFlowGraphLoops(writer);
            }
            writer.Write("}\n");
        }
        else
        {
            writer.Write("\n    </edges>\n</method>\n");
        }
    }

    private void fgWriteFlowGraphMemorySsa(TextWriter writer, BasicBlock block)
    {
        writer.Write("\\n");
        var ssaIn = block.bbMemorySsaNumIn[(int)MemoryKind.GcHeap];
        var ssaOut = block.bbMemorySsaNumOut[(int)MemoryKind.GcHeap];
        if (ssaIn != SsaConfig.RESERVED_SSA_NUM)
        {
            var vn = GetMemoryPerSsaData(ssaIn)._vnPair.Liberal;
            writer.Write($"MI {ssaIn} ${vn:x}");
            var phi = block.bbMemorySsaPhiFunc[(int)MemoryKind.GcHeap];
            if ((phi is not null) && (phi != BasicBlock.EmptyMemoryPhiDef))
            {
                writer.Write(" = PHI(");
                var first = true;
                for (; phi is not null; phi = phi._nextArg)
                {
                    writer.Write($"{(first ? "" : ",")}{phi.SsaNum} ${GetMemoryPerSsaData(phi.SsaNum)._vnPair.Liberal:x}");
                    first = false;
                }
                writer.Write(')');
            }
            writer.Write("\\n");
            if (block.bbMemoryHavoc != 0)
            {
                writer.Write("** HAVOC **\\n");
            }
            writer.Write($"MO {ssaOut} ${GetMemoryPerSsaData(ssaOut)._vnPair.Liberal:x}");
        }
    }
}
#endif
