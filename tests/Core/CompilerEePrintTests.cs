// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CompilerEePrintTests
{
    [Test]
    public static void ShortUtf8MessagesPreserveReplacementFallback()
    {
        var message = Convert.FromHexString("41C32842");
        var compiler = NewCompiler();
        var output = compiler.eeAppendPrint(new StringBuilder(), CreatePrintFunc(message));

        Assert.That(output.ToString(), Is.EqualTo("A\uFFFD(B"));
    }

    [Test]
    public static void LongUtf8MessagesUseTheExistingStringPath()
    {
        var message = new byte[300];
        Array.Fill(message, (byte)'A');

        var compiler = NewCompiler();
        var output = compiler.eeAppendPrint(new StringBuilder(), CreatePrintFunc(message));

        Assert.That(output.ToString(), Is.EqualTo(new string('A', message.Length)));
    }

    [Test]
    public static void ShortMessagesAvoidPerCallStringAllocations()
    {
        var message = "ABC"u8.ToArray();
        var printFunc = CreatePrintFunc(message);
        var compiler = NewCompiler();
        var output = new StringBuilder(256);

        _ = compiler.eeAppendPrint(output, printFunc);
        _ = output.Clear();

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 32; i++)
        {
            _ = compiler.eeAppendPrint(output, printFunc);
        }
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytes;

        Assert.That(allocatedBytes, Is.Zero);
        Assert.That(output.Length, Is.EqualTo(message.Length * 32));
    }

    [Test]
    public static void PrintExceptionPropagatesAfterPooledBufferRent()
    {
        var compiler = NewCompiler();
        var exception = new InvalidOperationException();
        var printCount = 0;

        int Print(Span<byte> buffer, out int requiredBufferSize)
        {
            requiredBufferSize = 300;
            printCount++;

            if (printCount is 1)
            {
                return requiredBufferSize - 1;
            }

            throw exception;
        }

        EEPrintFunc printFunc = Print;

        var actual = Assert.Throws<InvalidOperationException>(() =>
            compiler.eeAppendPrint(new StringBuilder(), printFunc));

        Assert.That(actual, Is.SameAs(exception));
        Assert.That(printCount, Is.EqualTo(2));
    }

    private static Compiler NewCompiler() => (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

    private static EEPrintFunc CreatePrintFunc(byte[] message)
    {
        return (Span<byte> buffer, out int requiredBufferSize) => {
            requiredBufferSize = message.Length + 1;

            if (buffer.Length >= requiredBufferSize)
            {
                message.AsSpan().CopyTo(buffer);
                buffer[message.Length] = 0;
            }

            return message.Length;
        };
    }
}
