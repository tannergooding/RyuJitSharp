// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

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
        Assume.That(Configuration.TARGET_XARCH);

        Assert.That(CodeGen.instInputSize(instruction), Is.EqualTo(expected));
    }

    [Test]
    public static void InstructionWithoutInputSizeIsNotSilentlyAccepted()
    {
        Assume.That(Configuration.TARGET_XARCH);

        Assert.That(() => CodeGen.instInputSize(INS_invalid), Throws.TypeOf<InvalidOperationException>());
    }
}
