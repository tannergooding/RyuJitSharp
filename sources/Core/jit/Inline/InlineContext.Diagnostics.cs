// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Text;

namespace RyuJitSharp;

public sealed partial class InlineContext
{
#if DEBUG
    internal unsafe void DumpXml(StreamWriter file, int indent)
    {
        _sibling?.DumpXml(file, indent);

        if ((JitConfig.JitInlineDumpXml == 3) && !IsSuccess)
        {
            return;
        }

        var isRoot = _parent is null;
        var hasChild = _child is not null;
        var inlineType = IsSuccess ? "Inline" : "FailedInline";
        var childIndent = indent;

        if (!isRoot)
        {
            var compiler = _inlineStrategy.Compiler;
            var token = compiler.info.compCompHnd->getMethodDefFromMethod(_callee);
            var hash = compiler.compMethodHash(_callee);
            var reason = _observation.String;
            var name = compiler.eeGetMethodFullName(_callee);
            var offset = _location.IsValid ? _location.Offset : -1;

            file.Write($"{new string(' ', indent)}<{inlineType}>\n");
            file.Write($"{new string(' ', indent + 2)}<Token>{token:x8}</Token>\n");
            file.Write($"{new string(' ', indent + 2)}<Hash>{hash:x8}</Hash>\n");
            file.Write($"{new string(' ', indent + 2)}<Offset>{unchecked((uint)offset)}</Offset>\n");
            file.Write($"{new string(' ', indent + 2)}<Reason>{reason}</Reason>\n");
            file.Write($"{new string(' ', indent + 2)}<Name>");
            WriteEscapedName(file, name);
            file.Write("</Name>\n");
            file.Write($"{new string(' ', indent + 2)}<ILSize>{_ilSize}</ILSize>\n");
            file.Write($"{new string(' ', indent + 2)}<Devirtualized>{(IsDevirtualized ? "True" : "False")}</Devirtualized>\n");
            file.Write($"{new string(' ', indent + 2)}<Guarded>{(IsGuarded ? "True" : "False")}</Guarded>\n");
            file.Write($"{new string(' ', indent + 2)}<Unboxed>{(IsUnboxed ? "True" : "False")}</Unboxed>\n");

            if ((_policy is not null) && (JitConfig.JitInlinePolicyDumpXml != 0))
            {
                _policy.DumpXml(file, indent + 2);
            }

            var dumpDataSetting = JitConfig.JitInlineDumpData;
            if ((dumpDataSetting == 1) && (this == _inlineStrategy.LastContext))
            {
                file.Write($"{new string(' ', indent + 2)}<Data>");
                _inlineStrategy.DumpDataContents(file);
                file.Write("</Data>\n");
            }

            if ((dumpDataSetting == 2) && (_policy is not null))
            {
                file.Write($"{new string(' ', indent + 2)}<Data>");
                _policy.DumpData(file);
                file.Write("</Data>\n");
            }

            childIndent = indent + 2;
        }

        if (hasChild)
        {
            assert(_child is not null);
            file.Write($"{new string(' ', childIndent)}<Inlines>\n");
            _child.DumpXml(file, childIndent + 2);
            file.Write($"{new string(' ', childIndent)}</Inlines>\n");
        }
        else
        {
            file.Write($"{new string(' ', childIndent)}<Inlines />\n");
        }

        if (!isRoot)
        {
            file.Write($"{new string(' ', indent)}</{inlineType}>\n");
        }
    }

    internal static void WriteEscapedName(StreamWriter file, string name)
    {
        // Native strncpy copies at most 1023 UTF-8 bytes, even if this splits a code point.
        var bytes = Encoding.UTF8.GetBytes(name);
        var length = Math.Min(bytes.Length, 1023);
        for (var i = 0; i < length; i++)
        {
            if (bytes[i] == 0)
            {
                length = i;
                break;
            }

            bytes[i] = bytes[i] switch {
                (byte)'<' => (byte)'[',
                (byte)'>' => (byte)']',
                (byte)'&' => (byte)'#',
                _ => bytes[i]
            };
        }

        file.Flush();
        file.BaseStream.Write(bytes.AsSpan(0, length));
    }
#endif
}
