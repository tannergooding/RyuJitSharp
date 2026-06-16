// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public static int HashString(string str)
    {
        var hash = 5381;

        for (var i = 0; i < str.Length; i++)
        {
            hash = unchecked(((hash << 5) + hash) ^ str[i]);
        }
        return hash;
    }
}
