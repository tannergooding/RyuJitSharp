// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenRegisterPairShiftAdapterTests
{
    [TestCase(EA_UNKNOWN, EA_4BYTE)]
    [TestCase(EA_1BYTE, EA_1BYTE)]
    public static void RegisterPairUsesTheTypeUnlessAnExplicitSizeOverridesIt(emitAttr size, emitAttr expected)
    {
        WithCodeGen(codeGen =>
        {
            codeGen.inst_RV_RV(INS_add, FirstRegister, SecondRegister, TYP_INT, size);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(FirstRegister));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(SecondRegister));
        });
    }

    [TestCase(0u, INS_shl_N, 0)]
    [TestCase(1u, INS_shl_1, 0)]
    [TestCase(33u, INS_shl_N, 33)]
    [TestCase(255u, INS_shl_N, 127)]
    [TestCase(uint.MaxValue, INS_shl_N, 127)]
    public static void ShiftSelectsImplicitOneOrImmediateInstruction(uint count, instruction expected, int immediate)
    {
        if (count == uint.MaxValue)
        {
            Assume.That(Configuration.TARGET_X86);
        }

        WithCodeGen(codeGen =>
        {
            codeGen.inst_RV_SH(INS_shl, EA_4BYTE, FirstRegister, count);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(FirstRegister));
            if (count != 1)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)immediate));
            }
        });
    }

    private static void WithCodeGen(System.Action<CodeGen> action)
    {
        Assume.That(Configuration.TARGET_XARCH);
        if (Configuration.TARGET_AMD64)
        {
            CodeGenBinaryTests.WithCodeGen((_, codeGen) => action(codeGen));
        }
        else
        {
            X86EmitterStaticOutputTests.WithEmitter((compiler, _) =>
            {
                var codeGen = compiler.codeGen as CodeGen
                    ?? throw new AssertionException("Missing x86 code generator.");
                action(codeGen);
            });
        }
    }

    private static regNumber FirstRegister => Configuration.TARGET_AMD64 ? REG_X64_RAX : REG_X64_EAX;

    private static regNumber SecondRegister => Configuration.TARGET_AMD64 ? REG_X64_RCX : REG_X64_ECX;
}
