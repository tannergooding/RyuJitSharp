using System;

namespace RyuJitSharp.UnitTests;

internal static class TestConfiguration
{
    public static bool IsDebug => Configuration.DEBUG;

    public static unsafe IDisposable? CreateJitTls()
    {
        return CreateJitTls(null);
    }

    public static unsafe IDisposable? CreateJitTls(ICorJitInfo* jitInfo)
    {
#if DEBUG
        return new JitTls(jitInfo);
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

    public static void EnableVerbose(Compiler compiler)
    {
#if DEBUG
        compiler.verbose = true;
#endif
    }

    public static string CaptureDebugOutput(Action action)
    {
#if DEBUG
        return CodeGenLifeTransitionTests.Capture(action);
#else
        throw new InvalidOperationException("Debug output capture is only available in Debug builds.");
#endif
    }
}
