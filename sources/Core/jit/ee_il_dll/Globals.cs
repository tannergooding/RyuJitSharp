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

public partial class Globals
{
    internal static volatile StreamWriter? s_jitstdout;

    private static unsafe StreamWriter jitstdoutInit()
    {
        var jitStdOutFile = JitConfig.JitStdOutFile;

        StreamWriter jitstdout;

        if (jitStdOutFile is not null)
        {
            jitstdout = new StreamWriter(Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(jitStdOutFile)), append: true);
        }
        else
        {
            jitstdout = new StreamWriter(Console.OpenStandardOutput(), leaveOpen: true);
        }

        jitstdout.AutoFlush = true;
        jitstdout.NewLine = "\n";

        var observed = Interlocked.CompareExchange(ref s_jitstdout, jitstdout, null);

        if (observed is not null)
        {
            jitstdout.Dispose();
            return observed;
        }
        return jitstdout;
    }
}
