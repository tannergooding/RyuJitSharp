// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH
using System;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class IntrinsicInputSizeTests
{
    [TestCase(INS_pextrb, 1)]
    [TestCase(INS_pextrw, 2)]
    [TestCase(INS_addps, 4)]
    [TestCase(INS_addpd, 8)]
    public static void InstructionInputSizeMatchesGeneratedFlags(instruction instruction, int expected)
    {
        Assert.That(CodeGen.instInputSize(instruction), Is.EqualTo(expected));
    }

    [Test]
    public static void InstructionWithoutInputSizeIsNotSilentlyAccepted()
    {
        Assert.That(() => CodeGen.instInputSize(INS_invalid), Throws.TypeOf<InvalidOperationException>());
    }
}
#endif
