using System;

namespace RyuJitSharp.UnitTests;

internal static class TestConfiguration
{
    public static bool IsDebug => Configuration.DEBUG;

    public static unsafe IDisposable? CreateJitTls()
    {
#if DEBUG
        return new JitTls(null);
#else
        return null;
#endif
    }
}
