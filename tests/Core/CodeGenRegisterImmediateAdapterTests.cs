// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenRegisterImmediateAdapterTests
{
    [TestCase(TYP_INT, EA_4BYTE)]
    [TestCase(var_types.TYP_LONG, EA_8BYTE)]
    public static void Amd64SingleRegisterDefaultSizeRemainsTheActualTypeSize(var_types type, emitAttr size)
    {
        if (!Configuration.TARGET_AMD64)
        {
            Assert.Ignore("Default register-size case requires the AMD64 target.");
        }

        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_RV(INS_inc, REG_X64_RAX, type);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_inc));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(size));
        });
    }

    [TestCase(0L, EA_4BYTE)]
    [TestCase(4294967295L, EA_4BYTE)]
    [TestCase(4294967296L, EA_8BYTE)]
    [TestCase(-1L, EA_8BYTE)]
    public static void Amd64MoveImmediateRetainsZeroExtendedWidthChoice(long value, emitAttr size)
    {
        if (!Configuration.TARGET_AMD64)
        {
            Assert.Ignore("Immediate-width case requires the AMD64 target.");
        }

        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_RV_IV(INS_mov, REG_X64_RAX, unchecked((nint)value), EA_8BYTE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(size));
        });
    }

    [Test]
    public static void X86RegisterAdaptersReachTheirInstructionEmitters()
    {
        if (!Configuration.TARGET_X86)
        {
            Assert.Ignore("Register-adapter case requires the x86 target.");
        }

        X86EmitterStaticOutputTests.WithEmitter((compiler, _) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");

            codeGen.inst_RV(INS_inc, REG_X86_EAX, TYP_INT);
            codeGen.inst_RV_IV(INS_mov, REG_X86_ECX, 7, EA_4BYTE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_inc));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
        });
    }
}
