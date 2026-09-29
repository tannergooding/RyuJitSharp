// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && TARGET_WINDOWS && EMITTER_STATS
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64EmitterStaticStatisticsTests
{
    [Test]
    public static void StaticReportUsesTheWindowsArm64SecondGcReturnLayout()
    {
        var previous = Globals.s_jitstdout;
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

        try
        {
            Globals.s_jitstdout = output;
            StaticReport(null);

#if DEBUG
            const int groupSize = 136;
            const int groupNumberOffset = 88;
            const int groupRegisterOffset = 112;
            const int groupDataOffset = 120;
            const int groupInstructionOffset = 132;
            const int alignSize = 48;
#else
            const int groupSize = 56;
            const int groupNumberOffset = 8;
            const int groupRegisterOffset = 32;
            const int groupDataOffset = 40;
            const int groupInstructionOffset = 52;
            const int alignSize = 40;
#endif
            var text = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(text, Does.Contain("Size of instrDescCGCA          = 80"));
            Assert.That(text, Does.Contain("Size of insPlaceholderGroupData     = 104"));
            Assert.That(text, Does.Contain("igBuffSize                           = 3200"));
            Assert.That(text, Does.Contain($"Size of insGroup                    = {groupSize}"));
            Assert.That(text, Does.Contain("Size of instrDescJmp           = 48"));
            Assert.That(text, Does.Contain($"Size of instrDescAlign         = {alignSize}"));
            Assert.That(Emitter.DescriptorSizes.DebugInfo, Is.EqualTo(56));
            AssertField(text, "igNum", groupNumberOffset, 4);
            AssertField(text, "igGCregs", groupRegisterOffset, 8);
            AssertField(text, "igData", groupDataOffset, 8);
            AssertField(text, "igInsCnt", groupInstructionOffset, 1);
            AssertField(text, "igPhInitGCrefRegs", 24, 16);
            AssertField(text, "igPhPrevByrefRegs", 80, 16);
            AssertField(text, "igPhType", 96, 1);
        }
        finally
        {
            Globals.s_jitstdout = previous;
        }
    }

    private static void AssertField(string report, string name, int offset, int size)
    {
        var match = Regex.Match(report, $@"(?m)^Offset / size of {Regex.Escape(name)}\s+=\s*(\d+)\s*/\s*(\d+)\s*$");
        Assert.That(match.Success, Is.True, $"Missing native layout for {name}");
        Assert.That(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(offset), name);
        Assert.That(int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(size), name);
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitterStaticStats")]
    private static extern void StaticReport(Compiler? compiler);
}
#endif
