// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class RiscVZeroOperandInstructionTests
{
    [TestCase(INS_nop, 0x00000013u)]
    [TestCase(INS_ecall, 0x00000073u)]
    public static void ZeroOperandInstructionsRecordTheirOpcodeAndSize(instruction ins, uint expectedEncoding)
    {
        RiscVCodeGenPortTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var descriptors = RiscVCodeGenPortTests.CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialCount = descriptors.Count;
            var initialSize = RiscVCodeGenPortTests.CurrentInstructionGroupSize(emitter);

            FatalJitException? failure = null;
            try
            {
                emitter.emitIns(ins);
            }
            catch (FatalJitException exception)
            {
                failure = exception;
            }

#if DEBUG
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain("Instruction sanity checking outside AMD64 is not ported."));
            Assert.That(RiscVCodeGenPortTests.CurrentInstructionGroupSize(emitter), Is.EqualTo(initialSize));
#else
            Assert.That(failure, Is.Null);
            Assert.That(RiscVCodeGenPortTests.CurrentInstructionGroupSize(emitter), Is.EqualTo(initialSize + 4));
#endif

            Assert.That(descriptors.Count, Is.EqualTo(initialCount + 1));
            var descriptor = descriptors[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedEncoding));
        });
    }
}
#endif
