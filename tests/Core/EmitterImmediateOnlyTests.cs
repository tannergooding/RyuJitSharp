// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class EmitterImmediateOnlyTests
{
    [TestCase(INS_push, -129, 5)]
    [TestCase(INS_push, -128, 2)]
    [TestCase(INS_push_hide, 0, 2)]
    [TestCase(INS_push_hide, 127, 2)]
    [TestCase(INS_push_hide, 128, 5)]
    [TestCase(INS_loop, 1, 2)]
    [TestCase(INS_ret, 16, 3)]
    public static void ImmediateOnlyDescriptorsRetainNativeFormatsAndSizes(instruction ins, int value, int size)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_IV(ins, value);
            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_CNS));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)value));
            Assert.That(codeGen.getCurrentStackLevel(), Is.Zero);
        });
    }

    [Test]
    public static void RelocatableImmediateRetainsItsFullConstantDescriptor()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            codeGen.Emitter.emitIns_I(INS_push, EA_4BYTE | EA_CNS_RELOC_FLG, 0x123456);
            var id = Descriptors(codeGen).Single();
            Assert.That(id.idCodeSize(), Is.EqualTo(5u));
            Assert.That(id.idIsCnsReloc(), Is.True);
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)0x123456));
        });
    }

    [Test]
    public static void UnsupportedImmediateInstructionFailsBeforeAllocating()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            _ = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitIns_I(INS_add, EA_PTRSIZE, 0));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

#if DEBUG
    [Test]
    public static void D005RejectsImmediateInstructionsBeforeRecording()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitIns_I(INS_push_hide, EA_PTRSIZE, 0));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif
}
