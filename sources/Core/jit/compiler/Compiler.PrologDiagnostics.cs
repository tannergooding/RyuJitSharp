// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public unsafe uint compMethodHash(CORINFO_METHOD_HANDLE method)
    {
        if (method == info.compMethodHnd)
        {
            return unchecked((uint)info.compMethodHash());
        }

        return HashMethodName(eeGetMethodFullName(method));
    }

    private static uint HashMethodName(string name)
    {
        var hash = 5381u;
        foreach (var value in Encoding.UTF8.GetBytes(name))
        {
            if (value == 0)
            {
                break;
            }

            // HashStringA reads a signed char on Windows x64 before widening it to int.
            hash = unchecked(((hash << 5) + hash) ^ (uint)(sbyte)value);
        }

        return hash;
    }

    public unsafe bool compJitHaltMethod()
    {
        if (JitConfig.JitHalt.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            return true;
        }

        var hash = unchecked((uint)JitConfig.JitHashHalt);
        return (hash != uint.MaxValue) && (hash == info.compMethodHash());
    }
#endif
}
