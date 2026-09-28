// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    [TestCase(false)]
    [TestCase(true)]
    public static void AppendSharesOutputAndFollowsOtherWriters(bool truncate)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllBytes(path, GetExpectedBytes("seed\n"));
            using (var writer = new JitTextWriter(path, append: true))
            {
                writer.Write("managed first\n");
                writer.Flush();
                using (var other = new FileStream(path, truncate ? FileMode.Create : FileMode.Append,
                    FileAccess.Write, FileShare.ReadWrite))
                {
                    other.Write(GetExpectedBytes("native\n"));
                }

                writer.Write("managed last\n");
                writer.Flush();
                writer.BaseStream.WriteByte((byte)'!');
            }

            var expected = truncate ? "native\nmanaged last\n!" : "seed\nmanaged first\nnative\nmanaged last\n!";
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(GetExpectedBytes(expected)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [NonParallelizable]
    public static unsafe void SharedStdoutPreservesConcurrentRecords()
    {
        const int workerCount = 4;
        const int recordsPerWorker = 128;
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.txt");
        var previousConfig = Globals.JitConfig;
        var previousWriter = Globals.s_jitstdout;
        var records = new string[workerCount][];
        var expected = new HashSet<string>();
        var workers = new Task[workerCount];
        StreamWriter? writer = null;
        using var start = new Barrier(workerCount);

        for (var worker = 0; worker < workerCount; worker++)
        {
            records[worker] = new string[recordsPerWorker];
            for (var record = 0; record < recordsPerWorker; record++)
            {
                var text = $"{worker}:{record}:{new string((char)('a' + worker), 1024)}";
                records[worker][record] = text + "\n";
                _ = expected.Add(text);
            }
        }

        try
        {
            fixed (byte* filename = Encoding.UTF8.GetBytes(path + "\0"))
            {
                StdoutFile(ref Globals.JitConfig) = filename;
                Globals.s_jitstdout = null;
                writer = Globals.jitstdout();

                for (var worker = 0; worker < workerCount; worker++)
                {
                    var index = worker;
                    workers[worker] = Task.Factory.StartNew(() => {
                        if (!start.SignalAndWait(TimeSpan.FromSeconds(30)))
                        {
                            throw new TimeoutException("Concurrent writers did not reach the start barrier.");
                        }

                        var output = Globals.jitstdout();
                        foreach (var record in records[index])
                        {
                            switch (index)
                            {
                                case 0:
                                {
                                    Globals.jitprintf(record);
                                    break;
                                }

                                case 1:
                                {
                                    output.Write(record.AsSpan());
                                    break;
                                }

                                case 2:
                                {
                                    output.Write("{0}", record);
                                    break;
                                }

                                default:
                                {
                                    output.WriteLine(record.AsSpan(0, record.Length - 1));
                                    break;
                                }
                            }
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                Task.WaitAll(workers);
                Globals.jitstdout().Flush();
                writer.Dispose();
                writer = null;
                var actual = File.ReadAllLines(path);
                Assert.That(actual, Has.Length.EqualTo(workerCount * recordsPerWorker));
                Assert.That(new HashSet<string>(actual).SetEquals(expected), Is.True);
            }
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
            Globals.JitConfig = previousConfig;
            try
            {
                writer?.Dispose();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public static void SynchronousStreamWriterMethodsAreOverridden()
    {
        foreach (var method in typeof(StreamWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.Name is not (nameof(StreamWriter.Write) or nameof(StreamWriter.WriteLine) or nameof(StreamWriter.Flush)))
            {
                continue;
            }

            var parameters = method.GetParameters();
            var types = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                types[i] = parameters[i].ParameterType;
            }

            var implementation = typeof(JitTextWriter).GetMethod(method.Name, types);
            Assert.That(implementation?.DeclaringType, Is.EqualTo(typeof(JitTextWriter)), method.ToString());
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStdOutFile")]
    private static extern unsafe ref byte* StdoutFile(ref JitConfigValues config);

    private static byte[] GetExpectedBytes(string text)
    {
        if (OperatingSystem.IsWindows())
        {
            text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        return Encoding.UTF8.GetBytes(text);
    }
}
