// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32DataLabelAddressTests
{
    [TestCase(1u, true)]
    [TestCase(0x12345678u, false)]
    public static void DataLabelAddressesRecordTheirNativeFormatAndOffset(uint offs, bool isSmallDescriptor)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var descriptor = RecordAddress(codeGen.Emitter, INS_movw, EA_HANDLE_CNS_RELOC, offs, REG_R4);

            Assert.That(descriptor.idIns(), Is.EqualTo(INS_movw));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R4));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_N2));
            Assert.That(descriptor.idInsSize(), Is.EqualTo(ISZ_32BIT));
            Assert.That(descriptor.idIsSmallDsc(), Is.EqualTo(isSmallDescriptor));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo(unchecked((nint)offs)));
        }, captureAssertions: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DataLabelRelocationFlagsFollowCompilerRelocationPolicy(bool compReloc)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = compReloc;
            var descriptor = RecordAddress(codeGen.Emitter, INS_movt, EA_HANDLE_CNS_RELOC, 42, REG_R4);

            Assert.That(descriptor.idIsCnsReloc(), Is.EqualTo(compReloc));
        }, captureAssertions: true);
    }

    [Test]
    public static void RelocatableDataLabelCodeGenUsesTheDataAddressEmitter()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            RecordArm32Instruction(() => codeGen.genMov32RelocatableDataLabel(42, REG_R4));
#if DEBUG
            var expectedInstruction = INS_movw;
#else
            var expectedInstruction = INS_movt;
#endif
            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No data-label instruction was recorded.");

            Assert.That(descriptor.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R4));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)42));
        }, captureAssertions: true);
    }

    private static Emitter.instrDesc RecordAddress(
        Emitter emitter, instruction ins, emitAttr attr, uint offs, regNumber reg)
    {
        RecordArm32Instruction(() => emitter.emitIns_R_D(ins, attr, offs, reg));
        return LastInstruction(emitter)
            ?? throw new AssertionException("No data-label descriptor was recorded.");
    }

    private static void RecordArm32Instruction(TestDelegate action)
    {
#if DEBUG
        try
        {
            action();
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        action();
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);
}
#endif
