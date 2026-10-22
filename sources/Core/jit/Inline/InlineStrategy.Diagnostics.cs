// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace RyuJitSharp;

public sealed partial class InlineStrategy
{
    [Conditional("DEBUG")]
    public void DumpData()
    {
        if ((JitConfig.JitInlineDumpData == 0) || (JitConfig.JitInlineDumpXml != 0))
        {
            return;
        }

        var limit = JitConfig.JitInlineLimit;
        if ((limit >= 0) && (unchecked((uint)_inlineCount) < (uint)limit))
        {
            return;
        }

        DumpDataEnsurePolicyIsSet();

        using var stream = Console.OpenStandardError();
        using var file = new JitTextWriter(stream, leaveOpen: true) {
            AutoFlush = true
        };

        if (!global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.HasDumpedDataHeader)
        {
            DumpDataHeader(file);
            global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.HasDumpedDataHeader = true;
        }

        DumpDataContents(file);
        file.Write('\n');
    }

    [MemberNotNull(nameof(_lastSuccessfulPolicy))]
    private unsafe void DumpDataEnsurePolicyIsSet()
    {
        if (_lastSuccessfulPolicy is null)
        {
            var info = _compiler.info;
            _lastSuccessfulPolicy = InlinePolicy.GetPolicy(_compiler, _compiler.IsAot);

            var isForceInline = (info.compFlags & CORINFO_FLG_FORCEINLINE) != 0;
            _lastSuccessfulPolicy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, isForceInline);
            _lastSuccessfulPolicy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, info.compMethodInfo->ILCodeSize);
        }
    }

    [Conditional("DEBUG")]
    internal void DumpDataHeader(StreamWriter file)
    {
        DumpDataEnsurePolicyIsSet();
        file.Write($"*** Inline Data: Policy={_lastSuccessfulPolicy.Name} JitInlineLimit={JitConfig.JitInlineLimit} ***\n");
        DumpDataSchema(file);
        file.Write('\n');
    }

    [Conditional("DEBUG")]
    internal void DumpDataSchema(StreamWriter file)
    {
        DumpDataEnsurePolicyIsSet();
        file.Write("Method,Version,HotSize,ColdSize,JitTime,SizeEstimate,TimeEstimate,");
        _lastSuccessfulPolicy.DumpSchema(file);
    }

    [Conditional("DEBUG")]
    internal unsafe void DumpDataContents(StreamWriter file)
    {
        DumpDataEnsurePolicyIsSet();

        var info = _compiler.info;
        var token = info.compCompHnd->getMethodDefFromMethod(info.compMethodHnd);
        var jitTime = _compiler.InlineDiagnosticJitTimeMicroseconds;

        file.Write($"{token:X8},{unchecked((uint)_inlineCount)},{unchecked((uint)info.compTotalHotCodeSize)}," +
            $"{unchecked((uint)info.compTotalColdCodeSize)},{jitTime},{_currentSizeEstimate / 10},{_currentTimeEstimate},");
        _lastSuccessfulPolicy.DumpData(file);
    }

    [Conditional("DEBUG")]
    public unsafe void DumpXml(StreamWriter? file = null, int indent = 0)
    {
        if (JitConfig.JitInlineDumpXml == 0)
        {
            return;
        }

        file ??= jitstdout();

        lock (global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.WriterLock)
        {
            if (!global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.HasDumpedXmlHeader)
            {
                DumpDataEnsurePolicyIsSet();
                var dumpDataSetting = JitConfig.JitInlineDumpData;
                file.Write("<?xml version=\"1.0\"?>\n");
                file.Write("<InlineForest>\n");
                file.Write($"<Policy>{_lastSuccessfulPolicy.Name}</Policy>\n");

                if (dumpDataSetting != 0)
                {
                    file.Write("<DataSchema>");
                    if (dumpDataSetting == 1)
                    {
                        DumpDataSchema(file);
                    }
                    else if (dumpDataSetting == 2)
                    {
                        _lastSuccessfulPolicy.DumpSchema(file);
                    }

                    file.Write("</DataSchema>\n");
                }

                file.Write("<Methods>\n");
                global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.HasDumpedXmlHeader = true;
            }

            if ((_inlineCount == 0) && (JitConfig.JitInlineDumpXml >= 2))
            {
                return;
            }

            var info = _compiler.info;
            var token = info.compCompHnd->getMethodDefFromMethod(info.compMethodHnd);
            var hash = unchecked((uint)info.compMethodHash());
            var jitTime = _compiler.InlineDiagnosticJitTimeMicroseconds;
            var methodName = _compiler.eeGetMethodFullName(info.compMethodHnd);

            file.Write($"{new string(' ', indent)}<Method>\n");
            file.Write($"{new string(' ', indent + 2)}<Token>{token:x8}</Token>\n");
            file.Write($"{new string(' ', indent + 2)}<Hash>{hash:x8}</Hash>\n");
            file.Write($"{new string(' ', indent + 2)}<InlineCount>{unchecked((uint)_inlineCount)}</InlineCount>\n");
            file.Write($"{new string(' ', indent + 2)}<HotSize>{unchecked((uint)info.compTotalHotCodeSize)}</HotSize>\n");
            file.Write($"{new string(' ', indent + 2)}<ColdSize>{unchecked((uint)info.compTotalColdCodeSize)}</ColdSize>\n");
            file.Write($"{new string(' ', indent + 2)}<JitTime>{jitTime}</JitTime>\n");
            file.Write($"{new string(' ', indent + 2)}<SizeEstimate>{unchecked((uint)(_currentSizeEstimate / 10))}</SizeEstimate>\n");
            file.Write($"{new string(' ', indent + 2)}<TimeEstimate>{unchecked((uint)_currentTimeEstimate)}</TimeEstimate>\n");
            file.Write($"{new string(' ', indent + 2)}<Name>");
            InlineContext.WriteEscapedName(file, methodName);
            file.Write("</Name>\n");

            if (_compiler.IsAot)
            {
                file.Write($"{new string(' ', indent + 2)}<PrejitDecision>{_prejitRootDecision.String}</PrejitDecision>\n");
                file.Write($"{new string(' ', indent + 2)}<PrejitObservation>{_prejitRootObservation.String}</PrejitObservation>\n");
            }

            if (_rootContext is not null)
            {
                _rootContext.DumpXml(file, indent + 2);
            }
            else
            {
                file.Write($"{new string(' ', indent + 2)}<Inlines/>\n");
            }

            file.Write($"{new string(' ', indent)}</Method>\n");
        }
    }

    [Conditional("DEBUG")]
    public static void FinalizeXml(StreamWriter? file = null)
    {
        file ??= jitstdout();
        lock (global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.WriterLock)
        {
            if (global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.HasDumpedXmlHeader)
            {
                file.Write("</Methods>\n");
                file.Write("</InlineForest>\n");
                file.Flush();
                global::RyuJitSharp.Compiler.DebugData.InlineXmlDumpData.HasDumpedXmlHeader = false;
            }
        }

        if (Configuration.DEBUG)
        {
            ReplayPolicy.FinalizeXml();
        }
    }
}
