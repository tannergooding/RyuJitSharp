// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if EMITTER_STATS && TARGET_ARM
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
#if TARGET_WINDOWS
#if DEBUG
        const int expectedGroupSize = 88;
#elif LATE_DISASM
        const int expectedGroupSize = 60;
#else
        const int expectedGroupSize = 40;
#endif
#elif TARGET_UNIX
#if DEBUG
        const int expectedGroupSize = 96;
#elif LATE_DISASM
        const int expectedGroupSize = 64;
#else
        const int expectedGroupSize = 48;
#endif
#else
        Assert.Fail("ARM32 emitter statistics require a known native ABI.");
        return;
#endif
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
#endif
