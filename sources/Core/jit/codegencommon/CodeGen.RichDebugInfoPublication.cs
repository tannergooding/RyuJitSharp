// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if DEBUG
    private static readonly Lock s_richDebugInfoFileLock = new();

    public unsafe void genReportRichDebugInfoInlineTreeToFile(TextWriter writer, InlineContext context, ref bool first)
    {
        if (context.Sibling is not null)
        {
            genReportRichDebugInfoInlineTreeToFile(writer, context.Sibling, ref first);
        }

        if (context.IsSuccess)
        {
            if (!first)
            {
                writer.Write(',');
            }

            first = false;
            writer.Write($"{{\"Ordinal\":{unchecked((uint)context.Ordinal)},");
            writer.Write($"\"MethodID\":{unchecked((long)(nint)context.Callee)},");
            writer.Write($"\"ILOffset\":{unchecked((uint)context.Location.Offset)},");
            writer.Write($"\"LocationFlags\":{unchecked((uint)context.Location.SourceTypes)},");
            writer.Write($"\"ExactILOffset\":{unchecked((uint)context.ActualCallOffset)},");
            writer.Write($"\"MethodName\":\"{_compiler.eeGetMethodName(context.Callee)}\",");
            writer.Write("\"Inlinees\":[");
            if (context.Child is not null)
            {
                var childFirst = true;
                genReportRichDebugInfoInlineTreeToFile(writer, context.Child, ref childFirst);
            }
            writer.Write("]}");
        }
    }

    public unsafe void genReportRichDebugInfoToFile()
    {
        var pathPointer = JitConfig.WriteRichDebugInfoFile;
        if (pathPointer is null)
        {
            return;
        }

        lock (s_richDebugInfoFileLock)
        {
            var path = Marshal.PtrToStringUTF8((nint)pathPointer)
                ?? throw new FatalJitException(CORJIT_SKIPPED, "Rich debug-info file path is invalid.");
            using var file = OpenRichDebugInfoFile(path);
            if (file is null)
            {
                return;
            }

            using var writer = new StreamWriter(file, new UTF8Encoding(false));
            genWriteRichDebugInfo(writer);
        }
    }

    private static FileStream? OpenRichDebugInfoFile(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal unsafe void genWriteRichDebugInfo(TextWriter writer)
    {
        writer.Write($"{{\"MethodID\":{unchecked((long)(nint)_compiler.info.compMethodHnd)},");
        writer.Write("\"InlineTree\":");

        var first = true;
        assert(_compiler.compInlineContext is not null);
        genReportRichDebugInfoInlineTreeToFile(writer, _compiler.compInlineContext, ref first);
        writer.Write(",\"Mappings\":[");
        first = true;
        foreach (var mapping in _compiler.genRichIPmappings)
        {
            if (!first)
            {
                writer.Write(',');
            }

            first = false;
            assert(mapping.debugInfo.InlineContext is not null);
            writer.Write(
                $"{{\"NativeOffset\":{mapping.nativeLoc.CodeOffset(Emitter)}," +
                $"\"InlineContext\":{unchecked((uint)mapping.debugInfo.InlineContext.Ordinal)}," +
                $"\"ILOffset\":{unchecked((uint)mapping.debugInfo.Location.Offset)}}}");
        }

        writer.Write("]}");
        writer.Write(Environment.NewLine);
    }
#endif

    private static InlineContext? SuccessfulSibling(InlineContext? context)
    {
        while ((context is not null) && !context.IsSuccess)
        {
            context = context.Sibling;
        }

        return context;
    }

    public unsafe void genRecordRichDebugInfoInlineTree(InlineContext context, ICorDebugInfo.InlineTreeNode* nodes)
    {
        assert(context.IsSuccess);
        assert(_compiler._inlineStrategy is not null);
        assert(context.Ordinal <= _compiler._inlineStrategy.InlineCount);

        var successfulChild = SuccessfulSibling(context.Child);
        var successfulSibling = SuccessfulSibling(context.Sibling);

        var node = nodes + context.Ordinal;
        node->Method = context.Callee;
        node->ILOffset = context.ActualCallOffset;
        node->Child = successfulChild is null ? 0 : successfulChild.Ordinal;
        node->Sibling = successfulSibling is null ? 0 : successfulSibling.Ordinal;

        if (successfulSibling is not null)
        {
            genRecordRichDebugInfoInlineTree(successfulSibling, nodes);
        }

        if (successfulChild is not null)
        {
            genRecordRichDebugInfoInlineTree(successfulChild, nodes);
        }
    }

    public unsafe void genReportRichDebugInfo()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Rich debug-info publication requires Windows AMD64.");
#else
#if DEBUG
        genReportRichDebugInfoToFile();
#endif
        if (JitConfig.RichDebugInfo == 0)
        {
            return;
        }

        assert(_compiler._inlineStrategy is not null);
        var numContexts = unchecked((uint)(1 + _compiler._inlineStrategy.InlineCount));
        var numRichMappings = unchecked((uint)_compiler.genRichIPmappings.Count);
        var jitInfo = _compiler.info.compCompHnd;
        var inlineTree = (ICorDebugInfo.InlineTreeNode*)jitInfo->allocateArray(
            unchecked((nint)((nuint)numContexts * (nuint)sizeof(ICorDebugInfo.InlineTreeNode))));
        var mappings = (ICorDebugInfo.RichOffsetMapping*)jitInfo->allocateArray(
            unchecked((nint)((nuint)numRichMappings * (nuint)sizeof(ICorDebugInfo.RichOffsetMapping))));

        NativeMemory.Clear(inlineTree, unchecked((nuint)numContexts * (nuint)sizeof(ICorDebugInfo.InlineTreeNode)));
        NativeMemory.Clear(mappings, unchecked((nuint)numRichMappings * (nuint)sizeof(ICorDebugInfo.RichOffsetMapping)));

        assert(_compiler.compInlineContext is not null);
        genRecordRichDebugInfoInlineTree(_compiler.compInlineContext, inlineTree);

#if DEBUG
        for (var index = 0u; index < numContexts; index++)
        {
            assert(inlineTree[index].Method != NO_METHOD_HANDLE);
        }
#endif

        var mappingIndex = 0;
        foreach (var richMapping in _compiler.genRichIPmappings)
        {
            assert(richMapping.debugInfo.IsValid);
            var mapping = mappings + mappingIndex;
            mapping->NativeOffset = unchecked((int)richMapping.nativeLoc.CodeOffset(Emitter));
            assert(richMapping.debugInfo.InlineContext is not null);
            mapping->Inlinee = richMapping.debugInfo.InlineContext.Ordinal;
            mapping->ILOffset = richMapping.debugInfo.Location.Offset;
            mapping->Source = richMapping.debugInfo.Location.SourceTypes;
            mappingIndex++;
        }

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("Reported inline tree:\n");
            for (var index = 0u; index < numContexts; index++)
            {
                var node = inlineTree[index];
                jitprintf($"  [#{index}] {_compiler.eeGetMethodFullName(node.Method)} @ {node.ILOffset}, " +
                    $"child = {node.Child}, sibling = {node.Sibling}\n");
            }

            jitprintf("\nReported rich mappings:\n");
            for (var index = 0; index < mappingIndex; index++)
            {
                var mapping = mappings[index];
                jitprintf($"  [{index}] 0x{unchecked((uint)mapping.NativeOffset):x} <-> IL {mapping.ILOffset} " +
                    $"in #{mapping.Inlinee}\n");
            }

            jitprintf("\n");
        }
#endif

        jitInfo->reportRichMappings(inlineTree, unchecked((int)numContexts), mappings, unchecked((int)numRichMappings));
#endif
    }
}
