// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64InstructionNameTableTests
{
    [TestCase(INS_add, "add")]
    [TestCase(INS_ldr, "ldr")]
    [TestCase(INS_sve_mov, "mov")]
    public static void InstructionNamesUseTheTargetSpecificPackedTable(instruction ins, string expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(new CodeGen(compiler).genInsName(ins).ToString(), Is.EqualTo(expected));
    }
}
#endif
