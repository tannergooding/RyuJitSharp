using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class InstructionFlagModelTests
{
    [Test]
    public static void EmissionRequestFlagsUseTargetNeutralValues()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(insFlags)), Is.EqualTo(typeof(uint)));
        Assert.Multiple(() =>
        {
            Assert.That((uint)insFlags.INS_FLAGS_NOT_SET, Is.Zero);
            Assert.That((uint)insFlags.INS_FLAGS_SET, Is.EqualTo(1u));
            Assert.That((uint)insFlags.INS_FLAGS_DONT_CARE, Is.EqualTo(2u));
        });
    }

    [Test]
    public static void XarchInstructionMetadataRetainsItsUnsignedWidthAndHighBits()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(insInfoFlags)), Is.EqualTo(typeof(ulong)));
        Assert.Multiple(() =>
        {
            Assert.That((ulong)insInfoFlags.INS_FLAGS_None, Is.Zero);
            Assert.That((ulong)insInfoFlags.Encoding_EVEX_APX_ONLY, Is.EqualTo(1UL << 43));
            Assert.That((ulong)insInfoFlags.INS_FLAGS_HasPseudoName, Is.EqualTo(1UL << 52));
        });
    }
}
