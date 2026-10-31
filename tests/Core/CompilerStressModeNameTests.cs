using System;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CompilerStressModeNameTests
{
    [Test]
    public static void Utf8NamesMatchEnumNames()
    {
        var enumNames = Enum.GetNames<Compiler.compStressArea>();
        var utf8Names = Compiler.DebugData.CompStressData.ModeNamesUtf8;

        Assert.That(utf8Names.Length, Is.EqualTo(enumNames.Length));

        for (var index = 0; index < enumNames.Length; index++)
        {
            Assert.That(utf8Names[index], Is.EqualTo(Encoding.ASCII.GetBytes(enumNames[index])), enumNames[index]);
        }
    }

    [Test]
    public static void AreaHashesMatchStringHash()
    {
        Assume.That(TestConfiguration.IsDebug, "Stress-area hashes are only available in Debug builds.");

        var enumNames = Enum.GetNames<Compiler.compStressArea>();

        for (var index = 0; index < (int)Compiler.compStressArea.STRESS_COUNT; index++)
        {
            var expectedHash = unchecked((uint)Globals.HashString(enumNames[index]));

            if (expectedHash == 0)
            {
                expectedHash = 1;
            }

            Assert.That(
                Compiler.compStressAreaHash((Compiler.compStressArea)index),
                Is.EqualTo(expectedHash),
                enumNames[index]);
        }
    }
}
