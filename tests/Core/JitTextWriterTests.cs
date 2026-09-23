// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitTextWriterTests
{
    [Test]
    public static void WritePreservesNativeTextSemantics(
        [Values("", "\n", "first\n\nlast", "first\r\nlast", "first\rlast", "\u00E9\n")] string text,
        [Values] bool fragmented)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        writer.AutoFlush = true;

        if (fragmented)
        {
            foreach (var character in text)
            {
                writer.Write(character);
            }
        }
        else
        {
            writer.Write(text);
        }

        Assert.That(stream.ToArray(), Is.EqualTo(GetExpectedBytes(text)));
        Assert.That(writer.BaseStream.Length, Is.EqualTo(stream.Length));
    }

    [Test]
    public static void WriteOverloadsUseTheSameNewlinePolicy()
    {
        using var stream = new MemoryStream();

        using (var writer = new JitTextWriter(stream, leaveOpen: true))
        {
            writer.Write("span\n".AsSpan());
            writer.Write(['a', '\n'], 0, 2);
            writer.WriteLine("line\n");
            writer.WriteLine();
            writer.Write("{0}\n", "formatted");
        }

        Assert.That(stream.ToArray(), Is.EqualTo(GetExpectedBytes("span\na\nline\n\n\nformatted\n")));
    }

    [Test]
    public static void DisposeHonorsStreamOwnership([Values] bool leaveOpen)
    {
        using var stream = new MemoryStream();

        using (var writer = new JitTextWriter(stream, leaveOpen))
        {
            writer.Write("text\n");
        }

        Assert.That(stream.CanWrite, Is.EqualTo(leaveOpen));
    }

    [Test]
    public static void AppendPreservesExistingOutput()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.txt");

        try
        {
            using (var writer = new JitTextWriter(path, append: false))
            {
                writer.Write("first\n");
            }

            using (var writer = new JitTextWriter(path, append: true))
            {
                writer.WriteLine("second");
            }

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(GetExpectedBytes("first\nsecond\n")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] GetExpectedBytes(string text)
    {
        if (OperatingSystem.IsWindows())
        {
            text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        return Encoding.UTF8.GetBytes(text);
    }
}
