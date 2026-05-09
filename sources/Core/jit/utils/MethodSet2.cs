// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

/// <summary>Manage a list of methods that is read from a file.</summary>
public sealed class MethodSet2
{
    // Methods are approximately in the format output by JitFunctionTrace, e.g.:
    //
    //     System.CLRConfig:GetBoolValue(ref,byref):bool (MethodHash=3c54d35e)
    //       -- use the MethodHash, not the method name
    //
    //     System.CLRConfig:GetBoolValue(ref,byref):bool
    //       -- use just the name
    //
    // Method names should not have any leading whitespace.
    //
    // TODO: Should this be more related to JitConfigValues.MethodSet?

    /// <summary>List of function info</summary>
    private MethodInfo? _infos;

    // Take a Unicode string with the filename containing a list of function names, parse it, and store it.
    public unsafe MethodSet2(byte* pFilenameUtf8)
    {
        const string MethodHashPrefix = "(MethodHash=";

        var filenameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pFilenameUtf8);

        var filename = Encoding.UTF8.GetString(filenameUtf8);
        using var streamReader = new StreamReader(filename);

        ref var nextInfo = ref _infos;

        for (var line = streamReader.ReadLine(); line is not null; line = streamReader.ReadLine())
        {
            if (line.Length == 0)
            {
                // Ignore empty lines
                continue;
            }
            if ((line.Length >= 1) && (line[0] is ';' or '#'))
            {
                // Ignore lines starting with leading ";" "#"
                continue;
            }
            else if ((line.Length >= 2) && ((line[0] is '/') && (line[1] is '/')))
            {
                // Ignore lines starting with leading "//"
                continue;
            }

            var methodName = line;
            var methodHash = 0;

            // Parse the line. Very simple. One of:
            //    <method-name>
            //    <method-name><whitespace>(MethodHash=<hash>)

            var lineSpan = line.AsSpan();
            var whitespaceIndex = lineSpan.IndexOfAny(' ', '\t');

            if (whitespaceIndex >= 0)
            {
                var methodNameSpan = lineSpan[..whitespaceIndex];
                var methodHashSpan = lineSpan[(whitespaceIndex + 1)..];

                var methodHashIndex = methodHashSpan.IndexOf(MethodHashPrefix, StringComparison.Ordinal);

                if (methodHashIndex >= 0)
                {
                    methodHashSpan = methodHashSpan[(methodHashIndex + MethodHashPrefix.Length)..];

                    if (methodHashSpan[^1] == ')')
                    {
                        methodHashSpan = methodHashSpan[..^1];

                        if (int.TryParse(methodHashSpan, CultureInfo.InvariantCulture, out methodHash))
                        {
                            methodName = methodNameSpan.ToString();
                        }
                        else
                        {
                            methodHash = 0;
                            JITDUMP($"Couldn't parse method hash: {methodHashSpan}\n");
                        }
                    }
                    else
                    {
                        JITDUMP($"Couldn't locate method hash: {line}\n");
                    }
                }
            }

            var newInfo = new MethodInfo(methodName, methodHash);

            nextInfo = newInfo;
            nextInfo = ref newInfo.Next;
        }

        nextInfo = null;

        if (_infos is null)
        {
            JITDUMP($"No methods read from {filename}\n");
        }
        else
        {
            JITDUMP($"Methods read from {filename}:\n");

            var methodCount = 0;

            for (var info = _infos; info is not null; info = info.Next)
            {
                JITDUMP($"  {info.Name} (MethodHash: {info.Hash:x})\n");
                ++methodCount;
            }

            if (methodCount > 100)
            {
                JITDUMP($"Warning: high method count ({methodCount}) for MethodSet with linear search lookups might be slow\n");
            }
        }
    }

    /// <summary>Return 'true' if the assembly name set is empty.</summary>
    public bool IsEmpty => _infos is null;

    /// <summary>Return 'true' if 'methodName' is in the stored set of assembly names.</summary>
    /// <param name="methodName"></param>
    /// <returns></returns>
    // TODO: make this more like JitConfigValues.MethodSet.contains()?
    public unsafe bool IsInSet(string methodName)
    {
        for (var info = _infos; info is not null; info = info.Next)
        {
            if (info.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Return 'true' if 'methodHash' is in the stored set of assembly names.</summary>
    /// <param name="methodHash"></param>
    /// <returns></returns>
    public bool IsInSet(int methodHash)
    {
        for (var info = _infos; info is not null; info = info.Next)
        {
            if (info.Hash == methodHash)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Return 'true' if this method is active.</summary>
    /// <param name="methodName"></param>
    /// <param name="methodHash"></param>
    /// <returns></returns>
    /// <remarks>Prefer non-zero methodHash for check over (non-null) methodName.</remarks>
    public unsafe bool IsActiveMethod(string methodName, int methodHash)
    {
        if ((methodHash != 0) && IsInSet(methodHash))
        {
            // Use the method hash.
            JITDUMP($"Method active in MethodSet (hash match): {methodName} Hash: {methodHash:x}\n");
            return true;
        }

        if (IsInSet(methodName))
        {
            // Else, fall back and use the method name.
            JITDUMP($"Method active in MethodSet (name match): {methodName} Hash: {methodHash:x}\n");
            return true;
        }
        return false;
    }

    // TODO: use a hash table? or two: one on hash value, one on function name
    private sealed class MethodInfo
    {
        public string Name;
        public int Hash;
        public MethodInfo? Next;

        public unsafe MethodInfo(string name, int hash)
        {
            Name = name;
            Hash = hash;
        }
    };
}
