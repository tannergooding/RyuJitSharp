// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerHeaderPortTests
{
    [TestCase(false, -1)]
    [TestCase(false, 17)]
    [TestCase(true, -1)]
    [TestCase(true, 17)]
    public static unsafe void ShadowParameterPrintPreservesPointerAndSignedLocalFormat(bool hasGroup, int shadowCopy)
    {
        Assume.That(TestConfiguration.IsDebug);

        var info = new Compiler.ShadowParamVarInfo {
            AssignGroup = hasGroup ? [1, 2] : null,
            ShadowCopy = shadowCopy,
        };
        var originalOutput = s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);

        try
        {
            s_jitstdout = writer;
            info.Print();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = originalOutput;
        }

        var text = Encoding.UTF8.GetString(stream.ToArray()).Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        var pointerDigits = FMT_PTR(null).Length;
        Assert.That(text, Does.Match(
            $"\\AassignGroup \\[[0-9A-F]{{{pointerDigits}}}\\]; shadowCopy: \\[{shadowCopy}\\];\\n\\z"));
        Assert.That(text.StartsWith($"assignGroup [{new string('0', pointerDigits)}]", StringComparison.Ordinal),
            Is.EqualTo(!hasGroup));
        Assert.That(info.ShadowCopy, Is.EqualTo(shadowCopy));
        Assert.That(info.AssignGroup, hasGroup ? Is.EqualTo(new nint[] { 1, 2 }) : Is.Null);
    }
}
