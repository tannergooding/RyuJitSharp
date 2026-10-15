// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class OpcodeTableTests
{
    [TestCase(OPCODE.CEE_ILLEGAL, 0x123)]
    [TestCase(OPCODE.CEE_MACRO_END, 0x124)]
    [TestCase(OPCODE.CEE_CODE_LABEL, 0x125)]
    public static void InternalOpcodesHaveStableIdentityAndTableEntries(OPCODE opcode, int expectedValue)
    {
        Assert.That((int)opcode, Is.EqualTo(expectedValue));
        Assert.That(opcode.Size, Is.Zero);
    }

    [Test]
    public static void CountIncludesInternalOpcodes()
    {
        Assert.That((int)OPCODE.CEE_COUNT, Is.EqualTo(0x126));
    }

    [Test]
    public static void OpcodeDiagnosticMetadataIsAvailableInEveryConfiguration()
    {
        var opcode = OPCODE.CEE_NOP;

        Assert.That(opcode.ArgKind, Is.EqualTo(OPCODE_FORMAT.InlineNone));
        Assert.That(opcode.FlowKind, Is.EqualTo(OpFlow.FLOW_NEXT));
        Assert.That(opcode.Name, Is.EqualTo("nop"));
    }
}
