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

    public static void EnableSafeBasicBlockCreation(Compiler compiler)
    {
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
    }

    public static void EnableSafeFlowEdgeCreation(Compiler compiler)
    {
#if DEBUG
        compiler.fgSafeFlowEdgeCreation = true;
#endif
    }
}
