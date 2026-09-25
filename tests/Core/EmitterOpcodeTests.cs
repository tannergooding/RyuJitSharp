// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class EmitterOpcodeTests
{
    [Test]
    public static void ScalarOpcodeFormsRetainNativeByteOrder()
    {
        Assert.That(insCode(INS_push), Is.EqualTo((nuint)0x0030FE));
        Assert.That(insCodeRR(INS_push), Is.EqualTo((nuint)0x50));
        Assert.That(insCodeRR(INS_pop), Is.EqualTo((nuint)0x58));
        Assert.That(insCodeMR(INS_mov), Is.EqualTo((nuint)0x88));
        Assert.That(insCodeRM(INS_mov), Is.EqualTo((nuint)0x8A));
        Assert.That(insCodeMI(INS_mov), Is.EqualTo((nuint)0xC6));
        Assert.That(insCodeACC(INS_mov), Is.EqualTo((nuint)0xB0));
    }

    [TestCase(INS_mov, true, true, true)]
    [TestCase(INS_lea, false, false, true)]
    [TestCase(INS_push, true, true, false)]
    [TestCase(INS_pop, true, false, false)]
    public static void MissingEncodingSentinelsAreDistinctFromOpcodeZero(
        instruction ins, bool mr, bool mi, bool rm)
    {
        Assert.That(hasCodeMR(ins), Is.EqualTo(mr));
        Assert.That(hasCodeMI(ins), Is.EqualTo(mi));
        Assert.That(hasCodeRM(ins), Is.EqualTo(rm));
        Assert.That(hasCodeMR(INS_add), Is.True);
        Assert.That(insCodeMR(INS_add), Is.EqualTo((nuint)0));
    }

    [Test]
    public static void ImmediateMultiplyPreservesRexBitsAboveTheLowWord()
    {
        Assert.That(insCodeMI(INS_imul_AX), Is.EqualTo((nuint)0x68));
        Assert.That(insCodeMI(INS_imul_08), Is.EqualTo(unchecked((nuint)0x4400000068UL)));
        Assert.That(insCodeMI(INS_imul_15), Is.EqualTo(unchecked((nuint)0x4400003868UL)));
    }

    [Test]
    public static void NestedPackedOpcodeMacrosPreserveMixedEndianEncoding()
    {
        Assert.That(insCodeRM(INS_sarx), Is.EqualTo((nuint)0x0FF3F738));
    }
}
