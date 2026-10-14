using System;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    public static string CaptureDebugOutput(Action action)
    {
#if DEBUG
        return CodeGenLifeTransitionTests.Capture(action);
#else
        throw new InvalidOperationException("Debug output capture is only available in Debug builds.");
#endif
    }
}
