// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class X86EmitterInstructionDispatchTests
{
    [Test]
    public static void ThreeRegisterAvxOutputIsNotSupported()
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            Assert.That(emitter.IsThreeOperandAVXInstruction(INS_addps), Is.False);
        });
    }
}
#endif
