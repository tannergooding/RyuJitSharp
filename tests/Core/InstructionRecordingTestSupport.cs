// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class InstructionRecordingTestSupport
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    internal static unsafe byte UnavailableMethodMetadata(ICorJitInfo* self,
        delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        return 0;
    }

    internal static string Capture(Action record)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            record();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
#endif
