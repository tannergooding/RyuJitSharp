// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32EmitterStatisticsTests
{
    [Test]
    public static void InstructionGroupSizeAndStaticReportMatchNativeLayout()
    {
        Assume.That(Configuration.EMITTER_STATS && Configuration.TARGET_ARM);

        int expectedGroupSize;
        if (Configuration.TARGET_WINDOWS)
        {
            expectedGroupSize = Configuration.DEBUG
                ? 88
                : Configuration.LATE_DISASM ? 60 : 40;
        }
        else if (Configuration.TARGET_UNIX)
        {
            expectedGroupSize = Configuration.DEBUG
                ? 96
                : Configuration.LATE_DISASM ? 64 : 48;
        }
        else
        {
            Assert.Fail("ARM32 emitter statistics require a known native ABI.");
            return;
        }

        Assert.That(Emitter.emitNativeIGSize(), Is.EqualTo((nuint)expectedGroupSize));

        var previous = Globals.s_jitstdout;
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
        {
            AutoFlush = true,
        };

        try
        {
            Globals.s_jitstdout = output;
            Emitter.emitStaticStats();

            var text = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(text, Does.Contain($"Size of insGroup                    = {expectedGroupSize}"));
        }
        finally
        {
            Globals.s_jitstdout = previous;
        }
    }
}
