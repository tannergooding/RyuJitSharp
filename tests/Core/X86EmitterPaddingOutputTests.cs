// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class X86EmitterPaddingOutputTests
{
    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(7u)]
    [TestCase(15u)]
    public static void NopsUseSingleByteX86Padding(uint size)
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var buffer = stackalloc byte[20];
            new Span<byte>(buffer, 20).Fill(0xA5);
            var end = emitter.emitOutputNOP(buffer, size);

            Assert.That(end - buffer, Is.EqualTo((long)size));
            for (var i = 0; i < size; i++)
            {
                Assert.That(buffer[i], Is.EqualTo(0x90));
            }
            Assert.That(buffer[size], Is.EqualTo(0xA5));
        });
    }

    [Test]
    public static void Data16LeavesX86CodeUnchanged()
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var buffer = stackalloc byte[2];
            buffer[0] = 0xA5;
            var end = emitter.emitOutputData16(buffer);

            Assert.That((nint)end, Is.EqualTo((nint)buffer));
            Assert.That(buffer[0], Is.EqualTo(0xA5));
        });
    }

#if !DEBUG
    [Test]
    public static void OutOfRangePaddingRetainsNativeReleaseBehavior()
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var buffer = stackalloc byte[20];
            buffer[0] = 0xA5;
            var end = emitter.emitOutputNOP(buffer, 16);

            Assert.That((nint)end, Is.EqualTo((nint)buffer));
            Assert.That(buffer[0], Is.EqualTo(0xA5));
        });
    }
#endif
}
#endif
