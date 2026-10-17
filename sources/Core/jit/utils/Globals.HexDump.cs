// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace RyuJitSharp;

public static partial class Globals
{
    [Conditional("DEBUG")]
    public static unsafe void hexDump(StreamWriter writer, byte* address, nuint size)
    {
        if (size == 0)
        {
            return;
        }

        assert(address is not null);
        for (nuint index = 0; index < size; index++)
        {
            writer.Write((*address++).ToString("X2", CultureInfo.InvariantCulture));
        }
    }
}
