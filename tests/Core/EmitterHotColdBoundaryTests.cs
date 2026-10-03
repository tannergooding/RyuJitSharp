#if TARGET_XARCH
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class EmitterHotColdBoundaryTests
{
    [TestCase(64, 0, 0u, 0u, false)]
    [TestCase(64, 32, 0u, 63u, false)]
    [TestCase(64, 32, 64u, 95u, false)]
    [TestCase(64, 32, 63u, 64u, true)]
    [TestCase(64, 32, 64u, 63u, true)]
    public static void DetectsHotColdBoundary(
        int hotSize, int coldSize, uint srcOffset, uint dstOffset, bool expected)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        emitter.emitTotalHotCodeSize = hotSize;
        emitter.emitTotalColdCodeSize = coldSize;

        Assert.That(CrossBoundary(emitter, srcOffset, dstOffset), Is.EqualTo(expected));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitJumpCrossHotColdBoundary")]
    private static extern bool CrossBoundary(Emitter emitter, uint srcOffset, uint dstOffset);
}
#endif
