// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Text;

namespace RyuJitSharp;

/// <summary>Parses and stores a list of Assembly names, and provides a function for determining whether a given assembly name is part of the list.</summary>
public sealed class AssemblyNamesList2
{
    /// <summary>List of names</summary>
    private AssemblyName? _names;

    // Take a UTF8 string list of assembly names, parse it, and store it.
    public unsafe AssemblyNamesList2(byte* list)
    {
        // dummy
        var prevChar   = '?';     

        // start of the name currently being processed. nullptr if no current name
        var nameStart = (byte*)(null);

        ref var nextName = ref _names;

        for (var listWalk = list; prevChar != '\0'; prevChar = (char)(listWalk[0]), listWalk++)
        {
            var curChar = (char)(listWalk[0]);

            if (curChar is ';' or '\0')
            {
                // Found separator or end of string
                if (nameStart is not null)
                {
                    // Found the end of the current name; add a new assembly name to the list.
                    var nameLenUtf8 = unchecked((int)(listWalk - nameStart));
                    var nameUtf8 = new ReadOnlySpan<byte>(nameStart, nameLenUtf8);

                    var name = Encoding.UTF8.GetString(nameUtf8);
                    var newName = new AssemblyName(name);

                    nextName = newName;
                    nextName = ref newName.Next;

                    nameStart = null;
                }
            }
            else if (nameStart is null)
            {
                // Found the start of a new name
                nameStart = listWalk;
            }
        }

        // cannot be in the middle of a name
        assert(nameStart is null);

        // Terminate the last element of the list.
        nextName = null;
    }

    /// <summary>Return 'true' if the assembly name list is empty.</summary>
    public bool IsEmpty => _names is null;

    /// <summary>Return 'true' if 'assemblyName' is in the stored list of assembly names.</summary>
    /// <param name="assemblyName"></param>
    /// <returns></returns>
    public bool IsInList(string assemblyName)
    {
        for (var name = _names; name is not null; name = name.Next)
        {
            if (name.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private sealed class AssemblyName
    {
        public string Name;

        public AssemblyName? Next;

        public AssemblyName(string name)
        {
            Name = name;
        }
    }
}
