using System;
using System.IO;
using System.Text;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    public static string CaptureDebugOutput(Action action)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Debug output capture is only available in Debug builds.");
        }

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
